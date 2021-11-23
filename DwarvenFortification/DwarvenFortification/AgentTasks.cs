using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using MonoGame.Extended;

namespace DwarvenFortification
{
	public interface IAgentTask
	{
		bool Update();
		void Draw(SpriteBatch sb);
	}

	public class MoveToTask : BaseAgentTask
	{
		const int _cost = 0;

		public MoveToTask(Agent owner, Point goal) : base(owner, _cost)
		{
			this.Goal = goal;
		}

		public Point Goal;

		public override bool Update()
		{
			if (!success)
			{
				var direction = (Goal - owner.Position).ToVector2();
				var distance = direction.Length();

				if (Cost == 0)
				{
					Cost = (int)distance;
				}

				direction.Normalize();
				if (distance < owner.Speed)
				{
					owner.Position = Goal;
					success = true;
					Progress = Cost;
				}
				else
				{
					var newPos = owner.Position + (direction * owner.Speed).ToPoint();

					//if (owner.world.CellTypeAtXY(newPos.X, newPos.Y) != CellType.Water)
					{
						owner.Position = newPos;
						Progress = Cost - (int)(Goal - owner.Position).ToVector2().Length();
					}
				}

			}

			return base.Update();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(8, 0);
			Draw(sb, tileIndex);
		}
	}

	public class PickUpTask : BaseAgentTask
	{
		const int _cost = 200;

		public PickUpTask(Agent owner, Item item) : base(owner, _cost)
		{
			this.item = item;
		}

		Item item;

		public override bool Update()
		{
			if (!success)
			{
				var foundItem = owner.CurrentCell.ItemsInCell.Find(i => i.Name == item.Name);
				if (foundItem != null)
				{
					owner.Inventory.Add(foundItem);
					_ = owner.CurrentCell.ItemsInCell.Remove(foundItem);
					success = true;
				}
			}

			return base.Update();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(2, 0);
			Draw(sb, tileIndex);
		}
	}

	public class PutDownTask : BaseAgentTask
	{
		const int _cost = 200;

		public PutDownTask(Agent owner, Item item) : base(owner, _cost)
		{
			this.items.Clear();
			this.items.Add(item);
		}

		public PutDownTask(Agent owner, IEnumerable<Item> items) : base(owner, _cost)
		{
			this.items = new List<Item>(items);
		}

		List<Item> items = new();

		public override bool Update()
		{
			if (!success)
			{
				for (int i = 0; i < items.Count; ++i)
				{
					owner.CurrentCell.ItemsInCell.Add(items[i]);
					_ = owner.Inventory.Remove(items[i]);
					success = true;
				}

				items.Clear();
			}

			return base.Update();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(2, 1);
			Draw(sb, tileIndex);
		}
	}

	public abstract class BaseAgentTask : IAgentTask
	{
		public BaseAgentTask(Agent owner, int cost)
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
