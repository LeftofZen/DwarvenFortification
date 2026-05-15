using Arch.Core;
using Arch.Core.Extensions;
using System.Linq;

namespace DwarvenFortification
{
	public sealed class GoapPlanExecutor : IGoapPlanExecutor
	{
		readonly IActionRuntimeContext runtimeContext;

		public GoapPlanExecutor(IActionRuntimeContext runtimeContext)
		{
			this.runtimeContext = runtimeContext;
		}

		public bool Enqueue(Entity agent, GoapPlan plan)
			=> Enqueue(agent, plan, null);

		public bool Enqueue(Entity agent, GoapPlan plan, AgentActionMetadata metadata)
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

		public bool Enqueue(Entity agent, GoapActionCandidate step)
			=> Enqueue(agent, step, null);

		public bool Enqueue(Entity agent, GoapActionCandidate step, AgentActionMetadata metadata)
		{
			Enqueue(agent, step, runtimeContext.World, metadata);
			return true;
		}

		void Enqueue(Entity agent, GoapActionCandidate step, ISimulationWorld world, AgentActionMetadata metadata)
		{
			{
				world.PlotPath(agent, step.DestinationCell, metadata);

				switch (step.Definition.Id)
				{
					case "mine":
					case "cut-tree":
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
							EnqueueAction(agent, new ExtractResourceNodeAction(runtimeContext, agent, step.TargetCell), metadata);
							EnqueueAction(agent, new CollectItemsFromCellAction(runtimeContext, agent, step.TargetCell, 1), metadata);
						break;

					case "store-items":
						if (step.TargetEntity.HasValue)
						{
							var storableItems = agent.GetInventory().Where(item => !item.Get<ItemDefinitionComponent>().IsTool && step.TargetEntity.Value.CanStore(item)).ToList();
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
							EnqueueAction(agent, new StoreItemsInWorldObjectAction(runtimeContext, agent, step.TargetEntity.Value, storableItems), metadata);
						}

						break;

					case "sleep":
						if (step.TargetEntity.HasValue)
						{
							EnqueueAction(agent, new SleepAction(runtimeContext, agent, step.TargetEntity.Value, step.Definition.DurationTicks), metadata);
						}

						break;

					case "eat":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
						EnqueueAction(agent, new ConsumeInventoryItemAction(runtimeContext, agent, step.Definition.RequiredItemIds.First(), true, false), metadata);
						break;

					case "drink":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
						EnqueueAction(agent, new ConsumeInventoryItemAction(runtimeContext, agent, step.Definition.RequiredItemIds.First(), false, true), metadata);
						break;

					case "scan-area":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
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
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
							EnqueueAction(agent, new ThrowItemAction(runtimeContext, agent, step.TargetEntity.Value, step.Definition.RequiredItemIds.First()), metadata);
						}

						break;

					case "search-for-item":
						var searchedItemId = GetItemIdFromKnowledgeFact(step.AddFacts.FirstOrDefault());
						if (!string.IsNullOrWhiteSpace(searchedItemId))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
							EnqueueAction(agent, new SearchForItemAction(runtimeContext, agent, searchedItemId, step.TargetCell), metadata);
						}

						break;

					case "retrieve-known-item":
						var retrievedItemId = GetItemIdFromHasItemFact(step.AddFacts.FirstOrDefault());
						if (!string.IsNullOrWhiteSpace(retrievedItemId))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
							EnqueueAction(agent, new RetrieveRememberedItemAction(runtimeContext, agent, retrievedItemId, step.TargetCell), metadata);
						}

						break;

					case "communicate":
						if (step.TargetEntity.HasValue && step.AddFacts.Length > 0)
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
							EnqueueAction(agent, new CommunicateAction(runtimeContext, agent, step.TargetEntity.Value, step.AddFacts.First()), metadata);
						}

						break;

					case "read-cookbook":
						if (step.TargetEntity.HasValue)
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks), metadata);
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

		static string GetItemIdFromKnowledgeFact(string fact)
			=> !string.IsNullOrWhiteSpace(fact) && fact.StartsWith("knows.item-location.", System.StringComparison.OrdinalIgnoreCase)
				? fact["knows.item-location.".Length..]
				: string.Empty;

		static string GetItemIdFromHasItemFact(string fact)
			=> !string.IsNullOrWhiteSpace(fact) && fact.StartsWith("has.item.", System.StringComparison.OrdinalIgnoreCase)
				? fact["has.item.".Length..]
				: string.Empty;
	}
}