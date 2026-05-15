using Arch.Core;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public interface IAgentRenderer
	{
		void Draw(SpriteBatch spriteBatch, AgentRuntimeContext context, Entity agent);
	}
}