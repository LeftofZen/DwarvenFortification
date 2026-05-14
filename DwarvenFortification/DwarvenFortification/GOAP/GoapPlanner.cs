using Arch.Core;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public sealed class GoapPlanner
	{
		const int MaxSearchDepth = 4;
		readonly SimulationDefinitionRegistry definitions;
		readonly IGoapWorldQueryService worldQueryService;

		public GoapPlanner(SimulationDefinitionRegistry definitions, IGoapWorldQueryService worldQueryService)
		{
			this.definitions = definitions;
			this.worldQueryService = worldQueryService;
		}

		public GoapPlan Plan(Entity agent)
		{
			var actions = definitions.GetActionDefinitions();
			var currentFacts = worldQueryService.BuildCurrentFacts(agent);
			var candidates = worldQueryService.BuildCandidates(agent, actions, currentFacts).ToList();
			if (candidates.Count == 0)
			{
				return null;
			}

			foreach (var goal in definitions.GetGoalDefinitions())
			{
				if (!GoalIsEligible(goal, currentFacts))
				{
					continue;
				}

				var plan = Search(goal, currentFacts, candidates);
				if (plan != null)
				{
					return plan;
				}
			}

			return null;
		}

		GoapPlan Search(GoapGoal goal, HashSet<string> currentFacts, List<GoapActionCandidate> candidates)
		{
			var frontier = new List<GoapSearchNode>
			{
				new()
				{
					Facts = new HashSet<string>(currentFacts),
					Steps = new List<GoapActionCandidate>(),
					Cost = 0,
					StateKey = BuildStateKey(currentFacts),
				},
			};
			var visited = new Dictionary<string, int>
			{
				[frontier[0].StateKey] = 0,
			};

			while (frontier.Count > 0)
			{
				var currentIndex = frontier
					.Select((node, index) => (node, index))
					.OrderBy(pair => pair.node.Cost)
					.First().index;
				var current = frontier[currentIndex];
				frontier.RemoveAt(currentIndex);

				if (GoalSatisfied(goal, current.Facts))
				{
					return new GoapPlan(goal, current.Steps, current.Cost);
				}

				if (current.Steps.Count >= MaxSearchDepth)
				{
					continue;
				}

				foreach (var candidate in candidates)
				{
					if (!candidate.RequiredFacts.All(fact => fact.StartsWith('!') ? !current.Facts.Contains(fact[1..]) : current.Facts.Contains(fact)))
					{
						continue;
					}

					var nextFacts = new HashSet<string>(current.Facts);
					foreach (var fact in candidate.RemoveFacts)
					{
						nextFacts.Remove(fact);
					}
					foreach (var fact in candidate.AddFacts)
					{
						nextFacts.Add(fact);
					}

					var nextCost = current.Cost + candidate.Cost;
					var nextKey = BuildStateKey(nextFacts);
					if (visited.TryGetValue(nextKey, out var bestCost) && bestCost <= nextCost)
					{
						continue;
					}

					visited[nextKey] = nextCost;
					var steps = new List<GoapActionCandidate>(current.Steps) { candidate };
					frontier.Add(new GoapSearchNode
					{
						Facts = nextFacts,
						Steps = steps,
						Cost = nextCost,
						StateKey = nextKey,
					});
				}
			}

			return null;
		}

		static bool GoalSatisfied(GoapGoal goal, HashSet<string> facts)
			=> goal.DesiredFacts.All(facts.Contains) && goal.ForbiddenFacts.All(fact => !facts.Contains(fact));

		static bool GoalIsEligible(GoapGoal goal, HashSet<string> facts)
			=> goal.RequiredFacts.All(facts.Contains) && goal.BlockedByFacts.All(fact => !facts.Contains(fact));

		static string BuildStateKey(IEnumerable<string> facts)
			=> string.Join("|", facts.OrderBy(f => f, System.StringComparer.OrdinalIgnoreCase));
	}
}