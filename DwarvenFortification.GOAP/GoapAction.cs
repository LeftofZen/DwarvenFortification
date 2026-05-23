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
}