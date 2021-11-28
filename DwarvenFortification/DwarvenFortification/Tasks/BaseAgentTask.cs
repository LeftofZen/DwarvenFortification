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

			GameServices.Logger.Log(Logging.LogLevel.Debug, $"new task created: {this.ToString()}");
		}

		protected int Progress;
		protected int Cost;
		protected bool success = false;

		protected Agent owner;

		public override string ToString()
			=> $"Task={this.GetType().Name} Agent={owner.Name} Cost={Cost} Progress={Progress}";

		// IAgentTask
		public virtual bool Update()
		{
			Progress++;

			GameServices.Logger.Log(Logging.LogLevel.Debug, $"{this}");

			return Progress >= Cost && success;
		}

		public abstract void Draw(SpriteBatch sb);

		protected void Draw(SpriteBatch sb, Point tileIndex)
		{
			const int tileSize = 18;

			var srcRect = new Rectangle(
				tileSize * tileIndex.X,
				tileSize * tileIndex.Y,
				tileSize,
				tileSize);

			// task icon
			sb.Draw(GameServices.Textures["ui"], owner.Position.ToVector2() + new Vector2(9, -22), srcRect, Color.White);

			// progress bar to goal
			var goalPercent = Cost == 0 ? 1f : Progress / (float)Cost;
			const int borderThickness = 2;
			int barHeight = owner.Height / 4;
			sb.FillRectangle(owner.Left, owner.Top - barHeight, owner.Width, barHeight, Color.Black); // border
			sb.FillRectangle(owner.Left + borderThickness, owner.Top - barHeight + borderThickness, (owner.Width - borderThickness * 2) * goalPercent, (barHeight - (borderThickness * 2)), Color.White); // inside
		}
	}
}
