using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.Actions;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP.Actions;
using DwarvenFortification.GOAP.Plans;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using DwarvenFortification.UI;
using System;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public sealed class GoapPlanExecutor : IPlanExecutor
	{
		readonly IActionRuntimeContext runtimeContext;

		public GoapPlanExecutor(IActionRuntimeContext runtimeContext)
		{
			this.runtimeContext = runtimeContext;
		}

		public bool Enqueue(Entity agent, Plan plan)
			=> Enqueue(agent, plan, null);

		public bool Enqueue(Entity agent, Plan plan, AgentActionMetadata metadata)
		{
			if (plan == null || plan.Steps.Count == 0)
			{
				return false;
			}

			var world = runtimeContext.World;

			foreach (var step in plan.Steps)
			{
				Enqueue(agent, step, world, metadata);
			}

			return true;
		}

		public bool Enqueue(Entity agent, ActionCandidate step)
			=> Enqueue(agent, step, null);

		public bool Enqueue(Entity agent, ActionCandidate step, AgentActionMetadata metadata)
		{
			Enqueue(agent, step, runtimeContext.World, metadata);
			return true;
		}

		void Enqueue(Entity agent, ActionCandidate step, ISimulationWorld world, AgentActionMetadata metadata)
		{
			{
				world.PlotPath(agent, step.DestinationCell, metadata);

				switch (step.Definition.Id)
				{
					case "mine":
					case "cut-tree":
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new ExtractResourceNodeAction(runtimeContext, agent, step.TargetCell, agent.ComputeSkillYieldMultiplier(step.Definition.Skills)), metadata);
							EnqueueAction(agent, new CollectItemsFromCellAction(runtimeContext, agent, step.TargetCell, 1), metadata);
						break;

					case "store-items":
						if (step.TargetEntity.HasValue)
						{
							var storableItems = agent.GetInventory().Where(item => !item.Get<ItemDefinitionComponent>().IsTool && step.TargetEntity.Value.CanStore(item)).ToList();
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new StoreItemsInWorldObjectAction(runtimeContext, agent, step.TargetEntity.Value, storableItems), metadata);
						}

						break;

				case "haul-material":
					if (step.TargetEntity.HasValue)
					{
						var haulItemId = string.Empty;
						string[] haulItemFilter = null;
						foreach (var fact in step.RequiredFacts)
						{
							if (Facts.TryGetHasItemFilter(fact, out var filterTags)) { haulItemFilter = filterTags; break; }
							if (Facts.TryGetHasItemId(fact, out var id)) { haulItemId = id; break; }
						}

						var itemToHaul = agent.GetInventory().FirstOrDefault(item =>
							!item.Get<ItemDefinitionComponent>().IsTool &&
							(haulItemFilter != null
								? item.ItemMatchesFilter(haulItemFilter)
								: string.Equals(item.GetItemDefinitionId(), haulItemId, StringComparison.OrdinalIgnoreCase)));
						if (!itemToHaul.Equals(default(Entity)))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new StoreItemsInWorldObjectAction(runtimeContext, agent, step.TargetEntity.Value, new System.Collections.Generic.List<Entity> { itemToHaul }), metadata);
						}
					}

					break;

				case "complete-construction":
					if (step.TargetEntity.HasValue)
					{
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
						EnqueueAction(agent, new CompleteConstructionAction(runtimeContext, agent, step.TargetEntity.Value), metadata);
					}

					break;

				case "process-recipe":
					if (step.TargetEntity.HasValue)
					{
						var outputItemId = GetCraftedOutputItemId(step);
						if (string.IsNullOrWhiteSpace(outputItemId))
						{
							break;
						}

						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
						EnqueueAction(agent, new ProcessWorldObjectRecipeAction(runtimeContext, agent, step.TargetEntity.Value, outputItemId), metadata);
					}

					break;

				case "eat":
						if (!agent.TrySelectConsumableItem(SimulationEntityExtensions.ConsumableKind.Food, out var eatItem))
						{
							break;
						}

						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
						EnqueueAction(agent, new ConsumeInventoryItemAction(runtimeContext, agent, eatItem.GetItemDefinitionId(), true, false), metadata);
						break;

					case "drink":
						if (!agent.TrySelectConsumableItem(SimulationEntityExtensions.ConsumableKind.Drink, out var drinkItem))
						{
							break;
						}

						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
						EnqueueAction(agent, new ConsumeInventoryItemAction(runtimeContext, agent, drinkItem.GetItemDefinitionId(), false, true), metadata);
						break;

					case "scan-area":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
						EnqueueAction(agent, new ScanAreaAction(runtimeContext, agent, 8, 180), metadata);
						break;

					case "hide":
						var currentCell = world.CoordsAtXY(agent.GetPosition());
						var dangerCell = agent.Has<PerceptionComponent>()
							? agent.Get<PerceptionComponent>().LastKnownEnemyCell
							: currentCell;
						if (world.TryFindHideDestination(currentCell, dangerCell, 6, out var hideCell))
						{
							world.PlotPath(agent, hideCell, metadata);
							EnqueueAction(agent, new SetHiddenAction(runtimeContext, agent, 180), metadata);
						}

						break;

					case "patrol-area":
						if (world.TryFindPatrolRoute(world.CoordsAtXY(agent.GetPosition()), 6, 4, out var route))
						{
							foreach (var waypoint in route)
							{
								world.PlotPath(agent, waypoint, metadata);
								EnqueueAction(agent, new ScanAreaAction(runtimeContext, agent, 8, 120), metadata);
							}

							EnqueueAction(agent, new MarkPatrolledAction(runtimeContext, agent, 240), metadata);
						}

						break;

					case "throw-item":
						if (step.TargetEntity.HasValue)
						{
							var throwItemId = GetRequiredItemId(step.Definition);
							if (string.IsNullOrWhiteSpace(throwItemId))
							{
								break;
							}

							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new ThrowItemAction(runtimeContext, agent, step.TargetEntity.Value, throwItemId), metadata);
						}

						break;

					case "search-for-item":
						var searchedItemId = GetItemIdFromKnowledgeFact(step.AddFacts.FirstOrDefault());
						if (!string.IsNullOrWhiteSpace(searchedItemId))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new SearchForItemAction(runtimeContext, agent, searchedItemId, step.TargetCell), metadata);
						}

						break;

					case "retrieve-known-item":
						var retrievedItemId = GetItemIdFromHasItemFact(step.AddFacts.FirstOrDefault());
						if (!string.IsNullOrWhiteSpace(retrievedItemId))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new RetrieveRememberedItemAction(runtimeContext, agent, retrievedItemId, step.TargetCell), metadata);
						}

						break;

					case "communicate":
						if (step.TargetEntity.HasValue && step.AddFacts.Length > 0)
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new CommunicateAction(runtimeContext, agent, step.TargetEntity.Value, step.AddFacts.First()), metadata);
						}

						break;

					case "read-cookbook":
						if (step.TargetEntity.HasValue)
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, agent.ComputeEffectiveDuration(step.Definition.Skills, step.Definition.DurationTicks)), metadata);
							EnqueueAction(agent, new ReadKnowledgeItemAction(runtimeContext, agent, step.TargetEntity.Value, step.TargetCell), metadata);
						}

						break;
				}
			}
		}

		static void EnqueueAction(Entity agent, IAgentAction action, AgentActionMetadata metadata)
		{
			action.ApplyActionMetadata(metadata);
			agent.EnqueueAction(action);
		}

		static string GetRequiredItemId(ActionDefinitionSnapshot definition)
		{
			foreach (var fact in definition.RequiredFacts)
			{
				if (Facts.TryGetHasItemId(fact, out var itemId))
				{
					return itemId;
				}
			}

			return string.Empty;
		}

		static string GetItemIdFromKnowledgeFact(string fact)
			=> Facts.TryGetKnownItemLocationId(fact, out var itemId)
				? itemId
				: string.Empty;

		static string GetItemIdFromHasItemFact(string fact)
			=> Facts.TryGetHasItemId(fact, out var itemId)
				? itemId
				: string.Empty;

		static string GetCraftedOutputItemId(ActionCandidate step)
			=> step.AddFacts.FirstOrDefault(fact => Facts.TryGetHasItemId(fact, out _)) is string fact
				&& Facts.TryGetHasItemId(fact, out var itemId)
					? itemId
					: string.Empty;
	}
}
