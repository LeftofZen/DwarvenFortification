using System;

namespace DwarvenFortification.GOAP
{
	public static class Facts
	{
		const string HasItemPrefix = "has.item.";
		const string HasItemTagPrefix = "has.item-tag.";
		const string HasItemFilterPrefix = "has.item-filter.";
		const string HasBodyPartPrefix = "has.body-part.";
		const string HasOrganPrefix = "has.organ.";
		const string HasSystemPrefix = "has.system.";
		const string SystemImpairedPrefix = "system.impaired.";
		const string MemoryProviderPrefix = "memory.provider.";
		const string KnowsItemLocationPrefix = "knows.item-location.";
		const string NutrientLowPrefix = "nutrient.low.";
		const string NutrientOkPrefix = "nutrient.ok.";
		const string HasStructurePrefix = "has.structure.";
		const string CraftableItemPrefix = "craftable.item.";

		public const string MemoryCapable = "memory.capable";
		public const string ConstructionPending = "construction.pending";
		public const string SiteNeedsMaterials = "site.needs-materials";
		public const string WorkstationNeedsInputs = "workstation.needs-inputs";
		public const string ProductionOrderActive = "production.order.active";
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

		// --- Numeric state keys (registered with GoapStateBounds on the agent). ---
		// All vitals are normalized to a 0..100 percentage of capacity.
		public const string VitalRest = "vital.rest";
		public const string VitalHunger = "vital.hunger";          // Higher = more energetic / less hungry.
		public const string VitalThirst = "vital.thirst";          // Higher = better hydrated / less thirsty.
		public const string VitalCarbohydrates = "vital.carbohydrates";
		public const string VitalProtein = "vital.protein";
		public const string VitalFat = "vital.fat";
		public const string VitalSugar = "vital.sugar";
		public const string VitalHydration = "vital.hydration";

		// Inventory scalars.
		public const string InventoryCount = "inventory.count";
		public const string InventoryCapacity = "inventory.capacity";
		public const string InventoryFree = "inventory.free";

		// Perception / patrol cooldowns (ticks remaining; decay externally).
		public const string PerceptionScanTicks = "perception.scan-ticks";

		public static string HasItem(string itemId)
			=> $"{HasItemPrefix}{itemId}";

		public static string HasBodyPart(string bodyPart)
			=> $"{HasBodyPartPrefix}{bodyPart}";

		public static string HasOrgan(string organ)
			=> $"{HasOrganPrefix}{organ}";

		public static string HasSystem(string system)
			=> $"{HasSystemPrefix}{system}";

		public static string SystemImpaired(string system)
			=> $"{SystemImpairedPrefix}{system}";

		public static string MemoryProvider(string providerId)
			=> $"{MemoryProviderPrefix}{providerId}";

		public static string KnowsItemLocation(string itemId)
			=> $"{KnowsItemLocationPrefix}{itemId}";

		public static string NutrientLow(string nutrient)
			=> $"{NutrientLowPrefix}{nutrient}";

		public static string NutrientOk(string nutrient)
			=> $"{NutrientOkPrefix}{nutrient}";

		public static string HasStructure(string structureId)
			=> $"{HasStructurePrefix}{structureId}";

		public static string CraftableItem(string itemId)
			=> $"{CraftableItemPrefix}{itemId}";

		public static string HasItemTag(string tag)
			=> $"{HasItemTagPrefix}{tag}";

		public static string HasItemFilter(string[] tags)
		{
			if (tags.Length == 1)
			{
				return HasItemTag(tags[0]);
			}

			var sorted = (string[])tags.Clone();
			Array.Sort(sorted, StringComparer.OrdinalIgnoreCase);
			return $"{HasItemFilterPrefix}{string.Join(",", sorted)}";
		}

		public static bool TryGetHasItemId(string fact, out string itemId)
			=> TryGetFactSuffix(fact, HasItemPrefix, out itemId);

		public static bool TryGetHasItemTag(string fact, out string tag)
			=> TryGetFactSuffix(fact, HasItemTagPrefix, out tag);

		public static bool TryGetHasItemFilter(string fact, out string[] tags)
		{
			if (TryGetFactSuffix(fact, HasItemFilterPrefix, out var suffix))
			{
				tags = suffix.Split(',', StringSplitOptions.RemoveEmptyEntries);
				return tags.Length > 0;
			}

			if (TryGetFactSuffix(fact, HasItemTagPrefix, out var singleTag))
			{
				tags = [singleTag];
				return true;
			}

			tags = [];
			return false;
		}

		public static bool TryGetKnownItemLocationId(string fact, out string itemId)
			=> TryGetFactSuffix(fact, KnowsItemLocationPrefix, out itemId);

		static bool TryGetFactSuffix(string fact, string prefix, out string suffix)
		{
			if (!string.IsNullOrWhiteSpace(fact) && fact.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				suffix = fact[prefix.Length..];
				return !string.IsNullOrWhiteSpace(suffix);
			}

			suffix = string.Empty;
			return false;
		}
	}
}