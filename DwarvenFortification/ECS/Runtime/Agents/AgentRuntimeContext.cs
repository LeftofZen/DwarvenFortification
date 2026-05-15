using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.World;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class AgentRuntimeContext
	{
		public AgentRuntimeContext(ISimulationWorld world, ILogger logger)
		{
			World = world;
			Logger = logger;
		}

		public ISimulationWorld World { get; }
		public ILogger Logger { get; }
	}
}