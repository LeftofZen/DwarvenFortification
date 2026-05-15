using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace DwarvenFortification
{
	public class CollectItemsFromCellAction : BaseAgentAction
	{
		readonly Point targetCell;
		readonly int maxItems;

		public CollectItemsFromCellAction(IActionRuntimeContext runtimeContext, Entity owner, Point targetCell, int maxItems) : base(runtimeContext, owner, "collect-items", 1)
		{
			this.targetCell = targetCell;
			this.maxItems = maxItems;
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			var cell = world.CellAtCoords(targetCell);
			return cell != null && cell.ItemsInCell.Count > 0;
		}

		protected override AgentActionStatus OnTick()
		{
			var cell = runtimeContext.World.CellAtCoords(targetCell);
			if (cell == null)
			{
				return FailAction("Target cell does not exist for item collection.");
			}

			var availableSlots = owner.GetInventoryCapacity() - owner.GetInventory().Count;
			if (availableSlots <= 0)
			{
				return FailAction("Inventory is full.");
			}

			var items = cell.ItemsInCell.Take(System.Math.Min(maxItems, availableSlots)).ToList();
			if (items.Count == 0)
			{
				return FailAction("No collectible items remained in the target cell.");
			}

			foreach (var item in items)
			{
				var itemDefinitionId = item.GetItemDefinitionId();
				owner.AddInventoryItem(item);
				cell.ItemsInCell.Remove(item);
				if (runtimeContext.World.CellContainsItem(targetCell, itemDefinitionId))
				{
					owner.RememberItemLocation(itemDefinitionId, targetCell);
				}
				else
				{
					owner.ForgetItemLocation(itemDefinitionId);
				}
			}

			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(2, 0));
		}
	}
}