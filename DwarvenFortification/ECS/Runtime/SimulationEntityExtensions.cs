using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.Actions;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.GOAP;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification.ECS.Runtime
{
	public static class SimulationEntityExtensions
	{
		public static bool IsAgent(this Entity entity)
			=> entity.Has<AgentTagComponent>();

		public static bool IsWorldObject(this Entity entity)
			=> entity.Has<WorldObjectTagComponent>();

		public static string GetName(this Entity entity)
		{
			ref var identity = ref entity.Get<DefinitionIdentityComponent>();
			return string.IsNullOrWhiteSpace(identity.Name) ? identity.Id : identity.Name;
		}

		public static Point GetPosition(this Entity entity)
			=> entity.Get<RuntimeTransformComponent>().Position;

		public static void SetPosition(this Entity entity, Point position)
		{
			ref var transform = ref entity.Get<RuntimeTransformComponent>();
			transform.Position = position;
		}

		public static float GetStrength(this Entity entity)
			=> entity.Get<AgentStatsComponent>().Strength;

		public static int GetSkillLevel(this Entity entity, string skillId)
			=> entity.Has<AgentSkillsComponent>() ? entity.Get<AgentSkillsComponent>().GetSkill(skillId) : 10;

		public static float ComputeSkillAverageLevel(this Entity entity, string[] actionSkills)
		{
			if (actionSkills == null || actionSkills.Length == 0) return 10f;
			if (!entity.Has<AgentSkillsComponent>()) return 10f;
			var total = 0;
			foreach (var skill in actionSkills) total += entity.GetSkillLevel(skill);
			return (float)total / actionSkills.Length;
		}

		public static int ComputeEffectiveDuration(this Entity entity, string[] actionSkills, int baseDuration)
		{
			// Skill 1 (novice) = full base duration. Skill 100 (master) = ~50% of base duration.
			var avgSkill = entity.ComputeSkillAverageLevel(actionSkills);
			var multiplier = 1.0f - (avgSkill - 1f) * 0.5f / 99f;
			return Math.Max(1, (int)Math.Round(baseDuration * multiplier));
		}

		public static float ComputeSkillYieldMultiplier(this Entity entity, string[] actionSkills)
		{
			// Skill 1 = 1.0x yield. Skill 100 = 2.0x yield.
			var avgSkill = entity.ComputeSkillAverageLevel(actionSkills);
			return 1.0f + (avgSkill - 1f) / 99f;
		}

		public static float GetSpeed(this Entity entity)
		{
			ref var stats = ref entity.Get<AgentStatsComponent>();
			ref var inventory = ref entity.Get<InventoryComponent>();
			var capacity = inventory.Capacity <= 0 ? 1 : inventory.Capacity;
			return stats.BaseSpeed * (1 - (0.25f * ((float)inventory.Items.Count / capacity)));
		}

		public static int GetWidth(this Entity entity)
			=> entity.Get<BodyComponent>().Width;

		public static int GetHeight(this Entity entity)
			=> entity.Get<BodyComponent>().Height;

		public static int GetTop(this Entity entity)
			=> entity.GetPosition().Y - (entity.GetHeight() / 2);

		public static int GetLeft(this Entity entity)
			=> entity.GetPosition().X - (entity.GetWidth() / 2);

		public static int GetInventoryCapacity(this Entity entity)
			=> entity.Get<InventoryComponent>().Capacity;

		public static List<Entity> GetInventory(this Entity entity)
			=> entity.Get<InventoryComponent>().Items;

		public static void AddInventoryItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			inventory.Items.Add(item);
		}

		public static bool RemoveInventoryItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			return inventory.Items.Remove(item);
		}

		public static bool HasQueuedActions(this Entity entity)
			=> entity.Get<ActionQueueComponent>().Actions.Count > 0;

		public static void EnqueueAction(this Entity entity, IAgentAction action, int count = 1)
		{
			ref var actionQueue = ref entity.Get<ActionQueueComponent>();
			for (var i = 0; i < count; ++i)
			{
				actionQueue.Actions.Enqueue(action);
			}
		}

		public static bool TryPeekAction(this Entity entity, out IAgentAction action)
		{
			ref var actionQueue = ref entity.Get<ActionQueueComponent>();
			if (actionQueue.Actions.Count > 0)
			{
				action = actionQueue.Actions.Peek();
				return true;
			}

			action = null;
			return false;
		}

		public static void ClearQueuedActions(this Entity entity)
		{
			ref var actionQueue = ref entity.Get<ActionQueueComponent>();
			actionQueue.Actions.Clear();
		}

		public static bool HasInvalidQueuedAction(this Entity entity, ISimulationWorld world)
		{
			ref var actionQueue = ref entity.Get<ActionQueueComponent>();
			return actionQueue.Actions.Any(action => !action.IsStillValid(world));
		}

		public static void DequeueAction(this Entity entity)
		{
			ref var actionQueue = ref entity.Get<ActionQueueComponent>();
			if (actionQueue.Actions.Count > 0)
			{
				actionQueue.Actions.Dequeue();
			}
		}

		public static Point GetCurrentPathGoal(this Entity entity)
		{
			ref var actionQueue = ref entity.Get<ActionQueueComponent>();
			return actionQueue.Actions.OfType<MoveAlongPathAction>().LastOrDefault()?.Destination ?? entity.GetPosition();
		}

		public static GridCell GetCurrentCell(this Entity entity, ISimulationWorld world)
			=> world.CellAtXY(entity.GetPosition());

		public static Rectangle GetCellBounds(this Entity entity, ISimulationWorld world)
		{
			var position = entity.GetPosition();
			return world.CellBoundsAt(position.X, position.Y);
		}

		public static string GetItemDefinitionId(this Entity entity)
			=> entity.Get<ItemInstanceComponent>().DefinitionId;

		public static Point GetCellReference(this Entity entity)
			=> entity.Get<CellReferenceComponent>().Cell;

		public static void AddStoredItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			inventory.Items.Add(item);
		}

		public static bool RemoveStoredItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			return inventory.Items.Remove(item);
		}

		public static int CountStoredItems(this Entity entity, string itemDefinitionId)
			=> !entity.Has<InventoryComponent>()
				? 0
				: entity.Get<InventoryComponent>().Items.Count(item => string.Equals(item.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));

		public static bool ItemMatchesFilter(this Entity item, string[] itemFilter)
			=> itemFilter.All(tag => item.Has<TagCollectionComponent>() && item.Get<TagCollectionComponent>().Contains(tag));

		public static int CountStoredItemsByFilter(this Entity entity, string[] itemFilter)
			=> !entity.Has<InventoryComponent>()
				? 0
				: entity.Get<InventoryComponent>().Items.Count(item => item.ItemMatchesFilter(itemFilter));

		public static int CountStoredItemsForCost(this Entity entity, MaterialCostComponent cost)
			=> cost.UsesFilter
				? entity.CountStoredItemsByFilter(cost.ItemFilter)
				: entity.CountStoredItems(cost.ItemId);

		public static bool IsStorageObject(this Entity entity)
			=> entity.Has<WorldObjectDefinitionComponent>() && entity.Has<TagCollectionComponent>() && entity.Get<TagCollectionComponent>().Contains("storage");

		public static bool IsConstructionSite(this Entity entity)
			=> entity.Has<ConstructionSiteComponent>();

		public static MaterialCostComponent[] GetBuildCosts(this Entity entity)
			=> entity.IsConstructionSite()
				? entity.Get<ConstructionSiteComponent>().BuildCosts ?? []
				: entity.Has<WorldObjectDefinitionComponent>()
					? entity.Get<WorldObjectDefinitionComponent>().BuildCosts ?? []
					: [];

		public static MaterialCostComponent[] GetMissingBuildCosts(this Entity entity)
			=> [.. entity.GetBuildCosts()
				.Select(cost => cost.WithQuantity(System.Math.Max(0, cost.Quantity - entity.CountStoredItemsForCost(cost))))
				.Where(cost => cost.Quantity > 0)];

		public static bool HasAllBuildMaterials(this Entity entity)
			=> entity.GetMissingBuildCosts().Length == 0;

		public static CraftRecipeComponent[] GetRecipes(this Entity entity)
			=> entity.Has<WorldObjectDefinitionComponent>()
				? entity.Get<WorldObjectDefinitionComponent>().Recipes ?? []
				: [];

		public static bool TryGetRecipeForOutput(this Entity entity, string outputItemId, out CraftRecipeComponent recipe)
		{
			recipe = entity.GetRecipes().FirstOrDefault(candidate => string.Equals(candidate.OutputItemId, outputItemId, System.StringComparison.OrdinalIgnoreCase));
			return !string.IsNullOrWhiteSpace(recipe.Id);
		}

		public static bool HasStoredMaterials(this Entity entity, MaterialCostComponent[] materials)
			=> materials.All(cost => entity.CountStoredItemsForCost(cost) >= cost.Quantity);

		public static Entity[] ConsumeStoredMaterials(this Entity entity, MaterialCostComponent[] materials)
		{
			var consumed = new List<Entity>();
			if (!entity.Has<InventoryComponent>())
			{
				return [.. consumed];
			}

			ref var inventory = ref entity.Get<InventoryComponent>();
			foreach (var material in materials)
			{
				for (var i = 0; i < material.Quantity; ++i)
				{
					Entity stored;
					if (material.UsesFilter)
					{
						stored = inventory.Items.FirstOrDefault(item => item.ItemMatchesFilter(material.ItemFilter));
					}
					else
					{
						stored = inventory.Items.FirstOrDefault(item => string.Equals(item.GetItemDefinitionId(), material.ItemId, System.StringComparison.OrdinalIgnoreCase));
					}

					if (stored.Equals(default(Entity)))
					{
						return [.. consumed];
					}

					inventory.Items.Remove(stored);
					consumed.Add(stored);
				}
			}

			return [.. consumed];
		}

		public static bool CanStore(this Entity entity, Entity item)
		{
			if (!entity.Has<WorldObjectDefinitionComponent>() || !item.Has<ItemInstanceComponent>())
			{
				return false;
			}

			if (entity.IsConstructionSite())
			{
				return entity.GetMissingBuildCosts().Any(cost => string.Equals(cost.ItemId, item.GetItemDefinitionId(), System.StringComparison.OrdinalIgnoreCase));
			}

			var recipes = entity.GetRecipes();
			if (recipes.Length > 0 && recipes.SelectMany(recipe => recipe.Inputs).Any(cost => string.Equals(cost.ItemId, item.GetItemDefinitionId(), System.StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}

			if (!entity.IsStorageObject() || !item.Has<TagCollectionComponent>())
			{
				return false;
			}

			var worldObject = entity.Get<WorldObjectDefinitionComponent>();
			if (worldObject.AcceptedItemTags.Length == 0)
			{
				return true;
			}

			var itemTags = item.Get<TagCollectionComponent>();
			return worldObject.AcceptedItemTags.Any(itemTags.Contains);
		}

		public static bool HasInventorySpace(this Entity entity)
		{
			if (!entity.Has<InventoryComponent>())
			{
				return false;
			}

			var inventory = entity.Get<InventoryComponent>();
			return inventory.Capacity <= 0 || inventory.Items.Count < inventory.Capacity;
		}

		public static bool HasItemDefinition(this Entity entity, string itemDefinitionId)
			=> entity.GetInventory().Any(item => string.Equals(item.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));

		public static bool TrySelectConsumableItem(this Entity entity, ConsumableKind kind, out Entity item)
		{
			item = default;
			if (!entity.Has<InventoryComponent>())
			{
				return false;
			}

			var bestMatch = entity.GetInventory()
				.Where(candidate => IsConsumableMatch(candidate, kind))
				.OrderByDescending(candidate => ScoreConsumable(entity, candidate, kind))
				.FirstOrDefault();

			if (bestMatch.Equals(default(Entity)))
			{
				return false;
			}

			item = bestMatch;
			return true;
		}

		public static bool HasBodyPart(this Entity entity, string bodyPart)
			=> entity.Has<LifeBodyComponent>() && entity.Get<LifeBodyComponent>().BodyParts.Any(part => string.Equals(part, bodyPart, System.StringComparison.OrdinalIgnoreCase));

		public static bool HasOrgan(this Entity entity, string organ)
			=> entity.Has<LifeBodyComponent>() && entity.Get<LifeBodyComponent>().Organs.Any(part => string.Equals(part, organ, System.StringComparison.OrdinalIgnoreCase));

		public static bool HasSystem(this Entity entity, string system)
			=> entity.Has<LifeBodyComponent>() && entity.Get<LifeBodyComponent>().Systems.Any(part => string.Equals(part, system, System.StringComparison.OrdinalIgnoreCase));

		public static IEnumerable<string> GetSystems(this Entity entity)
			=> entity.Has<LifeBodyComponent>() ? entity.Get<LifeBodyComponent>().Systems : Enumerable.Empty<string>();

		public static IEnumerable<string> GetImpairedSystems(this Entity entity)
			=> entity.GetSystems().Where(system => !entity.IsSystemOperational(system));

		public static string GetFactionId(this Entity entity)
			=> entity.Has<FactionComponent>() ? entity.Get<FactionComponent>().FactionId : "neutral";

		public static bool HasMemoryCapability(this Entity entity)
			=> entity.Has<MemoryComponent>() && !string.IsNullOrWhiteSpace(entity.Get<MemoryComponent>().ProviderId);

		public static string GetMemoryProviderId(this Entity entity)
			=> entity.Has<MemoryComponent>() ? entity.Get<MemoryComponent>().ProviderId : string.Empty;

		public static void LearnFact(this Entity entity, string fact)
		{
			if (!entity.HasMemoryCapability() || string.IsNullOrWhiteSpace(fact))
			{
				return;
			}

			ref var memory = ref entity.Get<MemoryComponent>();
			memory.KnownFacts.Add(fact);
		}

		public static bool KnowsFact(this Entity entity, string fact)
			=> entity.HasMemoryCapability() && entity.Get<MemoryComponent>().KnownFacts.Contains(fact);

		public static IEnumerable<string> GetKnownFacts(this Entity entity)
			=> entity.HasMemoryCapability() ? entity.Get<MemoryComponent>().KnownFacts : Enumerable.Empty<string>();

		public static IEnumerable<KeyValuePair<string, Point>> GetKnownItemLocations(this Entity entity)
			=> entity.HasMemoryCapability() ? entity.Get<MemoryComponent>().KnownItemLocations : Enumerable.Empty<KeyValuePair<string, Point>>();

		public static void RememberItemLocation(this Entity entity, string itemId, Point cell)
		{
			if (!entity.HasMemoryCapability() || string.IsNullOrWhiteSpace(itemId))
			{
				return;
			}

			ref var memory = ref entity.Get<MemoryComponent>();
			memory.KnownItemLocations[itemId] = cell;
			memory.KnownFacts.Add(Facts.KnowsItemLocation(itemId));
		}

		public static bool TryRecallItemLocation(this Entity entity, string itemId, out Point cell)
		{
			cell = default;
			if (!entity.HasMemoryCapability())
			{
				return false;
			}

			return entity.Get<MemoryComponent>().KnownItemLocations.TryGetValue(itemId, out cell);
		}

		public static void ForgetItemLocation(this Entity entity, string itemId)
		{
			if (!entity.HasMemoryCapability())
			{
				return;
			}

			ref var memory = ref entity.Get<MemoryComponent>();
			memory.KnownItemLocations.Remove(itemId);
			memory.KnownFacts.Remove(Facts.KnowsItemLocation(itemId));
		}

		public static void ShareKnownItemLocation(this Entity entity, Entity source, string itemId)
		{
			if (source.TryRecallItemLocation(itemId, out var cell))
			{
				entity.RememberItemLocation(itemId, cell);
			}
		}

		public static void ShareKnownFact(this Entity entity, Entity source, string fact)
		{
			if (source.KnowsFact(fact))
			{
				entity.LearnFact(fact);
			}
		}

		public static bool IsRestLow(this Entity entity)
		{
			if (!entity.Has<RestNeedComponent>())
			{
				return false;
			}

			var rest = entity.Get<RestNeedComponent>();
			return rest.Current <= rest.Max * 0.35f;
		}

		public static bool IsHungry(this Entity entity)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return false;
			}

			return GetMetabolicEnergyRatio(entity) <= 0.35f;
		}

		public static bool IsThirsty(this Entity entity)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return false;
			}

			return GetHydrationRatio(entity) <= 0.35f;
		}

		public static void DecayRest(this Entity entity)
		{
			if (!entity.Has<RestNeedComponent>())
			{
				return;
			}

			ref var rest = ref entity.Get<RestNeedComponent>();
			rest.Current = System.Math.Clamp(rest.Current - rest.DecayPerTick, 0f, rest.Max);
		}

		public static void RestoreRest(this Entity entity)
		{
			if (!entity.Has<RestNeedComponent>())
			{
				return;
			}

			ref var rest = ref entity.Get<RestNeedComponent>();
			rest.Current = System.Math.Clamp(rest.Current + rest.RecoveryPerTick, 0f, rest.Max);
		}

		public static void TickBodyNutrition(this Entity entity)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return;
			}

			ref var nutrition = ref entity.Get<BodyNutritionComponent>();
			nutrition.HydrationCurrentLiters = System.Math.Clamp(
				nutrition.HydrationCurrentLiters - nutrition.HydrationUsePerTick,
				0f,
				nutrition.HydrationMaxLiters);
			nutrition.SugarCurrent = System.Math.Clamp(
				nutrition.SugarCurrent - nutrition.SugarUsePerTick,
				0f,
				nutrition.SugarMax);

			var preferredSugarFloor = nutrition.SugarMax * 0.45f;
			var sugarGap = System.Math.Max(0f, preferredSugarFloor - nutrition.SugarCurrent);
			if (sugarGap > 0f && nutrition.CarbohydratesCurrent > 0f)
			{
				var carbTransfer = System.Math.Min(sugarGap, System.Math.Min(nutrition.SugarFromCarbohydratesPerTick, nutrition.CarbohydratesCurrent));
				nutrition.CarbohydratesCurrent -= carbTransfer;
				nutrition.SugarCurrent = System.Math.Clamp(nutrition.SugarCurrent + carbTransfer, 0f, nutrition.SugarMax);
			}

			sugarGap = System.Math.Max(0f, preferredSugarFloor - nutrition.SugarCurrent);
			if (sugarGap > 0f && nutrition.FatCurrent > 0f)
			{
				var fatTransfer = System.Math.Min(nutrition.SugarFromFatPerTick, nutrition.FatCurrent);
				nutrition.FatCurrent -= fatTransfer;
				nutrition.SugarCurrent = System.Math.Clamp(nutrition.SugarCurrent + (fatTransfer * 0.5f), 0f, nutrition.SugarMax);
			}

			if (nutrition.SugarCurrent <= nutrition.SugarMax * 0.15f && nutrition.ProteinCurrent > 0f)
			{
				var proteinTransfer = System.Math.Min(nutrition.ProteinCatabolismPerTick, nutrition.ProteinCurrent);
				nutrition.ProteinCurrent -= proteinTransfer;
				nutrition.SugarCurrent = System.Math.Clamp(nutrition.SugarCurrent + (proteinTransfer * 0.5f), 0f, nutrition.SugarMax);
			}
		}

		public static void AbsorbNutrition(this Entity entity, ItemNutritionComponent intake, bool includeMacronutrients = true, bool includeFluids = true)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return;
			}

			ref var nutrition = ref entity.Get<BodyNutritionComponent>();
			if (includeMacronutrients)
			{
				nutrition.CarbohydratesCurrent = System.Math.Clamp(nutrition.CarbohydratesCurrent + intake.CarbohydratesGrams, 0f, nutrition.CarbohydratesMax);
				nutrition.ProteinCurrent = System.Math.Clamp(nutrition.ProteinCurrent + intake.ProteinGrams, 0f, nutrition.ProteinMax);
				nutrition.FatCurrent = System.Math.Clamp(nutrition.FatCurrent + intake.FatGrams, 0f, nutrition.FatMax);
				nutrition.SugarCurrent = System.Math.Clamp(nutrition.SugarCurrent + intake.SugarGrams, 0f, nutrition.SugarMax);
			}

			if (includeFluids)
			{
				nutrition.HydrationCurrentLiters = System.Math.Clamp(nutrition.HydrationCurrentLiters + intake.FluidLiters, 0f, nutrition.HydrationMaxLiters);
			}
		}

		public static bool IsSystemOperational(this Entity entity, string system)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return true;
			}

			return system?.ToLowerInvariant() switch
			{
				// Digestion does NOT require hydration — removing that dependency prevents a
				// circular deadlock where the agent cannot plan 'drink' when critically dehydrated.
				"digestion" => !IsNutrientLow(entity, NutrientKind.Protein, 0.08f),
				"respiratory" => !IsHydrationCritical(entity) && !IsNutrientLow(entity, NutrientKind.Sugar, 0.1f),
				"nervous" => !IsHydrationCritical(entity) && !IsNutrientLow(entity, NutrientKind.Sugar, 0.18f) && !IsNutrientLow(entity, NutrientKind.Fat, 0.12f),
				"musculoskeletal" => !IsHydrationCritical(entity) && !IsNutrientLow(entity, NutrientKind.Carbohydrates, 0.18f) && !IsNutrientLow(entity, NutrientKind.Protein, 0.14f),
				_ => true,
			};
		}

		public static bool IsNutrientLow(this Entity entity, NutrientKind nutrient, float thresholdRatio = 0.18f)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return false;
			}

			var nutrition = entity.Get<BodyNutritionComponent>();
			var (current, max) = nutrient switch
			{
				NutrientKind.Carbohydrates => (nutrition.CarbohydratesCurrent, nutrition.CarbohydratesMax),
				NutrientKind.Protein => (nutrition.ProteinCurrent, nutrition.ProteinMax),
				NutrientKind.Fat => (nutrition.FatCurrent, nutrition.FatMax),
				NutrientKind.Sugar => (nutrition.SugarCurrent, nutrition.SugarMax),
				NutrientKind.Hydration => (nutrition.HydrationCurrentLiters, nutrition.HydrationMaxLiters),
				_ => (0f, 1f),
			};

			return max <= 0f ? false : current <= max * thresholdRatio;
		}

		public static float GetMetabolicEnergyRatio(this Entity entity)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return 1f;
			}

			var nutrition = entity.Get<BodyNutritionComponent>();
			var currentEnergy = ((nutrition.CarbohydratesCurrent + nutrition.SugarCurrent + nutrition.ProteinCurrent) * 4f) + (nutrition.FatCurrent * 9f);
			var maxEnergy = ((nutrition.CarbohydratesMax + nutrition.SugarMax + nutrition.ProteinMax) * 4f) + (nutrition.FatMax * 9f);
			return maxEnergy <= 0f ? 1f : currentEnergy / maxEnergy;
		}

		public static float GetHydrationRatio(this Entity entity)
		{
			if (!entity.Has<BodyNutritionComponent>())
			{
				return 1f;
			}

			var nutrition = entity.Get<BodyNutritionComponent>();
			return nutrition.HydrationMaxLiters <= 0f ? 1f : nutrition.HydrationCurrentLiters / nutrition.HydrationMaxLiters;
		}

		public static float GetRestRatio(this Entity entity)
		{
			if (!entity.Has<RestNeedComponent>())
			{
				return 1f;
			}

			var rest = entity.Get<RestNeedComponent>();
			return rest.Max <= 0f ? 1f : rest.Current / rest.Max;
		}

		static bool IsHydrationCritical(this Entity entity)
			=> entity.IsNutrientLow(NutrientKind.Hydration, 0.16f);

		static bool IsConsumableMatch(Entity item, ConsumableKind kind)
		{
			if (!item.Has<ItemDefinitionComponent>() || !item.Has<TagCollectionComponent>())
			{
				return false;
			}

			var definition = item.Get<ItemDefinitionComponent>();
			var tags = item.Get<TagCollectionComponent>();
			return kind switch
			{
				ConsumableKind.Food => tags.Contains("food") || definition.Nutrition.CarbohydratesGrams > 0f || definition.Nutrition.ProteinGrams > 0f || definition.Nutrition.FatGrams > 0f || definition.Nutrition.SugarGrams > 0f,
				ConsumableKind.Drink => tags.Contains("drink") || definition.Nutrition.FluidLiters > 0f,
				_ => false,
			};
		}

		static float ScoreConsumable(Entity entity, Entity item, ConsumableKind kind)
		{
			var definition = item.Get<ItemDefinitionComponent>();
			var nutrition = definition.Nutrition;
			var body = entity.Has<BodyNutritionComponent>() ? entity.Get<BodyNutritionComponent>() : default;
			return kind switch
			{
				ConsumableKind.Food =>
					(NeedGap(body.CarbohydratesCurrent, body.CarbohydratesMax) * nutrition.CarbohydratesGrams) +
					(NeedGap(body.ProteinCurrent, body.ProteinMax) * nutrition.ProteinGrams * 1.2f) +
					(NeedGap(body.FatCurrent, body.FatMax) * nutrition.FatGrams * 0.9f) +
					(NeedGap(body.SugarCurrent, body.SugarMax) * nutrition.SugarGrams * 1.4f) +
					(nutrition.FiberGrams * 0.05f),
				ConsumableKind.Drink =>
					(NeedGap(body.HydrationCurrentLiters, body.HydrationMaxLiters) * nutrition.FluidLiters * 10f) +
					(NeedGap(body.SugarCurrent, body.SugarMax) * nutrition.SugarGrams * 0.5f),
				_ => 0f,
			};
		}

		static float NeedGap(float current, float max)
			=> max <= 0f ? 0f : System.Math.Max(0f, max - current);

		public enum NutrientKind
		{
			Carbohydrates,
			Protein,
			Fat,
			Sugar,
			Hydration,
		}

		public enum ConsumableKind
		{
			None,
			Food,
			Drink,
		}

		public static void SetEnemyVisible(this Entity entity, bool visible, Point enemyCell, int durationTicks)
		{
			if (!entity.Has<PerceptionComponent>())
			{
				return;
			}

			ref var perception = ref entity.Get<PerceptionComponent>();
			perception.EnemyVisible = visible;
			perception.LastKnownEnemyCell = enemyCell;
			perception.ScanTicksRemaining = durationTicks;
		}

		public static void TickPerception(this Entity entity)
		{
			if (!entity.Has<PerceptionComponent>())
			{
				return;
			}

			ref var perception = ref entity.Get<PerceptionComponent>();
			perception.ScanTicksRemaining = System.Math.Max(0, perception.ScanTicksRemaining - 1);
			if (perception.ScanTicksRemaining == 0)
			{
				perception.EnemyVisible = false;
				perception.LastKnownEnemyCell = new Point(-1, -1);
			}
		}

		public static void SetHidden(this Entity entity, int durationTicks)
		{
			if (!entity.Has<StealthComponent>())
			{
				return;
			}

			ref var stealth = ref entity.Get<StealthComponent>();
			stealth.HiddenTicksRemaining = System.Math.Max(stealth.HiddenTicksRemaining, durationTicks);
		}

		public static void TickStealth(this Entity entity)
		{
			if (!entity.Has<StealthComponent>())
			{
				return;
			}

			ref var stealth = ref entity.Get<StealthComponent>();
			stealth.HiddenTicksRemaining = System.Math.Max(0, stealth.HiddenTicksRemaining - 1);
		}

		public static bool IsHidden(this Entity entity)
			=> entity.Has<StealthComponent>() && entity.Get<StealthComponent>().HiddenTicksRemaining > 0;

		public static void SetPatrolled(this Entity entity, int durationTicks)
		{
			if (!entity.Has<PatrolStateComponent>())
			{
				return;
			}

			ref var patrol = ref entity.Get<PatrolStateComponent>();
			patrol.PatrolledTicksRemaining = System.Math.Max(patrol.PatrolledTicksRemaining, durationTicks);
		}

		public static void TickPatrolState(this Entity entity)
		{
			if (!entity.Has<PatrolStateComponent>())
			{
				return;
			}

			ref var patrol = ref entity.Get<PatrolStateComponent>();
			patrol.PatrolledTicksRemaining = System.Math.Max(0, patrol.PatrolledTicksRemaining - 1);
		}

		public static bool IsRecentlyPatrolling(this Entity entity)
			=> entity.Has<PatrolStateComponent>() && entity.Get<PatrolStateComponent>().PatrolledTicksRemaining > 0;

		public static bool HasActiveProductionOrder(this Entity entity)
			=> entity.Has<ProductionOrderComponent>()
				&& !string.IsNullOrWhiteSpace(entity.Get<ProductionOrderComponent>().ActiveRecipeId)
				&& entity.Get<ProductionOrderComponent>().BatchesCompleted < entity.Get<ProductionOrderComponent>().BatchesRequested;

		public static ProductionOrderComponent GetProductionOrder(this Entity entity)
			=> entity.Has<ProductionOrderComponent>() ? entity.Get<ProductionOrderComponent>() : default;

		public static bool TryGetRecipeById(this Entity entity, string recipeId, out CraftRecipeComponent recipe)
		{
			recipe = entity.GetRecipes().FirstOrDefault(r => string.Equals(r.Id, recipeId, System.StringComparison.OrdinalIgnoreCase));
			return !string.IsNullOrWhiteSpace(recipe.Id);
		}

		public static MaterialCostComponent[] GetMissingOrderInputs(this Entity entity)
		{
			if (!entity.HasActiveProductionOrder())
			{
				return [];
			}

			var order = entity.Get<ProductionOrderComponent>();
			if (!entity.TryGetRecipeById(order.ActiveRecipeId, out var recipe))
			{
				return [];
			}

			return [.. recipe.Inputs
				.Select(cost => cost.WithQuantity(System.Math.Max(0, cost.Quantity - entity.CountStoredItemsForCost(cost))))
				.Where(cost => cost.Quantity > 0)];
		}

		public static void SetProductionOrder(this Entity entity, string recipeId, int batches)
		{
			if (!entity.Has<ProductionOrderComponent>())
			{
				return;
			}

			ref var order = ref entity.Get<ProductionOrderComponent>();
			order.ActiveRecipeId = recipeId;
			order.BatchesRequested = batches;
			order.BatchesCompleted = 0;
		}

		public static void ClearProductionOrder(this Entity entity)
		{
			if (!entity.Has<ProductionOrderComponent>())
			{
				return;
			}

			ref var order = ref entity.Get<ProductionOrderComponent>();
			order.ActiveRecipeId = string.Empty;
			order.BatchesRequested = 0;
			order.BatchesCompleted = 0;
		}

		public static void IncrementProductionBatch(this Entity entity)
		{
			if (!entity.Has<ProductionOrderComponent>())
			{
				return;
			}

			ref var order = ref entity.Get<ProductionOrderComponent>();
			order.BatchesCompleted++;
			if (order.BatchesCompleted >= order.BatchesRequested)
			{
				order.ActiveRecipeId = string.Empty;
			}
		}
	}
}
