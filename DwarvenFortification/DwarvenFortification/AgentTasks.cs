using Microsoft.Xna.Framework;

namespace DwarvenFortification
{
	public interface IAgentTask
	{
		bool Update();
	}

	public class MoveToTask : BaseAgentTask, IAgentTask
	{
		public MoveToTask(Agent owner, Point goal) : base(owner)
		{
			this.goal = goal;
		}

		Point goal;

		public bool Update()
		{
			var direction = (goal - owner.Position).ToVector2();
			var distance = direction.Length();

			direction.Normalize();
			if (distance < owner.Speed)
			{
				owner.Position = goal;
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
	}
	public class PickUpTask : BaseAgentTask, IAgentTask
	{
		public PickUpTask(Agent owner, Item item) : base(owner)
		{
			this.item = item;
		}

		Item item;

		public bool Update()
		{
			var foundItem = owner.CurrentCell.ItemsInCell.Find(i => i.Name == item.Name);
			if (foundItem != null)
			{
				owner.Inventory.Add(foundItem);
				owner.CurrentCell.ItemsInCell.Remove(foundItem);
			}
			return true;
		}
	}

	public class PutDownTask : BaseAgentTask, IAgentTask
	{
		public PutDownTask(Agent owner, Item item) : base(owner)
		{
			this.item = item;
		}

		Item item;

		public bool Update()
		{
			var foundItem = owner.Inventory.Find(i => i.Name == item.Name);
			if (foundItem != null)
			{
				owner.CurrentCell.ItemsInCell.Add(foundItem);
				_ = owner.Inventory.Remove(foundItem);
			}
			return true;
		}
	}

	public abstract class BaseAgentTask
	{
		public BaseAgentTask(Agent owner)
		{
			this.owner = owner;
		}

		protected Agent owner;
	}
}
