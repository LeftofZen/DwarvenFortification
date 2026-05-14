using DwarvenFortification.Logging;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public static class AgentRuntimeFactory
	{
		public static IAgentRuntime CreateDefault(SimulationDefinitionRegistry definitions, ILogger logger, ITaskRuntimeContext taskRuntimeContext)
		{
			var planner = new GoapPlanner(definitions, new GoapWorldQueryService(definitions, () => taskRuntimeContext.World));
			var planExecutor = new GoapPlanExecutor(taskRuntimeContext);
			var planningService = new GoapAgentPlanningService(planner, planExecutor);
			var updateStages = new List<IAgentUpdateStage>
			{
				new NeedStateUpdateStage(),
				new TransientStateUpdateStage(),
				new TaskQueueIntegrityStage(),
				new PlanningStage(planningService),
				new TaskExecutionStage(),
			};

			return new AgentRuntimePipeline(updateStages, new SimpleAgentRenderer(), logger);
		}
	}
}