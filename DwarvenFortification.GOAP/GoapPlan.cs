using System.Globalization;

namespace DwarvenFortification.GOAP;

public class GoapPlan
{
	public GoapAgent Agent { get; set; }

	public GoapGoal Goal { get; set; }

	public List<GoapAction> Actions { get; set; }

	/// <summary>
	/// Safety cap on A* node expansions. Numeric-operator goals (e.g. <c>hunger &gt; 100</c>) over
	/// unbounded actions (e.g. <c>IncreaseBy 1</c>) produce an infinite state space; without this
	/// cap the planner would run until OOM. Bumped if you genuinely need longer plans.
	/// </summary>
	public static int MaxExpansions { get; set; } = 100;

	public static GoapPlan? Find(GoapAgent Agent, GoapGoal Goal)
	{
		// 1. Refresh the agent's view of the world.
		Agent.SenseStates();

		// Snapshot the agent's real state so we can restore it after planning.
		var realStates = Agent.States;

		try
		{
			// 2. Already there?
			var initialState = CloneState(realStates);
			if (GoalSatisfied(Goal, initialState))
			{
				return new GoapPlan { Agent = Agent, Goal = Goal, Actions = [] };
			}

			// Build (or reuse) the precomputed action graph. Parametric templates are expanded into
			// concrete instances ONCE per agent here; producer/consumer indices on the resulting
			// graph let the planner traverse only connected paths instead of scanning the full
			// action list per A* node.
			var graph = Agent.ActionGraph ??= BuildAgentActionGraph(Agent);

			// Reverse-BFS from the goal's state ids through producer edges (and declared
			// cost-relevance edges) to compute the action subset that can transitively contribute
			// to satisfying the goal. A* iterates only this set; non-connected actions are never
			// considered, so adding 1000 unrelated actions to the agent costs nothing per Find.
			var goalStateIds = (Goal.Goals ?? [])
				.Select(c => c.StateId)
				.Where(id => !string.IsNullOrEmpty(id))
				.ToList();
			var plannerActions = graph.BackwardClosure(goalStateIds);
			if (plannerActions.Count == 0)
			{
				// No action chain can possibly affect any goal objective; bail without searching.
				return null;
			}

			// Delete-relaxed reachability prune. If no chain of actions (ignoring delete-effects and
			// numeric magnitudes) can satisfy every unmet objective, A* would just thrash through
			// MaxExpansions before giving up. Bail out now so the planner driver can try the next goal.
			if (!IsGoalRelaxedReachable(plannerActions, Goal, initialState))
			{
				return null;
			}

			// Cheapest single action in the connected subgraph; admissible per-unmet-goal lower bound for A*.
			var minActionCost = plannerActions
				.DefaultIfEmpty()
				.Min(action => action == null ? 0d : Math.Max(0d, ProbeCost(Agent, action, initialState, realStates)));

			// 3. Forward A* through the (state, action) graph. Each search node owns a fully
			//    simulated GoapWorldState. Action.Cost(Agent) is sampled with Agent.States
			//    temporarily pointing at the simulated pre-action state, so callers see the
			//    world as it would be when the action actually executes.
			var rootKey = CanonicalizeState(initialState);
			var root = new SearchNode(initialState, Parent: null, Action: null, Cost: 0d);
			var open = new PriorityQueue<SearchNode, double>();
			open.Enqueue(root, root.Cost + Heuristic(Agent, plannerActions, Goal, initialState, minActionCost));

			var bestCost = new Dictionary<string, double>(StringComparer.Ordinal) { [rootKey] = 0d };

			var expansions = 0;
			while (open.TryDequeue(out var node, out _))
			{
				if (++expansions > MaxExpansions)
				{
					// Bail out instead of blowing the heap on unreachable numeric goals.
					return null;
				}

				// Stale entry that has since been beaten by a cheaper path to the same state.
				var nodeKey = CanonicalizeState(node.State);
				if (bestCost.TryGetValue(nodeKey, out var bestKnown) && node.Cost > bestKnown)
				{
					continue;
				}

				// 3a. Success: simulated state satisfies the goal.
				if (GoalSatisfied(Goal, node.State))
				{
					var actions = new List<GoapAction>();
					for (var step = node; step.Action != null; step = step.Parent!)
					{
						actions.Add(step.Action);
					}

					actions.Reverse();
					return new GoapPlan { Agent = Agent, Goal = Goal, Actions = actions };
				}

				// 3b. Expand by every action whose conditions are met in the simulated state.
				foreach (var action in plannerActions)
				{
					if (!ActionConditionsMet(action, node.State))
					{
						continue;
					}

					// Sample cost against the pre-action simulated state.
					var actionCost = Math.Max(0d, ProbeCost(Agent, action, node.State, realStates));

					// Apply effects to a fresh clone to produce the post-action state. The agent's
					// StateBounds clamp the result so bounded values stay in range — that's what makes
					// the search space finite for operator goals over numeric domains.
					var nextState = CloneState(node.State);
					action.SuccessEffect.ApplyTo(nextState, Agent.StateBounds);

					var newCost = node.Cost + actionCost;
					var newKey = CanonicalizeState(nextState);
					if (bestCost.TryGetValue(newKey, out var prior) && prior <= newCost)
					{
						continue;
					}

					bestCost[newKey] = newCost;

					var priority = newCost + Heuristic(Agent, plannerActions, Goal, nextState, minActionCost);
					open.Enqueue(new SearchNode(nextState, node, action, newCost), priority);
				}
			}

			// 4. No path from the agent's current state to the goal.
			return null;
		}
		finally
		{
			// Always restore the agent's real state, even if cost callbacks swapped it.
			Agent.States = realStates;
		}
	}

