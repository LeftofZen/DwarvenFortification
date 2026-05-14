using Arch.Core;
using Arch.Core.Extensions;
using System.Linq;

namespace DwarvenFortification
{
	public sealed class GoapPlanExecutor : IGoapPlanExecutor
	{
		readonly ITaskRuntimeContext runtimeContext;

		public GoapPlanExecutor(ITaskRuntimeContext runtimeContext)
		{
			this.runtimeContext = runtimeContext;
		}

		public bool Enqueue(Entity agent, GoapPlan plan)
		{
			if (plan == null || plan.Steps.Count == 0)
			{
				return false;
			}

			var world = runtimeContext.World;

			foreach (var step in plan.Steps)
			{
				world.PlotPath(agent, step.DestinationCell);

				switch (step.Definition.Id)
				{
					case "mine":
					case "cut-tree":
							agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
							agent.EnqueueTask(new ExtractResourceNodeTask(runtimeContext, agent, step.TargetCell));
							agent.EnqueueTask(new CollectItemsFromCellTask(runtimeContext, agent, step.TargetCell, 1));
						break;

					case "store-items":
						if (step.TargetEntity.HasValue)
						{
							var storableItems = agent.GetInventory().Where(item => !item.Get<ItemDefinitionComponent>().IsTool && step.TargetEntity.Value.CanStore(item)).ToList();
							agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
							agent.EnqueueTask(new StoreItemsInWorldObjectTask(runtimeContext, agent, step.TargetEntity.Value, storableItems));
						}
						break;

					case "sleep":
						if (step.TargetEntity.HasValue)
						{
							agent.EnqueueTask(new SleepTask(runtimeContext, agent, step.TargetEntity.Value, step.Definition.DurationTicks));
						}
						break;

					case "eat":
						agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
						agent.EnqueueTask(new ConsumeInventoryItemTask(runtimeContext, agent, step.Definition.RequiredItemIds.First(), true, false));
						break;

					case "drink":
						agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
						agent.EnqueueTask(new ConsumeInventoryItemTask(runtimeContext, agent, step.Definition.RequiredItemIds.First(), false, true));
						break;

					case "scan-area":
						agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
						agent.EnqueueTask(new ScanAreaTask(runtimeContext, agent, 8, 180));
						break;

					case "hide":
						var currentCell = world.CoordsAtXY(agent.GetPosition());
						var dangerCell = agent.Has<PerceptionComponent>()
							? agent.Get<PerceptionComponent>().LastKnownEnemyCell
							: currentCell;
						if (world.TryFindHideDestination(currentCell, dangerCell, 6, out var hideCell))
						{
							world.PlotPath(agent, hideCell);
							agent.EnqueueTask(new SetHiddenTask(runtimeContext, agent, 180));
						}
						break;

					case "patrol-area":
						if (world.TryFindPatrolRoute(world.CoordsAtXY(agent.GetPosition()), 6, 4, out var route))
						{
							foreach (var waypoint in route)
							{
								world.PlotPath(agent, waypoint);
								agent.EnqueueTask(new ScanAreaTask(runtimeContext, agent, 8, 120));
							}
							agent.EnqueueTask(new MarkPatrolledTask(runtimeContext, agent, 240));
						}
						break;

					case "throw-item":
						if (step.TargetEntity.HasValue)
						{
							agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
							agent.EnqueueTask(new ThrowItemTask(runtimeContext, agent, step.TargetEntity.Value, step.Definition.RequiredItemIds.First()));
						}
						break;

					case "search-for-item":
						var searchedItemId = GetItemIdFromKnowledgeFact(step.AddFacts.FirstOrDefault());
						if (!string.IsNullOrWhiteSpace(searchedItemId))
						{
							agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
							agent.EnqueueTask(new SearchForItemTask(runtimeContext, agent, searchedItemId, step.TargetCell));
						}
						break;

					case "retrieve-known-item":
						var retrievedItemId = GetItemIdFromHasItemFact(step.AddFacts.FirstOrDefault());
						if (!string.IsNullOrWhiteSpace(retrievedItemId))
						{
							agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
							agent.EnqueueTask(new RetrieveRememberedItemTask(runtimeContext, agent, retrievedItemId, step.TargetCell));
						}
						break;

					case "communicate":
						if (step.TargetEntity.HasValue && step.AddFacts.Length > 0)
						{
							agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
							agent.EnqueueTask(new CommunicateTask(runtimeContext, agent, step.TargetEntity.Value, step.AddFacts.First()));
						}
						break;

					case "read-cookbook":
						if (step.TargetEntity.HasValue)
						{
							agent.EnqueueTask(new TimedActionTask(runtimeContext, agent, step.Definition.Id, step.Definition.DurationTicks));
							agent.EnqueueTask(new ReadKnowledgeItemTask(runtimeContext, agent, step.TargetEntity.Value, step.TargetCell));
						}
						break;
				}
			}

			return true;
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