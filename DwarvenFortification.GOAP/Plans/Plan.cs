using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP.Plans
{
	public sealed class Plan
	{
		readonly IReadOnlyList<ActionCandidate> steps;

		public Plan(Goal goal, IReadOnlyList<ActionCandidate> steps, int cost)
			: this(goal, CreateSequenceRoot(goal, steps, cost), cost)
		{
		}

		public Plan(Goal goal, PlanNode root, int cost)
		{
			Goal = goal;
			Root = root;
			this.steps = FlattenSteps(root);
			Cost = cost;
		}

		public Goal Goal { get; }
		public PlanNode Root { get; }
		public IReadOnlyList<ActionCandidate> Steps => steps;
		public int Cost { get; }

		static PlanNode CreateSequenceRoot(Goal goal, IReadOnlyList<ActionCandidate> steps, int cost)
			=> new(
				PlanNodeKind.Goal,
				goal.Name,
				null,
				new[]
				{
					new PlanNode(
						PlanNodeKind.Sequence,
						"Plan sequence",
						null,
						steps.Select(step => new PlanNode(PlanNodeKind.Action, step.Definition.Name, step, Array.Empty<PlanNode>(), step.AddFacts, step.Cost)).ToArray(),
						goal.DesiredFacts,
						cost),
				},
				goal.DesiredFacts,
				cost);

		static IReadOnlyList<ActionCandidate> FlattenSteps(PlanNode root)
		{
			var flattened = new List<ActionCandidate>();
			CollectSteps(root, flattened);
			return flattened;
		}

		static void CollectSteps(PlanNode node, List<ActionCandidate> flattened)
		{
			if (node.Candidate.HasValue)
			{
				flattened.Add(node.Candidate.Value);
			}

			foreach (var child in node.Children)
			{
				CollectSteps(child, flattened);
			}
		}
	}
}
