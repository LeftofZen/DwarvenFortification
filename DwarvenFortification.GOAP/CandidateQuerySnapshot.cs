using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP
{
	public sealed class CandidateQuerySnapshot
	{
		public CandidateQuerySnapshot(IReadOnlyList<ActionCandidate> candidates, IReadOnlyList<ActionDiagnostic> diagnostics)
		{
			Candidates = candidates;
			Diagnostics = diagnostics;
		}

		public IReadOnlyList<ActionCandidate> Candidates { get; }
		public IReadOnlyList<ActionDiagnostic> Diagnostics { get; }
	}
}
