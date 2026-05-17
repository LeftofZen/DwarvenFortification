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
	public class ConsumeInventoryItemAction : BaseAgentAction
	{
		readonly string itemDefinitionId;
		readonly bool applyMacronutrients;
		readonly bool applyFluids;

		public ConsumeInventoryItemAction(IActionRuntimeContext runtimeContext, Entity owner, string itemDefinitionId, bool applyMacronutrients, bool applyFluids) : base(runtimeContext, owner, "consume-item", 1)
		{
			this.itemDefinitionId = itemDefinitionId;
			this.applyMacronutrients = applyMacronutrients;
			this.applyFluids = applyFluids;
		}

		public override bool IsStillValid(ISimulationWorld world)
			=> owner.HasItemDefinition(itemDefinitionId);

		protected override AgentActionStatus OnTick()
		{
			var item = owner.GetInventory().FirstOrDefault(entity => string.Equals(entity.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));
			if (item.Equals(default(Entity)))
			{
				return FailAction($"No '{itemDefinitionId}' item was available to consume.");
			}

			var definition = item.Get<ItemDefinitionComponent>();
			owner.AbsorbNutrition(definition.Nutrition, includeMacronutrients: applyMacronutrients, includeFluids: applyFluids);

			owner.RemoveInventoryItem(item);
			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(3, 0));
		}
	}
}