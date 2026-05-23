using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public sealed class GoapWorldQueryService : IGoapWorldQueryService
	{
		readonly SimulationDefinitionRegistry definitions;
		readonly Func<ISimulationWorld> worldAccessor;

		public GoapWorldQueryService(SimulationDefinitionRegistry definitions, Func<ISimulationWorld> worldAccessor)
		{
			this.definitions = definitions;
			this.worldAccessor = worldAccessor;
		}

		public IReadOnlyDictionary<string, GoapValue> BuildNumericState(Entity agent)
		{
			var values = new Dictionary<string, GoapValue>(StringComparer.OrdinalIgnoreCase);

			// Vitals normalized to 0..100 percent of capacity.
			values[Facts.VitalRest] = (int)Math.Round(Math.Clamp(agent.GetRestRatio(), 0f, 1f) * 100f);
			values[Facts.VitalHunger] = (int)Math.Round(Math.Clamp(agent.GetMetabolicEnergyRatio(), 0f, 1f) * 100f);
			values[Facts.VitalThirst] = (int)Math.Round(Math.Clamp(agent.GetHydrationRatio(), 0f, 1f) * 100f);

			if (agent.Has<BodyNutritionComponent>())
			{
				var n = agent.Get<BodyNutritionComponent>();
				values[Facts.VitalCarbohydrates] = ToPercent(n.CarbohydratesCurrent, n.CarbohydratesMax);
				values[Facts.VitalProtein] = ToPercent(n.ProteinCurrent, n.ProteinMax);
				values[Facts.VitalFat] = ToPercent(n.FatCurrent, n.FatMax);
				values[Facts.VitalSugar] = ToPercent(n.SugarCurrent, n.SugarMax);
				values[Facts.VitalHydration] = ToPercent(n.HydrationCurrentLiters, n.HydrationMaxLiters);
			}

			var inventory = agent.GetInventory();
			var capacity = agent.GetInventoryCapacity();
			var count = inventory.Count;
			values[Facts.InventoryCount] = count;
			values[Facts.InventoryCapacity] = capacity;
			values[Facts.InventoryFree] = Math.Max(0, capacity - count);

			values[Facts.PerceptionScanTicks] = agent.Has<PerceptionComponent>()
				? agent.Get<PerceptionComponent>().ScanTicksRemaining
				: 0;

			return values;
		}

		static int ToPercent(float current, float max)
			=> max <= 0f ? 0 : (int)Math.Round(Math.Clamp(current / max, 0f, 1f) * 100f);

		public HashSet<string> BuildCurrentState(Entity agent)
		{
			var world = worldAccessor();
			var facts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var inventory = agent.GetInventory();

			foreach (var item in inventory)
			{
				facts.Add(Facts.HasItem(item.GetItemDefinitionId()));
				if (item.Has<TagCollectionComponent>())
				{
					foreach (var tag in item.Get<TagCollectionComponent>().Values)
					{
						facts.Add(Facts.HasItemTag(tag));
					}
				}

				if (!item.Get<ItemDefinitionComponent>().IsTool)
				{
					facts.Add(Facts.InventoryHasResourceItems);
				}
			}

			foreach (var filter in definitions.GetKnownItemFilters())
			{
				if (inventory.Any(item => item.ItemMatchesFilter(filter)))
				{
					facts.Add(Facts.HasItemFilter(filter));
				}
			}

			if (agent.Has<LifeBodyComponent>())
			{
				var body = agent.Get<LifeBodyComponent>();
				foreach (var bodyPart in body.BodyParts)
				{
					facts.Add(Facts.HasBodyPart(bodyPart));
				}

				foreach (var organ in body.Organs)
				{
					facts.Add(Facts.HasOrgan(organ));
				}

				foreach (var system in body.Systems)
				{
					if (agent.IsSystemOperational(system))
					{
						facts.Add(Facts.HasSystem(system));
					}
					else
					{
						facts.Add(Facts.SystemImpaired(system));
					}
				}
			}

			if (agent.HasMemoryCapability())
			{
				facts.Add(Facts.MemoryCapable);
				facts.Add(Facts.MemoryProvider(agent.GetMemoryProviderId()));
				foreach (var knownFact in agent.GetKnownFacts())
				{
					facts.Add(knownFact);
				}

				foreach (var entry in agent.GetKnownItemLocations())
				{
					if (world.CellContainsItem(entry.Value, entry.Key))
					{
						facts.Add(Facts.KnowsItemLocation(entry.Key));
					}
				}
			}

			if (inventory.Count < agent.GetInventoryCapacity())
			{
				facts.Add(Facts.InventoryHasSpace);
			}

			var rest = agent.Get<RestNeedComponent>();
			facts.Add(rest.Current <= rest.Max * 0.35f ? Facts.RestLow : Facts.RestOk);

			facts.Add(agent.IsHungry() ? Facts.HungerLow : Facts.HungerOk);
			facts.Add(agent.IsThirsty() ? Facts.ThirstLow : Facts.ThirstOk);
			facts.Add(agent.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Carbohydrates) ? Facts.NutrientLow("carbohydrates") : Facts.NutrientOk("carbohydrates"));
			facts.Add(agent.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Protein) ? Facts.NutrientLow("protein") : Facts.NutrientOk("protein"));
			facts.Add(agent.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Fat) ? Facts.NutrientLow("fat") : Facts.NutrientOk("fat"));
			facts.Add(agent.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Sugar) ? Facts.NutrientLow("sugar") : Facts.NutrientOk("sugar"));
			facts.Add(agent.IsNutrientLow(SimulationEntityExtensions.NutrientKind.Hydration) ? Facts.NutrientLow("hydration") : Facts.NutrientOk("hydration"));

			if (agent.IsHidden())
			{
				facts.Add(Facts.SelfHidden);
			}

			if (agent.IsRecentlyPatrolling())
			{
				facts.Add(Facts.AreaPatrolled);
			}

			if (agent.Has<PerceptionComponent>())
			{
				var perception = agent.Get<PerceptionComponent>();
				if (perception.ScanTicksRemaining > 0)
				{
					facts.Add(Facts.AreaScanned);
				}

				if (perception.EnemyVisible)
				{
					facts.Add(Facts.EnemyVisible);
				}
			}

			var agentCell = world.CoordsAtXY(agent.GetPosition());
			foreach (var (cell, _, _) in world.EnumerateCells())
			{
				if (!cell.TryGetWorldObject(out var worldObject))
				{
					continue;
				}

				if (worldObject.IsConstructionSite())
				{
					facts.Add(Facts.ConstructionPending);
					if (worldObject.GetMissingBuildCosts().Length > 0)
					{
						facts.Add(Facts.SiteNeedsMaterials);
					}
					continue;
				}

				if (worldObject.Has<WorldObjectReferenceComponent>())
				{
					facts.Add(Facts.HasStructure(worldObject.Get<WorldObjectReferenceComponent>().DefinitionId));
				}

				foreach (var recipe in worldObject.GetRecipes())
				{
					if (recipe.RequiredFacts.All(facts.Contains) && worldObject.HasStoredMaterials(recipe.Inputs))
					{
						facts.Add(Facts.CraftableItem(recipe.OutputItemId));
					}
				}

				if (worldObject.HasActiveProductionOrder())
				{
					facts.Add(Facts.ProductionOrderActive);
					if (worldObject.GetMissingOrderInputs().Length > 0)
					{
						facts.Add(Facts.WorkstationNeedsInputs);
					}
				}
			}

			var hasNearbyEnemy = world.GetAgents()
				.Where(other => !other.Equals(agent) && !string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase))
				.Any(other => Vector2.DistanceSquared(world.CoordsAtXY(other.GetPosition()).ToVector2(), agentCell.ToVector2()) <= 64f);
			if (hasNearbyEnemy)
			{
				facts.Add(Facts.EnemyNearby);
			}

			return facts;
		}

		public bool TryFindActionTarget(Entity agent, GoapAction action, HashSet<string> state,
			out Entity? targetEntity, out Point targetCell, out Point destinationCell, out string actionContext)
		{
			var simAction = (SimulationGoapAction)action;
			var actionId = simAction.Id;
			var targetKind = simAction.TargetKind;
			var destinationMode = simAction.DestinationMode;
			targetEntity = null;
			targetCell = default;
			destinationCell = default;
			actionContext = string.Empty;

			var world = worldAccessor();
			var agentCell = world.CoordsAtXY(agent.GetPosition());

			// Knowledge-bridge actions (search, retrieve, communicate, read-cookbook)
			if (string.Equals(actionId, "search-for-item", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(actionId, "retrieve-known-item", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(actionId, "communicate", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(actionId, "read-cookbook", StringComparison.OrdinalIgnoreCase))
			{
				return TryFindKnowledgeBridgeTarget(agent, simAction, state, world, agentCell, out targetEntity, out targetCell, out destinationCell, out actionContext);
			}

			// Tag-acquisition primitives: locate the nearest world item carrying the desired tag.
			if (string.Equals(actionId, "find-drink", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(actionId, "find-food", StringComparison.OrdinalIgnoreCase))
			{
				var tag = string.Equals(actionId, "find-drink", StringComparison.OrdinalIgnoreCase) ? "drink" : "food";
				if (world.TryFindNearestItemLocationByTag(new[] { tag }, agentCell, out var itemCell, out var matchedItemId)
					&& world.TryFindActionDestinationCell(agentCell, itemCell, destinationMode, out var dest))
				{
					targetCell = itemCell;
					destinationCell = dest;
					actionContext = matchedItemId;
					return true;
				}
				return false;
			}

			// Self-targeted actions
			if (string.Equals(targetKind, "self", StringComparison.OrdinalIgnoreCase))
			{
				targetCell = agentCell;
				destinationCell = agentCell;
				return true;
			}

			// Resource node targeted actions
			if (string.Equals(targetKind, "resourceNode", StringComparison.OrdinalIgnoreCase))
			{
				foreach (var (cell, _, coords) in world.EnumerateCells())
				{
					if (!cell.TryGetResourceNode(out var resourceNode))
					{
						continue;
					}

					if (!definitions.TryGetResourceNodeDefinition(resourceNode.GetName(), out var rDef))
					{
						continue;
					}

					if (!rDef.SupportedActionIds.Any(id => string.Equals(id, actionId, StringComparison.OrdinalIgnoreCase)))
					{
						continue;
					}

					if (!world.TryFindActionDestinationCell(agentCell, coords, destinationMode, out var dest))
					{
						continue;
					}

					targetEntity = resourceNode;
					targetCell = coords;
					destinationCell = dest;
					return true;
				}
				return false;
			}

			// World object targeted actions
			if (string.Equals(targetKind, "worldObject", StringComparison.OrdinalIgnoreCase))
			{
				foreach (var (cell, _, coords) in world.EnumerateCells())
				{
					if (!cell.TryGetWorldObject(out var worldObject))
					{
						continue;
					}

					if (string.Equals(actionId, "store-items", StringComparison.OrdinalIgnoreCase))
					{
						var storable = agent.GetInventory().Any(item => !item.Get<ItemDefinitionComponent>().IsTool && worldObject.CanStore(item));
						if (!storable)
						{
							continue;
						}
					}
					else if (string.Equals(actionId, "haul-material", StringComparison.OrdinalIgnoreCase))
					{
						MaterialCostComponent[] missingItems;
						if (worldObject.IsConstructionSite())
						{
							missingItems = worldObject.GetMissingBuildCosts();
						}
						else if (worldObject.HasActiveProductionOrder())
						{
							missingItems = worldObject.GetMissingOrderInputs();
						}
						else
						{
							continue;
						}

						if (missingItems.Length == 0)
						{
							continue;
						}
					}
					else if (string.Equals(actionId, "complete-construction", StringComparison.OrdinalIgnoreCase))
					{
						if (!worldObject.IsConstructionSite())
						{
							continue;
						}

						if (worldObject.GetMissingBuildCosts().Length > 0)
						{
							continue;
						}
					}
					else if (string.Equals(actionId, "process-recipe", StringComparison.OrdinalIgnoreCase))
					{
						var hasReady = worldObject.GetRecipes().Any(r => r.RequiredFacts.All(state.Contains) && worldObject.HasStoredMaterials(r.Inputs));
						if (!hasReady)
						{
							continue;
						}
					}
					else if (string.Equals(actionId, "sleep", StringComparison.OrdinalIgnoreCase))
					{
						if (!agent.IsRestLow() || !worldObject.Get<TagCollectionComponent>().Contains("bed"))
						{
							continue;
						}
					}

					if (!world.TryFindActionDestinationCell(agentCell, coords, destinationMode, out var dest))
					{
						continue;
					}

					targetEntity = worldObject;
					targetCell = coords;
					destinationCell = dest;
					return true;
				}
				return false;
			}

			// Enemy targeted actions
			if (string.Equals(targetKind, "enemy", StringComparison.OrdinalIgnoreCase))
			{
				foreach (var other in world.GetAgents())
				{
					if (other.Equals(agent) || string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					var otherCell = world.CoordsAtXY(other.GetPosition());
					var throwItem = GetRequiredItemsByTag(simAction, agent.GetInventory()).FirstOrDefault();
					if (!throwItem.Equals(default(Entity)))
					{
						var range = throwItem.Get<ItemDefinitionComponent>().ThrowRange;
						if (Vector2.DistanceSquared(otherCell.ToVector2(), agentCell.ToVector2()) > range * range)
						{
							continue;
						}
					}
					targetEntity = other;
					targetCell = otherCell;
					destinationCell = agentCell;
					return true;
				}
				return false;
			}

			return false;
		}

		bool TryFindKnowledgeBridgeTarget(Entity agent, SimulationGoapAction action, HashSet<string> state, ISimulationWorld world, Point agentCell,
			out Entity? targetEntity, out Point targetCell, out Point destinationCell, out string actionContext)
		{
			targetEntity = null;
			targetCell = default;
			destinationCell = default;
			actionContext = string.Empty;

			var actionId = action.Id;
			var destinationMode = action.DestinationMode;
			var isSearch = string.Equals(actionId, "search-for-item", StringComparison.OrdinalIgnoreCase);
			var isRetrieve = string.Equals(actionId, "retrieve-known-item", StringComparison.OrdinalIgnoreCase);
			var isCommunicate = string.Equals(actionId, "communicate", StringComparison.OrdinalIgnoreCase);
			var isRead = string.Equals(actionId, "read-cookbook", StringComparison.OrdinalIgnoreCase);

			// Find missing items to search/retrieve
			if (isSearch || isRetrieve)
			{
				var missingItemIds = GetRequiredItemIds(action)
					.Where(id => !string.IsNullOrWhiteSpace(id) && !state.Contains(Facts.HasItem(id)))
					.ToArray();

				// If no explicit items, scan all item-related missing facts in the world
				if (missingItemIds.Length == 0)
				{
					// Gather item IDs known in the world that agent doesn't have
					missingItemIds = world.EnumerateCells()
						.SelectMany(t => t.Item1.ItemsInCell)
						.Select(item => item.GetItemDefinitionId())
						.Distinct(StringComparer.OrdinalIgnoreCase)
						.Where(id => !state.Contains(Facts.HasItem(id)))
						.Take(1)
						.ToArray();
				}

				foreach (var itemId in missingItemIds)
				{
					if (state.Contains(Facts.KnowsItemLocation(itemId)) && isRetrieve)
					{
						var hasCell = agent.TryRecallItemLocation(itemId, out var knownCell) && world.CellContainsItem(knownCell, itemId);
						if (!hasCell)
						{
							world.TryFindNearestItemLocation(itemId, agentCell, out knownCell);
						}

						if (world.TryFindActionDestinationCell(agentCell, knownCell, destinationMode, out var dest))
						{
							targetCell = knownCell;
							destinationCell = dest;
							actionContext = itemId;
							return true;
						}
					}
					else if (isSearch && world.TryFindNearestItemLocation(itemId, agentCell, out var searchCell))
					{
						if (world.TryFindActionDestinationCell(agentCell, searchCell, destinationMode, out var dest))
						{
							targetCell = searchCell;
							destinationCell = dest;
							actionContext = itemId;
							return true;
						}
					}
				}
				return false;
			}

			// Communicate with ally who knows something
			if (isCommunicate)
			{
				foreach (var ally in world.GetAgents().Where(other => !other.Equals(agent)
					&& string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase)))
				{
					// Find an item or fact this ally knows that agent needs
					foreach (var entry in ally.GetKnownItemLocations())
					{
						if (world.CellContainsItem(entry.Value, entry.Key) && !state.Contains(Facts.KnowsItemLocation(entry.Key)))
						{
							var allyCell = world.CoordsAtXY(ally.GetPosition());
							if (world.TryFindActionDestinationCell(agentCell, allyCell, destinationMode, out var dest))
							{
								targetEntity = ally;
								targetCell = allyCell;
								destinationCell = dest;
								actionContext = Facts.KnowsItemLocation(entry.Key);
								return true;
							}
						}
					}
				}
				return false;
			}

			// Read a cookbook from inventory or world
			if (isRead)
			{
				// Try readable items in inventory first
				foreach (var item in agent.GetInventory())
				{
					var learnedFacts = item.Get<ItemDefinitionComponent>().LearnedFacts;
					var missingFact = learnedFacts.FirstOrDefault(f => IsLearnableKnowledgeFact(f) && !state.Contains(f));
					if (missingFact != null)
					{
						targetEntity = item;
						targetCell = agentCell;
						destinationCell = agentCell;
						actionContext = missingFact;
						return true;
					}
				}

				// Try readable items in the world
				foreach (var (cell, _, coords) in world.EnumerateCells())
				{
					foreach (var item in cell.ItemsInCell)
					{
						var learnedFacts = item.Get<ItemDefinitionComponent>().LearnedFacts;
						var missingFact = learnedFacts.FirstOrDefault(f => IsLearnableKnowledgeFact(f) && !state.Contains(f));
						if (missingFact == null)
						{
							continue;
						}

						if (!world.TryFindActionDestinationCell(agentCell, coords, destinationMode, out var dest))
						{
							continue;
						}

						targetEntity = item;
						targetCell = coords;
						destinationCell = dest;
						actionContext = missingFact;
						return true;
					}
				}
				return false;
			}

			return false;
		}

		static IEnumerable<string> GetRequiredItemIds(SimulationGoapAction action)
		{
			foreach (var fact in action.RequiredFacts)
			{
				if (Facts.TryGetHasItemId(fact, out var itemId))
				{
					yield return itemId;
				}
			}
		}

		static IEnumerable<Entity> GetRequiredItemsByTag(SimulationGoapAction action, IList<Entity> inventory)
		{
			foreach (var fact in action.RequiredFacts)
			{
				if (Facts.TryGetHasItemFilter(fact, out var tags))
				{
					var match = inventory.FirstOrDefault(item => item.ItemMatchesFilter(tags));
					if (!match.Equals(default(Entity)))
					{
						yield return match;
					}
				}
				else if (Facts.TryGetHasItemId(fact, out var itemId))
				{
					var match = inventory.FirstOrDefault(item => string.Equals(item.GetItemDefinitionId(), itemId, StringComparison.OrdinalIgnoreCase));
					if (!match.Equals(default(Entity)))
					{
						yield return match;
					}
				}
			}
		}

		static bool IsLearnableKnowledgeFact(string fact)
			=> fact.StartsWith("recipe.", StringComparison.OrdinalIgnoreCase) || fact.StartsWith("knowledge.", StringComparison.OrdinalIgnoreCase);
	}
}
