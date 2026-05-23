using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.GOAP;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DwarvenFortification.ECS
{
	public sealed class SimulationDefinitionRegistry : IGoapDefinitionSource
	{
		readonly Dictionary<string, Entity> itemDefinitionEntities;
		readonly Dictionary<string, Entity> worldObjectDefinitionEntities;
		readonly Dictionary<string, Entity> worldObjectDefinitionEntitiesById;
		readonly Dictionary<string, Entity> resourceNodeDefinitionEntitiesById;
		readonly List<OccupantPaletteEntry> paintableOccupants;
		readonly List<ItemPaletteEntry> paletteItems;
		readonly Dictionary<string, Entity> agentArchetypeEntities;
		readonly Entity? defaultAgentArchetypeEntity;
		readonly List<string[]> knownItemFilters;
		readonly List<FactDefinition> factDefinitions;
		readonly List<SkillDefinition> skillDefinitions;
		readonly List<SimulationGoapAction> goapActions;
		readonly List<SimulationGoapGoal> goapGoals;

		SimulationDefinitionRegistry(
			World world,
			IEnumerable<ItemDefinition> items,
			IEnumerable<ActionDefinition> actions,
			IEnumerable<WorldObjectDefinition> objects,
			IEnumerable<ResourceNodeDefinition> resourceNodes,
			IEnumerable<AgentDefinition> agents,
			IEnumerable<GoalDefinition> goals,
			IEnumerable<FactDefinition> facts,
			IEnumerable<SkillDefinition> skills)
		{
			World = world;
			itemDefinitionEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			worldObjectDefinitionEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			worldObjectDefinitionEntitiesById = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			paintableOccupants = [];
			paletteItems = [];
			goapActions = [];
			goapGoals = [];
			resourceNodeDefinitionEntitiesById = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			agentArchetypeEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			knownItemFilters = new List<string[]>();
			factDefinitions = new List<FactDefinition>(facts ?? Array.Empty<FactDefinition>());
			skillDefinitions = new List<SkillDefinition>(skills ?? Array.Empty<SkillDefinition>());

			foreach (var item in items.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				var propertyTags = (item.Properties ?? new System.Collections.Generic.Dictionary<string, string>())
					.Select(kvp => $"{kvp.Key}:{kvp.Value}")
					.ToArray();
				var allTags = (item.Tags ?? []).Concat(propertyTags).ToArray();
				itemDefinitionEntities[item.Id] = World.Create(
					new DefinitionIdentityComponent(item.Id, item.Name),
					new TagCollectionComponent(allTags),
					new ItemDefinitionComponent(
						item.IsTool,
						item.Stackable,
						item.WeightKg,
						item.LearnedFacts ?? [],
						new ItemNutritionComponent(
							item.Nutrition?.CarbohydratesGrams ?? 0f,
							item.Nutrition?.ProteinGrams ?? 0f,
							item.Nutrition?.FatGrams ?? 0f,
							item.Nutrition?.SugarGrams ?? 0f,
							item.Nutrition?.FiberGrams ?? 0f,
							item.Nutrition?.FluidLiters ?? 0f),
						item.ThrowRange));
				var itemGroup = DetermineItemGroup(item.Tags);
				paletteItems.Add(new ItemPaletteEntry(item.Id, item.Name, DetermineItemGroupColor(itemGroup), itemGroup));
			}

			foreach (var action in actions.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				goapActions.Add(new SimulationGoapAction(
					action.Id,
					action.Name,
					action.BaseCost,
					action.DurationTicks,
					action.TargetKind,
					action.DestinationMode,
					action.Requirements ?? [],
					action.Effects?.Success,
					action.Effects?.Interrupted,
					action.Effects?.Failed,
					action.Skills ?? [],
					action.Children ?? []));
			}

			// Second pass: resolve compound children by ID.
			var actionsById = goapActions.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
			foreach (var action in goapActions.Where(a => a.ChildActionIds.Length > 0))
			{
				foreach (var childId in action.ChildActionIds)
				{
					if (!actionsById.TryGetValue(childId, out var child))
					{
						throw new InvalidOperationException(
							$"Compound action '{action.Id}' references unknown child action '{childId}'.");
					}
					action.Children.Add(child);
				}
			}

			foreach (var worldObject in objects.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				var color = ParseColor(worldObject.DisplayColor);
				var buildCosts = (worldObject.BuildCosts ?? [])
					.Where(cost => (!string.IsNullOrWhiteSpace(cost.ItemId) || cost.ItemFilter?.Length > 0) && cost.Quantity > 0)
					.Select(cost =>
					{
						var filter = cost.ItemFilter ?? [];
						if (filter.Length > 0)
						{
							if (!knownItemFilters.Any(f => Facts.HasItemFilter(f) == Facts.HasItemFilter(filter)))
							{
								knownItemFilters.Add(filter);
							}

							return new MaterialCostComponent(filter, cost.Quantity);
						}
						return new MaterialCostComponent(cost.ItemId, cost.Quantity);
					})
					.ToArray();
				var recipes = (worldObject.Recipes ?? [])
					.Where(recipe => !string.IsNullOrWhiteSpace(recipe.Id) && !string.IsNullOrWhiteSpace(recipe.OutputItemId))
					.Select(recipe => new CraftRecipeComponent(
						recipe.Id,
						recipe.Name,
						recipe.RequiredFacts ?? [],
						[.. (recipe.Inputs ?? [])
							.Where(cost => (!string.IsNullOrWhiteSpace(cost.ItemId) || cost.ItemFilter?.Length > 0) && cost.Quantity > 0)
							.Select(cost =>
							{
								var filter = cost.ItemFilter ?? [];
								if (filter.Length > 0)
								{
									if (!knownItemFilters.Any(f => Facts.HasItemFilter(f) == Facts.HasItemFilter(filter))) { knownItemFilters.Add(filter); } return new MaterialCostComponent(filter, cost.Quantity);
								}
								return new MaterialCostComponent(cost.ItemId, cost.Quantity);
							})],
						recipe.OutputItemId,
						recipe.OutputQuantity))
					.ToArray();
				var entity = World.Create(
					new DefinitionIdentityComponent(worldObject.Id, worldObject.Name),
					new TagCollectionComponent(worldObject.Tags ?? []),
					new WorldObjectDefinitionComponent(worldObject.DisplayColor, worldObject.AcceptedItemTags ?? [], worldObject.BlocksMovement, worldObject.IsReservable, worldObject.Capacity, buildCosts, recipes),
					new OccupantVisualComponent(color));

				worldObjectDefinitionEntities[worldObject.Id] = entity;
				worldObjectDefinitionEntitiesById[worldObject.Id] = entity;
				paintableOccupants.Add(new OccupantPaletteEntry(worldObject.Id, worldObject.Name, color, OccupantPaletteKind.WorldObject, DetermineOccupantGroup(worldObject.Tags, OccupantPaletteKind.WorldObject)));
			}

			foreach (var resourceNode in resourceNodes)
			{
				var color = ParseColor(resourceNode.DisplayColor);
				resourceNodeDefinitionEntitiesById[resourceNode.Id] = World.Create(
					new DefinitionIdentityComponent(resourceNode.Id, resourceNode.Name),
					new TagCollectionComponent(resourceNode.Tags ?? []),
					new ResourceNodeDefinitionComponent(
						resourceNode.DisplayColor,
						resourceNode.SupportedActionIds ?? [],
						resourceNode.RequiredToolItemTags ?? [],
						resourceNode.YieldItemId,
						resourceNode.YieldCount,
						resourceNode.BlocksMovement),
					new OccupantVisualComponent(color));

				paintableOccupants.Add(new OccupantPaletteEntry(resourceNode.Id, resourceNode.Name, color, OccupantPaletteKind.ResourceNode, DetermineOccupantGroup(resourceNode.Tags, OccupantPaletteKind.ResourceNode)));
			}

			foreach (var agent in agents.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				var agentStartingSkills = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				if (agent.Skills != null)
				{
					foreach (var kvp in agent.Skills)
					{
						agentStartingSkills[kvp.Key] = kvp.Value;
					}
				}

				agentArchetypeEntities[agent.Id] = World.Create(
					new DefinitionIdentityComponent(agent.Id, agent.Name),
					new AgentSkillsComponent(agentStartingSkills),
					new AgentArchetypeComponent(
						agent.FactionId,
						agent.MemoryProviderId,
						agent.StartingItemIds ?? [],
						agent.BodyParts ?? [],
						agent.Organs ?? [],
						agent.Systems ?? [],
						agent.InventoryCapacity,
						agent.MinSpeed,
						agent.MaxSpeed,
						agent.MinStrength,
						agent.MaxStrength,
						agent.StartingRest,
						agent.MaxRest,
						agent.RestDecayPerTick,
						agent.RestRecoveryPerTick,
						agent.StartingCarbohydratesGrams,
						agent.MaxCarbohydratesGrams,
						agent.StartingProteinGrams,
						agent.MaxProteinGrams,
						agent.StartingFatGrams,
						agent.MaxFatGrams,
						agent.StartingSugarGrams,
						agent.MaxSugarGrams,
						agent.StartingHydrationLiters,
						agent.MaxHydrationLiters,
						agent.SugarUsePerTick,
						agent.HydrationUsePerTick,
						agent.SugarFromCarbohydratesPerTick,
						agent.SugarFromFatPerTick,
						agent.ProteinCatabolismPerTick,
						agent.BodyWidth,
						agent.BodyHeight));
			}

			foreach (var goal in goals.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				goapGoals.Add(new SimulationGoapGoal(
					goal.Id,
					goal.Name,
					goal.Priority,
					goal.Effects ?? [],
					goal.Requirements ?? []));
			}

			defaultAgentArchetypeEntity = agentArchetypeEntities.Count > 0
				? agentArchetypeEntities.Values.First()
				: null;
		}

		public World World { get; }
		public World RuntimeWorld { get; } = World.Create();

		public bool TryGetItemDefinitionEntity(string itemId, out Entity entity)
			=> itemDefinitionEntities.TryGetValue(itemId, out entity);

		public bool TryGetItemDisplayName(string itemId, out string name)
		{
			name = string.Empty;
			if (!itemDefinitionEntities.TryGetValue(itemId, out var entity))
			{
				return false;
			}

			ref var identity = ref entity.Get<DefinitionIdentityComponent>();
			name = string.IsNullOrWhiteSpace(identity.Name) ? identity.Id : identity.Name;
			return true;
		}

		public IReadOnlyList<string[]> GetKnownItemFilters()
			=> knownItemFilters;

		public IReadOnlyList<FactDefinition> GetFactDefinitions()
			=> factDefinitions;

		public IReadOnlyList<string> GetItemDefinitionIdsGrantingFact(string fact)
			=> [.. itemDefinitionEntities
				.Where(pair => pair.Value.Get<ItemDefinitionComponent>().LearnedFacts.Contains(fact, StringComparer.OrdinalIgnoreCase))
				.Select(pair => pair.Key)];

		public bool TryCreateItemEntity(string itemId, out Entity entity)
		{
			entity = default;
			if (!itemDefinitionEntities.TryGetValue(itemId, out var definitionEntity))
			{
				return false;
			}

			ref var identity = ref definitionEntity.Get<DefinitionIdentityComponent>();
			ref var tags = ref definitionEntity.Get<TagCollectionComponent>();
			ref var itemDefinition = ref definitionEntity.Get<ItemDefinitionComponent>();

			entity = RuntimeWorld.Create(
				new ItemInstanceComponent(identity.Id),
				new DefinitionIdentityComponent(identity.Id, identity.Name),
				new TagCollectionComponent(tags.Values),
				new ItemDefinitionComponent(itemDefinition.IsTool, itemDefinition.Stackable, itemDefinition.WeightKg, itemDefinition.LearnedFacts, itemDefinition.Nutrition, itemDefinition.ThrowRange));

			return true;
		}

		public bool TryGetWorldObjectDefinitionEntity(string worldObjectId, out Entity entity)
			=> worldObjectDefinitionEntitiesById.TryGetValue(worldObjectId, out entity);

		public bool TryGetResourceNodeDefinitionEntity(string resourceNodeId, out Entity entity)
			=> resourceNodeDefinitionEntitiesById.TryGetValue(resourceNodeId, out entity);

		public IReadOnlyList<OccupantPaletteEntry> GetPaintableOccupants()
			=> paintableOccupants;

		public IReadOnlyList<ItemPaletteEntry> GetPaletteItems()
			=> paletteItems;

		public bool TryCreateWorldObjectEntity(string worldObjectId, Point position, Point cell, out Entity entity)
		{
			if (!worldObjectDefinitionEntitiesById.TryGetValue(worldObjectId, out var definitionEntity))
			{
				entity = default;
				return false;
			}

			var definition = definitionEntity.Get<WorldObjectDefinitionComponent>();
			if (definition.BuildCosts.Length > 0)
			{
				return TryCreateConstructionSiteEntity(definitionEntity, position, cell, out entity);
			}

			return TryCreateCompletedWorldObjectEntity(worldObjectId, position, cell, out entity);
		}

		public bool TryCreateCompletedWorldObjectEntity(string worldObjectId, Point position, Point cell, out Entity entity)
		{
			entity = default;
			if (!worldObjectDefinitionEntitiesById.TryGetValue(worldObjectId, out var definitionEntity))
			{
				return false;
			}

			var identity = definitionEntity.Get<DefinitionIdentityComponent>();
			var tags = definitionEntity.Get<TagCollectionComponent>();
			var worldObjectDefinition = definitionEntity.Get<WorldObjectDefinitionComponent>();

			entity = RuntimeWorld.Create(
				new WorldObjectTagComponent(),
				new WorldObjectReferenceComponent { DefinitionId = identity.Id },
				new DefinitionIdentityComponent(identity.Id, identity.Name),
				new TagCollectionComponent(tags.Values),
				new WorldObjectDefinitionComponent(
					worldObjectDefinition.DisplayColorHex,
					worldObjectDefinition.AcceptedItemTags,
					worldObjectDefinition.BlocksMovement,
					worldObjectDefinition.IsReservable,
					worldObjectDefinition.Capacity,
					worldObjectDefinition.BuildCosts,
					worldObjectDefinition.Recipes),
				new OccupantVisualComponent(definitionEntity.Get<OccupantVisualComponent>().Color),
				new RuntimeTransformComponent { Position = position },
				new CellReferenceComponent { Cell = cell },
				new InventoryComponent { Items = [], Capacity = worldObjectDefinition.Capacity });

			return true;
		}

		bool TryCreateConstructionSiteEntity(Entity definitionEntity, Point position, Point cell, out Entity entity)
		{
			var identity = definitionEntity.Get<DefinitionIdentityComponent>();
			var tags = definitionEntity.Get<TagCollectionComponent>();
			var worldObjectDefinition = definitionEntity.Get<WorldObjectDefinitionComponent>();
			var siteTags = tags.Values
				.Concat(["construction-site", "storage", "container"])
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
			var siteColor = Color.Lerp(definitionEntity.Get<OccupantVisualComponent>().Color, Color.SandyBrown, 0.45f);
			var capacity = System.Math.Max(1, worldObjectDefinition.BuildCosts.Sum(cost => cost.Quantity));

			entity = RuntimeWorld.Create(
				new WorldObjectTagComponent(),
				new WorldObjectReferenceComponent { DefinitionId = identity.Id },
				new DefinitionIdentityComponent($"{identity.Id}-site", $"{identity.Name} (Site)"),
				new TagCollectionComponent(siteTags),
				new WorldObjectDefinitionComponent(
					worldObjectDefinition.DisplayColorHex,
					[],
					false,
					false,
					capacity,
					worldObjectDefinition.BuildCosts,
					[]),
				new OccupantVisualComponent(siteColor),
				new RuntimeTransformComponent { Position = position },
				new CellReferenceComponent { Cell = cell },
				new InventoryComponent { Items = [], Capacity = capacity },
				new ConstructionSiteComponent
				{
					TargetDefinitionId = identity.Id,
					BuildCosts = worldObjectDefinition.BuildCosts,
				});

			return true;
		}

		public bool TryCreateResourceNodeEntity(string resourceNodeId, Point position, Point cell, out Entity entity)
		{
			entity = default;
			if (!resourceNodeDefinitionEntitiesById.TryGetValue(resourceNodeId, out var definitionEntity))
			{
				return false;
			}

			var identity = definitionEntity.Get<DefinitionIdentityComponent>();
			var tags = definitionEntity.Get<TagCollectionComponent>();
			var resourceDefinition = definitionEntity.Get<ResourceNodeDefinitionComponent>();

			entity = RuntimeWorld.Create(
				new WorldObjectTagComponent(),
				new WorldObjectReferenceComponent { DefinitionId = identity.Id },
				new DefinitionIdentityComponent(identity.Id, identity.Name),
				new TagCollectionComponent(tags.Values),
				new ResourceNodeDefinitionComponent(
					resourceDefinition.DisplayColorHex,
					resourceDefinition.SupportedActionIds,
					resourceDefinition.RequiredToolItemTags,
					resourceDefinition.YieldItemId,
					resourceDefinition.YieldCount,
					resourceDefinition.BlocksMovement),
				new OccupantVisualComponent(definitionEntity.Get<OccupantVisualComponent>().Color),
				new RuntimeTransformComponent { Position = position },
				new CellReferenceComponent { Cell = cell });

			return true;
		}

		public IReadOnlyList<SimulationGoapAction> GetActionDefinitions()
			=> goapActions;

		IReadOnlyList<GoapAction> IGoapDefinitionSource.GetActionDefinitions()
			=> goapActions;

		public bool TryGetResourceNodeDefinition(string resourceNodeId, out ResourceNodeDefinitionSnapshot snapshot)
		{
			if (!resourceNodeDefinitionEntitiesById.TryGetValue(resourceNodeId, out var entity))
			{
				snapshot = default;
				return false;
			}

			var identity = entity.Get<DefinitionIdentityComponent>();
			var tags = entity.Get<TagCollectionComponent>();
			var definition = entity.Get<ResourceNodeDefinitionComponent>();
			snapshot = new ResourceNodeDefinitionSnapshot(
				identity.Id,
				identity.Name,
				definition.DisplayColorHex,
				tags.Values,
				definition.SupportedActionIds,
				definition.RequiredToolItemTags,
				definition.YieldItemId,
				definition.YieldCount,
				definition.BlocksMovement);
			return true;
		}

		public IReadOnlyList<SimulationGoapGoal> GetGoalDefinitions()
			=> [.. goapGoals.OrderByDescending(goal => goal.PriorityValue)];

		IReadOnlyList<GoapGoal> IGoapDefinitionSource.GetGoalDefinitions()
			=> [.. goapGoals.OrderByDescending(goal => goal.PriorityValue)];

		public bool IsCellTypeWalkable(CellType cellType) => cellType != CellType.Water;

		static string DetermineOccupantGroup(string[] tags, OccupantPaletteKind kind)
		{
			if (tags == null || tags.Length == 0)
			{
				return "Misc";
			}

			if (kind == OccupantPaletteKind.ResourceNode)
			{
				if (Array.Exists(tags, t => string.Equals(t, "tree", StringComparison.OrdinalIgnoreCase)))
				{
					return "Trees";
				}

				if (Array.Exists(tags, t => string.Equals(t, "ore", StringComparison.OrdinalIgnoreCase)))
				{
					return "Ore Veins";
				}

				if (Array.Exists(tags, t => string.Equals(t, "mineable", StringComparison.OrdinalIgnoreCase)))
				{
					return "Minerals";
				}
			}
			else
			{
				if (Array.Exists(tags, t => string.Equals(t, "workstation", StringComparison.OrdinalIgnoreCase)))
				{
					return "Workstations";
				}

				if (Array.Exists(tags, t => string.Equals(t, "structure", StringComparison.OrdinalIgnoreCase)))
				{
					return "Structures";
				}

				if (Array.Exists(tags, t => string.Equals(t, "bed", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "furniture", StringComparison.OrdinalIgnoreCase)))
				{
					return "Furniture";
				}

				if (Array.Exists(tags, t => string.Equals(t, "storage", StringComparison.OrdinalIgnoreCase)))
				{
					return "Storage";
				}
			}

			return "Misc";
		}

		static string DetermineItemGroup(string[] tags)
		{
			if (tags == null || tags.Length == 0)
			{
				return "Misc";
			}

			if (Array.Exists(tags, t => string.Equals(t, "tool", StringComparison.OrdinalIgnoreCase)))
			{
				return "Tools";
			}

			if (Array.Exists(tags, t => string.Equals(t, "food", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "drink", StringComparison.OrdinalIgnoreCase)))
			{
				return "Food & Drink";
			}

			if (Array.Exists(tags, t => string.Equals(t, "ore", StringComparison.OrdinalIgnoreCase)))
			{
				return "Ores";
			}

			if (Array.Exists(tags, t => string.Equals(t, "fuel", StringComparison.OrdinalIgnoreCase)))
			{
				return "Fuel";
			}

			if (Array.Exists(tags, t => string.Equals(t, "ingot", StringComparison.OrdinalIgnoreCase)))
			{
				return "Ingots";
			}

			if (Array.Exists(tags, t => string.Equals(t, "plank", StringComparison.OrdinalIgnoreCase)))
			{
				return "Planks";
			}

			if (Array.Exists(tags, t => string.Equals(t, "weapon", StringComparison.OrdinalIgnoreCase)))
			{
				return "Weapons";
			}

			if (Array.Exists(tags, t => string.Equals(t, "book", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "knowledge", StringComparison.OrdinalIgnoreCase)))
			{
				return "Knowledge";
			}

			if (Array.Exists(tags, t => string.Equals(t, "crafted", StringComparison.OrdinalIgnoreCase)))
			{
				return "Parts & Components";
			}

			if (Array.Exists(tags, t => string.Equals(t, "wood", StringComparison.OrdinalIgnoreCase)))
			{
				return "Logs";
			}

			if (Array.Exists(tags, t => string.Equals(t, "resource", StringComparison.OrdinalIgnoreCase)))
			{
				return "Resources";
			}

			return "Misc";
		}

		static Color DetermineItemGroupColor(string group) => group switch
		{
			"Tools"               => new Color(0xC8, 0x80, 0x40),
			"Food & Drink"        => new Color(0x78, 0xA8, 0x52),
			"Ores"                => new Color(0x88, 0x88, 0x8A),
			"Fuel"                => new Color(0x50, 0x40, 0x30),
			"Ingots"              => new Color(0xB8, 0xB0, 0xA0),
			"Logs"                => new Color(0x8B, 0x5E, 0x38),
			"Planks"              => new Color(0xC4, 0x9A, 0x6C),
			"Parts & Components"  => new Color(0x90, 0x90, 0x90),
			"Weapons"             => new Color(0xA0, 0x30, 0x30),
			"Knowledge"           => new Color(0x50, 0x70, 0xC0),
			"Resources"           => new Color(0x88, 0x88, 0x80),
			_                     => new Color(0xC0, 0xC0, 0xC0),
		};

		static Color ParseColor(string hex)
		{
			if (string.IsNullOrWhiteSpace(hex))
			{
				return Color.White;
			}

			var value = hex.Trim().TrimStart('#');
			if (value.Length == 6
				&& byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
				&& byte.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
				&& byte.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
			{
				return new Color(r, g, b);
			}

			return Color.White;
		}

		public bool TryGetAgentArchetype(string agentId, out AgentArchetypeSnapshot snapshot)
		{
			snapshot = AgentArchetypeSnapshot.Default;
			if (!agentArchetypeEntities.TryGetValue(agentId, out var entity))
			{
				return false;
			}

			ref var archetype = ref entity.Get<AgentArchetypeComponent>();
			snapshot = new AgentArchetypeSnapshot(
				archetype.FactionId,
				archetype.MemoryProviderId,
				archetype.StartingItemIds,
				archetype.BodyParts,
				archetype.Organs,
				archetype.Systems,
				archetype.InventoryCapacity,
				archetype.MinSpeed,
				archetype.MaxSpeed,
				archetype.MinStrength,
				archetype.MaxStrength,
				archetype.StartingRest,
				archetype.MaxRest,
				archetype.RestDecayPerTick,
				archetype.RestRecoveryPerTick,
				archetype.StartingCarbohydratesGrams,
				archetype.MaxCarbohydratesGrams,
				archetype.StartingProteinGrams,
				archetype.MaxProteinGrams,
				archetype.StartingFatGrams,
				archetype.MaxFatGrams,
				archetype.StartingSugarGrams,
				archetype.MaxSugarGrams,
				archetype.StartingHydrationLiters,
				archetype.MaxHydrationLiters,
				archetype.SugarUsePerTick,
				archetype.HydrationUsePerTick,
				archetype.SugarFromCarbohydratesPerTick,
				archetype.SugarFromFatPerTick,
				archetype.ProteinCatabolismPerTick,
				archetype.BodyWidth,
				archetype.BodyHeight);
			return true;
		}

		public AgentArchetypeSnapshot GetDefaultAgentArchetype()
		{
			if (defaultAgentArchetypeEntity.HasValue)
			{
				var entity = defaultAgentArchetypeEntity.Value;
				if (!entity.Has<AgentArchetypeComponent>())
				{
					return AgentArchetypeSnapshot.Default;
				}

				ref var archetype = ref entity.Get<AgentArchetypeComponent>();
				return new AgentArchetypeSnapshot(
					archetype.FactionId,
					archetype.MemoryProviderId,
					archetype.StartingItemIds,
					archetype.BodyParts,
					archetype.Organs,
					archetype.Systems,
					archetype.InventoryCapacity,
					archetype.MinSpeed,
					archetype.MaxSpeed,
					archetype.MinStrength,
					archetype.MaxStrength,
					archetype.StartingRest,
					archetype.MaxRest,
					archetype.RestDecayPerTick,
					archetype.RestRecoveryPerTick,
					archetype.StartingCarbohydratesGrams,
					archetype.MaxCarbohydratesGrams,
					archetype.StartingProteinGrams,
					archetype.MaxProteinGrams,
					archetype.StartingFatGrams,
					archetype.MaxFatGrams,
					archetype.StartingSugarGrams,
					archetype.MaxSugarGrams,
					archetype.StartingHydrationLiters,
					archetype.MaxHydrationLiters,
					archetype.SugarUsePerTick,
					archetype.HydrationUsePerTick,
					archetype.SugarFromCarbohydratesPerTick,
					archetype.SugarFromFatPerTick,
					archetype.ProteinCatabolismPerTick,
					archetype.BodyWidth,
					archetype.BodyHeight);
			}

			return AgentArchetypeSnapshot.Default;
		}

		public IReadOnlyList<SkillDefinition> GetSkillDefinitions() => skillDefinitions;

		public bool TryGetAgentSkills(string agentId, out System.Collections.Generic.Dictionary<string, int> skills)
		{
			skills = null;
			if (!agentArchetypeEntities.TryGetValue(agentId, out var entity))
			{
				return false;
			}

			skills = entity.Get<AgentSkillsComponent>().Skills;
			return true;
		}

		public static SimulationDefinitionRegistry LoadFromContentDirectory(string contentRoot)
		{
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true,
			};
			options.Converters.Add(new JsonStringEnumConverter());

			var items = LoadDocument<ItemDefinitionDocument>(Path.Combine(contentRoot, "items.json"), options).Items;
			var actions = LoadDocument<ActionDefinitionDocument>(Path.Combine(contentRoot, "actions.json"), options).Actions;
			var objects = LoadDocument<WorldObjectDefinitionDocument>(Path.Combine(contentRoot, "objects.json"), options).Objects;
			var resources = LoadDocument<ResourceNodeDefinitionDocument>(Path.Combine(contentRoot, "resources.json"), options).ResourceNodes;
			var agents = LoadDocument<AgentDefinitionDocument>(Path.Combine(contentRoot, "agents.json"), options).Agents;
			var goals = LoadDocument<GoalDefinitionDocument>(Path.Combine(contentRoot, "goals.json"), options).Goals;
			var facts = LoadDocument<FactDefinitionDocument>(Path.Combine(contentRoot, "facts.json"), options).Facts;
			var skills = LoadDocument<SkillDefinitionDocument>(Path.Combine(contentRoot, "skills.json"), options).Skills;

			return new SimulationDefinitionRegistry(World.Create(), items, actions, objects, resources, agents, goals, facts, skills);
		}

		static T LoadDocument<T>(string path, JsonSerializerOptions options)
			where T : new()
		{
			if (!File.Exists(path))
			{
				return new T();
			}

			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<T>(json, options) ?? new T();
		}
	}
}
