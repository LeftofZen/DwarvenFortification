using Arch.Core;

namespace DwarvenFortification.GOAP.Plans
{
	public interface IPlanSelector
	{
		Plan SelectCandidatePlan(Entity agent, PlanningSnapshot snapshot);
	}

	public sealed class DefaultGoapPlanSelector : IPlanSelector
	{
		public Plan SelectCandidatePlan(Entity agent, PlanningSnapshot snapshot)
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
