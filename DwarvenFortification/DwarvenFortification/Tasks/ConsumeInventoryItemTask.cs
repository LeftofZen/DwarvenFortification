using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace DwarvenFortification
{
	public class ConsumeInventoryItemTask : BaseAgentTask
	{
		readonly string itemDefinitionId;
		readonly bool restoreHunger;
		readonly bool restoreThirst;

		public ConsumeInventoryItemTask(ITaskRuntimeContext runtimeContext, Entity owner, string itemDefinitionId, bool restoreHunger, bool restoreThirst) : base(runtimeContext, owner, "consume-item", 1)
		{
			this.itemDefinitionId = itemDefinitionId;
			this.restoreHunger = restoreHunger;
			this.restoreThirst = restoreThirst;
		}

		public override bool IsStillValid(ISimulationWorld world)
			=> owner.HasItemDefinition(itemDefinitionId);

		protected override AgentTaskStatus OnTick()
		{
			var item = owner.GetInventory().FirstOrDefault(entity => string.Equals(entity.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));
			if (item.Equals(default(Entity)))
			{
				return FailTask($"No '{itemDefinitionId}' item was available to consume.");
			}

			var definition = item.Get<ItemDefinitionComponent>();
			if (restoreHunger)
			{
				owner.RestoreHunger(definition.NutritionValue > 0f ? definition.NutritionValue : 25f);
			}

			if (restoreThirst)
			{
				owner.RestoreThirst(definition.HydrationValue > 0f ? definition.HydrationValue : 35f);
			}

			owner.RemoveInventoryItem(item);
			AdvanceProgress(Cost);
			return CompleteTask();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(3, 0));
		}
	}
}