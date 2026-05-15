using Arch.Core;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public interface IAgentRuntime
	{
		void Update(ISimulationWorld world, Entity agent);
		void Draw(SpriteBatch spriteBatch, ISimulationWorld world, Entity agent);
	}
}