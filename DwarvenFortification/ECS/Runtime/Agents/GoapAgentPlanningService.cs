using Arch.Core;
using DwarvenFortification.GOAP.Plans;
using DwarvenFortification.Logging;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class GoapAgentPlanningService : IAgentPlanningService
	{
		readonly Planner planner;
		readonly IPlanSelector planSelector;
		readonly IPlanExecutor planExecutor;

		public GoapAgentPlanningService(Planner planner, IPlanSelector planSelector, IPlanExecutor planExecutor)
		{
			this.planner = planner;
			this.planSelector = planSelector;
			this.planExecutor = planExecutor;
		}

		public bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent)
		{
			var planningSnapshot = planner.Inspect(agent);
			var plan = planSelector.SelectCandidatePlan(agent, planningSnapshot);
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