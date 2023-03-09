using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public class PutDownTask : BaseAgentTask
	{
		const int _cost = 100;

		public PutDownTask(Agent owner, Item item) : base(owner, (int)(_cost * (1 - owner.Strength)))
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
}
