using Arch.Core;
using DwarvenFortification.Actions;
using DwarvenFortification.GOAP;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.Goap;
using System.Linq;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class GoapAgentPlanningService : IAgentPlanningService
	{
		const int IdleDurationTicks = 60;

		readonly GoapPlanner planner;
		readonly IGoapPlanExecutor planExecutor;
		readonly IActionRuntimeContext runtimeContext;
		readonly IGoapWorldQueryService queryService;

		public GoapAgentPlanningService(GoapPlanner planner, IGoapPlanExecutor planExecutor, IActionRuntimeContext runtimeContext, IGoapWorldQueryService queryService)
		{
			this.planner = planner;
			this.planExecutor = planExecutor;
			this.runtimeContext = runtimeContext;
			this.queryService = queryService;
		}

		public bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent)
		{
			var currentState = queryService.BuildCurrentState(agent);
			var goapAgent = new EntityGoapAgent(agent, currentState);
			var plan = planner.CreatePlan(goapAgent);
			if (plan == null)
			{
				agent.EnqueueAction(new TimedAction(runtimeContext, agent, "idle", IdleDurationTicks));
				return false;
			}

			context.Logger.Log(LogLevel.Info, $"{agent.GetName()} enqueuing plan for goal '{plan.Goal.Id}' with {plan.Actions.Count} steps: [{string.Join(" -> ", plan.Actions.Select(s => s.Id))}]");

			if (planExecutor.Enqueue(goapAgent, plan))
			{
				return true;
			}

			context.Logger.Log(LogLevel.Warning, $"planner produced an invalid plan for {agent.GetName()}.");
			agent.EnqueueAction(new TimedAction(runtimeContext, agent, "idle", IdleDurationTicks));
			return false;
		}
	}
}