using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP.Plans
{
	internal sealed class SearchNode
	{
		public required HashSet<string> Facts { get; init; }
		public required List<ActionCandidate> Steps { get; init; }
		public required int Cost { get; init; }
		public required string StateKey { get; init; }
	}
}
