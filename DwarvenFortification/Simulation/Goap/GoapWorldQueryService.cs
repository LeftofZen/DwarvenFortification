using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP.Actions;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public sealed class GoapWorldQueryService : IWorldQueryService
	{
		readonly SimulationDefinitionRegistry definitions;
		readonly Func<ISimulationWorld> worldAccessor;

		public GoapWorldQueryService(SimulationDefinitionRegistry definitions, Func<ISimulationWorld> worldAccessor)
		{
			this.definitions = definitions;
			this.worldAccessor = worldAccessor;
		}

		public HashSet<string> BuildCurrentFacts(Entity agent)
		{
			var world = worldAccessor();
			var facts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var inventory = agent.GetInventory();

			foreach (var item in inventory)
			{
				facts.Add(Facts.HasItem(item.GetItemDefinitionId()));
				if (!item.Get<ItemDefinitionComponent>().IsTool)
				{
					facts.Add(Facts.InventoryHasResourceItems);
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
					facts.Add(Facts.HasSystem(system));
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
			var hasNearbyEnemy = world.GetAgents()
				.Where(other => !other.Equals(agent) && !string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase))
				.Any(other => Vector2.DistanceSquared(world.CoordsAtXY(other.GetPosition()).ToVector2(), agentCell.ToVector2()) <= 64f);
			if (hasNearbyEnemy)
			{
				facts.Add(Facts.EnemyNearby);
			}

			return facts;
		}

		public CandidateQuerySnapshot InspectCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts)
		{
			var accepted = new List<ActionCandidate>();
			var diagnostics = new List<ActionDiagnostic>();
			var world = worldAccessor();
			var agentCell = world.CoordsAtXY(agent.GetPosition());

			void AddAccepted(ActionCandidate candidate, string targetSummary)
			{
				accepted.Add(candidate);
				diagnostics.Add(new ActionDiagnostic(
					candidate.Definition,
					ActionDiagnosticStatus.Available,
					"Action manifestation available.",
					candidate.TargetCell,
					candidate.DestinationCell,
					targetSummary,
					candidate.Cost));
			}

			void AddRejected(ActionDefinitionSnapshot definition, string reason, Point? targetCell = null, Point? destinationCell = null, string targetSummary = "none")
				=> diagnostics.Add(new ActionDiagnostic(definition, ActionDiagnosticStatus.Rejected, reason, targetCell, destinationCell, targetSummary, null));

			foreach (var bridgeCandidate in BuildKnowledgeBridgeCandidates(agent, actions, currentFacts, AddRejected))
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

						if (!action.RequiredTargetTags.All(tag => resourceDefinition.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
						{
							AddRejected(action, $"Target is missing required tags: {string.Join(", ", action.RequiredTargetTags.Where(tag => !resourceDefinition.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))}.", coords, null, targetSummary);
							continue;
						}

						if (!world.TryFindActionDestinationCell(agentCell, coords, action.DestinationMode, out var destinationCell))
						{
							AddRejected(action, "No valid destination cell found for this target.", coords, null, targetSummary);
							continue;
						}

						var candidate = new ActionCandidate(action, coords, destinationCell, resourceNode, action.BaseCost + action.DurationTicks, BuildRequiredFacts(action), action.AddFacts, action.RemoveFacts);
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
						if (!action.RequiredTargetTags.All(tag => worldObject.Get<TagCollectionComponent>().Contains(tag)))
						{
							AddRejected(action, $"Target is missing required tags: {string.Join(", ", action.RequiredTargetTags.Where(tag => !worldObject.Get<TagCollectionComponent>().Contains(tag)))}.", coords, null, targetSummary);
							continue;
						}

						if (string.Equals(action.Id, "store-items", StringComparison.OrdinalIgnoreCase))
						{
							var storable = agent.GetInventory().Any(item => !item.Get<ItemDefinitionComponent>().IsTool && worldObject.CanStore(item));
							if (!storable)
							{
								AddRejected(action, "Agent has no non-tool inventory items that this target can store.", coords, null, targetSummary);
								continue;
							}
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

						var candidate = new ActionCandidate(action, coords, destinationCell, worldObject, action.BaseCost + action.DurationTicks, BuildRequiredFacts(action), action.AddFacts, action.RemoveFacts);
						AddAccepted(candidate, targetSummary);
					}
				}
				else if (string.Equals(action.TargetKind, "self", StringComparison.OrdinalIgnoreCase))
				{
					var candidate = new ActionCandidate(action, agentCell, agentCell, agent, action.BaseCost + action.DurationTicks, BuildRequiredFacts(action), action.AddFacts, action.RemoveFacts);
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
						var throwItem = GetRequiredItemIds(action).Select(itemId => agent.GetInventory().FirstOrDefault(item => string.Equals(item.GetItemDefinitionId(), itemId, StringComparison.OrdinalIgnoreCase))).FirstOrDefault();
						if (!throwItem.Equals(default(Entity)))
						{
							var range = throwItem.Get<ItemDefinitionComponent>().ThrowRange;
							if (Vector2.DistanceSquared(otherCell.ToVector2(), agentCell.ToVector2()) > range * range)
							{
								AddRejected(action, $"Target is out of range for required item '{throwItem.GetItemDefinitionId()}'.", otherCell, agentCell, targetSummary);
								continue;
							}
						}

						var candidate = new ActionCandidate(action, otherCell, agentCell, other, action.BaseCost + action.DurationTicks, BuildRequiredFacts(action), action.AddFacts, action.RemoveFacts);
						AddAccepted(candidate, targetSummary);
					}
				}
				else
				{
					AddRejected(action, $"Unsupported target kind '{action.TargetKind}'.");
				}
			}

			return new CandidateQuerySnapshot(accepted, diagnostics);
		}

		public IEnumerable<ActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts)
			=> InspectCandidates(agent, actions, currentFacts).Candidates;

		IEnumerable<ActionCandidate> BuildKnowledgeBridgeCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts, Action<ActionDefinitionSnapshot, string, Point?, Point?, string> addRejected)
		{
			var world = worldAccessor();
			var agentCell = world.CoordsAtXY(agent.GetPosition());
			var searchAction = actions.FirstOrDefault(action => string.Equals(action.Id, "search-for-item", StringComparison.OrdinalIgnoreCase));
			var retrieveAction = actions.FirstOrDefault(action => string.Equals(action.Id, "retrieve-known-item", StringComparison.OrdinalIgnoreCase));
			var communicateAction = actions.FirstOrDefault(action => string.Equals(action.Id, "communicate", StringComparison.OrdinalIgnoreCase));
			var readAction = actions.FirstOrDefault(action => string.Equals(action.Id, "read-cookbook", StringComparison.OrdinalIgnoreCase));

			var missingItemIds = actions
				.SelectMany(GetRequiredItemIds)
				.Where(itemId => !string.IsNullOrWhiteSpace(itemId) && !currentFacts.Contains(Facts.HasItem(itemId)))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			foreach (var itemId in missingItemIds)
			{
				if (currentFacts.Contains(Facts.KnowsItemLocation(itemId)) && agent.TryRecallItemLocation(itemId, out var knownCell) && world.CellContainsItem(knownCell, itemId) && !string.IsNullOrWhiteSpace(retrieveAction.Id))
				{
					if (world.TryFindActionDestinationCell(agentCell, knownCell, retrieveAction.DestinationMode, out var retrieveDestination))
					{
						yield return new ActionCandidate(retrieveAction, knownCell, retrieveDestination, null, retrieveAction.BaseCost + retrieveAction.DurationTicks, BuildRequiredFacts(retrieveAction, Facts.KnowsItemLocation(itemId)), new[] { Facts.HasItem(itemId) }, Array.Empty<string>());
					}
					else
					{
						addRejected(retrieveAction, "Remembered item location exists but no route to a valid action destination was found.", knownCell, null, itemId);
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

						yield return new ActionCandidate(communicateAction, allyCell, communicateDestination, ally, communicateAction.BaseCost + communicateAction.DurationTicks, BuildRequiredFacts(communicateAction), new[] { Facts.KnowsItemLocation(itemId) }, Array.Empty<string>());
					}
				}

				if (!string.IsNullOrWhiteSpace(searchAction.Id) && world.TryFindNearestItemLocation(itemId, agentCell, out var searchCell))
				{
					if (world.TryFindActionDestinationCell(agentCell, searchCell, searchAction.DestinationMode, out var searchDestination))
					{
						yield return new ActionCandidate(searchAction, searchCell, searchDestination, null, searchAction.BaseCost + searchAction.DurationTicks, BuildRequiredFacts(searchAction), new[] { Facts.KnowsItemLocation(itemId) }, Array.Empty<string>());
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

			var missingLearnableFacts = actions
				.SelectMany(action => action.RequiredFacts)
				.Where(fact => IsLearnableKnowledgeFact(fact) && !currentFacts.Contains(fact))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			foreach (var fact in missingLearnableFacts)
			{
				if (!string.IsNullOrWhiteSpace(communicateAction.Id))
				{
					foreach (var ally in world.GetAgents().Where(other => !other.Equals(agent) && string.Equals(other.GetFactionId(), agent.GetFactionId(), StringComparison.OrdinalIgnoreCase) && other.KnowsFact(fact)))
					{
						var allyCell = world.CoordsAtXY(ally.GetPosition());
						if (!world.TryFindActionDestinationCell(agentCell, allyCell, communicateAction.DestinationMode, out var communicateDestination))
						{
							addRejected(communicateAction, "Ally knows the fact, but no communication position was reachable.", allyCell, null, ally.GetName());
							continue;
						}

						yield return new ActionCandidate(communicateAction, allyCell, communicateDestination, ally, communicateAction.BaseCost + communicateAction.DurationTicks, BuildRequiredFacts(communicateAction), new[] { fact }, Array.Empty<string>());
					}
				}

				if (string.IsNullOrWhiteSpace(readAction.Id))
				{
					continue;
				}

				foreach (var readableItem in agent.GetInventory().Where(item => item.Get<ItemDefinitionComponent>().LearnedFacts.Contains(fact, StringComparer.OrdinalIgnoreCase)))
				{
					yield return new ActionCandidate(readAction, agentCell, agentCell, readableItem, readAction.BaseCost + readAction.DurationTicks, BuildRequiredFacts(readAction, Facts.HasItem(readableItem.GetItemDefinitionId())), new[] { fact }, Array.Empty<string>());
				}

				var readableDefinitionIds = definitions.GetItemDefinitionIdsGrantingFact(fact);
				foreach (var readableDefinitionId in readableDefinitionIds.Where(itemId => !currentFacts.Contains(Facts.HasItem(itemId))))
				{
					if (currentFacts.Contains(Facts.KnowsItemLocation(readableDefinitionId)) && agent.TryRecallItemLocation(readableDefinitionId, out var knownReadableCell) && world.CellContainsItem(knownReadableCell, readableDefinitionId) && world.TryGetItemEntity(knownReadableCell, readableDefinitionId, out var knownReadableItem))
					{
						yield return new ActionCandidate(readAction, knownReadableCell, knownReadableCell, knownReadableItem, readAction.BaseCost + readAction.DurationTicks, BuildRequiredFacts(readAction, Facts.KnowsItemLocation(readableDefinitionId)), new[] { fact }, Array.Empty<string>());
					}

					if (currentFacts.Contains(Facts.KnowsItemLocation(readableDefinitionId)) && agent.TryRecallItemLocation(readableDefinitionId, out var knownBookCell) && world.CellContainsItem(knownBookCell, readableDefinitionId) && !string.IsNullOrWhiteSpace(retrieveAction.Id))
					{
						if (world.TryFindActionDestinationCell(agentCell, knownBookCell, retrieveAction.DestinationMode, out var retrieveDestination))
						{
							yield return new ActionCandidate(retrieveAction, knownBookCell, retrieveDestination, null, retrieveAction.BaseCost + retrieveAction.DurationTicks, BuildRequiredFacts(retrieveAction, Facts.KnowsItemLocation(readableDefinitionId)), new[] { Facts.HasItem(readableDefinitionId) }, Array.Empty<string>());
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

							yield return new ActionCandidate(communicateAction, allyCell, communicateDestination, ally, communicateAction.BaseCost + communicateAction.DurationTicks, BuildRequiredFacts(communicateAction), new[] { Facts.KnowsItemLocation(readableDefinitionId) }, Array.Empty<string>());
						}
					}

					if (!string.IsNullOrWhiteSpace(searchAction.Id) && world.TryFindNearestItemLocation(readableDefinitionId, agentCell, out var searchCell))
					{
						if (world.TryGetItemEntity(searchCell, readableDefinitionId, out var searchedReadableItem))
						{
							yield return new ActionCandidate(readAction, searchCell, searchCell, searchedReadableItem, readAction.BaseCost + readAction.DurationTicks, BuildRequiredFacts(readAction, Facts.KnowsItemLocation(readableDefinitionId)), new[] { fact }, Array.Empty<string>());
						}

						if (world.TryFindActionDestinationCell(agentCell, searchCell, searchAction.DestinationMode, out var searchDestination))
						{
							yield return new ActionCandidate(searchAction, searchCell, searchDestination, null, searchAction.BaseCost + searchAction.DurationTicks, BuildRequiredFacts(searchAction), new[] { Facts.KnowsItemLocation(readableDefinitionId) }, Array.Empty<string>());
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

		string[] BuildRequiredFacts(ActionDefinitionSnapshot action, params string[] extraFacts)
			=> action.RequiredFacts.Concat(extraFacts).Concat(action.BlockedByFacts.Select(fact => $"!{fact}")).ToArray();

		static IEnumerable<string> GetRequiredItemIds(ActionDefinitionSnapshot action)
		{
			foreach (var fact in action.RequiredFacts)
			{
				if (Facts.TryGetHasItemId(fact, out var itemId))
				{
					yield return itemId;
				}
			}
		}

		static bool IsLearnableKnowledgeFact(string fact)
			=> fact.StartsWith("recipe.", StringComparison.OrdinalIgnoreCase) || fact.StartsWith("knowledge.", StringComparison.OrdinalIgnoreCase);
	}
}
