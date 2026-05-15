using DwarvenFortification.GOAP;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.Composition;
using System.Collections.Generic;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public static class AgentRuntimeFactory
	{
		public static IAgentRuntime CreateDefault(SimulationDefinitionRegistry definitions, ILogger logger, IActionRuntimeContext taskRuntimeContext)
		{
			var planner = new GoapPlanner(definitions, new GoapWorldQueryService(definitions, () => taskRuntimeContext.World));
			var planExecutor = new GoapPlanExecutor(taskRuntimeContext);
			var planningService = new GoapAgentPlanningService(planner, planExecutor);
			var updateStages = new List<IAgentUpdateStage>
			{
				new NeedStateUpdateStage(),
				new TransientStateUpdateStage(),
				new ActionQueueIntegrityStage(),
				new PlanningStage(planningService),
				new ActionExecutionStage(),
			};

			return new AgentRuntimePipeline(updateStages, new SimpleAgentRenderer(), logger);
		}
	}
}