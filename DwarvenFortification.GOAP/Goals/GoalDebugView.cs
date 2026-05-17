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
			Plan candidatePlan,
			int effectivePriority)
		{
			Goal = goal;
			IsEligible = isEligible;
			IsSatisfied = isSatisfied;
			MissingRequiredFacts = missingRequiredFacts;
			ActiveBlockingFacts = activeBlockingFacts;
			CandidatePlan = candidatePlan;
			EffectivePriority = effectivePriority;
		}

		public Goal Goal { get; }
		public bool IsEligible { get; }
		public bool IsSatisfied { get; }
		public IReadOnlyList<string> MissingRequiredFacts { get; }
		public IReadOnlyList<string> ActiveBlockingFacts { get; }
		public Plan CandidatePlan { get; }

		/// <summary>
		/// The runtime-computed priority, which may differ from Goal.Priority when dynamic
		/// scaling is applied (e.g. priority rises as hunger/thirst/rest deficits deepen).
		/// </summary>
		public int EffectivePriority { get; }
	}
}
