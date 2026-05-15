using Arch.Core;

namespace DwarvenFortification.ECS.Runtime.Agents
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