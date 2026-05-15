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
			=> snapshot?.CandidatePlans.FirstOrDefault();
	}
}
