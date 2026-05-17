using Arch.Core;
using DwarvenFortification.GOAP.Actions;
using DwarvenFortification.Simulation.Pathfinding;

namespace DwarvenFortification.GOAP.Plans
{
	public sealed class Planner
	{
		const int MaxSearchDepth = 8;
		readonly IDefinitionSource definitions;
		readonly IGraphPathfinder pathfinder;
		readonly IWorldQueryService worldQueryService;

		public Planner(IDefinitionSource definitions, IWorldQueryService worldQueryService)
			: this(definitions, worldQueryService, new AStarGraphPathfinder())
		{
		}

		public Planner(IDefinitionSource definitions, IWorldQueryService worldQueryService, IGraphPathfinder pathfinder)
		{
			this.definitions = definitions;
			this.worldQueryService = worldQueryService;
			this.pathfinder = pathfinder;
		}

		public IReadOnlyList<Plan> BuildCandidatePlans(Entity agent)
			=> Inspect(agent).CandidatePlans;

		public PlanningSnapshot Inspect(Entity agent)
		{
			var currentFacts = worldQueryService.BuildCurrentFacts(agent);
			var actions = definitions.GetActionDefinitions();
			var actionManifestationQuery = worldQueryService.InspectCandidates(agent, actions, currentFacts);
			var goals = new List<GoalDebugView>();
			var candidatePlans = new List<Plan>();

			foreach (var goal in definitions.GetGoalDefinitions())
			{
				var missingRequiredFacts = goal.RequiredFacts
					.Where(fact => !currentFacts.Contains(fact))
					.ToArray();
				var activeBlockingFacts = goal.BlockedByFacts
					.Where(currentFacts.Contains)
					.ToArray();
				var isEligible = missingRequiredFacts.Length == 0 && activeBlockingFacts.Length == 0;
				var isSatisfied = GoalSatisfied(goal, currentFacts);

				Plan candidatePlan = null;
				if (isEligible && !isSatisfied)
				{
					candidatePlan = Search(agent, goal, currentFacts, actions);
					if (candidatePlan != null)
					{
						candidatePlans.Add(candidatePlan);
					}
				}

				var effectivePriority = worldQueryService.GetEffectivePriority(agent, goal);
				goals.Add(new GoalDebugView(goal, isEligible, isSatisfied, missingRequiredFacts, activeBlockingFacts, candidatePlan, effectivePriority));
			}

			return new PlanningSnapshot(
				currentFacts.OrderBy(fact => fact, StringComparer.OrdinalIgnoreCase).ToArray(),
				actionManifestationQuery.Candidates,
				actionManifestationQuery.Diagnostics,
				goals.OrderByDescending(g => g.EffectivePriority).ToList(),
				candidatePlans);
		}

		Plan Search(Entity agent, Goal goal, HashSet<string> currentFacts, IReadOnlyList<ActionDefinitionSnapshot> actions)
		{
			var startFacts = new HashSet<string>(currentFacts, StringComparer.OrdinalIgnoreCase);
			var startState = new PlannerSearchState(startFacts, BuildStateKey(startFacts), null, 0);
			var request = new GraphSearchRequest<PlannerSearchState>(
				startState,
				state => GoalSatisfied(goal, state.Facts),
				state => ExpandSearchEdges(agent, actions, state),
				EstimateRemainingCost: static _ => 0f,
				NodeComparer: PlannerSearchStateComparer.Instance);

			if (!pathfinder.TryFindPath(request, out var result))
			{
				return null;
			}

			var traceSteps = BuildTraceSteps(result.PathNodes);
			var totalCost = traceSteps.Sum(step => step.Candidate.Cost);

			var root = new PlanNode(
				PlanNodeKind.Goal,
				goal.Name,
				null,
				BuildGoalNodes(goal, startFacts, traceSteps),
				goal.DesiredFacts.Concat(goal.ForbiddenFacts.Select(fact => $"!{fact}")).ToArray(),
				totalCost);

			return new Plan(goal, root, totalCost);
		}