	public bool Execute(bool CancelOnGoalChange = true)
		=> Run(CancelOnGoalChange) == GoapActionResult.Success;

	/// <summary>
	/// Executes the plan with full three-outcome semantics. Compound actions decompose into their
	/// children at runtime. Any non-Success outcome stops the plan and is returned; the matching
	/// outcome effect (<see cref="GoapAction.SuccessEffect"/>, <see cref="GoapAction.InterruptedEffect"/>,
	/// or <see cref="GoapAction.FailedEffect"/>) is applied to the agent's state.
	/// </summary>
	public GoapActionResult Run(bool CancelOnGoalChange = true)
	{
		foreach (var step in Actions)
		{
			// Cancel if prioritised goal has changed.
			if (CancelOnGoalChange && Goal != Agent.CurrentGoals().FirstOrDefault())
			{
				return GoapActionResult.Interrupted;
			}

			var outcome = RunStep(step);
			step.ApplyResult(outcome, Agent.States, Agent.StateBounds);
			Agent.SenseStates();

			if (outcome != GoapActionResult.Success)
			{
				return outcome;
			}
		}

		return GoapActionResult.Success;
	}

	GoapActionResult RunStep(GoapAction step)
	{
		if (!step.IsCompound)
		{
			return step.Run();
		}

		// Compound: execute children in order. Propagate the first non-Success outcome.
		// Each child's own ApplyResult fires here so the world reflects partial progress.
		foreach (var child in step.Children)
		{
			var outcome = RunStep(child);
			child.ApplyResult(outcome, Agent.States, Agent.StateBounds);
			Agent.SenseStates();
			if (outcome != GoapActionResult.Success)
			{
				return outcome;
			}
		}

		return GoapActionResult.Success;
	}

	/// <summary>
	/// Replan-feasibility check against the current agent state: verifies every step's preconditions
	/// are reachable along the remaining plan (simulating the success effect of each preceding step).
	/// Returns false as soon as any step's conditions cannot be met by the simulated cumulative state.
	/// </summary>
	public bool IsStillValid()
	{
		var simulated = CloneState(Agent.States);
		foreach (var step in Actions)
		{
			if (!ActionConditionsMet(step, simulated))
			{
				return false;
			}

			if (step.SuccessEffect is not null)
			{
				step.SuccessEffect.ApplyTo(simulated, Agent.StateBounds);
			}
		}

		return GoalSatisfied(Goal, simulated);
	}

	sealed record SearchNode(GoapWorldState State, SearchNode? Parent, GoapAction? Action, double Cost);

	/// <summary>
	/// Build the precomputed action graph for <paramref name="agent"/>: expand every parametric
	/// template into concrete instances (using the agent's current binding resolver), then index
	/// them by producer / consumer state id. Called once per agent and cached on
	/// <see cref="GoapAgent.ActionGraph"/>; subsequent <see cref="Find"/> calls reuse the same
	/// graph instance.
	/// </summary>
	static GoapActionGraph BuildAgentActionGraph(GoapAgent agent)
	{
		var expanded = GoapParameterSubstitution.ExpandParametricActions(
			agent.Actions ?? [],
			agent.ResolveParameterBindings,
			relevantStateIds: null);
		return new GoapActionGraph(expanded);
	}

