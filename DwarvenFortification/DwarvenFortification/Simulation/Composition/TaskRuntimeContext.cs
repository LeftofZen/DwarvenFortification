using DwarvenFortification.Logging;
using System;

namespace DwarvenFortification
{
	public sealed class TaskRuntimeContext : ITaskRuntimeContext
	{
		readonly Func<ISimulationWorld> worldAccessor;

		public TaskRuntimeContext(Func<ISimulationWorld> worldAccessor, ILogger logger, SimulationRenderAssets renderAssets)
		{
			this.worldAccessor = worldAccessor;
			Logger = logger;
			RenderAssets = renderAssets;
		}

		public ISimulationWorld World => worldAccessor();
		public ILogger Logger { get; }
		public SimulationRenderAssets RenderAssets { get; }
	}
}