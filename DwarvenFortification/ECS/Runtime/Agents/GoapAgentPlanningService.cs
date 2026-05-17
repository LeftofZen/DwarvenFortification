using Arch.Core;
using DwarvenFortification.Actions;
using DwarvenFortification.GOAP.Plans;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.Composition;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class GoapAgentPlanningService : IAgentPlanningService
	{
		const int IdleDurationTicks = 60;

		readonly Planner planner;
		readonly IPlanSelector planSelector;
		readonly IPlanExecutor planExecutor;
		readonly IActionRuntimeContext runtimeContext;

		public GoapAgentPlanningService(Planner planner, IPlanSelector planSelector, IPlanExecutor planExecutor, IActionRuntimeContext runtimeContext)
		{
			this.planner = planner;
			this.planSelector = planSelector;
			this.planExecutor = planExecutor;
			this.runtimeContext = runtimeContext;
		}

		public bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent)
		{
			var planningSnapshot = planner.Inspect(agent);
			var plan = planSelector.SelectCandidatePlan(agent, planningSnapshot);
			if (plan == null)
			{
				agent.EnqueueAction(new TimedAction(runtimeContext, agent, "idle", IdleDurationTicks));
				return false;
			}

			if (planExecutor.Enqueue(agent, plan))
			{
				return true;
			}

			context.Logger.Log(LogLevel.Warning, $"planner produced an invalid plan for {agent.GetName()}.");
			agent.EnqueueAction(new TimedAction(runtimeContext, agent, "idle", IdleDurationTicks));
			return false;
		}
	}
}