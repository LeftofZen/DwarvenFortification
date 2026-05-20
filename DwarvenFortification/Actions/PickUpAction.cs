using Arch.Core;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace DwarvenFortification.Actions
{
	public class PickUpAction : BaseAgentAction
	{
		const int _cost = 100;
		const string _actionId = "pick-up";

		public PickUpAction(IActionRuntimeContext runtimeContext, Entity owner, Entity item) : base(runtimeContext, owner, _actionId, (int)(_cost * (1 - owner.GetStrength()))) => this.item = item;

		Entity item;

		protected override bool CanStart()
			=> owner.GetInventory().Count < owner.GetInventoryCapacity();

		protected override string BuildCannotStartReason()
			=> "Cannot pick up item because the inventory is full.";

		protected override AgentActionStatus OnTick()
		{
			var currentCell = owner.GetCurrentCell(runtimeContext.World);
			if (currentCell == null)
			{
				return FailAction("Cannot pick up item because the owner is not inside a valid cell.");
			}

			var foundItem = currentCell.ItemsInCell.FirstOrDefault(i => i.Equals(item) || i.GetItemDefinitionId() == item.GetItemDefinitionId());
			if (foundItem.Equals(default(Entity)))
			{
				return FailAction($"Could not find item '{item.GetItemDefinitionId()}' in the current cell.");
			}

			owner.AddInventoryItem(foundItem);
			_ = currentCell.ItemsInCell.Remove(foundItem);
			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(2, 0);
			Draw(sb, tileIndex);
		}
	}
}
