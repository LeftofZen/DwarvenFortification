using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public class StoreItemsInWorldObjectTask : BaseAgentTask
	{
		readonly Entity targetWorldObject;
		readonly Point targetCell;
		readonly List<Entity> items;

		public StoreItemsInWorldObjectTask(ITaskRuntimeContext runtimeContext, Entity owner, Entity targetWorldObject, IEnumerable<Entity> items) : base(runtimeContext, owner, "store-items", 1)
		{
			this.targetWorldObject = targetWorldObject;
			this.targetCell = targetWorldObject.GetCellReference();
			this.items = new List<Entity>(items);
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			var cell = world.CellAtCoords(targetCell);
			if (cell == null || !cell.TryGetWorldObject(out var currentWorldObject) || !currentWorldObject.Equals(targetWorldObject))
			{
				return false;
			}

			return owner.GetInventory().Any(item => !item.Get<ItemDefinitionComponent>().IsTool && currentWorldObject.CanStore(item) && currentWorldObject.HasInventorySpace());
		}

		protected override bool CanStart()
			=> targetWorldObject.IsStorageObject() && items.Count > 0;

		protected override string BuildCannotStartReason()
			=> "Cannot store items because the target is not a storage object or no items were supplied.";

		protected override AgentTaskStatus OnTick()
		{
			var placed = 0;
			foreach (var item in items.ToList())
			{
				if (!owner.GetInventory().Contains(item))
				{
					continue;
				}

				if (!targetWorldObject.CanStore(item) || !targetWorldObject.HasInventorySpace())
				{
					continue;
				}

				targetWorldObject.AddStoredItem(item);
				owner.RememberItemLocation(item.GetItemDefinitionId(), targetCell);
				owner.RemoveInventoryItem(item);
				placed++;
			}

			if (placed == 0)
			{
				return FailTask("No items could be stored in the target world object.");
			}

			AdvanceProgress(Cost);
			return CompleteTask();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(2, 1));
		}
	}
}