	static GoapWorldState CloneState(GoapWorldState source)
		=> new Dictionary<string, GoapValue>(source);

	static double ProbeCost(GoapAgent agent, GoapAction action, GoapWorldState simulatedState, GoapWorldState realStates)
	{
		// Briefly point the agent at the simulated state so cost callbacks see the world
		// as it would be at execution time, then put the real state back.
		agent.States = simulatedState;
		try
		{
			return action.Cost(agent);
		}
		finally
		{
			agent.States = realStates;
		}
	}

	static bool ActionConditionsMet(GoapAction action, GoapWorldState state)
	{
		foreach (var condition in action.Conditions ?? [])
		{
			if (!condition.Comparison.IsMet(state.GetValueOrDefault(condition.StateId), condition.Operand))
			{
				return false;
			}
		}

		return true;
	}

	static bool GoalSatisfied(GoapGoal goal, GoapWorldState state)
	{
		foreach (var condition in goal.Goals)
		{
			if (!condition.Evaluate(state))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Delete-relaxed reachability test for the goal. Performs a forward fixpoint over
	/// <paramref name="plannerActions"/>, ignoring delete-effects and numeric magnitudes, to compute
	/// the set of state ids that could become "present" from <paramref name="initialState"/>. Each
	/// unmet objective must have at least one contributing action (effect targets the objective's
	/// state id) whose positive boolean preconditions all live in that reachable set. If any
	/// objective has no such contributor the goal is unreachable and A* would waste its expansion
	/// budget before giving up.
	/// </summary>
	static bool IsGoalRelaxedReachable(IReadOnlyList<GoapAction> plannerActions, GoapGoal goal, GoapWorldState initialState)
	{
		var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, value) in initialState)
		{
			if (value.Value is bool b)
			{
				if (b) reachable.Add(key);
			}
			else
			{
				reachable.Add(key);
			}
		}

		var changed = true;
		while (changed)
		{
			changed = false;
			foreach (var action in plannerActions)
			{
				var effect = action?.SuccessEffect;
				if (effect is null) continue;
				if (string.IsNullOrEmpty(effect.StateId)) continue;
				if (effect.Operand.Value is bool eb && !eb) continue; // delete-relaxation: skip negative SetTo
				if (reachable.Contains(effect.StateId)) continue;
				if (!ActionPositiveBoolPreconditionsReachable(action!, reachable)) continue;
				reachable.Add(effect.StateId);
				changed = true;
			}
		}

		foreach (var condition in goal.Goals ?? [])
		{
			if (condition.Evaluate(initialState)) continue;

			var hasContributor = false;
			foreach (var action in plannerActions)
			{
				var effect = action?.SuccessEffect;
				if (effect is null) continue;
				if (!string.Equals(effect.StateId, condition.StateId, StringComparison.OrdinalIgnoreCase)) continue;
				if (!ActionPositiveBoolPreconditionsReachable(action!, reachable)) continue;
				hasContributor = true;
				break;
			}

			if (!hasContributor) return false;
		}

		return true;
	}

	static bool ActionPositiveBoolPreconditionsReachable(GoapAction action, HashSet<string> reachable)
	{
		foreach (var cond in action.Conditions ?? [])
		{
			if (cond.Operand.Value is bool b && b && cond.Comparison == GoapComparison.EqualTo)
			{
				if (!reachable.Contains(cond.StateId)) return false;
			}
		}

		return true;
	}

	static double Heuristic(GoapAgent agent, IReadOnlyList<GoapAction> plannerActions, GoapGoal goal, GoapWorldState state, double minActionCost)
	{
		if (minActionCost <= 0d)
		{
			return 0d;
		}

		// Per-condition admissible estimator: for each unmet objective, ask "what is the minimum
		// number of action applications that could satisfy this condition, taken alone?". Take the
		// max across conditions (admissible — any single condition is a lower bound on the joint
		// remaining cost). Multiplying by minActionCost (cheapest action in the agent's action set)
		// keeps the estimate a lower bound on true cost-to-goal.
		var maxEstimate = 0d;
		foreach (var condition in goal.Goals)
		{
			if (condition.Evaluate(state))
			{
				continue;
			}

			var steps = EstimateMinStepsToSatisfy(agent, plannerActions, condition, state);
			if (double.IsPositiveInfinity(steps))
			{
				return double.PositiveInfinity;
			}

			var estimate = steps * minActionCost;
			if (estimate > maxEstimate)
			{
				maxEstimate = estimate;
			}
		}

		return maxEstimate;
	}

