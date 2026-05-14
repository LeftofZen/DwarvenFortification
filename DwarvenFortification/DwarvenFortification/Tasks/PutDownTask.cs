using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public class PutDownTask : BaseAgentTask
	{
		const int _cost = 100;
		const string _actionId = "put-down";

		public PutDownTask(ITaskRuntimeContext runtimeContext, Entity owner, Entity item) : base(runtimeContext, owner, _actionId, (int)(_cost * (1 - owner.GetStrength())))
		{
			this.items.Clear();
			this.items.Add(item);
		}

		public PutDownTask(ITaskRuntimeContext runtimeContext, Entity owner, IEnumerable<Entity> items) : base(runtimeContext, owner, _actionId, _cost)
		{
			this.items = new List<Entity>(items);
		}

		List<Entity> items = new();

		protected override bool CanStart()
			=> items.Count > 0;

		protected override string BuildCannotStartReason()
			=> "Cannot put down items because no items were supplied.";

		protected override AgentTaskStatus OnTick()
		{
			var currentCell = owner.GetCurrentCell(runtimeContext.World);
			if (currentCell == null)
			{
				return FailTask("Cannot put down items because the owner is not inside a valid cell.");
			}

			var targetStorage = currentCell.TryGetWorldObject(out var worldObject) && worldObject.IsStorageObject()
				? worldObject
				: default;

			var placedCount = 0;
			for (int i = 0; i < items.Count; ++i)
			{
				if (!owner.GetInventory().Contains(items[i]))
				{
					continue;
				}

				if (!targetStorage.Equals(default(Entity)) && targetStorage.CanStore(items[i]) && targetStorage.HasInventorySpace())
				{
					targetStorage.AddStoredItem(items[i]);
					owner.RememberItemLocation(items[i].GetItemDefinitionId(), runtimeContext.World.CoordsAtXY(owner.GetPosition()));
				}
				else
				{
					currentCell.ItemsInCell.Add(items[i]);
					owner.RememberItemLocation(items[i].GetItemDefinitionId(), runtimeContext.World.CoordsAtXY(owner.GetPosition()));
				}

				_ = owner.RemoveInventoryItem(items[i]);
				placedCount++;
			}

			items.Clear();
			if (placedCount == 0)
			{
				return FailTask("No matching items could be put down from the owner's inventory.");
			}

			AdvanceProgress(Cost);
			return CompleteTask();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(2, 1);
			Draw(sb, tileIndex);
		}
	}
}
