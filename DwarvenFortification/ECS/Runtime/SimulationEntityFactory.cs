using Arch.Core;
using DwarvenFortification.Actions;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.Simulation.Composition;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace DwarvenFortification.ECS.Runtime
{
	public sealed class SimulationEntityFactory : ISimulationEntityFactory
	{
		static readonly Random random = new(1);
		readonly SimulationDefinitionRegistry definitions;

		public SimulationEntityFactory(SimulationDefinitionRegistry definitions)
			=> this.definitions = definitions;

		public Entity CreateAgent(string name, Point position, string archetypeId = "dwarf")
		{
			var definition = definitions.TryGetAgentArchetype(archetypeId, out var loadedDefinition)
				? loadedDefinition
				: definitions.GetDefaultAgentArchetype();

			var startingSkills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			if (definitions.TryGetAgentSkills(archetypeId, out var archetypeSkills) && archetypeSkills != null)
			{
				foreach (var kvp in archetypeSkills)
				{
					startingSkills[kvp.Key] = kvp.Value;
				}
			}

			var world = definitions.RuntimeWorld;

			var agent = world.Create(
				new AgentTagComponent(),
				new DefinitionIdentityComponent(name, name),
				new AgentArchetypeReferenceComponent { ArchetypeId = archetypeId },
				new FactionComponent { FactionId = definition.FactionId },
				new AgentSkillsComponent(startingSkills),
				new RuntimeTransformComponent { Position = position },
				new BodyComponent { Width = definition.BodyWidth, Height = definition.BodyHeight },
				new LifeBodyComponent
				{
					BodyParts = definition.BodyParts,
					Organs = definition.Organs,
					Systems = definition.Systems,
				},
				new AgentStatsComponent
				{
					Strength = Lerp(definition.MinStrength, definition.MaxStrength, (float)random.NextDouble()),
					BaseSpeed = Lerp(definition.MinSpeed, definition.MaxSpeed, (float)random.NextDouble()),
				},
				new RestNeedComponent
				{
					Current = definition.StartingRest,
					Max = definition.MaxRest,
					DecayPerTick = definition.RestDecayPerTick,
					RecoveryPerTick = definition.RestRecoveryPerTick,
				},
				new BodyNutritionComponent
				{
					CarbohydratesCurrent = definition.StartingCarbohydratesGrams,
					CarbohydratesMax = definition.MaxCarbohydratesGrams,
					ProteinCurrent = definition.StartingProteinGrams,
					ProteinMax = definition.MaxProteinGrams,
					FatCurrent = definition.StartingFatGrams,
					FatMax = definition.MaxFatGrams,
					SugarCurrent = definition.StartingSugarGrams,
					SugarMax = definition.MaxSugarGrams,
					HydrationCurrentLiters = definition.StartingHydrationLiters,
					HydrationMaxLiters = definition.MaxHydrationLiters,
					SugarUsePerTick = definition.SugarUsePerTick,
					HydrationUsePerTick = definition.HydrationUsePerTick,
					SugarFromCarbohydratesPerTick = definition.SugarFromCarbohydratesPerTick,
					SugarFromFatPerTick = definition.SugarFromFatPerTick,
					ProteinCatabolismPerTick = definition.ProteinCatabolismPerTick,
				},
				new PerceptionComponent { LastKnownEnemyCell = new Point(-1, -1) },
				new StealthComponent(),
				new PatrolStateComponent(),
				new MemoryComponent
				{
					ProviderId = definition.MemoryProviderId,
					KnownFacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
					KnownItemLocations = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase),
				},
				new InventoryComponent { Items = [], Capacity = definition.InventoryCapacity },
				new ActionQueueComponent { Actions = new Queue<IAgentAction>() });

			foreach (var itemId in definition.StartingItemIds)
			{
				agent.AddInventoryItem(CreateItem(itemId));
			}

			return agent;
		}

		public Entity CreateItem(string definitionId)
		{
			if (!definitions.TryCreateItemEntity(definitionId, out var entity))
			{
				throw new InvalidOperationException($"Unable to create item entity for '{definitionId}'.");
			}

			return entity;
		}

		public Entity CreateWorldObject(string definitionId, Point position, Point cell)
		{
			if (!definitions.TryCreateWorldObjectEntity(definitionId, position, cell, out var entity))
			{
				throw new InvalidOperationException($"Unable to create world object entity for '{definitionId}'.");
			}

			return entity;
		}

		public Entity CreateCompletedWorldObject(string definitionId, Point position, Point cell)
		{
			if (!definitions.TryCreateCompletedWorldObjectEntity(definitionId, position, cell, out var entity))
			{
				throw new InvalidOperationException($"Unable to create completed world object entity for '{definitionId}'.");
			}

			return entity;
		}

		public Entity CreateResourceNode(string definitionId, Point position, Point cell)
		{
			if (!definitions.TryCreateResourceNodeEntity(definitionId, position, cell, out var entity))
			{
				throw new InvalidOperationException($"Unable to create resource node entity for '{definitionId}'.");
			}

			return entity;
		}

		static float Lerp(float min, float max, float amount)
			=> min + ((max - min) * amount);
	}
}