	/// <summary>
	/// Lower-bound on the number of action applications required to satisfy <paramref name="condition"/>
	/// from <paramref name="state"/>, ignoring action preconditions. For numeric comparisons it walks
	/// every action whose success effect targets the same state and simulates one application to
	/// measure the per-step delta, then divides the gap by the best (largest) useful delta. For
	/// non-numeric conditions it returns 1 if any single action's success effect would satisfy the
	/// condition in one application, else +∞ (unreachable — A* prunes the branch).
	/// </summary>
	static double EstimateMinStepsToSatisfy(GoapAgent agent, IReadOnlyList<GoapAction> plannerActions, GoapCondition condition, GoapWorldState state)
	{
		var currentValue = state.GetValueOrDefault(condition.StateId);
		var current = currentValue.Value;
		var operand = condition.Operand.Value;

		var numericPath = IsNumeric(current) && IsNumeric(operand);
		if (numericPath)
		{
			var curr = Convert.ToDouble(current, CultureInfo.InvariantCulture);
			var target = Convert.ToDouble(operand, CultureInfo.InvariantCulture);
			var bestSteps = double.PositiveInfinity;

			foreach (var action in plannerActions)
			{
				var effect = action?.SuccessEffect;
				if (effect is null || !string.Equals(effect.StateId, condition.StateId, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var after = effect.Operate(state);
				if (agent.StateBounds.TryGetValue(condition.StateId, out var bounds))
				{
					after = bounds.Clamp(after);
				}

				if (!IsNumeric(after.Value))
				{
					continue;
				}

				var afterValue = Convert.ToDouble(after.Value, CultureInfo.InvariantCulture);
				var delta = afterValue - curr;
				var steps = StepsForNumericProgress(condition.Comparison, curr, target, delta, afterValue);
				if (steps < bestSteps)
				{
					bestSteps = steps;
				}
			}

			return bestSteps;
		}

		// Boolean / non-numeric: any action whose success effect would satisfy the condition in one application.
		foreach (var action in plannerActions)
		{
			var effect = action?.SuccessEffect;
			if (effect is null || !string.Equals(effect.StateId, condition.StateId, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var after = effect.Operate(state);
			if (agent.StateBounds.TryGetValue(condition.StateId, out var bounds))
			{
				after = bounds.Clamp(after);
			}

			if (condition.Comparison.IsMet(after, condition.Operand))
			{
				return 1d;
			}
		}

		return double.PositiveInfinity;
	}

	static double StepsForNumericProgress(GoapComparison comparison, double curr, double target, double delta, double afterValue)
	{
		// If a single application already satisfies the condition (e.g. SetTo with a sufficient value), 1 step.
		if (comparison.IsMet(new GoapValue(afterValue), new GoapValue(target)))
		{
			return 1d;
		}

		switch (comparison)
		{
			case GoapComparison.GreaterThanOrEqualTo:
			case GoapComparison.GreaterThan:
				if (delta <= 0d) return double.PositiveInfinity;
				return Math.Max(1d, Math.Ceiling((target - curr) / delta));
			case GoapComparison.LessThanOrEqualTo:
			case GoapComparison.LessThan:
				if (delta >= 0d) return double.PositiveInfinity;
				return Math.Max(1d, Math.Ceiling((curr - target) / -delta));
			case GoapComparison.EqualTo:
			case GoapComparison.NotEqualTo:
			default:
				// Hard to bound for equality on numerics under arbitrary ops; the IsMet check above
				// already handled the one-shot case. Fall back to admissible 1.
				return 1d;
		}
	}

	static bool IsNumeric(object? value)
		=> value is not null and not bool and not string and IConvertible;

	static string CanonicalizeState(GoapWorldState state)
		=> string.Join("|", state
			.Select(pair => $"{pair.Key}={FormatInvariant(pair.Value.Value)}")
			.OrderBy(entry => entry, StringComparer.Ordinal));

	static string FormatInvariant(object? value) => value switch
	{
		null => "null",
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? "null",
	};
}
