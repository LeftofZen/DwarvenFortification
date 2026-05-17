using System;
using System.Collections.Generic;

namespace DwarvenFortification.ECS.Authoring
{
	public sealed class ItemDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string[] Tags { get; init; } = Array.Empty<string>();
		public Dictionary<string, string> Properties { get; init; } = new();
		public string[] LearnedFacts { get; init; } = Array.Empty<string>();
		public bool IsTool { get; init; }
		public bool Stackable { get; init; } = true;
		public float WeightKg { get; init; } = 1f;
		public ItemNutritionDefinition Nutrition { get; init; } = new();
		public float ThrowRange { get; init; } = 5f;
	}

	public sealed class ItemNutritionDefinition
	{
		public float CarbohydratesGrams { get; init; }
		public float ProteinGrams { get; init; }
		public float FatGrams { get; init; }
		public float SugarGrams { get; init; }
		public float FiberGrams { get; init; }
		public float FluidLiters { get; init; }
	}

	public sealed class ItemDefinitionDocument
	{
		public ItemDefinition[] Items { get; init; } = Array.Empty<ItemDefinition>();
	}

	public sealed class ActionRequirementDefinition
	{
		public string[] RequiredItemTags { get; init; } = Array.Empty<string>();
		public string[] RequiredTargetTags { get; init; } = Array.Empty<string>();
		public string[] RequiredBodyParts { get; init; } = Array.Empty<string>();
		public string[] RequiredOrgans { get; init; } = Array.Empty<string>();
		public string[] RequiredSystems { get; init; } = Array.Empty<string>();
		public string[] RequiredFacts { get; init; } = Array.Empty<string>();
		public string[] BlockedByFacts { get; init; } = Array.Empty<string>();
		public bool RequiresFreeInventorySlot { get; init; }
		public bool RequiresReservation { get; init; }
	}

	public sealed class ActionOutputDefinition
	{
		public string ItemId { get; init; } = string.Empty;
		public int Quantity { get; init; } = 1;
	}

	public sealed class MaterialCostDefinition
	{
		public string ItemId { get; init; } = string.Empty;
		public string[] ItemFilter { get; init; } = Array.Empty<string>();
		public int Quantity { get; init; } = 1;
	}

	public sealed class CraftRecipeDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string[] RequiredFacts { get; init; } = Array.Empty<string>();
		public MaterialCostDefinition[] Inputs { get; init; } = Array.Empty<MaterialCostDefinition>();
		public string OutputItemId { get; init; } = string.Empty;
		public int OutputQuantity { get; init; } = 1;
	}

	public sealed class ActionEffectDefinition
	{
		public string[] AddFacts { get; init; } = Array.Empty<string>();
		public string[] RemoveFacts { get; init; } = Array.Empty<string>();
	}

	public sealed class ActionDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public int BaseCost { get; init; } = 1;
		public int DurationTicks { get; init; } = 1;
		public string TargetKind { get; init; } = string.Empty;
		public string DestinationMode { get; init; } = string.Empty;
		public string[] Skills { get; init; } = Array.Empty<string>();
		public ActionRequirementDefinition Requires { get; init; } = new();
		public ActionOutputDefinition[] Outputs { get; init; } = Array.Empty<ActionOutputDefinition>();
		public ActionEffectDefinition Effects { get; init; } = new();
	}

	public sealed class ActionDefinitionDocument
	{
		public ActionDefinition[] Actions { get; init; } = Array.Empty<ActionDefinition>();
	}

	public sealed class WorldObjectDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string DisplayColor { get; init; } = "#FFFFFF";
		public string[] Tags { get; init; } = Array.Empty<string>();
		public string[] AcceptedItemTags { get; init; } = Array.Empty<string>();
		public bool BlocksMovement { get; init; }
		public bool IsReservable { get; init; }
		public int Capacity { get; init; }
		public MaterialCostDefinition[] BuildCosts { get; init; } = Array.Empty<MaterialCostDefinition>();
		public CraftRecipeDefinition[] Recipes { get; init; } = Array.Empty<CraftRecipeDefinition>();
	}

	public sealed class WorldObjectDefinitionDocument
	{
		public WorldObjectDefinition[] Objects { get; init; } = Array.Empty<WorldObjectDefinition>();
	}

	public sealed class ResourceNodeDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string DisplayColor { get; init; } = "#FFFFFF";
		public string[] Tags { get; init; } = Array.Empty<string>();
		public string[] SupportedActionIds { get; init; } = Array.Empty<string>();
		public string[] RequiredToolItemTags { get; init; } = Array.Empty<string>();
		public string YieldItemId { get; init; } = string.Empty;
		public int YieldCount { get; init; }
		public bool BlocksMovement { get; init; }
	}

	public sealed class ResourceNodeDefinitionDocument
	{
		public ResourceNodeDefinition[] ResourceNodes { get; init; } = Array.Empty<ResourceNodeDefinition>();
	}

	public sealed class AgentDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string FactionId { get; init; } = "neutral";
		public string MemoryProviderId { get; init; } = string.Empty;
		public string[] StartingItemIds { get; init; } = Array.Empty<string>();
		public string[] BodyParts { get; init; } = Array.Empty<string>();
		public string[] Organs { get; init; } = Array.Empty<string>();
		public string[] Systems { get; init; } = Array.Empty<string>();
		public int InventoryCapacity { get; init; } = 5;
		public float MinSpeed { get; init; } = 2f;
		public float MaxSpeed { get; init; } = 4f;
		public float MinStrength { get; init; } = 0.5f;
		public float MaxStrength { get; init; } = 1f;
		public float StartingRest { get; init; } = 100f;
		public float MaxRest { get; init; } = 100f;
		public float RestDecayPerTick { get; init; } = 0.02f;
		public float RestRecoveryPerTick { get; init; } = 1f;
		public float StartingCarbohydratesGrams { get; init; } = 280f;
		public float MaxCarbohydratesGrams { get; init; } = 320f;
		public float StartingProteinGrams { get; init; } = 90f;
		public float MaxProteinGrams { get; init; } = 120f;
		public float StartingFatGrams { get; init; } = 70f;
		public float MaxFatGrams { get; init; } = 100f;
		public float StartingSugarGrams { get; init; } = 24f;
		public float MaxSugarGrams { get; init; } = 40f;
		public float StartingHydrationLiters { get; init; } = 3.2f;
		public float MaxHydrationLiters { get; init; } = 4.2f;
		public float SugarUsePerTick { get; init; } = 0.03f;
		public float HydrationUsePerTick { get; init; } = 0.0025f;
		public float SugarFromCarbohydratesPerTick { get; init; } = 0.05f;
		public float SugarFromFatPerTick { get; init; } = 0.02f;
		public float ProteinCatabolismPerTick { get; init; } = 0.004f;
		public int BodyWidth { get; init; } = 32;
		public int BodyHeight { get; init; } = 32;
		public Dictionary<string, int> Skills { get; init; } = new();
	}

	public sealed class AgentDefinitionDocument
	{
		public AgentDefinition[] Agents { get; init; } = Array.Empty<AgentDefinition>();
	}

	public sealed class GoalDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public int Priority { get; init; }
		public string[] DesiredFacts { get; init; } = Array.Empty<string>();
		public string[] ForbiddenFacts { get; init; } = Array.Empty<string>();
		public string[] RequiredFacts { get; init; } = Array.Empty<string>();
		public string[] BlockedByFacts { get; init; } = Array.Empty<string>();
	}

	public sealed class GoalDefinitionDocument
	{
		public GoalDefinition[] Goals { get; init; } = Array.Empty<GoalDefinition>();
	}

	public sealed class FactDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Description { get; init; } = string.Empty;
		public string Category { get; init; } = string.Empty;
		public bool IsDynamic { get; init; }
	}

	public sealed class FactDefinitionDocument
	{
		public FactDefinition[] Facts { get; init; } = Array.Empty<FactDefinition>();
	}

	public sealed class SkillDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string Description { get; init; } = string.Empty;
		public string Category { get; init; } = string.Empty;
		public string Group { get; init; } = string.Empty;
	}

	public sealed class SkillDefinitionDocument
	{
		public SkillDefinition[] Skills { get; init; } = Array.Empty<SkillDefinition>();
	}
}
