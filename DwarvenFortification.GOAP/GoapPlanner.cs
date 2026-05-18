using Arch.Core;
using DwarvenFortification.Simulation.Pathfinding;

namespace DwarvenFortification.GOAP
{
	public sealed class GoapPlanner
	{
		const int MaxSearchDepth = 8;
		readonly IGoapDefinitionSource definitions;
		readonly IGraphPathfinder pathfinder;
		readonly IGoapWorldQueryService worldQueryService;

		public GoapPlanner(IGoapDefinitionSource definitions, IGoapWorldQueryService worldQueryService)
			: this(definitions, worldQueryService, new AStarGraphPathfinder())
		{
		}

		public GoapPlanner(IGoapDefinitionSource definitions, IGoapWorldQueryService worldQueryService, IGraphPathfinder pathfinder)
		{
			this.definitions = definitions;
			this.worldQueryService = worldQueryService;
			this.pathfinder = pathfinder;
		}

		public IReadOnlyList<GoapPlan> BuildCandidatePlans(Entity agent)
			=> Inspect(agent).CandidatePlans;

		public GoapSnapshot Inspect(Entity agent)
		{
			var currentState = worldQueryService.BuildCurrentState(agent);
			var actions = definitions.GetActionDefinitions();
			var actionManifestationQuery = worldQueryService.InspectCandidates(agent, actions, currentState);
			var goals = new List<GoapGoalView>();
			var candidatePlans = new List<GoapPlan>();

			foreach (var goal in definitions.GetGoalDefinitions())
			{
				var missingRequiredStates = goal.Requirements
					.Where(s => !currentState.Contains(s))
					.ToArray();
				var isEligible = missingRequiredStates.Length == 0;
				var isSatisfied = GoalSatisfied(goal, currentState);

				GoapPlan candidatePlan = null;
				if (isEligible && !isSatisfied)
				{
					candidatePlan = Search(agent, goal, currentState, actions);
					if (candidatePlan != null)
					{
						candidatePlans.Add(candidatePlan);
					}
				}

				var effectivePriority = worldQueryService.GetEffectivePriority(agent, goal);
				goals.Add(new GoapGoalView(goal, isEligible, isSatisfied, missingRequiredStates, candidatePlan, effectivePriority));
			}

			return new GoapSnapshot(
				[.. currentState.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)],
				actionManifestationQuery.Candidates,
				actionManifestationQuery.Diagnostics,
				[.. goals.OrderByDescending(g => g.EffectivePriority)],
				candidatePlans);
		}

		GoapPlan Search(Entity agent, GoapGoal goal, HashSet<string> currentState, IReadOnlyList<GoapAction> actions)
		{
			var startState = new HashSet<string>(currentState, StringComparer.OrdinalIgnoreCase);
			var startSearchState = new PlannerSearchState(startState, BuildStateKey(startState), 0);
			var request = new GraphSearchRequest<PlannerSearchState, GoapActionCandidate>(
				startSearchState,
				node => GoalSatisfied(goal, node.State),
				node => ExpandSearchEdges(agent, actions, node),
				EstimateRemainingCost: static _ => 0f,
				NodeComparer: PlannerSearchStateComparer.Instance);

			if (!pathfinder.TryFindPath(request, out var result))
			{
				return null;
			}

			var traceSteps = BuildTraceSteps(result.PathNodes, result.PathEdges);
			var totalCost = traceSteps.Sum(step => step.Candidate.Cost);

			var root = new GoapPlanNode(
				GoapPlanNodeKind.Goal,
				goal.Name,
				null,
				BuildGoalNodes(goal, startState, traceSteps),
				[.. goal.Effects],
				totalCost);

			return new GoapPlan(goal, root, totalCost);
		}

