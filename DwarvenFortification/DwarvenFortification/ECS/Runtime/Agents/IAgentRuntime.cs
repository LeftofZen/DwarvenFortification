using Arch.Core;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public interface IAgentRuntime
	{
		void Update(ISimulationWorld world, Entity agent);
		void Draw(SpriteBatch spriteBatch, ISimulationWorld world, Entity agent);
	}
}