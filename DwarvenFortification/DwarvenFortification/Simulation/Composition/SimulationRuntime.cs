using DwarvenFortification.Logging;

namespace DwarvenFortification
{
	public sealed class SimulationRuntime
	{
		public SimulationRuntime(
			SimulationDefinitionRegistry definitions,
			SimulationRenderAssets renderAssets,
			ILogger logger,
			ISimulationEntityFactory entityFactory,
			IAgentRuntime agentRuntime,
			GridWorld world,
			ImGuiSimulationUi ui)
		{
			Definitions = definitions;
			RenderAssets = renderAssets;
			Logger = logger;
			EntityFactory = entityFactory;
			AgentRuntime = agentRuntime;
			World = world;
			Ui = ui;
		}

		public SimulationDefinitionRegistry Definitions { get; }
		public SimulationRenderAssets RenderAssets { get; }
		public ILogger Logger { get; }
		public ISimulationEntityFactory EntityFactory { get; }
		public IAgentRuntime AgentRuntime { get; }
		public GridWorld World { get; }
		public ImGuiSimulationUi Ui { get; }
	}
}