using DwarvenFortification.Logging;

namespace DwarvenFortification
{
	public interface IActionRuntimeContext
	{
		ISimulationWorld World { get; }
		ILogger Logger { get; }
		SimulationRenderAssets RenderAssets { get; }
	}
}