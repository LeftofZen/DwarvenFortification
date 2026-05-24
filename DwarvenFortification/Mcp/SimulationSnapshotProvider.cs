#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP;
using DwarvenFortification.Simulation.World;

namespace DwarvenFortification.Mcp
{
	/// <summary>
	/// Holds the most recently captured <see cref="SimulationSnapshot"/>. The game loop calls
	/// <see cref="Capture"/> on the main thread once per simulation tick; MCP tools read the
	/// snapshot from any thread via <see cref="Current"/>.
	/// </summary>
	public sealed class SimulationSnapshotProvider
	{
		readonly SimulationDefinitionRegistry definitions;
		readonly IGoapWorldQueryService queryService;
		SimulationSnapshot current;

		public SimulationSnapshotProvider(SimulationDefinitionRegistry definitions, IGoapWorldQueryService queryService)
		{
			this.definitions = definitions;
			this.queryService = queryService;
			current = new SimulationSnapshot(0, 0, Array.Empty<AgentSnapshot>(), Array.Empty<WorldItemSnapshot>(), Array.Empty<WorldStructureSnapshot>());
		}

		public SimulationSnapshot Current => Volatile.Read(ref current);

		public SimulationDefinitionRegistry Definitions => definitions;

		/// <summary>Called from the main game thread once per simulation tick.</summary>
		public void Capture(ISimulationWorld world, long tick)
		{
			var agents = new List<AgentSnapshot>();
			foreach (var entity in world.GetAgents())
			{
				agents.Add(BuildAgentSnapshot(world, entity));
			}

			var items = new List<WorldItemSnapshot>();
			var structures = new List<WorldStructureSnapshot>();
			foreach (var (cell, _, coords) in world.EnumerateCells())
			{
				foreach (var item in cell.ItemsInCell)
				{
					items.Add(new WorldItemSnapshot(item.GetItemDefinitionId(), coords.X, coords.Y, GetTags(item)));
				}

				if (cell.TryGetWorldObject(out var worldObject) && worldObject.Has<WorldObjectReferenceComponent>())
				{
					structures.Add(new WorldStructureSnapshot(
						worldObject.Get<WorldObjectReferenceComponent>().DefinitionId,
						coords.X,
						coords.Y,
						GetTags(worldObject)));
				}
			}

			var snapshot = new SimulationSnapshot(
				tick,
				DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				agents,
				items,
				structures);

			Volatile.Write(ref current, snapshot);
		}

		AgentSnapshot BuildAgentSnapshot(ISimulationWorld world, Entity entity)
		{
			var name = entity.GetName();
			var pos = entity.GetPosition();
			var coords = world.CoordsAtXY(pos);

			var state = queryService.BuildCurrentState(entity);
			var numericState = queryService.BuildNumericState(entity);
			var goapAgent = SimulationGoapAgentFactory.CreateAgent(definitions, name, state, numericState);

			var booleanFacts = goapAgent.States
				.Where(kvp => kvp.Value.Value is bool b && b)
				.Select(kvp => kvp.Key)
				.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
				.ToList();

			var numericFacts = new Dictionary<string, NumericFactSnapshot>(StringComparer.OrdinalIgnoreCase);
			foreach (var kvp in goapAgent.States)
			{
				if (kvp.Value.Value is bool)
				{
					continue;
				}

				var value = TryToDouble(kvp.Value);
				if (value is null)
				{
					continue;
				}

				double? min = null, max = null;
				if (goapAgent.StateBounds.TryGetValue(kvp.Key, out var bounds))
				{
					min = bounds.Min.HasValue ? TryToDouble(bounds.Min.Value) : null;
					max = bounds.Max.HasValue ? TryToDouble(bounds.Max.Value) : null;
				}

				numericFacts[kvp.Key] = new NumericFactSnapshot(value.Value, min, max);
			}

			var goals = new List<GoalSnapshot>();
			SimulationGoapGoal? pursuedGoal = null;
			foreach (var goal in goapAgent.Goals.OfType<SimulationGoapGoal>().OrderByDescending(g => g.PriorityValue))
			{
				var unsatisfiedObjectives = goal.Goals.Where(c => !c.Evaluate(goapAgent.States)).Select(FormatCondition).ToList();
				var unsatisfiedEntry = goal.RequirementExpressions
					.Select(expr => expr.ToCondition())
					.Where(c => !c.Evaluate(goapAgent.States))
					.Select(FormatCondition)
					.ToList();

				var status = unsatisfiedObjectives.Count == 0
					? "achieved"
					: unsatisfiedEntry.Count > 0 ? "blocked" : "pursuable";

				goals.Add(new GoalSnapshot(goal.Id, goal.Name, goal.PriorityValue, status, unsatisfiedObjectives, unsatisfiedEntry));

				if (pursuedGoal is null && status == "pursuable")
				{
					pursuedGoal = goal;
				}
			}

			PlanSnapshot? activePlan = null;

			var queue = new List<QueuedActionSnapshot>();
			if (entity.Has<ActionQueueComponent>())
			{
				var queuedActions = entity.Get<ActionQueueComponent>().Actions;
				foreach (var action in queuedActions)
				{
					queue.Add(new QueuedActionSnapshot(
						action.ActionId ?? action.Name,
						action.Name,
						action.Status.ToString(),
						action.Progress,
						action.Cost));
				}

				// The "active plan" reported by the snapshot is the agent's currently-executing
				// action queue \u2014 NOT a freshly-computed speculative plan. The snapshot provider is
				// read-only by contract; running the planner here would both burn CPU and produce a
				// plan that may differ from what the agent is actually doing.
				if (queue.Count > 0)
				{
					var steps = queuedActions.Select(action =>
					{
						var matched = goapAgent.Actions
							.OfType<SimulationGoapAction>()
							.FirstOrDefault(a => string.Equals(a.Id, action.ActionId, StringComparison.OrdinalIgnoreCase));
						var successEffect = matched?.SuccessEffect is null ? string.Empty : FormatEffect(matched.SuccessEffect);
						return new PlanStepSnapshot(
							action.ActionId ?? action.Name,
							action.Name,
							action.Cost,
							successEffect);
					}).ToList();

					activePlan = new PlanSnapshot(
						pursuedGoal?.Id ?? string.Empty,
						pursuedGoal?.Name ?? string.Empty,
						steps.Sum(s => s.Cost),
						steps);
				}
			}

			var available = new List<ActionAvailabilitySnapshot>();
			var unavailable = new List<ActionAvailabilitySnapshot>();
			foreach (var action in goapAgent.Actions.OfType<SimulationGoapAction>())
			{
				var unsatisfied = action.Conditions.Where(c => !c.Evaluate(goapAgent.States)).Select(FormatConditionWithCurrent(goapAgent.States)).ToList();
				var entry = new ActionAvailabilitySnapshot(
					action.Id,
					action.Name ?? action.Id,
					action.SuccessEffect is null ? string.Empty : FormatEffect(action.SuccessEffect),
					unsatisfied);
				(unsatisfied.Count == 0 ? available : unavailable).Add(entry);
			}

			return new AgentSnapshot(
				entity.Id,
				name,
				coords.X,
				coords.Y,
				booleanFacts,
				numericFacts,
				goals,
				activePlan,
				available,
				unavailable,
				queue);
		}

