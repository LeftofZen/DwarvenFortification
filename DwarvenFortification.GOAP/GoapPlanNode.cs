namespace DwarvenFortification.GOAP
{
	public enum GoapPlanNodeKind
	{
		Goal,
		Sequence,
		Requirement,
		Action,
	}

	public sealed class GoapPlanNode
	{
		public GoapPlanNode(
			GoapPlanNodeKind kind,
			string label,
			GoapActionCandidate? candidate,
			IReadOnlyList<GoapPlanNode> children,
			IReadOnlyList<string> resolvedFacts,
			int cost)
		{
			Kind = kind;
			Label = label;
			Candidate = candidate;
			Children = children;
			ResolvedStates = resolvedFacts;
			Cost = cost;
		}

		public GoapPlanNodeKind Kind { get; }
		public string Label { get; }
		public GoapActionCandidate? Candidate { get; }
		public IReadOnlyList<GoapPlanNode> Children { get; }
		public IReadOnlyList<string> ResolvedStates { get; }
		public int Cost { get; }
	}
}
