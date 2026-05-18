namespace DwarvenFortification.GOAP
{
	public sealed class GoapPlan
	{
		readonly IReadOnlyList<GoapActionCandidate> steps;

		public GoapPlan(GoapGoal goal, IReadOnlyList<GoapActionCandidate> steps, int cost)
			: this(goal, CreateSequenceRoot(goal, steps, cost), cost)
		{
		}

		public GoapPlan(GoapGoal goal, GoapPlanNode root, int cost)
		{
			Goal = goal;
			Root = root;
			this.steps = FlattenSteps(root);
			Cost = cost;
		}

		public GoapGoal Goal { get; }
		public GoapPlanNode Root { get; }
		public IReadOnlyList<GoapActionCandidate> Steps => steps;
		public int Cost { get; }

		static GoapPlanNode CreateSequenceRoot(GoapGoal goal, IReadOnlyList<GoapActionCandidate> steps, int cost)
			=> new(
				GoapPlanNodeKind.Goal,
				goal.Name,
				null,
				[
					new GoapPlanNode(
						GoapPlanNodeKind.Sequence,
						"Plan sequence",
						null,
						[.. steps.Select(step => new GoapPlanNode(GoapPlanNodeKind.Action, step.Definition.Name, step, Array.Empty<GoapPlanNode>(), step.Effects, step.Cost))],
						goal.Effects,
						cost),
				],
				goal.Effects,
				cost);

		static IReadOnlyList<GoapActionCandidate> FlattenSteps(GoapPlanNode root)
		{
			var flattened = new List<GoapActionCandidate>();
			CollectSteps(root, flattened);
			return flattened;
		}

		static void CollectSteps(GoapPlanNode node, List<GoapActionCandidate> flattened)
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