		static IReadOnlyList<string> GetTags(Entity entity)
		{
			if (!entity.Has<TagCollectionComponent>())
			{
				return Array.Empty<string>();
			}

			return entity.Get<TagCollectionComponent>().Values.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
		}

		static double? TryToDouble(GoapValue value)
		{
			return value.Value switch
			{
				int i => i,
				long l => l,
				double d => d,
				float f => f,
				_ => null,
			};
		}

		static string FormatCondition(GoapCondition condition)
		{
			if (condition.Operand.Value is bool flag)
			{
				return condition.Comparison switch
				{
					GoapComparison.EqualTo => flag ? condition.StateId : $"!{condition.StateId}",
					GoapComparison.NotEqualTo => flag ? $"!{condition.StateId}" : condition.StateId,
					_ => $"{condition.StateId} {SymbolFor(condition.Comparison)} {FormatValue(condition.Operand)}",
				};
			}

			return $"{condition.StateId} {SymbolFor(condition.Comparison)} {FormatValue(condition.Operand)}";
		}

		static Func<GoapCondition, string> FormatConditionWithCurrent(GoapWorldState states)
			=> condition =>
			{
				var head = FormatCondition(condition);
				if (condition.Operand.Value is bool)
				{
					return head;
				}

				var current = states.TryGetValue(condition.StateId, out var v) ? FormatValue(v) : "(unset)";
				return $"{head} (current: {current})";
			};

		static string FormatEffect(GoapEffect effect)
		{
			if (effect.Operation == GoapOperation.SetTo && effect.Operand.Value is bool flag)
			{
				return flag ? effect.StateId : $"!{effect.StateId}";
			}

			return $"{effect.StateId} {SymbolFor(effect.Operation)} {FormatValue(effect.Operand)}";
		}

		static string FormatValue(GoapValue v) => v.Value switch
		{
			null => "(null)",
			bool b => b ? "true" : "false",
			double d => d.ToString("0.##"),
			float f => f.ToString("0.##"),
			_ => v.Value.ToString() ?? string.Empty,
		};

		static string SymbolFor(GoapComparison c) => c switch
		{
			GoapComparison.EqualTo => "==",
			GoapComparison.NotEqualTo => "!=",
			GoapComparison.LessThan => "<",
			GoapComparison.LessThanOrEqualTo => "<=",
			GoapComparison.GreaterThan => ">",
			GoapComparison.GreaterThanOrEqualTo => ">=",
			_ => c.ToString(),
		};

		static string SymbolFor(GoapOperation o) => o switch
		{
			GoapOperation.SetTo => "=",
			GoapOperation.IncreaseBy => "+=",
			GoapOperation.DecreaseBy => "-=",
			GoapOperation.MultiplyBy => "*=",
			GoapOperation.DivideBy => "/=",
			GoapOperation.ModuloBy => "%=",
			GoapOperation.ExponentiateBy => "^=",
			_ => o.ToString(),
		};
	}
}
