using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace DwarvenFortification.Actions
{
	public class RetrieveRememberedItemAction : BaseAgentAction
	{
		readonly string itemDefinitionId;
		readonly Point targetCell;

		public RetrieveRememberedItemAction(IActionRuntimeContext runtimeContext, Entity owner, string itemDefinitionId, Point targetCell) : base(runtimeContext, owner, "retrieve-known-item", 1)
		{
			this.itemDefinitionId = itemDefinitionId;
			this.targetCell = targetCell;
		}

		public override bool IsStillValid(ISimulationWorld world)
			// Only check that the item is still physically at the target cell.
			// Do NOT require the agent to have already memorised the location — this action may
			// be queued as part of a plan whose earlier search step hasn't run yet.
			=> world.CellContainsItem(targetCell, itemDefinitionId);

		protected override AgentActionStatus OnTick()
		{
			var cell = runtimeContext.World.CellAtCoords(targetCell);
			if (cell == null)
			{
				owner.ForgetItemLocation(itemDefinitionId);
				return FailAction($"The remembered location for '{itemDefinitionId}' is invalid.");
			}

			var groundItem = cell.ItemsInCell.FirstOrDefault(item => string.Equals(item.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));
			if (!groundItem.Equals(default(Entity)))
			{
				owner.AddInventoryItem(groundItem);
				cell.ItemsInCell.Remove(groundItem);
				if (runtimeContext.World.CellContainsItem(targetCell, itemDefinitionId))
				{
					owner.RememberItemLocation(itemDefinitionId, targetCell);
				}
				else
				{
					owner.ForgetItemLocation(itemDefinitionId);
				}

				AdvanceProgress(Cost);
				return CompleteAction();
			}

			if (cell.TryGetWorldObject(out var worldObject) && worldObject.Has<InventoryComponent>())
			{
				ref var inventory = ref worldObject.Get<InventoryComponent>();
				var storedItem = inventory.Items.FirstOrDefault(item => string.Equals(item.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));
				if (!storedItem.Equals(default(Entity)))
				{
					inventory.Items.Remove(storedItem);
					owner.AddInventoryItem(storedItem);
					if (runtimeContext.World.CellContainsItem(targetCell, itemDefinitionId))
					{
						owner.RememberItemLocation(itemDefinitionId, targetCell);
					}
					else
					{
						owner.ForgetItemLocation(itemDefinitionId);
					}

					AdvanceProgress(Cost);
					return CompleteAction();
				}
			}

			owner.ForgetItemLocation(itemDefinitionId);
			return FailAction($"The remembered location no longer contains '{itemDefinitionId}'.");
		}

		public override void Draw(SpriteBatch sb) => Draw(sb, new Point(5, 1));
	}
}