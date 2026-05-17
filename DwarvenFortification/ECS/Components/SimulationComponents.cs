using System;
using System.Collections.Generic;
using System.Linq;
using DwarvenFortification.ECS.Authoring;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.ECS.Components
{
	public readonly record struct DefinitionIdentityComponent(string Id, string Name);

	public readonly record struct TagCollectionComponent(string[] Values)
	{
		public bool Contains(string value)
			=> Values.Any(tag => string.Equals(tag, value, StringComparison.OrdinalIgnoreCase));
	}

	public readonly record struct ItemNutritionComponent(float CarbohydratesGrams, float ProteinGrams, float FatGrams, float SugarGrams, float FiberGrams, float FluidLiters);

	public readonly record struct ItemDefinitionComponent(bool IsTool, bool Stackable, float WeightKg, string[] LearnedFacts, ItemNutritionComponent Nutrition, float ThrowRange);

	public readonly record struct MaterialCostComponent
	{
		public string ItemId { get; init; }
		public string[] ItemFilter { get; init; }
		public int Quantity { get; init; }

		public MaterialCostComponent(string itemId, int quantity)
			: this() { ItemId = itemId; ItemFilter = Array.Empty<string>(); Quantity = quantity; }

		public MaterialCostComponent(string[] itemFilter, int quantity)
			: this() { ItemId = string.Join(",", itemFilter); ItemFilter = itemFilter; Quantity = quantity; }

		public MaterialCostComponent(string itemId, string[] itemFilter, int quantity)
			: this() { ItemId = itemId; ItemFilter = itemFilter; Quantity = quantity; }

		public bool UsesFilter => ItemFilter != null && ItemFilter.Length > 0;

		public MaterialCostComponent WithQuantity(int newQuantity) => new(ItemId, ItemFilter ?? Array.Empty<string>(), newQuantity);
	}

	public readonly record struct CraftRecipeComponent(string Id, string Name, string[] RequiredFacts, MaterialCostComponent[] Inputs, string OutputItemId, int OutputQuantity);

	public readonly record struct ActionRequirementComponent(
		string[] RequiredItemTags,
		string[] RequiredTargetTags,
		string[] RequiredBodyParts,
		string[] RequiredOrgans,
		string[] RequiredSystems,
		string[] RequiredFacts,
		string[] BlockedByFacts,
		bool RequiresFreeInventorySlot,
		bool RequiresReservation);

	public readonly record struct ActionOutputComponent(ActionOutputDefinition[] Outputs);

	public readonly record struct ActionEffectComponent(string[] AddFacts, string[] RemoveFacts);

	public readonly record struct ActionDefinitionComponent(string TargetKind, string DestinationMode, int BaseCost, int DurationTicks);

	public readonly record struct ActionSkillsComponent(string[] Skills);

	public readonly record struct AgentSkillsComponent(Dictionary<string, int> Skills)
	{
		public int GetSkill(string skillId)
			=> Skills != null && Skills.TryGetValue(skillId, out var level) ? level : 1;
	}

	public readonly record struct WorldObjectDefinitionComponent(
		string DisplayColorHex,
		string[] AcceptedItemTags,
		bool BlocksMovement,
		bool IsReservable,
		int Capacity,
		MaterialCostComponent[] BuildCosts,
		CraftRecipeComponent[] Recipes);

	public readonly record struct ResourceNodeDefinitionComponent(
		string DisplayColorHex,
		string[] SupportedActionIds,
		string[] RequiredToolItemTags,
		string YieldItemId,
		int YieldCount,
		bool BlocksMovement);

	public readonly record struct OccupantVisualComponent(Color Color);

	public readonly record struct AgentArchetypeComponent(
		string FactionId,
		string MemoryProviderId,
		string[] StartingItemIds,
		string[] BodyParts,
		string[] Organs,
		string[] Systems,
		int InventoryCapacity,
		float MinSpeed,
		float MaxSpeed,
		float MinStrength,
		float MaxStrength,
		float StartingRest,
		float MaxRest,
		float RestDecayPerTick,
		float RestRecoveryPerTick,
		float StartingCarbohydratesGrams,
		float MaxCarbohydratesGrams,
		float StartingProteinGrams,
		float MaxProteinGrams,
		float StartingFatGrams,
		float MaxFatGrams,
		float StartingSugarGrams,
		float MaxSugarGrams,
		float StartingHydrationLiters,
		float MaxHydrationLiters,
		float SugarUsePerTick,
		float HydrationUsePerTick,
		float SugarFromCarbohydratesPerTick,
		float SugarFromFatPerTick,
		float ProteinCatabolismPerTick,
		int BodyWidth,
		int BodyHeight);

	public readonly record struct ItemInstanceComponent(string DefinitionId);

	public readonly record struct GoalDefinitionComponent(int Priority, string[] DesiredFacts, string[] ForbiddenFacts, string[] RequiredFacts, string[] BlockedByFacts);

	public readonly record struct AgentArchetypeSnapshot(
		string FactionId,
		string MemoryProviderId,
		string[] StartingItemIds,
		string[] BodyParts,
		string[] Organs,
		string[] Systems,
		int InventoryCapacity,
		float MinSpeed,
		float MaxSpeed,
		float MinStrength,
		float MaxStrength,
		float StartingRest,
		float MaxRest,
		float RestDecayPerTick,
		float RestRecoveryPerTick,
		float StartingCarbohydratesGrams,
		float MaxCarbohydratesGrams,
		float StartingProteinGrams,
		float MaxProteinGrams,
		float StartingFatGrams,
		float MaxFatGrams,
		float StartingSugarGrams,
		float MaxSugarGrams,
		float StartingHydrationLiters,
		float MaxHydrationLiters,
		float SugarUsePerTick,
		float HydrationUsePerTick,
		float SugarFromCarbohydratesPerTick,
		float SugarFromFatPerTick,
		float ProteinCatabolismPerTick,
		int BodyWidth,
		int BodyHeight)
	{
		public static AgentArchetypeSnapshot Default => new(
			"neutral",
			string.Empty,
			Array.Empty<string>(),
			Array.Empty<string>(),
			Array.Empty<string>(),
			Array.Empty<string>(),
			5,
			2f,
			4f,
			0.5f,
			1f,
			100f,
			100f,
			0.02f,
			1f,
			280f,
			320f,
			90f,
			120f,
			70f,
			100f,
			24f,
			40f,
			3.2f,
			4.2f,
			0.03f,
			0.0025f,
			0.05f,
			0.02f,
			0.004f,
			32,
			32);
	}
}
