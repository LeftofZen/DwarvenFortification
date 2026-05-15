using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.World;

namespace DwarvenFortification.Simulation.Composition
{
	public interface IActionRuntimeContext
	{
		ISimulationWorld World { get; }
		ILogger Logger { get; }
		SimulationRenderAssets RenderAssets { get; }
	}
}