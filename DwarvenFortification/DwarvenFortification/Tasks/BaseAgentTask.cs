using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DwarvenFortification
{
	public abstract class BaseAgentTask : IAgentTask
	{
		public BaseAgentTask(Agent owner, int cost = 0)
		{
			this.owner = owner;
			this.Cost = cost;
			this.Progress = 0;
		}

		protected int Progress;
		protected int Cost;
		protected bool success = false;

		protected Agent owner;

		// IAgentTask
		public virtual bool Update()
			=> Progress++ >= Cost && success;

		public abstract void Draw(SpriteBatch sb);

		protected void Draw(SpriteBatch sb, Point tileIndex)
		{
			const int tileSize = 18;

			var srcRect = new Rectangle(
				tileSize * tileIndex.X,
				tileSize * tileIndex.Y,
				tileSize,
				tileSize);

			// progress to goal
			var goalPercent = Cost == 0 ? 1f : Progress / (float)Cost;
			const int thickness = 2;
			sb.FillRectangle(owner.Left, owner.Top, owner.Width, owner.Height / 4, Color.Black); // border
			sb.FillRectangle(owner.Left + thickness, owner.Top + thickness, (owner.Width - thickness * 2) * goalPercent, ((owner.Height / 4) - (thickness * 2)), Color.White); // inside

			// task icon
			sb.Draw(GameServices.Textures["ui"], owner.Position.ToVector2() + new Vector2(9, -22), srcRect, Color.White);
		}
	}
}
