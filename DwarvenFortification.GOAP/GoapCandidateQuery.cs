namespace DwarvenFortification.GOAP
{
	public sealed class GoapCandidateQuery
	{
		public GoapCandidateQuery(IReadOnlyList<GoapActionCandidate> candidates, IReadOnlyList<GoapActionDiagnostic> diagnostics)
		{
			Candidates = candidates;
			Diagnostics = diagnostics;
		}

		public IReadOnlyList<GoapActionCandidate> Candidates { get; }
		public IReadOnlyList<GoapActionDiagnostic> Diagnostics { get; }
	}
}
