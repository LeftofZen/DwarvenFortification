namespace DwarvenFortification.GOAP
{
	public sealed class GoapSnapshot
	{
		public GoapSnapshot(
			IReadOnlyList<string> currentState,
			IReadOnlyList<GoapActionCandidate> actionManifestations,
			IReadOnlyList<GoapActionDiagnostic> actionDiagnostics,
			IReadOnlyList<GoapGoalView> goals,
			IReadOnlyList<GoapPlan> candidatePlans)
		{
			CurrentState = currentState;
			ActionManifestations = actionManifestations;
			ActionDiagnostics = actionDiagnostics;
			Goals = goals;
			CandidatePlans = candidatePlans;
		}

		public IReadOnlyList<string> CurrentState { get; }
		public IReadOnlyList<GoapActionCandidate> ActionManifestations { get; }
		public IReadOnlyList<GoapActionDiagnostic> ActionDiagnostics { get; }
		public IReadOnlyList<GoapGoalView> Goals { get; }
		public IReadOnlyList<GoapPlan> CandidatePlans { get; }
	}
}
