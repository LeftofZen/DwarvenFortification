using Arch.Core;

namespace DwarvenFortification.GOAP
{
	public interface IGoapPlanSelector
	{
		GoapPlan SelectCandidatePlan(Entity agent, GoapSnapshot snapshot);
	}

	public sealed class GoapPlanSelector : IGoapPlanSelector
	{
		public GoapPlan SelectCandidatePlan(Entity agent, GoapSnapshot snapshot)
		{
			if (snapshot == null || snapshot.CandidatePlans.Count == 0)
			{
				return null;
			}

			// Pick the plan whose goal has the highest effective priority (snapshot.Goals is already sorted descending).
			foreach (var goalView in snapshot.Goals)
			{
				var matching = snapshot.CandidatePlans.FirstOrDefault(plan =>
					string.Equals(plan.Goal.Id, goalView.Goal.Id, StringComparison.OrdinalIgnoreCase));
				if (matching != null)
				{
					return matching;
				}
			}

			return snapshot.CandidatePlans.FirstOrDefault();
		}
	}
}
