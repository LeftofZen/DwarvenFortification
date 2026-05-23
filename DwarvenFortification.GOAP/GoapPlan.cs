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
	public static int MaxExpansions { get; set; } = 10_000;

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

			// Cheapest single action; admissible per-unmet-goal lower bound for A*.
			var minActionCost = (Agent.Actions ?? [])
				.DefaultIfEmpty()
				.Min(action => action == null ? 0d : Math.Max(0d, ProbeCost(Agent, action, initialState, realStates)));

			// 3. Forward A* through the (state, action) graph. Each search node owns a fully
			//    simulated GoapWorldState. Action.Cost(Agent) is sampled with Agent.States
			//    temporarily pointing at the simulated pre-action state, so callers see the
			//    world as it would be when the action actually executes.
			var rootKey = CanonicalizeState(initialState);
			var root = new SearchNode(initialState, Parent: null, Action: null, Cost: 0d);
			var open = new PriorityQueue<SearchNode, double>();
			open.Enqueue(root, root.Cost + Heuristic(Goal, initialState, minActionCost));

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
				foreach (var action in Agent.Actions ?? [])
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

					var priority = newCost + Heuristic(Goal, nextState, minActionCost);
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

	static double Heuristic(GoapGoal goal, GoapWorldState state, double minActionCost)
	{
		if (minActionCost <= 0d)
		{
			return 0d;
		}
		var unmet = 0;
		foreach (var condition in goal.Goals)
		{
			if (!condition.Evaluate(state))
			{
				unmet++;
			}
		}
		return unmet * minActionCost;
	}

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
