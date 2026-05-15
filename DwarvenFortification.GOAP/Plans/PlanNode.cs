using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP.Plans
{
	public sealed class PlanNode
	{
		public PlanNode(
			PlanNodeKind kind,
			string label,
			ActionCandidate? candidate,
			IReadOnlyList<PlanNode> children,
			IReadOnlyList<string> resolvedFacts,
			int cost)
		{
			Kind = kind;
			Label = label;
			Candidate = candidate;
			Children = children;
			ResolvedFacts = resolvedFacts;
			Cost = cost;
		}

		public PlanNodeKind Kind { get; }
		public string Label { get; }
		public ActionCandidate? Candidate { get; }
		public IReadOnlyList<PlanNode> Children { get; }
		public IReadOnlyList<string> ResolvedFacts { get; }
		public int Cost { get; }
	}
}
