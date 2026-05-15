using DwarvenFortification.GOAP.Actions;

namespace DwarvenFortification.GOAP.Plans
{
	public sealed class PlanningSnapshot
	{
		public PlanningSnapshot(
			IReadOnlyList<string> currentFacts,
			IReadOnlyList<ActionCandidate> actionManifestations,
			IReadOnlyList<ActionDiagnostic> actionDiagnostics,
			IReadOnlyList<GoalDebugView> goals,
			IReadOnlyList<Plan> candidatePlans)
		{
			CurrentFacts = currentFacts;
			ActionManifestations = actionManifestations;
			ActionDiagnostics = actionDiagnostics;
			Goals = goals;
			CandidatePlans = candidatePlans;
		}

		public IReadOnlyList<string> CurrentFacts { get; }
		public IReadOnlyList<ActionCandidate> ActionManifestations { get; }
		public IReadOnlyList<ActionDiagnostic> ActionDiagnostics { get; }
		public IReadOnlyList<GoalDebugView> Goals { get; }
		public IReadOnlyList<Plan> CandidatePlans { get; }
	}
}
