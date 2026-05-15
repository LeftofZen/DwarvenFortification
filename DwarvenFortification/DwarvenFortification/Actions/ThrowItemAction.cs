using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace DwarvenFortification
{
	public class ThrowItemAction : BaseAgentAction
	{
		readonly Entity target;
		readonly string itemDefinitionId;

		public ThrowItemAction(IActionRuntimeContext runtimeContext, Entity owner, Entity target, string itemDefinitionId) : base(runtimeContext, owner, "throw-item", 1)
		{
			this.target = target;
			this.itemDefinitionId = itemDefinitionId;
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			if (!owner.HasItemDefinition(itemDefinitionId))
			{
				return false;
			}

			var ownerCell = world.CoordsAtXY(owner.GetPosition());
			var targetCell = world.CoordsAtXY(target.GetPosition());
			return Vector2.DistanceSquared(ownerCell.ToVector2(), targetCell.ToVector2()) <= 64f;
		}

		protected override AgentActionStatus OnTick()
		{
			var item = owner.GetInventory().FirstOrDefault(entity => string.Equals(entity.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));
			if (item.Equals(default(Entity)))
			{
				return FailAction($"No '{itemDefinitionId}' item was available to throw.");
			}

			owner.RemoveInventoryItem(item);
			var targetCell = runtimeContext.World.GetAgents().Contains(target)
				? target.GetCurrentCell(runtimeContext.World)
				: null;
			targetCell?.ItemsInCell.Add(item);
			if (targetCell != null)
			{
				owner.RememberItemLocation(itemDefinitionId, runtimeContext.World.CoordsAtXY(target.GetPosition()));
			}

			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(7, 0));
		}
	}
}