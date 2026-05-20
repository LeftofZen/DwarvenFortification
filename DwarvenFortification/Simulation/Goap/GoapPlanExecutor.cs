#pragma warning disable CS8632

using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.Actions;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using DwarvenFortification.UI;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public sealed class GoapPlanExecutor : IGoapPlanExecutor
	{
		readonly IActionRuntimeContext runtimeContext;
		readonly IGoapWorldQueryService queryService;

		public GoapPlanExecutor(IActionRuntimeContext runtimeContext, IGoapWorldQueryService queryService)
		{
			this.runtimeContext = runtimeContext;
			this.queryService = queryService;
		}

		public bool Enqueue(Entity agent, GoapPlan plan)
			=> Enqueue(agent, plan, null);

		public bool Enqueue(Entity agent, GoapPlan plan, AgentActionMetadata metadata)
		{
			if (plan == null || plan.Actions.Count == 0)
			{
				return false;
			}

			var world = runtimeContext.World;
			var cumulativeState = ToFactSet(plan.Agent.States);
			foreach (var step in plan.Actions)
			{
				Entity? targetEntity = null;
				Point targetCell = default, destinationCell = default;
				string actionContext = string.Empty;
				if (queryService != null)
				{
					queryService.TryFindActionTarget(agent, step, cumulativeState,
						out targetEntity, out targetCell, out destinationCell, out actionContext);
				}

				if (targetCell != default && targetCell != world.CoordsAtXY(agent.GetPosition()))
				{
					world.PlotPath(agent, destinationCell, metadata);
				}

				Enqueue(agent, step, targetEntity, targetCell, destinationCell, actionContext, world, metadata);
				foreach (var effect in step.GetEffectFacts())
				{
					cumulativeState.Add(effect);
				}
			}

			return true;
		}

		public bool Enqueue(Entity agent, GoapAction step, GoapAgent goapAgent, AgentActionMetadata metadata)
		{
			var world = runtimeContext.World;
			var state = ToFactSet(goapAgent.States);
			Entity? targetEntity = null;
			Point targetCell = default, destinationCell = default;
			string actionContext = string.Empty;
			if (queryService != null)
			{
				queryService.TryFindActionTarget(agent, step, state,
					out targetEntity, out targetCell, out destinationCell, out actionContext);
			}

			if (targetCell != default && targetCell != world.CoordsAtXY(agent.GetPosition()))
			{
				world.PlotPath(agent, destinationCell, null);
			}

			Enqueue(agent, step, targetEntity, targetCell, destinationCell, actionContext, world, metadata);
			return true;
		}

		static System.Collections.Generic.HashSet<string> ToFactSet(System.Collections.Generic.IDictionary<object, object?> states)
			=> [.. states.Where(pair => pair.Value is bool value && value).Select(pair => pair.Key.ToString())];

		void Enqueue(Entity agent, GoapAction step, Entity? targetEntity, Point targetCell, Point destinationCell, string actionContext, ISimulationWorld world, AgentActionMetadata metadata)
		{
			var actionId = step.GetId();
			var actionSkills = step.GetSkills();
			var durationTicks = step.GetDurationTicks();

			switch (actionId)
				{
					case "mine":
					case "cut-tree":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
						EnqueueAction(agent, new ExtractResourceNodeAction(runtimeContext, agent, targetCell, agent.ComputeSkillYieldMultiplier(actionSkills)), metadata);
						EnqueueAction(agent, new CollectItemsFromCellAction(runtimeContext, agent, targetCell, 1), metadata);
						break;

					case "store-items":
						if (targetEntity.HasValue)
						{
							var storableItems = agent.GetInventory().Where(item => !item.Get<ItemDefinitionComponent>().IsTool && targetEntity.Value.CanStore(item)).ToList();
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
							EnqueueAction(agent, new StoreItemsInWorldObjectAction(runtimeContext, agent, targetEntity.Value, storableItems), metadata);
						}

						break;

				case "haul-material":
					if (targetEntity.HasValue)
					{
						var missingMaterials = targetEntity.Value.IsConstructionSite()
							? targetEntity.Value.GetMissingBuildCosts()
							: targetEntity.Value.GetMissingOrderInputs();
						if (missingMaterials.Length == 0)
							{
								break;
							}

							var missing = missingMaterials[0];
						var itemToHaul = agent.GetInventory().FirstOrDefault(item =>
							!item.Get<ItemDefinitionComponent>().IsTool &&
							(missing.UsesFilter ? item.ItemMatchesFilter(missing.ItemFilter)
								: string.Equals(item.GetItemDefinitionId(), missing.ItemId, StringComparison.OrdinalIgnoreCase)));
						if (!itemToHaul.Equals(default(Entity)))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
							EnqueueAction(agent, new StoreItemsInWorldObjectAction(runtimeContext, agent, targetEntity.Value, new System.Collections.Generic.List<Entity> { itemToHaul }), metadata);
						}
					}

					break;

				case "sleep":
					if (targetEntity.HasValue)
					{
						EnqueueAction(agent, new SleepAction(runtimeContext, agent, targetEntity.Value, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
					}

					break;

				case "complete-construction":
					if (targetEntity.HasValue)
					{
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
						EnqueueAction(agent, new CompleteConstructionAction(runtimeContext, agent, targetEntity.Value), metadata);
					}

					break;

				case "process-recipe":
					if (targetEntity.HasValue)
					{
						var recipe = targetEntity.Value.GetRecipes()
							.FirstOrDefault(r => targetEntity.Value.HasStoredMaterials(r.Inputs));
						if (recipe.Equals(default(CraftRecipeComponent)))
							{
								break;
							}

							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
						EnqueueAction(agent, new ProcessWorldObjectRecipeAction(runtimeContext, agent, targetEntity.Value, recipe.OutputItemId), metadata);
					}

					break;

				case "eat":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
						EnqueueAction(agent, new ConsumeInventoryItemAction(runtimeContext, agent, SimulationEntityExtensions.ConsumableKind.Food), metadata);
						break;

					case "drink":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
						EnqueueAction(agent, new ConsumeInventoryItemAction(runtimeContext, agent, SimulationEntityExtensions.ConsumableKind.Drink), metadata);
						break;

					case "scan-area":
						EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
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
						if (targetEntity.HasValue)
						{
							var throwItemId = GetRequiredItemId(step);
							if (string.IsNullOrWhiteSpace(throwItemId))
							{
								break;
							}

							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
							EnqueueAction(agent, new ThrowItemAction(runtimeContext, agent, targetEntity.Value, throwItemId), metadata);
						}

						break;

					case "search-for-item":
						if (!string.IsNullOrWhiteSpace(actionContext))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
							EnqueueAction(agent, new SearchForItemAction(runtimeContext, agent, actionContext, targetCell), metadata);
						}

						break;

					case "retrieve-known-item":
						if (!string.IsNullOrWhiteSpace(actionContext))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
							EnqueueAction(agent, new RetrieveRememberedItemAction(runtimeContext, agent, actionContext, targetCell), metadata);
						}

						break;

					case "communicate":
						if (targetEntity.HasValue && !string.IsNullOrWhiteSpace(actionContext))
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
							EnqueueAction(agent, new CommunicateAction(runtimeContext, agent, targetEntity.Value, actionContext), metadata);
						}

						break;

					case "read-cookbook":
						if (targetEntity.HasValue)
						{
							EnqueueAction(agent, new TimedAction(runtimeContext, agent, actionId, agent.ComputeEffectiveDuration(actionSkills, durationTicks)), metadata);
							EnqueueAction(agent, new ReadKnowledgeItemAction(runtimeContext, agent, targetEntity.Value, targetCell), metadata);
						}

						break;
				}
		}

		static void EnqueueAction(Entity agent, IAgentAction action, AgentActionMetadata metadata)
		{
			action.ApplyActionMetadata(metadata);
			agent.EnqueueAction(action);
		}

		static string GetRequiredItemId(GoapAction definition)
		{
			foreach (var fact in definition.GetRequiredFacts())
			{
				if (Facts.TryGetHasItemId(fact, out var itemId))
				{
					return itemId;
				}
			}

			return string.Empty;
		}

	}
}
