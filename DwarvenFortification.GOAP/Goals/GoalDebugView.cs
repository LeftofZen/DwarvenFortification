using DwarvenFortification.GOAP.Plans;

namespace DwarvenFortification.GOAP
{
	public sealed class GoalDebugView
	{
		public GoalDebugView(
			Goal goal,
			bool isEligible,
			bool isSatisfied,
			IReadOnlyList<string> missingRequiredFacts,
			IReadOnlyList<string> activeBlockingFacts,
			Plan candidatePlan)
		{
			Goal = goal;
			IsEligible = isEligible;
			IsSatisfied = isSatisfied;
			MissingRequiredFacts = missingRequiredFacts;
			ActiveBlockingFacts = activeBlockingFacts;
			CandidatePlan = candidatePlan;
		}

		public Goal Goal { get; }
		public bool IsEligible { get; }
		public bool IsSatisfied { get; }
		public IReadOnlyList<string> MissingRequiredFacts { get; }
		public IReadOnlyList<string> ActiveBlockingFacts { get; }
		public Plan CandidatePlan { get; }
	}
}
