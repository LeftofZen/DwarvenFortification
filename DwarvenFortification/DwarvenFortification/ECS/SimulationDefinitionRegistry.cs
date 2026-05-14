using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DwarvenFortification
{
	public sealed class SimulationDefinitionRegistry
	{
		readonly Dictionary<string, Entity> itemDefinitionEntities;
		readonly Dictionary<string, Entity> actionDefinitionEntities;
		readonly Dictionary<string, Entity> worldObjectDefinitionEntities;
		readonly Dictionary<string, Entity> worldObjectDefinitionEntitiesById;
		readonly Dictionary<string, Entity> resourceNodeDefinitionEntitiesById;
		readonly List<OccupantPaletteEntry> paintableOccupants;
		readonly Dictionary<string, Entity> agentArchetypeEntities;
		readonly Dictionary<string, Entity> goalDefinitionEntities;
		readonly Entity? defaultAgentArchetypeEntity;

		SimulationDefinitionRegistry(
			World world,
			IEnumerable<ItemDefinition> items,
			IEnumerable<ActionDefinition> actions,
			IEnumerable<WorldObjectDefinition> objects,
			IEnumerable<ResourceNodeDefinition> resourceNodes,
			IEnumerable<AgentDefinition> agents,
			IEnumerable<GoalDefinition> goals)
		{
			World = world;
			itemDefinitionEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			actionDefinitionEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			worldObjectDefinitionEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			worldObjectDefinitionEntitiesById = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			paintableOccupants = new List<OccupantPaletteEntry>();
			resourceNodeDefinitionEntitiesById = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			agentArchetypeEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
			goalDefinitionEntities = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);

			foreach (var item in items.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				itemDefinitionEntities[item.Id] = World.Create(
					new DefinitionIdentityComponent(item.Id, item.Name),
					new TagCollectionComponent(item.Tags ?? Array.Empty<string>()),
					new ItemDefinitionComponent(item.IsTool, item.Stackable, item.WeightKg, item.LearnedFacts ?? Array.Empty<string>(), item.NutritionValue, item.HydrationValue, item.ThrowRange));
			}

			foreach (var action in actions.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				actionDefinitionEntities[action.Id] = World.Create(
					new DefinitionIdentityComponent(action.Id, action.Name),
					new ActionDefinitionComponent(action.TargetKind, action.DestinationMode, action.BaseCost, action.DurationTicks),
					new ActionRequirementComponent(
						action.Requires.RequiredItemIds ?? Array.Empty<string>(),
						action.Requires.RequiredTargetTags ?? Array.Empty<string>(),
						action.Requires.RequiredBodyParts ?? Array.Empty<string>(),
						action.Requires.RequiredOrgans ?? Array.Empty<string>(),
						action.Requires.RequiredSystems ?? Array.Empty<string>(),
						action.Requires.RequiredFacts ?? Array.Empty<string>(),
						action.Requires.BlockedByFacts ?? Array.Empty<string>(),
						action.Requires.RequiresFreeInventorySlot,
						action.Requires.RequiresReservation),
					new ActionOutputComponent(action.Outputs ?? Array.Empty<ActionOutputDefinition>()),
					new ActionEffectComponent(action.Effects.AddFacts ?? Array.Empty<string>(), action.Effects.RemoveFacts ?? Array.Empty<string>()));
			}

			foreach (var worldObject in objects.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				var color = ParseColor(worldObject.DisplayColor);
				var entity = World.Create(
					new DefinitionIdentityComponent(worldObject.Id, worldObject.Name),
					new TagCollectionComponent(worldObject.Tags ?? Array.Empty<string>()),
					new WorldObjectDefinitionComponent(worldObject.DisplayColor, worldObject.AcceptedItemTags ?? Array.Empty<string>(), worldObject.BlocksMovement, worldObject.IsReservable, worldObject.Capacity),
					new OccupantVisualComponent(color));

				worldObjectDefinitionEntities[worldObject.Id] = entity;
				worldObjectDefinitionEntitiesById[worldObject.Id] = entity;
				paintableOccupants.Add(new OccupantPaletteEntry(worldObject.Id, worldObject.Name, color, OccupantPaletteKind.WorldObject));
			}

			foreach (var resourceNode in resourceNodes)
			{
				var color = ParseColor(resourceNode.DisplayColor);
				resourceNodeDefinitionEntitiesById[resourceNode.Id] = World.Create(
					new DefinitionIdentityComponent(resourceNode.Id, resourceNode.Name),
					new TagCollectionComponent(resourceNode.Tags ?? Array.Empty<string>()),
					new ResourceNodeDefinitionComponent(
						resourceNode.DisplayColor,
						resourceNode.SupportedActionIds ?? Array.Empty<string>(),
						resourceNode.RequiredToolItemIds ?? Array.Empty<string>(),
						resourceNode.YieldItemId,
						resourceNode.YieldCount,
						resourceNode.BlocksMovement),
					new OccupantVisualComponent(color));

				paintableOccupants.Add(new OccupantPaletteEntry(resourceNode.Id, resourceNode.Name, color, OccupantPaletteKind.ResourceNode));
			}

			foreach (var agent in agents.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				agentArchetypeEntities[agent.Id] = World.Create(
					new DefinitionIdentityComponent(agent.Id, agent.Name),
					new AgentArchetypeComponent(
						agent.FactionId,
						agent.MemoryProviderId,
						agent.StartingItemIds ?? Array.Empty<string>(),
						agent.BodyParts ?? Array.Empty<string>(),
						agent.Organs ?? Array.Empty<string>(),
						agent.Systems ?? Array.Empty<string>(),
						agent.InventoryCapacity,
						agent.MinSpeed,
						agent.MaxSpeed,
						agent.MinStrength,
						agent.MaxStrength,
						agent.StartingRest,
						agent.MaxRest,
						agent.RestDecayPerTick,
						agent.RestRecoveryPerTick,
						agent.StartingHunger,
						agent.MaxHunger,
						agent.HungerDecayPerTick,
						agent.HungerRecoveryPerTick,
						agent.StartingThirst,
						agent.MaxThirst,
						agent.ThirstDecayPerTick,
						agent.ThirstRecoveryPerTick,
						agent.BodyWidth,
						agent.BodyHeight));
			}

			foreach (var goal in goals.Where(def => !string.IsNullOrWhiteSpace(def.Id)))
			{
				goalDefinitionEntities[goal.Id] = World.Create(
					new DefinitionIdentityComponent(goal.Id, goal.Name),
					new GoalDefinitionComponent(
						goal.Priority,
						goal.DesiredFacts ?? Array.Empty<string>(),
						goal.ForbiddenFacts ?? Array.Empty<string>(),
						goal.RequiredFacts ?? Array.Empty<string>(),
						goal.BlockedByFacts ?? Array.Empty<string>()));
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

		public IReadOnlyList<string> GetItemDefinitionIdsGrantingFact(string fact)
			=> itemDefinitionEntities
				.Where(pair => pair.Value.Get<ItemDefinitionComponent>().LearnedFacts.Contains(fact, StringComparer.OrdinalIgnoreCase))
				.Select(pair => pair.Key)
				.ToArray();

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
				new ItemDefinitionComponent(itemDefinition.IsTool, itemDefinition.Stackable, itemDefinition.WeightKg, itemDefinition.LearnedFacts, itemDefinition.NutritionValue, itemDefinition.HydrationValue, itemDefinition.ThrowRange));

			return true;
		}

		public bool TryGetWorldObjectDefinitionEntity(string worldObjectId, out Entity entity)
			=> worldObjectDefinitionEntitiesById.TryGetValue(worldObjectId, out entity);

		public bool TryGetResourceNodeDefinitionEntity(string resourceNodeId, out Entity entity)
			=> resourceNodeDefinitionEntitiesById.TryGetValue(resourceNodeId, out entity);

		public IReadOnlyList<OccupantPaletteEntry> GetPaintableOccupants()
			=> paintableOccupants;

		public bool TryCreateWorldObjectEntity(string worldObjectId, Point position, Point cell, out Entity entity)
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
					worldObjectDefinition.Capacity),
				new OccupantVisualComponent(definitionEntity.Get<OccupantVisualComponent>().Color),
				new RuntimeTransformComponent { Position = position },
				new CellReferenceComponent { Cell = cell },
				new InventoryComponent { Items = new List<Entity>(), Capacity = worldObjectDefinition.Capacity });

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
					resourceDefinition.RequiredToolItemIds,
					resourceDefinition.YieldItemId,
					resourceDefinition.YieldCount,
					resourceDefinition.BlocksMovement),
				new OccupantVisualComponent(definitionEntity.Get<OccupantVisualComponent>().Color),
				new RuntimeTransformComponent { Position = position },
				new CellReferenceComponent { Cell = cell });

			return true;
		}

		public IReadOnlyList<ActionDefinitionSnapshot> GetActionDefinitions()
			=> actionDefinitionEntities.Values
				.Select(entity =>
				{
					var identity = entity.Get<DefinitionIdentityComponent>();
					var definition = entity.Get<ActionDefinitionComponent>();
					var requirements = entity.Get<ActionRequirementComponent>();
					var effects = entity.Get<ActionEffectComponent>();
					return new ActionDefinitionSnapshot(
						identity.Id,
						identity.Name,
						definition.TargetKind,
						definition.DestinationMode,
						definition.BaseCost,
						definition.DurationTicks,
						requirements.RequiredItemIds,
						requirements.RequiredTargetTags,
						requirements.RequiredBodyParts,
						requirements.RequiredOrgans,
						requirements.RequiredSystems,
						requirements.RequiredFacts,
						requirements.BlockedByFacts,
						requirements.RequiresFreeInventorySlot,
						requirements.RequiresReservation,
						effects.AddFacts,
						effects.RemoveFacts);
				})
				.ToArray();

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
				definition.RequiredToolItemIds,
				definition.YieldItemId,
				definition.YieldCount,
				definition.BlocksMovement);
			return true;
		}

		public IReadOnlyList<GoapGoal> GetGoalDefinitions()
			=> goalDefinitionEntities.Values
				.Select(entity =>
				{
					var identity = entity.Get<DefinitionIdentityComponent>();
					var goal = entity.Get<GoalDefinitionComponent>();
					return new GoapGoal(
						identity.Id,
						identity.Name,
						goal.Priority,
						goal.DesiredFacts,
						goal.ForbiddenFacts,
						goal.RequiredFacts,
						goal.BlockedByFacts);
				})
				.OrderByDescending(goal => goal.Priority)
				.ToArray();

		public bool IsCellTypeWalkable(CellType cellType)
		{
			return cellType != CellType.Water;
		}

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
				archetype.StartingHunger,
				archetype.MaxHunger,
				archetype.HungerDecayPerTick,
				archetype.HungerRecoveryPerTick,
				archetype.StartingThirst,
				archetype.MaxThirst,
				archetype.ThirstDecayPerTick,
				archetype.ThirstRecoveryPerTick,
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
					archetype.StartingHunger,
					archetype.MaxHunger,
					archetype.HungerDecayPerTick,
					archetype.HungerRecoveryPerTick,
					archetype.StartingThirst,
					archetype.MaxThirst,
					archetype.ThirstDecayPerTick,
					archetype.ThirstRecoveryPerTick,
					archetype.BodyWidth,
					archetype.BodyHeight);
			}

			return AgentArchetypeSnapshot.Default;
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

			return new SimulationDefinitionRegistry(World.Create(), items, actions, objects, resources, agents, goals);
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
