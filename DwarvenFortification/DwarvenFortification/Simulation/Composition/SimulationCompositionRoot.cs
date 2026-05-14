using DwarvenFortification.Logging;
using Microsoft.Xna.Framework;

namespace DwarvenFortification
{
	public static class SimulationCompositionRoot
	{
		public static SimulationRuntime Create(SimulationDefinitionRegistry definitions, SimulationRenderAssets renderAssets, ILogger logger, ImGuiSimulationUi ui)
		{
			GridWorld world = null;
			var taskRuntimeContext = new TaskRuntimeContext(() => world, logger, renderAssets);
			var entityFactory = new SimulationEntityFactory(definitions);
			var agentRuntime = AgentRuntimeFactory.CreateDefault(definitions, logger, taskRuntimeContext);
			world = new GridWorld(48, 32, agentRuntime, definitions, entityFactory, renderAssets, taskRuntimeContext, ui);

			return new SimulationRuntime(definitions, renderAssets, logger, entityFactory, agentRuntime, world, ui);
		}
	}
}