		IEnumerable<GraphEdge<PlannerSearchState>> ExpandSearchEdges(
			Entity agent,
			IReadOnlyList<ActionDefinitionSnapshot> actions,
			PlannerSearchState currentState)
		{
			if (currentState.ActionsUsed >= MaxSearchDepth)
			{
				yield break;
			}

			var candidateQuery = worldQueryService.InspectCandidates(agent, actions, currentState.Facts);
			var candidates = candidateQuery.Candidates
				.Where(candidate => CandidateApplicable(candidate, currentState.Facts))
				.OrderBy(candidate => candidate.Cost)
				.ThenBy(candidate => candidate.Definition.Id, StringComparer.OrdinalIgnoreCase);

			foreach (var candidate in candidates)
			{
				var nextFacts = ApplyCandidate(currentState.Facts, candidate);
				var nextStateKey = BuildStateKey(nextFacts);
				if (string.Equals(nextStateKey, currentState.StateKey, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				yield return new GraphEdge<PlannerSearchState>(
					new PlannerSearchState(nextFacts, nextStateKey, candidate, currentState.ActionsUsed + 1),
					candidate.Cost);
			}
		}

		static IReadOnlyList<PlanNode> BuildGoalNodes(Goal goal, HashSet<string> initialFacts, IReadOnlyList<PlannerTraceStep> traceSteps)
			=> BuildGoalObjectives(goal)
				.Select(objective => BuildObjectiveResolution(objective, initialFacts, traceSteps, traceSteps.Count - 1))
				.OfType<ObjectiveResolution>()
				.OrderBy(resolution => resolution.SupportingStepIndex)
				.Select(resolution => resolution.Node)
				.ToArray();

		static ObjectiveResolution? BuildObjectiveResolution(FactObjective objective, HashSet<string> initialFacts, IReadOnlyList<PlannerTraceStep> traceSteps, int maxStepIndex)
		{
			var supportingStepIndex = FindSupportingStepIndex(objective, traceSteps, maxStepIndex);
			if (supportingStepIndex < 0)
			{
				return ObjectiveSatisfied(objective, initialFacts) ? null : null;
			}

			var supportingStep = traceSteps[supportingStepIndex];
			var prerequisiteNodes = BuildFactObjectives(supportingStep.Candidate.RequiredFacts)
				.Select(prerequisite => BuildObjectiveResolution(prerequisite, initialFacts, traceSteps, supportingStepIndex - 1))
				.OfType<ObjectiveResolution>()
				.OrderBy(resolution => resolution.SupportingStepIndex)
				.Select(resolution => resolution.Node)
				.ToList();

			prerequisiteNodes.Add(CreateActionNode(supportingStep.Candidate));

			var nodeCost = prerequisiteNodes.Sum(node => node.Cost);
			var node = new PlanNode(
				PlanNodeKind.Requirement,
				FormatObjective(objective),
				null,
				prerequisiteNodes,
				new[] { BuildObjectiveKey(objective) },
				nodeCost);

			return new ObjectiveResolution(node, supportingStepIndex);
		}

		static int FindSupportingStepIndex(FactObjective objective, IReadOnlyList<PlannerTraceStep> traceSteps, int maxStepIndex)
		{
			for (var index = maxStepIndex; index >= 0; --index)
			{
				var step = traceSteps[index];
				if (!ObjectiveSatisfied(objective, step.BeforeFacts) && ObjectiveSatisfied(objective, step.AfterFacts))
				{
					return index;
				}
			}

			return -1;
		}

		static IReadOnlyList<PlannerTraceStep> BuildTraceSteps(IReadOnlyList<PlannerSearchState> pathNodes)
		{
			var steps = new List<PlannerTraceStep>();
			for (var index = 1; index < pathNodes.Count; ++index)
			{
				var current = pathNodes[index];
				if (!current.IncomingCandidate.HasValue)
				{
					continue;
				}

				steps.Add(new PlannerTraceStep(pathNodes[index - 1].Facts, current.Facts, current.IncomingCandidate.Value));
			}

			return steps;
		}

		static bool GoalSatisfied(Goal goal, HashSet<string> facts)
			=> goal.DesiredFacts.All(facts.Contains) && goal.ForbiddenFacts.All(fact => !facts.Contains(fact));

		static IEnumerable<FactObjective> BuildGoalObjectives(Goal goal)
		{
			foreach (var fact in goal.DesiredFacts.Where(fact => !string.IsNullOrWhiteSpace(fact)))
			{
				yield return new FactObjective(fact, true);
			}

			foreach (var fact in goal.ForbiddenFacts.Where(fact => !string.IsNullOrWhiteSpace(fact)))
			{
				yield return new FactObjective(fact, false);
			}
		}

		static IEnumerable<FactObjective> BuildFactObjectives(IEnumerable<string> facts)
		{
			foreach (var fact in facts.Where(fact => !string.IsNullOrWhiteSpace(fact)).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (fact.StartsWith('!'))
				{
					yield return new FactObjective(fact[1..], false);
					continue;
				}

				yield return new FactObjective(fact, true);
			}
		}

		static bool ObjectiveSatisfied(FactObjective objective, HashSet<string> facts)
			=> objective.MustBePresent
				? facts.Contains(objective.Fact)
				: !facts.Contains(objective.Fact);

		static bool CandidateApplicable(ActionCandidate candidate, HashSet<string> facts)
			=> candidate.RequiredFacts.All(fact => fact.StartsWith('!')
				? !facts.Contains(fact[1..])
				: facts.Contains(fact))
				&& candidate.Definition.BlockedByFacts.All(fact => !facts.Contains(fact));

		static HashSet<string> ApplyCandidate(HashSet<string> currentFacts, ActionCandidate candidate)
		{
			var nextFacts = new HashSet<string>(currentFacts, StringComparer.OrdinalIgnoreCase);
			foreach (var fact in candidate.RemoveFacts)
			{
				nextFacts.Remove(fact);
			}

			foreach (var fact in candidate.AddFacts)
			{
				nextFacts.Add(fact);
			}

			return nextFacts;
		}

		static PlanNode CreateActionNode(ActionCandidate candidate)
			=> new(
				PlanNodeKind.Action,
				candidate.Definition.Name,
				candidate,
				Array.Empty<PlanNode>(),
				candidate.AddFacts.Concat(candidate.RemoveFacts.Select(fact => $"!{fact}")).ToArray(),
				candidate.Cost);

		static string FormatObjective(FactObjective objective)
			=> objective.MustBePresent
				? $"Require fact '{objective.Fact}'"
				: $"Clear fact '{objective.Fact}'";

		static string BuildObjectiveKey(FactObjective objective)
			=> objective.MustBePresent ? objective.Fact : $"!{objective.Fact}";

		static string BuildStateKey(IEnumerable<string> facts)
			=> string.Join("|", facts.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));

		readonly record struct FactObjective(string Fact, bool MustBePresent);

		readonly record struct PlannerTraceStep(HashSet<string> BeforeFacts, HashSet<string> AfterFacts, ActionCandidate Candidate);

		readonly record struct ObjectiveResolution(PlanNode Node, int SupportingStepIndex);

		sealed class PlannerSearchState
		{
			public PlannerSearchState(HashSet<string> facts, string stateKey, ActionCandidate? incomingCandidate, int actionsUsed)
			{
				Facts = facts;
				StateKey = stateKey;
				IncomingCandidate = incomingCandidate;
				ActionsUsed = actionsUsed;
			}

			public HashSet<string> Facts { get; }
			public string StateKey { get; }
			public ActionCandidate? IncomingCandidate { get; }
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
