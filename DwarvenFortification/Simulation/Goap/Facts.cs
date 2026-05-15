using System;

namespace DwarvenFortification.GOAP
{
	public static class Facts
	{
		const string HasItemPrefix = "has.item.";
		const string HasBodyPartPrefix = "has.body-part.";
		const string HasOrganPrefix = "has.organ.";
		const string HasSystemPrefix = "has.system.";
		const string MemoryProviderPrefix = "memory.provider.";
		const string KnowsItemLocationPrefix = "knows.item-location.";

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
			=> $"{HasItemPrefix}{itemId}";

		public static string HasBodyPart(string bodyPart)
			=> $"{HasBodyPartPrefix}{bodyPart}";

		public static string HasOrgan(string organ)
			=> $"{HasOrganPrefix}{organ}";

		public static string HasSystem(string system)
			=> $"{HasSystemPrefix}{system}";

		public static string MemoryProvider(string providerId)
			=> $"{MemoryProviderPrefix}{providerId}";

		public static string KnowsItemLocation(string itemId)
			=> $"{KnowsItemLocationPrefix}{itemId}";

		public static bool TryGetHasItemId(string fact, out string itemId)
			=> TryGetFactSuffix(fact, HasItemPrefix, out itemId);

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