		IEnumerable<GraphEdge<PlannerSearchState, GoapActionCandidate>> ExpandSearchEdges(
			Entity agent,
			IReadOnlyList<GoapAction> actions,
			PlannerSearchState currentState)
		{
			if (currentState.ActionsUsed >= MaxSearchDepth)
			{
				yield break;
			}

			var candidateQuery = worldQueryService.InspectCandidates(agent, actions, currentState.State);
			var candidates = candidateQuery.Candidates
				.Where(candidate => CandidateApplicable(candidate, currentState.State))
				.OrderBy(candidate => candidate.Cost)
				.ThenBy(candidate => candidate.Definition.Id, StringComparer.OrdinalIgnoreCase);

			foreach (var candidate in candidates)
			{
				var nextState = ApplyCandidate(currentState.State, candidate);
				var nextStateKey = BuildStateKey(nextState);
				if (string.Equals(nextStateKey, currentState.StateKey, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				yield return new GraphEdge<PlannerSearchState, GoapActionCandidate>(
					new PlannerSearchState(nextState, nextStateKey, currentState.ActionsUsed + 1),
					candidate.Cost,
					candidate);
			}
		}

		static IReadOnlyList<GoapPlanNode> BuildGoalNodes(GoapGoal goal, HashSet<string> initialState, IReadOnlyList<PlannerTraceStep> traceSteps)
			=> [.. BuildGoalObjectives(goal)
				.Select(objective => BuildObjectiveResolution(objective, initialState, traceSteps, traceSteps.Count - 1))
				.OfType<ObjectiveResolution>()
				.OrderBy(resolution => resolution.SupportingStepIndex)
				.Select(resolution => resolution.Node)];

		static ObjectiveResolution? BuildObjectiveResolution(StateObjective objective, HashSet<string> initialState, IReadOnlyList<PlannerTraceStep> traceSteps, int maxStepIndex)
		{
			var supportingStepIndex = FindSupportingStepIndex(objective, traceSteps, maxStepIndex);
			if (supportingStepIndex < 0)
			{
				return ObjectiveSatisfied(objective, initialState) ? null : null;
			}

			var supportingStep = traceSteps[supportingStepIndex];
			var prerequisiteNodes = BuildStateObjectives(supportingStep.Candidate.Requirements)
				.Select(prerequisite => BuildObjectiveResolution(prerequisite, initialState, traceSteps, supportingStepIndex - 1))
				.OfType<ObjectiveResolution>()
				.OrderBy(resolution => resolution.SupportingStepIndex)
				.Select(resolution => resolution.Node)
				.ToList();

			prerequisiteNodes.Add(CreateActionNode(supportingStep.Candidate));

			var nodeCost = prerequisiteNodes.Sum(node => node.Cost);
			var node = new GoapPlanNode(
				GoapPlanNodeKind.Requirement,
				FormatObjective(objective),
				null,
				prerequisiteNodes,
					[BuildObjectiveKey(objective)],
				nodeCost);

			return new ObjectiveResolution(node, supportingStepIndex);
		}

		static int FindSupportingStepIndex(StateObjective objective, IReadOnlyList<PlannerTraceStep> traceSteps, int maxStepIndex)
		{
			for (var index = maxStepIndex; index >= 0; --index)
			{
				var step = traceSteps[index];
				if (!ObjectiveSatisfied(objective, step.BeforeState) && ObjectiveSatisfied(objective, step.AfterState))
				{
					return index;
				}
			}

			return -1;
		}

		static IReadOnlyList<PlannerTraceStep> BuildTraceSteps(IReadOnlyList<PlannerSearchState> pathNodes, IReadOnlyList<GoapActionCandidate> pathEdges)
		{
			var steps = new List<PlannerTraceStep>(pathEdges.Count);
			for (var i = 0; i < pathEdges.Count; i++)
			{
				steps.Add(new PlannerTraceStep(pathNodes[i].State, pathNodes[i + 1].State, pathEdges[i]));
			}

			return steps;
		}

		static bool GoalSatisfied(GoapGoal goal, HashSet<string> state)
			=> goal.Effects.All(state.Contains);

		static IEnumerable<StateObjective> BuildGoalObjectives(GoapGoal goal)
		{
			foreach (var s in goal.Effects.Where(s => !string.IsNullOrWhiteSpace(s)))
			{
				yield return new StateObjective(s, true);
			}
		}

		static IEnumerable<StateObjective> BuildStateObjectives(IEnumerable<string> states)
		{
			foreach (var s in states.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (s.StartsWith('!'))
				{
					yield return new StateObjective(s[1..], false);
					continue;
				}

				yield return new StateObjective(s, true);
			}
		}

		static bool ObjectiveSatisfied(StateObjective objective, HashSet<string> state)
			=> objective.MustBePresent
				? state.Contains(objective.State)
				: !state.Contains(objective.State);

		static bool CandidateApplicable(GoapActionCandidate candidate, HashSet<string> state)
			=> candidate.Requirements.All(s => s.StartsWith('!')
				? !state.Contains(s[1..])
				: state.Contains(s));

		static HashSet<string> ApplyCandidate(HashSet<string> currentState, GoapActionCandidate candidate)
		{
			var nextState = new HashSet<string>(currentState, StringComparer.OrdinalIgnoreCase);
			foreach (var s in candidate.Effects)
			{
				nextState.Add(s);
			}

			return nextState;
		}

		static GoapPlanNode CreateActionNode(GoapActionCandidate candidate)
			=> new(
				GoapPlanNodeKind.Action,
				candidate.Definition.Name,
				candidate,
				Array.Empty<GoapPlanNode>(),
				candidate.Effects,
				candidate.Cost);

		static string FormatObjective(StateObjective objective)
			=> objective.MustBePresent
				? $"Require state '{objective.State}'"
				: $"Clear state '{objective.State}'";

		static string BuildObjectiveKey(StateObjective objective)
			=> objective.MustBePresent ? objective.State : $"!{objective.State}";

		static string BuildStateKey(IEnumerable<string> states)
			=> string.Join("|", states.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

		readonly record struct StateObjective(string State, bool MustBePresent);

		readonly record struct PlannerTraceStep(HashSet<string> BeforeState, HashSet<string> AfterState, GoapActionCandidate Candidate);

		readonly record struct ObjectiveResolution(GoapPlanNode Node, int SupportingStepIndex);

		sealed class PlannerSearchState
		{
			public PlannerSearchState(HashSet<string> state, string stateKey, int actionsUsed)
			{
				State = state;
				StateKey = stateKey;
				ActionsUsed = actionsUsed;
			}

			public HashSet<string> State { get; }
			public string StateKey { get; }
			public int ActionsUsed { get; }
		}

		sealed class PlannerSearchStateComparer : IEqualityComparer<PlannerSearchState>
		{
			public static PlannerSearchStateComparer Instance { get; } = new();

			public bool Equals(PlannerSearchState x, PlannerSearchState y)
				=> string.Equals(x?.StateKey, y?.StateKey, StringComparison.OrdinalIgnoreCase);

			public int GetHashCode(PlannerSearchState obj)
				=> StringComparer.OrdinalIgnoreCase.GetHashCode(obj.StateKey);
		}
	}
}
