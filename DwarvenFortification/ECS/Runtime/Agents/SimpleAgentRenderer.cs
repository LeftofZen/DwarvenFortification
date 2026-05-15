using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class SimpleAgentRenderer : IAgentRenderer
	{
		public void Draw(SpriteBatch spriteBatch, AgentRuntimeContext context, Entity agent)
		{
			var position = agent.GetPosition();
			spriteBatch.DrawEllipse(position.ToVector2(), new Vector2(16), 8, Color.White, 3);

			var cellBounds = agent.GetCellBounds(context.World);
			if (cellBounds != Rectangle.Empty)
			{
				spriteBatch.DrawRectangle(cellBounds, Color.Blue, 1);
			}

			if (agent.TryPeekAction(out var currentAction))
			{
				currentAction.Draw(spriteBatch);
			}
		}
	}
}