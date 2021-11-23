using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public interface IAgentTask
	{
		bool Update();
	}

	public interface IDrawableTask
	{
		void Draw(SpriteBatch sb);
	}

	public class MoveToTask : BaseAgentTask
	{
		public MoveToTask(Agent owner, Point goal) : base(owner)
		{
			this.Goal = goal;
		}

		public Point Goal;

		public override bool Update()
		{
			var direction = (Goal - owner.Position).ToVector2();
			var distance = direction.Length();

			direction.Normalize();
			if (distance < owner.Speed)
			{
				owner.Position = Goal;
				return true;
			}
			else
			{
				var newPos = owner.Position + (direction * owner.Speed).ToPoint();

				if (owner.world.CellTypeAtXY(newPos.X, newPos.Y) != CellType.Water)
				{
					owner.Position = newPos;
					return false;
				}
				else
				{
					return true;
				}

			}
		}

		public override void Draw(SpriteBatch sb)
		{
			throw new System.NotImplementedException();
		}
	}
	public class PickUpTask : BaseAgentTask
	{
		public PickUpTask(Agent owner, Item item) : base(owner)
		{
			this.item = item;
		}

		Item item;

		public override bool Update()
		{
			var foundItem = owner.CurrentCell.ItemsInCell.Find(i => i.Name == item.Name);
			if (foundItem != null)
			{
				owner.Inventory.Add(foundItem);
				owner.CurrentCell.ItemsInCell.Remove(foundItem);
			}
			return true;
		}

		public override void Draw(SpriteBatch sb)
		{
			throw new System.NotImplementedException();
		}
	}

	public class PutDownTask : BaseAgentTask
	{
		public PutDownTask(Agent owner, Item item) : base(owner)
		{
			this.items.Clear();
			this.items.Add(item);
		}

		public PutDownTask(Agent owner, IEnumerable<Item> items) : base(owner)
		{
			this.items = new List<Item>(items);
		}

		List<Item> items = new();

		public override bool Update()
		{
			for (int i = 0; i < items.Count; ++i)
			{
				owner.CurrentCell.ItemsInCell.Add(items[i]);
				_ = owner.Inventory.Remove(items[i]);
			}

			items.Clear();
			return true;
		}

		public override void Draw(SpriteBatch sb)
		{
			throw new System.NotImplementedException();
		}
	}

	public abstract class BaseAgentTask : IAgentTask, IDrawableTask
	{
		public BaseAgentTask(Agent owner)
		{
			this.owner = owner;
		}

		protected Agent owner;

		// IAgentTask
		public abstract bool Update();

		// IDrawableTask
		public abstract void Draw(SpriteBatch sb);
	}
}
