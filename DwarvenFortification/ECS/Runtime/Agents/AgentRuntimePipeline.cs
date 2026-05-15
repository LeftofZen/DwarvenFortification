using Arch.Core;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class AgentRuntimePipeline : IAgentRuntime
	{
		readonly IReadOnlyList<IAgentUpdateStage> updateStages;
		readonly IAgentRenderer renderer;
		readonly ILogger logger;

		public AgentRuntimePipeline(IReadOnlyList<IAgentUpdateStage> updateStages, IAgentRenderer renderer, ILogger logger)
		{
			this.updateStages = updateStages;
			this.renderer = renderer;
			this.logger = logger;
		}

		public void Update(ISimulationWorld world, Entity agent)
		{
			var context = new AgentRuntimeContext(world, logger);
			foreach (var stage in updateStages)
			{
				stage.Update(context, agent);
			}
		}

		public void Draw(SpriteBatch spriteBatch, ISimulationWorld world, Entity agent)
		{
			renderer.Draw(spriteBatch, new AgentRuntimeContext(world, logger), agent);
		}
	}
}