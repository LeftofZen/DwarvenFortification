using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.World;
using System;

namespace DwarvenFortification.Simulation.Composition
{
	public sealed class ActionRuntimeContext : IActionRuntimeContext
	{
		readonly Func<ISimulationWorld> worldAccessor;

		public ActionRuntimeContext(Func<ISimulationWorld> worldAccessor, ILogger logger, SimulationRenderAssets renderAssets)
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