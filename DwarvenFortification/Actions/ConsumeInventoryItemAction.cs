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
		readonly SimulationEntityExtensions.ConsumableKind consumableKind;
		readonly bool applyMacronutrients;
		readonly bool applyFluids;

		/// <summary>
		/// Consume a specific item by definition ID (resolved at enqueue time).
		/// </summary>
		public ConsumeInventoryItemAction(IActionRuntimeContext runtimeContext, Entity owner, string itemDefinitionId, bool applyMacronutrients, bool applyFluids) : base(runtimeContext, owner, "consume-item", 1)
		{
			this.itemDefinitionId = itemDefinitionId;
			this.consumableKind = SimulationEntityExtensions.ConsumableKind.None;
			this.applyMacronutrients = applyMacronutrients;
			this.applyFluids = applyFluids;
		}

		/// <summary>
		/// Consume the best available item of <paramref name="kind"/> (resolved lazily at tick time).
		/// Use this when the item will be placed in inventory by a prior plan step.
		/// </summary>
		public ConsumeInventoryItemAction(IActionRuntimeContext runtimeContext, Entity owner, SimulationEntityExtensions.ConsumableKind kind) : base(runtimeContext, owner, "consume-item", 1)
		{
			this.itemDefinitionId = null;
			this.consumableKind = kind;
			this.applyMacronutrients = kind == SimulationEntityExtensions.ConsumableKind.Food;
			this.applyFluids = kind == SimulationEntityExtensions.ConsumableKind.Drink;
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			// If we have a specific item ID, check inventory directly.
			// If using lazy kind-based lookup, always consider valid — the action will fail
			// gracefully in OnTick if nothing suitable is in inventory by the time it runs.
			return itemDefinitionId != null
				? owner.HasItemDefinition(itemDefinitionId)
				: true;
		}

		protected override AgentActionStatus OnTick()
		{
			Entity item;

			if (itemDefinitionId != null)
			{
				item = owner.GetInventory().FirstOrDefault(entity => string.Equals(entity.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));
				if (item.Equals(default(Entity)))
				{
					return FailAction($"No '{itemDefinitionId}' item was available to consume.");
				}
			}
			else
			{
				if (!owner.TrySelectConsumableItem(consumableKind, out item))
				{
					return FailAction($"No suitable {consumableKind} item was available to consume.");
				}
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