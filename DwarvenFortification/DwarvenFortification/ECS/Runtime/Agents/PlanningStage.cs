using Arch.Core;

namespace DwarvenFortification
{
	public sealed class PlanningStage : IAgentUpdateStage
	{
		readonly IAgentPlanningService planningService;

		public PlanningStage(IAgentPlanningService planningService)
		{
			this.planningService = planningService;
		}

		public void Update(AgentRuntimeContext context, Entity agent)
		{
			if (agent.HasQueuedActions())
			{
				return;
			}

			planningService.TryEnqueuePlan(context, agent);
		}
	}
}