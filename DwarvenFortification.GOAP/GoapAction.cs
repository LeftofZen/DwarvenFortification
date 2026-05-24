global using GoapWorldState = System.Collections.Generic.IDictionary<string, DwarvenFortification.GOAP.GoapValue>;

namespace DwarvenFortification.GOAP;

public class GoapAction(string Name = null)
{
	public string Name { get; set; } = Name;
	public List<GoapCondition> Conditions { get; set; } = [];

	/// <summary>Effect applied when the action completes successfully. Required. Used by the planner.</summary>
	public GoapEffect SuccessEffect { get; set; }

	/// <summary>Optional effect applied when the action is interrupted (resumable). Runtime-only.</summary>
	public GoapEffect InterruptedEffect { get; set; }

	/// <summary>Optional effect applied when the action fails terminally. Runtime-only.</summary>
	public GoapEffect FailedEffect { get; set; }

	/// <summary>
	/// Declared parameter names. When non-empty this action is a parametric template: the planner
	/// enumerates the Cartesian product of legal bindings (one value per parameter, resolved via the
	/// agent's <see cref="GoapAgent.ResolveParameterBindings"/>) and emits one substituted concrete
	/// action instance per binding before searching. Placeholders in <see cref="Name"/>, condition
	/// state ids / string operands, and effect state ids / string operands use the form
	/// <c>{paramName}</c> and are replaced during substitution.
	/// </summary>
	public List<string> Parameters { get; set; } = [];

	/// <summary>
	/// Per-parameter fact-id pattern. Key = parameter name; value = a pattern containing exactly one
	/// <c>{paramName}</c> placeholder marking the capture position. The resolver receives the pattern
	/// and returns the set of captured values discoverable in the fact registry. E.g. parameter
	/// <c>tool</c> with pattern <c>has.item-tag.{tool}</c> yields {mining, woodcutting, throwable}
	/// when those <c>has.item-tag.*</c> facts are registered.
	/// </summary>
	public Dictionary<string, string> ParameterBindings { get; set; } = [];

	/// <summary>
	/// Bindings captured when this action was instantiated from a parametric template. Empty for
	/// non-parametric actions and for templates themselves. Runtime dispatch code can read this to
	/// recover the parameter value that drove planning (e.g. <c>Bindings["tool"] == "mining"</c>).
	/// </summary>
	public IReadOnlyDictionary<string, string> Bindings { get; set; } = new Dictionary<string, string>(0);

	public bool IsParametric => Parameters.Count > 0;

	/// <summary>
	/// Ordered child actions for a hierarchical (compound) action. Empty for primitive actions.
	/// At runtime the executor dispatches each child in sequence; the planner treats the compound
	/// atomically using <see cref="SuccessEffect"/>.
	///
	/// Design note: by default all actions should be primitive (no children). Compound/hierarchical
	/// actions exist as an opt-in optimisation — for example, to represent learned "experience"
	/// where an agent has discovered that a particular fixed sequence of primitives reliably
	/// achieves a sub-goal and is willing to short-circuit re-planning for that chain. They are
	/// NOT the preferred way to express ordinary multi-step behaviour: prefer primitives whose
	/// preconditions/effects let the planner discover dynamic, realistic paths.
	/// </summary>
	public List<GoapAction> Children { get; set; } = [];

	public bool IsCompound => Children.Count > 0;

	public Func<GoapAgent, double> Cost { get; set; } = _ => 1;

	/// <summary>
	/// State ids this action's <see cref="Cost"/> callback consults beyond its declared
	/// <see cref="Conditions"/>. The planner's action graph follows these as additional dependency
	/// edges so that backward closure from a goal includes actions whose only effect on a relevant
	/// stateId is via cost (e.g. a "prep" action that doesn't enable anything via preconditions but
	/// makes a downstream action cheaper). Leave empty when <see cref="Cost"/> reads no external
	/// state.
	/// </summary>
	public List<string> CostStateIds { get; set; } = [];

	/// <summary>
	/// Runtime entry point. Returns the outcome of attempting this action. Defaults to <see cref="GoapActionResult.Success"/>.
	/// Assign to drive the three-outcome state machine (<see cref="GoapAction.SuccessEffect"/>,
	/// <see cref="GoapAction.InterruptedEffect"/>, <see cref="GoapAction.FailedEffect"/>).
	/// </summary>
	public Func<GoapActionResult> Run { get; set; } = () => GoapActionResult.Success;

	public GoapAction(string Name, GoapEffect SuccessEffect) : this()
	{
		this.Name = Name;
		this.SuccessEffect = SuccessEffect;
	}

	public void Validate()
	{
		if (SuccessEffect is null)
		{
			throw new InvalidOperationException($"GoapAction '{Name}' must have a success effect.");
		}

		// Compound-action structural checks: no self-reference, no cycles, no null children.
		if (Children.Count > 0)
		{
			DetectCycles(this, [], []);
		}
	}

	static void DetectCycles(GoapAction node, HashSet<GoapAction> visiting, HashSet<GoapAction> visited)
	{
		if (visited.Contains(node))
		{
			return;
		}

		if (!visiting.Add(node))
		{
			throw new InvalidOperationException($"GoapAction '{node.Name}' is part of a compound-action cycle.");
		}

		for (var i = 0; i < node.Children.Count; i++)
		{
			var child = node.Children[i];
			if (child is null)
			{
				throw new InvalidOperationException($"GoapAction '{node.Name}' has a null child at index {i}.");
			}

			DetectCycles(child, visiting, visited);
		}

		visiting.Remove(node);
		visited.Add(node);
	}

	/// <summary>Planner-side: apply the success effect to the simulated state.</summary>
	public void UpdateStates(GoapWorldState States, IDictionary<string, GoapStateBounds>? Bounds = null)
		=> SuccessEffect.ApplyTo(States, Bounds);

	/// <summary>Runtime-side: apply the effect matching the produced outcome (if any).</summary>
	public void ApplyResult(GoapActionResult result, GoapWorldState States, IDictionary<string, GoapStateBounds>? Bounds = null)
	{
		var effect = result switch
		{
			GoapActionResult.Success => SuccessEffect,
			GoapActionResult.Interrupted => InterruptedEffect,
			GoapActionResult.Failed => FailedEffect,
			_ => null,
		};
		effect?.ApplyTo(States, Bounds);
	}

	/// <summary>
	/// Produce a concrete instance of this parametric template with the given bindings applied to
	/// the template's name, conditions, and effects. Subclasses (e.g. <c>SimulationGoapAction</c>)
	/// override this to preserve their concrete runtime type so plan-execution code that downcasts
	/// the action keeps working.
	/// </summary>
	public virtual GoapAction Instantiate(IReadOnlyDictionary<string, string> bindings)
	{
		if (bindings is null || bindings.Count == 0)
		{
			return this;
		}

		return new GoapAction(GoapParameterSubstitution.SubstituteString(Name, bindings))
		{
			Conditions = [.. Conditions.Select(c => GoapParameterSubstitution.SubstituteCondition(c, bindings))],
			SuccessEffect = GoapParameterSubstitution.SubstituteEffect(SuccessEffect, bindings),
			InterruptedEffect = InterruptedEffect is null ? null : GoapParameterSubstitution.SubstituteEffect(InterruptedEffect, bindings),
			FailedEffect = FailedEffect is null ? null : GoapParameterSubstitution.SubstituteEffect(FailedEffect, bindings),
			Children = Children,
			Cost = Cost,
			CostStateIds = [.. CostStateIds.Select(id => GoapParameterSubstitution.SubstituteString(id, bindings))],
			Run = Run,
			Bindings = bindings,
		};
	}
}