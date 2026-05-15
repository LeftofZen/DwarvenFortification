using System;

namespace DwarvenFortification
{
	public sealed class ItemDefinition
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string[] Tags { get; init; } = Array.Empty<string>();
		public string[] LearnedFacts { get; init; } = Array.Empty<string>();
		public bool IsTool { get; init; }
		public bool Stackable { get; init; } = true;
		public float WeightKg { get; init; } = 1f;
		public float NutritionValue { get; init; }
		public float HydrationValue { get; init; }
		public float ThrowRange { get; init; } = 5f;
	}

	public sealed class ItemDefinitionDocument
	{
		public ItemDefinition[] Items { get; init; } = Array.Empty<ItemDefinition>();
	}

	public sealed class ActionRequirementDefinition
	{
		public string[] RequiredItemIds { get; init; } = Array.Empty<string>();
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
		public string[] RequiredToolItemIds { get; init; } = Array.Empty<string>();
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
		public float StartingHunger { get; init; } = 100f;
		public float MaxHunger { get; init; } = 100f;
		public float HungerDecayPerTick { get; init; } = 0.03f;
		public float HungerRecoveryPerTick { get; init; } = 25f;
		public float StartingThirst { get; init; } = 100f;
		public float MaxThirst { get; init; } = 100f;
		public float ThirstDecayPerTick { get; init; } = 0.05f;
		public float ThirstRecoveryPerTick { get; init; } = 35f;
		public int BodyWidth { get; init; } = 32;
		public int BodyHeight { get; init; } = 32;
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
}
