using Arch.Core;
using DwarvenFortification.GOAP;
using DwarvenFortification.Logging;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class GoapAgentPlanningService : IAgentPlanningService
	{
		readonly GoapPlanner planner;
		readonly IGoapPlanExecutor planExecutor;

		public GoapAgentPlanningService(GoapPlanner planner, IGoapPlanExecutor planExecutor)
		{
			this.planner = planner;
			this.planExecutor = planExecutor;
		}

		public bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent)
		{
			var plan = planner.Plan(agent);
			if (plan == null)
			{
				return false;
			}

			if (planExecutor.Enqueue(agent, plan))
			{
				return true;
			}

			context.Logger.Log(LogLevel.Warning, $"planner produced an invalid plan for {agent.GetName()}.");
			return false;
		}
	}
}