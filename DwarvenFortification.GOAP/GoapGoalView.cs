namespace DwarvenFortification.GOAP
{
	public sealed class GoapGoalView
	{
		public GoapGoalView(
			GoapGoal goal,
			bool isEligible,
			bool isSatisfied,
			IReadOnlyList<string> missingRequiredFacts,
			GoapPlan candidatePlan,
			int effectivePriority)
		{
			Goal = goal;
			IsEligible = isEligible;
			IsSatisfied = isSatisfied;
			MissingRequiredStates = missingRequiredFacts;
			CandidatePlan = candidatePlan;
			EffectivePriority = effectivePriority;
		}

		public GoapGoal Goal { get; }
		public bool IsEligible { get; }
		public bool IsSatisfied { get; }
		public IReadOnlyList<string> MissingRequiredStates { get; }
		public GoapPlan CandidatePlan { get; }

		/// <summary>
		/// The runtime-computed priority, which may differ from GoapGoal.Priority when dynamic
		/// scaling is applied (e.g. priority rises as hunger/thirst/rest deficits deepen).
		/// </summary>
		public int EffectivePriority { get; }
	}
}
