using Arch.Core;
using DwarvenFortification.Actions;
using DwarvenFortification.ECS;
using DwarvenFortification.GOAP;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.Composition;
using System.Linq;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class GoapAgentPlanningService : IAgentPlanningService
	{
		const int IdleDurationTicks = 60;

		readonly SimulationDefinitionRegistry definitions;
		readonly IGoapPlanExecutor planExecutor;
		readonly IActionRuntimeContext runtimeContext;
		readonly IGoapWorldQueryService queryService;

		public GoapAgentPlanningService(SimulationDefinitionRegistry definitions, IGoapPlanExecutor planExecutor, IActionRuntimeContext runtimeContext, IGoapWorldQueryService queryService)
		{
			this.definitions = definitions;
			this.planExecutor = planExecutor;
			this.runtimeContext = runtimeContext;
			this.queryService = queryService;
		}

		public bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent)
		{
			var currentState = queryService.BuildCurrentState(agent);
			var numericState = queryService.BuildNumericState(agent);
			var goapAgent = SimulationGoapAgentFactory.CreateAgent(definitions, agent.GetName(), currentState, numericState);
			var plan = goapAgent.CurrentGoals()
				.Select(goal => GoapPlan.Find(goapAgent, goal))
				.FirstOrDefault(candidate => candidate != null);
			if (plan == null)
			{
				agent.EnqueueAction(new TimedAction(runtimeContext, agent, "idle", IdleDurationTicks));
				return false;
			}

			context.Logger.Log(LogLevel.Info, $"{agent.GetName()} enqueuing plan for goal '{plan.Goal.Id}' with {plan.Actions.Count} steps: [{string.Join(" -> ", plan.Actions.Cast<SimulationGoapAction>().Select(s => s.Id))}]");

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