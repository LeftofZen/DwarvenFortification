using Arch.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public readonly record struct ActionDefinitionSnapshot(
		string Id,
		string Name,
		string TargetKind,
		string DestinationMode,
		int BaseCost,
		int DurationTicks,
		string[] RequiredItemIds,
		string[] RequiredTargetTags,
		string[] RequiredBodyParts,
		string[] RequiredOrgans,
		string[] RequiredSystems,
		string[] RequiredFacts,
		string[] BlockedByFacts,
		bool RequiresFreeInventorySlot,
		bool RequiresReservation,
		string[] AddFacts,
		string[] RemoveFacts);

	public readonly record struct ResourceNodeDefinitionSnapshot(
		string Id,
		string Name,
		string DisplayColorHex,
		string[] Tags,
		string[] SupportedActionIds,
		string[] RequiredToolItemIds,
		string YieldItemId,
		int YieldCount,
		bool BlocksMovement);

	public readonly record struct GoapGoal(
		string Id,
		string Name,
		int Priority,
		string[] DesiredFacts,
		string[] ForbiddenFacts,
		string[] RequiredFacts,
		string[] BlockedByFacts);

	public readonly record struct GoapActionCandidate(
		ActionDefinitionSnapshot Definition,
		Point TargetCell,
		Point DestinationCell,
		Entity? TargetEntity,
		int Cost,
		string[] RequiredFacts,
		string[] AddFacts,
		string[] RemoveFacts);

	public sealed class GoapPlan
	{
		public GoapPlan(GoapGoal goal, IReadOnlyList<GoapActionCandidate> steps, int cost)
		{
			Goal = goal;
			Steps = steps;
			Cost = cost;
		}

		public GoapGoal Goal { get; }
		public IReadOnlyList<GoapActionCandidate> Steps { get; }
		public int Cost { get; }
	}

	internal sealed class GoapSearchNode
	{
		public required HashSet<string> Facts { get; init; }
		public required List<GoapActionCandidate> Steps { get; init; }
		public required int Cost { get; init; }
		public required string StateKey { get; init; }
	}

	public static class GoapFacts
	{
		public const string MemoryCapable = "memory.capable";
		public const string InventoryHasResourceItems = "inventory.has-resource-items";
		public const string InventoryHasSpace = "inventory.has-space";
		public const string RestLow = "rest.low";
		public const string RestOk = "rest.ok";
		public const string HungerLow = "hunger.low";
		public const string HungerOk = "hunger.ok";
		public const string ThirstLow = "thirst.low";
		public const string ThirstOk = "thirst.ok";
		public const string EnemyNearby = "enemy.nearby";
		public const string EnemyVisible = "enemy.visible";
		public const string SelfHidden = "self.hidden";
		public const string AreaScanned = "area.scanned";
		public const string AreaPatrolled = "area.patrolled";
		public const string EnemySuppressed = "enemy.suppressed";

		public static string HasItem(string itemId)
			=> $"has.item.{itemId}";

		public static string HasBodyPart(string bodyPart)
			=> $"has.body-part.{bodyPart}";

		public static string HasOrgan(string organ)
			=> $"has.organ.{organ}";

		public static string HasSystem(string system)
			=> $"has.system.{system}";

		public static string MemoryProvider(string providerId)
			=> $"memory.provider.{providerId}";

		public static string KnowsItemLocation(string itemId)
			=> $"knows.item-location.{itemId}";
	}
}