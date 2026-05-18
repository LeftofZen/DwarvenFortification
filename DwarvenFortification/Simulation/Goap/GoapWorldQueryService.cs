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

		public GoapCandidateQuery InspectCandidates(Entity agent, IReadOnlyList<GoapAction> actions, HashSet<string> currentState)
		{
			var accepted = new List<GoapActionCandidate>();
			var diagnostics = new List<GoapActionDiagnostic>();
			var world = worldAccessor();
			var agentCell = world.CoordsAtXY(agent.GetPosition());

			void AddAccepted(GoapActionCandidate candidate, string targetSummary)
			{
				accepted.Add(candidate);
				diagnostics.Add(new GoapActionDiagnostic(
					candidate.Definition,
					GoapActionDiagnosticStatus.Available,
					"Action manifestation available.",
					candidate.TargetCell,
					candidate.DestinationCell,
					targetSummary,
					candidate.Cost));
			}

			void AddRejected(GoapAction definition, string reason, Point? targetCell = null, Point? destinationCell = null, string targetSummary = "none")
				=> diagnostics.Add(new GoapActionDiagnostic(definition, GoapActionDiagnosticStatus.Rejected, reason, targetCell, destinationCell, targetSummary, null));

			foreach (var bridgeCandidate in BuildKnowledgeBridgeCandidates(agent, actions, currentState, AddRejected))
			{
				AddAccepted(bridgeCandidate, FormatTargetSummary(bridgeCandidate.TargetEntity, bridgeCandidate.TargetCell));
			}

			foreach (var action in actions.Where(action => action.Id is not "search-for-item" and not "retrieve-known-item" and not "communicate" and not "read-cookbook"))
			{
				if (string.Equals(action.TargetKind, "resourceNode", StringComparison.OrdinalIgnoreCase))
				{
					foreach (var (cell, _, coords) in world.EnumerateCells())
					{
						if (!cell.TryGetResourceNode(out var resourceNode))
						{
							continue;
						}

						var targetSummary = resourceNode.GetName();
						if (!definitions.TryGetResourceNodeDefinition(resourceNode.GetName(), out var resourceDefinition))
						{
							AddRejected(action, $"Missing resource node definition for {targetSummary}.", coords, null, targetSummary);
							continue;
						}

						if (!resourceDefinition.SupportedActionIds.Any(id => string.Equals(id, action.Id, StringComparison.OrdinalIgnoreCase)))
						{
							AddRejected(action, $"Target does not support action '{action.Id}'.", coords, null, targetSummary);
							continue;
						}

						if (!world.TryFindActionDestinationCell(agentCell, coords, action.DestinationMode, out var destinationCell))
						{
							AddRejected(action, "No valid destination cell found for this target.", coords, null, targetSummary);
							continue;
						}

						var candidate = new GoapActionCandidate(action, coords, destinationCell, resourceNode, action.BaseCost + action.DurationTicks, BuildRequirements(action), action.Effects);
						AddAccepted(candidate, targetSummary);
					}
				}
				else if (string.Equals(action.TargetKind, "worldObject", StringComparison.OrdinalIgnoreCase))
				{
					foreach (var (cell, _, coords) in world.EnumerateCells())
					{
						if (!cell.TryGetWorldObject(out var worldObject))
						{
							continue;
						}

						var targetSummary = worldObject.GetName();
						if (string.Equals(action.Id, "store-items", StringComparison.OrdinalIgnoreCase))
						{
							var storable = agent.GetInventory().Any(item => !item.Get<ItemDefinitionComponent>().IsTool && worldObject.CanStore(item));
							if (!storable)
							{
								AddRejected(action, "Agent has no non-tool inventory items that this target can store.", coords, null, targetSummary);
								continue;
							}
						}

						if (string.Equals(action.Id, "haul-material", StringComparison.OrdinalIgnoreCase))
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
								AddRejected(action, "Target is not a construction site or an active production workstation.", coords, null, targetSummary);
								continue;
							}

							if (missingItems.Length == 0)
							{
								AddRejected(action, "Target already has all required materials.", coords, null, targetSummary);
								continue;
							}

							if (!world.TryFindActionDestinationCell(agentCell, coords, action.DestinationMode, out var haulDestination))
							{
								AddRejected(action, "No valid destination cell found for haul target.", coords, null, targetSummary);
								continue;
							}

							foreach (var missing in missingItems)
						{
							var haulItemFact = missing.UsesFilter
								? Facts.HasItemFilter(missing.ItemFilter)
								: Facts.HasItem(missing.ItemId);
							var haulRequirements = BuildRequirements(action, haulItemFact);
							var haulCandidate = new GoapActionCandidate(action, coords, haulDestination, worldObject, action.BaseCost + action.DurationTicks, haulRequirements, action.Effects);
							AddAccepted(haulCandidate, $"{targetSummary}: haul {(missing.UsesFilter ? string.Join("+", missing.ItemFilter) : missing.ItemId)}");
						}
							continue;
						}

						if (string.Equals(action.Id, "complete-construction", StringComparison.OrdinalIgnoreCase))
						{
							if (!worldObject.IsConstructionSite())
							{
								AddRejected(action, "Target is not a construction site.", coords, null, targetSummary);
								continue;
							}

							var missingCosts = worldObject.GetMissingBuildCosts();
							if (missingCosts.Length > 0)
							{
								AddRejected(action, $"Construction site is missing: {string.Join(", ", missingCosts.Select(cost => $"{cost.Quantity}x {cost.ItemId}"))}.", coords, null, targetSummary);
								continue;
							}
						}

						if (string.Equals(action.Id, "process-recipe", StringComparison.OrdinalIgnoreCase))
						{
							var matchingRecipes = worldObject.GetRecipes()
								.Where(recipe => recipe.RequiredFacts.All(currentState.Contains) && worldObject.HasStoredMaterials(recipe.Inputs))
								.ToArray();
							if (matchingRecipes.Length == 0)
							{
								AddRejected(action, "Target has no recipe with all required stored materials and facts satisfied.", coords, null, targetSummary);
								continue;
							}

							if (!world.TryFindActionDestinationCell(agentCell, coords, action.DestinationMode, out var recipeDestination))
							{
								AddRejected(action, "No valid destination cell found for this target.", coords, null, targetSummary);
								continue;
							}

							var activeOrder = worldObject.HasActiveProductionOrder() ? worldObject.GetProductionOrder() : default;
							foreach (var recipe in matchingRecipes)
							{
								var addStatesForRecipe = action.Effects.Concat([Facts.HasItem(recipe.OutputItemId)]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
							var recipeCandidate = new GoapActionCandidate(action, coords, recipeDestination, worldObject, action.BaseCost + action.DurationTicks, BuildRequirements(action, recipe.RequiredFacts), addStatesForRecipe);
								AddAccepted(recipeCandidate, $"{targetSummary}: {recipe.Name}");
							}

							continue;
						}

						if (string.Equals(action.Id, "sleep", StringComparison.OrdinalIgnoreCase) && !agent.IsRestLow())
						{
							AddRejected(action, "Agent is not tired enough to sleep.", coords, null, targetSummary);
							continue;
						}

						if (!world.TryFindActionDestinationCell(agentCell, coords, action.DestinationMode, out var destinationCell))
						{
							AddRejected(action, "No valid destination cell found for this target.", coords, null, targetSummary);
							continue;
						}

					var addStatesForCandidate = action.Effects;
					if (string.Equals(action.Id, "complete-construction", StringComparison.OrdinalIgnoreCase))
					{
						var completedStructureId = worldObject.Get<ConstructionSiteComponent>().TargetDefinitionId;
						addStatesForCandidate = [.. action.Effects.Concat([Facts.HasStructure(completedStructureId)]).Distinct(StringComparer.OrdinalIgnoreCase)];
					}

					var candidate = new GoapActionCandidate(action, coords, destinationCell, worldObject, action.BaseCost + action.DurationTicks, BuildRequirements(action), addStatesForCandidate);
						AddAccepted(candidate, targetSummary);
					}
				}
				else if (string.Equals(action.TargetKind, "self", StringComparison.OrdinalIgnoreCase))
				{
					var candidate = new GoapActionCandidate(action, agentCell, agentCell, agent, action.BaseCost + action.DurationTicks, BuildRequirements(action), action.Effects);
					AddAccepted(candidate, agent.GetName());
				}
				else if (string.Equals(action.TargetKind, "enemy", StringComparison.OrdinalIgnoreCase))
				{
					foreach (var other in world.GetAgents())
					{
						if (other.Equals(agent) || string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						var otherCell = world.CoordsAtXY(other.GetPosition());
						var targetSummary = other.GetName();
						var throwItem = GetRequiredItemsByTag(action, agent.GetInventory()).FirstOrDefault();
						if (!throwItem.Equals(default(Entity)))
						{
							var range = throwItem.Get<ItemDefinitionComponent>().ThrowRange;
							if (Vector2.DistanceSquared(otherCell.ToVector2(), agentCell.ToVector2()) > range * range)
							{
								AddRejected(action, $"Target is out of range for required item '{throwItem.GetItemDefinitionId()}'.", otherCell, agentCell, targetSummary);
								continue;
							}
						}

						var candidate = new GoapActionCandidate(action, otherCell, agentCell, other, action.BaseCost + action.DurationTicks, BuildRequirements(action), action.Effects);
						AddAccepted(candidate, targetSummary);
					}
				}
				else
				{
					AddRejected(action, $"Unsupported target kind '{action.TargetKind}'.");
				}
			}

			return new GoapCandidateQuery(accepted, diagnostics);
		}

		public IEnumerable<GoapActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<GoapAction> actions, HashSet<string> currentState)
			=> InspectCandidates(agent, actions, currentState).Candidates;

		public int GetEffectivePriority(Entity agent, GoapGoal goal)
		{
			const float bonusRange = 100f;
			var bonus = 0f;

			foreach (var fact in goal.Requirements)
			{
				if (string.Equals(fact, "thirst.low", StringComparison.OrdinalIgnoreCase))
				{
					bonus = MathF.Max(bonus, (1f - Math.Clamp(agent.GetHydrationRatio(), 0f, 1f)) * bonusRange);
				}
				else if (string.Equals(fact, "hunger.low", StringComparison.OrdinalIgnoreCase))
				{
					bonus = MathF.Max(bonus, (1f - Math.Clamp(agent.GetMetabolicEnergyRatio(), 0f, 1f)) * bonusRange);
				}
				else if (string.Equals(fact, "rest.low", StringComparison.OrdinalIgnoreCase))
				{
					bonus = MathF.Max(bonus, (1f - Math.Clamp(agent.GetRestRatio(), 0f, 1f)) * bonusRange);
				}
			}

			return goal.Priority + (int)bonus;
		}

		IEnumerable<GoapActionCandidate> BuildKnowledgeBridgeCandidates(Entity agent, IReadOnlyList<GoapAction> actions, HashSet<string> currentState, Action<GoapAction, string, Point?, Point?, string> addRejected)
		{
			var world = worldAccessor();
			var agentCell = world.CoordsAtXY(agent.GetPosition());
			var searchAction = actions.FirstOrDefault(action => string.Equals(action.Id, "search-for-item", StringComparison.OrdinalIgnoreCase));
			var retrieveAction = actions.FirstOrDefault(action => string.Equals(action.Id, "retrieve-known-item", StringComparison.OrdinalIgnoreCase));
			var communicateAction = actions.FirstOrDefault(action => string.Equals(action.Id, "communicate", StringComparison.OrdinalIgnoreCase));
			var readAction = actions.FirstOrDefault(action => string.Equals(action.Id, "read-cookbook", StringComparison.OrdinalIgnoreCase));

			var missingItemIds = actions
				.SelectMany(GetRequiredItemIds)
				.Where(itemId => !string.IsNullOrWhiteSpace(itemId) && !currentState.Contains(Facts.HasItem(itemId)))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			var missingItemTags = actions
				.SelectMany(GetRequiredItemTags)
				.Where(tag => !string.IsNullOrWhiteSpace(tag) && !currentState.Contains(Facts.HasItemTag(tag)))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			foreach (var itemId in missingItemIds)
			{
				if (currentState.Contains(Facts.KnowsItemLocation(itemId)) && !string.IsNullOrWhiteSpace(retrieveAction.Id))
				{
					// Prefer the agent's confirmed recalled location; fall back to the world-found
					// cell for hypothetical planning states where KnowsItemLocation is a projected
					// fact added by a prior simulated search step before agent memory is updated.
					var hasItemCell = false;
					var itemCell = default(Point);
					if (agent.TryRecallItemLocation(itemId, out var knownCell) && world.CellContainsItem(knownCell, itemId))
					{
						hasItemCell = true;
						itemCell = knownCell;
					}
					else if (world.TryFindNearestItemLocation(itemId, agentCell, out var foundCell))
					{
						hasItemCell = true;
						itemCell = foundCell;
					}

					if (hasItemCell)
					{
						if (world.TryFindActionDestinationCell(agentCell, itemCell, retrieveAction.DestinationMode, out var retrieveDestination))
						{
							yield return new GoapActionCandidate(retrieveAction, itemCell, retrieveDestination, null, retrieveAction.BaseCost + retrieveAction.DurationTicks, BuildRequirements(retrieveAction, Facts.KnowsItemLocation(itemId)), [Facts.HasItem(itemId)]);
						}
						else
						{
							addRejected(retrieveAction, "Item location known but no route to a valid action destination was found.", itemCell, null, itemId);
						}
					}

					continue;
				}

				if (!string.IsNullOrWhiteSpace(communicateAction.Id))
				{
					foreach (var ally in world.GetAgents().Where(other => !other.Equals(agent) && string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase) && other.TryRecallItemLocation(itemId, out _)))
					{
						var allyCell = world.CoordsAtXY(ally.GetPosition());
						if (!world.TryFindActionDestinationCell(agentCell, allyCell, communicateAction.DestinationMode, out var communicateDestination))
						{
							addRejected(communicateAction, "Ally knows the item location, but no communication position was reachable.", allyCell, null, ally.GetName());
							continue;
						}

					yield return new GoapActionCandidate(communicateAction, allyCell, communicateDestination, ally, communicateAction.BaseCost + communicateAction.DurationTicks, BuildRequirements(communicateAction), [Facts.KnowsItemLocation(itemId)]);
					}
				}

				if (!string.IsNullOrWhiteSpace(searchAction.Id) && world.TryFindNearestItemLocation(itemId, agentCell, out var searchCell))
				{
					if (world.TryFindActionDestinationCell(agentCell, searchCell, searchAction.DestinationMode, out var searchDestination))
					{
						yield return new GoapActionCandidate(searchAction, searchCell, searchDestination, null, searchAction.BaseCost + searchAction.DurationTicks, BuildRequirements(searchAction), [Facts.KnowsItemLocation(itemId)]);
					}
					else
					{
						addRejected(searchAction, "Item exists in the world, but no valid search destination was reachable.", searchCell, null, itemId);
					}
				}
				else if (!string.IsNullOrWhiteSpace(searchAction.Id))
				{
					addRejected(searchAction, "No known or discoverable world location for the required item.", null, null, itemId);
				}
			}

			foreach (var tag in missingItemTags)
			{
				if (!string.IsNullOrWhiteSpace(searchAction.Id) && world.TryFindNearestItemLocationByTag([tag], agentCell, out var tagSearchCell, out var tagItemId))
				{
					if (currentState.Contains(Facts.KnowsItemLocation(tagItemId)) && !string.IsNullOrWhiteSpace(retrieveAction.Id))
					{
						// Prefer the agent's confirmed recalled location; fall back to tagSearchCell
						// for hypothetical planning states where KnowsItemLocation is projected.
						var tagItemCell = (agent.TryRecallItemLocation(tagItemId, out var knownTagCell) && world.CellContainsItem(knownTagCell, tagItemId))
							? knownTagCell
							: tagSearchCell;

						if (world.TryFindActionDestinationCell(agentCell, tagItemCell, retrieveAction.DestinationMode, out var tagRetrieveDestination))
						{
							yield return new GoapActionCandidate(retrieveAction, tagItemCell, tagRetrieveDestination, null, retrieveAction.BaseCost + retrieveAction.DurationTicks, BuildRequirements(retrieveAction, Facts.KnowsItemLocation(tagItemId)), [Facts.HasItem(tagItemId), Facts.HasItemTag(tag)]);
							continue;
						}
						else
						{
							addRejected(retrieveAction, $"Item with tag '{tag}' location known but no route to a valid action destination was found.", tagItemCell, null, tag);
						}
					}

					if (world.TryFindActionDestinationCell(agentCell, tagSearchCell, searchAction.DestinationMode, out var tagSearchDestination))
					{
						yield return new GoapActionCandidate(searchAction, tagSearchCell, tagSearchDestination, null, searchAction.BaseCost + searchAction.DurationTicks, BuildRequirements(searchAction), [Facts.KnowsItemLocation(tagItemId)]);
					}
					else
					{
						addRejected(searchAction, $"Item with tag '{tag}' exists but no valid search destination was reachable.", tagSearchCell, null, tag);
					}
				}
				else if (!string.IsNullOrWhiteSpace(searchAction.Id))
				{
					addRejected(searchAction, $"No discoverable item with tag '{tag}' found in the world.", null, null, tag);
				}
			}

			var missingLearnableStates = actions
				.SelectMany(action => action.Requirements)
				.Where(s => IsLearnableKnowledgeFact(s) && !currentState.Contains(s))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			foreach (var s in missingLearnableStates)
			{
				if (!string.IsNullOrWhiteSpace(communicateAction.Id))
				{
					foreach (var ally in world.GetAgents().Where(other => !other.Equals(agent) && string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase) && other.KnowsFact(s)))
					{
						var allyCell = world.CoordsAtXY(ally.GetPosition());
						if (!world.TryFindActionDestinationCell(agentCell, allyCell, communicateAction.DestinationMode, out var communicateDestination))
						{
							addRejected(communicateAction, "Ally knows the fact, but no communication position was reachable.", allyCell, null, ally.GetName());
							continue;
						}

						yield return new GoapActionCandidate(communicateAction, allyCell, communicateDestination, ally, communicateAction.BaseCost + communicateAction.DurationTicks, BuildRequirements(communicateAction), [s]);
					}
				}

				if (string.IsNullOrWhiteSpace(readAction.Id))
				{
					continue;
				}

				foreach (var readableItem in agent.GetInventory().Where(item => item.Get<ItemDefinitionComponent>().LearnedFacts.Contains(s, StringComparer.OrdinalIgnoreCase)))
				{
					yield return new GoapActionCandidate(readAction, agentCell, agentCell, readableItem, readAction.BaseCost + readAction.DurationTicks, BuildRequirements(readAction, Facts.HasItem(readableItem.GetItemDefinitionId())), [s]);
				}

				var readableDefinitionIds = definitions.GetItemDefinitionIdsGrantingFact(s);
				foreach (var readableDefinitionId in readableDefinitionIds.Where(itemId => !currentState.Contains(Facts.HasItem(itemId))))
				{
					if (currentState.Contains(Facts.KnowsItemLocation(readableDefinitionId)) && agent.TryRecallItemLocation(readableDefinitionId, out var knownReadableCell) && world.CellContainsItem(knownReadableCell, readableDefinitionId) && world.TryGetItemEntity(knownReadableCell, readableDefinitionId, out var knownReadableItem))
					{
						yield return new GoapActionCandidate(readAction, knownReadableCell, knownReadableCell, knownReadableItem, readAction.BaseCost + readAction.DurationTicks, BuildRequirements(readAction, Facts.KnowsItemLocation(readableDefinitionId)), [s]);
					}

					if (currentState.Contains(Facts.KnowsItemLocation(readableDefinitionId)) && agent.TryRecallItemLocation(readableDefinitionId, out var knownBookCell) && world.CellContainsItem(knownBookCell, readableDefinitionId) && !string.IsNullOrWhiteSpace(retrieveAction.Id))
					{
						if (world.TryFindActionDestinationCell(agentCell, knownBookCell, retrieveAction.DestinationMode, out var retrieveDestination))
						{
							yield return new GoapActionCandidate(retrieveAction, knownBookCell, retrieveDestination, null, retrieveAction.BaseCost + retrieveAction.DurationTicks, BuildRequirements(retrieveAction, Facts.KnowsItemLocation(readableDefinitionId)), [Facts.HasItem(readableDefinitionId)]);
						}
						else
						{
							addRejected(retrieveAction, "Readable item location is known, but no retrieval destination was reachable.", knownBookCell, null, readableDefinitionId);
						}

						continue;
					}

					if (!string.IsNullOrWhiteSpace(communicateAction.Id))
					{
						foreach (var ally in world.GetAgents().Where(other => !other.Equals(agent) && string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase) && other.TryRecallItemLocation(readableDefinitionId, out _)))
						{
							var allyCell = world.CoordsAtXY(ally.GetPosition());
							if (!world.TryFindActionDestinationCell(agentCell, allyCell, communicateAction.DestinationMode, out var communicateDestination))
							{
								addRejected(communicateAction, "Ally remembers the readable item, but no communication position was reachable.", allyCell, null, ally.GetName());
								continue;
							}

							yield return new GoapActionCandidate(communicateAction, allyCell, communicateDestination, ally, communicateAction.BaseCost + communicateAction.DurationTicks, BuildRequirements(communicateAction), [Facts.KnowsItemLocation(readableDefinitionId)]);
						}
					}

					if (!string.IsNullOrWhiteSpace(searchAction.Id) && world.TryFindNearestItemLocation(readableDefinitionId, agentCell, out var searchCell))
					{
						if (world.TryGetItemEntity(searchCell, readableDefinitionId, out var searchedReadableItem))
						{
							yield return new GoapActionCandidate(readAction, searchCell, searchCell, searchedReadableItem, readAction.BaseCost + readAction.DurationTicks, BuildRequirements(readAction, Facts.KnowsItemLocation(readableDefinitionId)), [s]);
						}

						if (world.TryFindActionDestinationCell(agentCell, searchCell, searchAction.DestinationMode, out var searchDestination))
						{
							yield return new GoapActionCandidate(searchAction, searchCell, searchDestination, null, searchAction.BaseCost + searchAction.DurationTicks, BuildRequirements(searchAction), [Facts.KnowsItemLocation(readableDefinitionId)]);
						}
						else
						{
							addRejected(searchAction, "Readable item exists in the world, but no valid search destination was reachable.", searchCell, null, readableDefinitionId);
						}
					}
					else if (!string.IsNullOrWhiteSpace(searchAction.Id))
					{
						addRejected(searchAction, "No discoverable readable item instance exists for the missing fact.", null, null, readableDefinitionId);
					}
				}
			}
		}

		static string FormatTargetSummary(Entity? entity, Point targetCell)
			=> entity.HasValue && !entity.Value.Equals(default(Entity))
				? entity.Value.GetName()
				: targetCell.ToString();

		string[] BuildRequirements(GoapAction action, params string[] extraStates)
			=> [.. action.Requirements, .. extraStates];

		static IEnumerable<string> GetRequiredItemIds(GoapAction action)
		{
			foreach (var fact in action.Requirements)
			{
				if (Facts.TryGetHasItemId(fact, out var itemId))
				{
					yield return itemId;
				}
			}
		}

		static IEnumerable<string> GetRequiredItemTags(GoapAction action)
		{
			foreach (var fact in action.Requirements)
			{
				if (Facts.TryGetHasItemTag(fact, out var tag))
				{
					yield return tag;
				}
			}
		}

		static IEnumerable<Entity> GetRequiredItemsByTag(GoapAction action, System.Collections.Generic.IList<Entity> inventory)
		{
			foreach (var fact in action.Requirements)
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
