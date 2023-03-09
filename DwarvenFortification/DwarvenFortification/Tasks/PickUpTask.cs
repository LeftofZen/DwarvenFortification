using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public class PickUpTask : BaseAgentTask
	{
		const int _cost = 100;

		public PickUpTask(Agent owner, Item item) : base(owner, (int)(_cost * (1 - owner.Strength)))
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
				}
				success = true;
				//else
				//{
				//	success = false;
				//}
			}

			return base.Update();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(2, 0);
			Draw(sb, tileIndex);
		}
	}
}
