using System;
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

	public readonly record struct ItemDefinitionComponent(bool IsTool, bool Stackable, float WeightKg, string[] LearnedFacts, float NutritionValue, float HydrationValue, float ThrowRange);

	public readonly record struct ActionRequirementComponent(
		string[] RequiredItemIds,
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

	public readonly record struct WorldObjectDefinitionComponent(string DisplayColorHex, string[] AcceptedItemTags, bool BlocksMovement, bool IsReservable, int Capacity);

	public readonly record struct ResourceNodeDefinitionComponent(
		string DisplayColorHex,
		string[] SupportedActionIds,
		string[] RequiredToolItemIds,
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
		float StartingHunger,
		float MaxHunger,
		float HungerDecayPerTick,
		float HungerRecoveryPerTick,
		float StartingThirst,
		float MaxThirst,
		float ThirstDecayPerTick,
		float ThirstRecoveryPerTick,
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
		float StartingHunger,
		float MaxHunger,
		float HungerDecayPerTick,
		float HungerRecoveryPerTick,
		float StartingThirst,
		float MaxThirst,
		float ThirstDecayPerTick,
		float ThirstRecoveryPerTick,
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
			100f,
			100f,
			0.03f,
			25f,
			100f,
			100f,
			0.05f,
			35f,
			32,
			32);
	}
}
