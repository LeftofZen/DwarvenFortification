using DwarvenFortification.Logging;

namespace DwarvenFortification
{
	public interface ITaskRuntimeContext
	{
		ISimulationWorld World { get; }
		ILogger Logger { get; }
		SimulationRenderAssets RenderAssets { get; }
	}
}