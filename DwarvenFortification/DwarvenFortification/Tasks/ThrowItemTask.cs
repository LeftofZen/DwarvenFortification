using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace DwarvenFortification
{
	public class ThrowItemTask : BaseAgentTask
	{
		readonly Entity target;
		readonly string itemDefinitionId;

		public ThrowItemTask(ITaskRuntimeContext runtimeContext, Entity owner, Entity target, string itemDefinitionId) : base(runtimeContext, owner, "throw-item", 1)
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

		protected override AgentTaskStatus OnTick()
		{
			var item = owner.GetInventory().FirstOrDefault(entity => string.Equals(entity.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));
			if (item.Equals(default(Entity)))
			{
				return FailTask($"No '{itemDefinitionId}' item was available to throw.");
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
			return CompleteTask();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(7, 0));
		}
	}
}