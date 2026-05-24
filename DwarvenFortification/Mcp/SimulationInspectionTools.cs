#nullable enable
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using DwarvenFortification.ECS;
using DwarvenFortification.GOAP;
using ModelContextProtocol.Server;

namespace DwarvenFortification.Mcp
{
	/// <summary>
	/// MCP tools that expose the latest <see cref="SimulationSnapshot"/> and static definition
	/// catalogue to an external AI assistant connected over HTTP/SSE.
	/// </summary>
	[McpServerToolType]
	public static class SimulationInspectionTools
	{
		static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

		[McpServerTool, Description("Returns simulation tick, capture timestamp, and counts of agents/items/structures.")]
		public static string GetSimulationOverview(SimulationSnapshotProvider snapshots)
		{
			var s = snapshots.Current;
			return Serialize(new
			{
				s.Tick,
				s.CapturedUnixMilliseconds,
				AgentCount = s.Agents.Count,
				ItemCount = s.Items.Count,
				StructureCount = s.Structures.Count,
				Agents = s.Agents.Select(a => new { a.EntityId, a.Name, a.X, a.Y }),
			});
		}

		[McpServerTool, Description("Lists all agents with their id, name and cell coordinates.")]
		public static string ListAgents(SimulationSnapshotProvider snapshots)
			=> Serialize(snapshots.Current.Agents.Select(a => new { a.EntityId, a.Name, a.X, a.Y }));

		[McpServerTool, Description("Returns the boolean and numeric GOAP world-state facts for the named agent.")]
		public static string GetAgentState(
			SimulationSnapshotProvider snapshots,
			[Description("Agent name as shown by list_agents (case-insensitive).")] string agentName)
		{
			var agent = FindAgent(snapshots, agentName);
			if (agent is null) return AgentNotFound(agentName);
			return Serialize(new { agent.Name, agent.BooleanFacts, agent.NumericFacts });
		}

		[McpServerTool, Description("Returns the ordered list of goals for the agent with status (achieved/pursuable/blocked) and any unsatisfied conditions.")]
		public static string GetAgentGoals(SimulationSnapshotProvider snapshots, string agentName)
		{
			var agent = FindAgent(snapshots, agentName);
			if (agent is null) return AgentNotFound(agentName);
			return Serialize(agent.Goals);
		}

		[McpServerTool, Description("Returns the current GOAP plan (action sequence) the agent is pursuing, if any.")]
		public static string GetAgentPlan(SimulationSnapshotProvider snapshots, string agentName)
		{
			var agent = FindAgent(snapshots, agentName);
			if (agent is null) return AgentNotFound(agentName);
			return Serialize(agent.ActivePlan ?? (object)new { Message = "No pursuable goal / no plan found." });
		}

		[McpServerTool, Description("Returns the agent's available actions (preconditions satisfied) and unavailable actions with the unsatisfied conditions blocking each.")]
		public static string GetAgentActions(SimulationSnapshotProvider snapshots, string agentName)
		{
			var agent = FindAgent(snapshots, agentName);
			if (agent is null) return AgentNotFound(agentName);
			return Serialize(new { agent.AvailableActions, agent.UnavailableActions });
		}

		[McpServerTool, Description("Returns the runtime action queue currently scheduled on the agent's ECS ActionQueueComponent.")]
		public static string GetAgentQueue(SimulationSnapshotProvider snapshots, string agentName)
		{
			var agent = FindAgent(snapshots, agentName);
			if (agent is null) return AgentNotFound(agentName);
			return Serialize(agent.ActionQueue);
		}

		[McpServerTool, Description("Returns a bundled view of the agent: state, goals, active plan, available/unavailable actions, runtime queue.")]
		public static string InspectAgent(SimulationSnapshotProvider snapshots, string agentName)
		{
			var agent = FindAgent(snapshots, agentName);
			if (agent is null) return AgentNotFound(agentName);
			return Serialize(agent);
		}

		[McpServerTool, Description("Lists every item entity sitting on the ground in any cell, with its definition id, cell coords and tags.")]
		public static string ListWorldItems(SimulationSnapshotProvider snapshots)
			=> Serialize(snapshots.Current.Items);

		[McpServerTool, Description("Lists every placed structure (world object) with its definition id, cell coords and tags.")]
		public static string ListWorldStructures(SimulationSnapshotProvider snapshots)
			=> Serialize(snapshots.Current.Structures);

		[McpServerTool, Description("Returns the static GOAP action definition catalogue (id, name, conditions, success effect, cost, duration, target kind).")]
		public static string ListActionDefinitions(SimulationSnapshotProvider snapshots)
		{
			var actions = snapshots.Definitions.GetActionDefinitions()
				.Select(a => new
				{
					a.Id,
					a.Name,
					Conditions = a.Conditions.Select(FormatCondition),
					SuccessEffect = a.SuccessEffect is null ? null : FormatEffect(a.SuccessEffect),
					BaseCost = a.BaseCost,
					DurationTicks = a.DurationTicks,
					TargetKind = a.TargetKind,
					DestinationMode = a.DestinationMode,
				});
			return Serialize(actions);
		}

		[McpServerTool, Description("Returns the static GOAP goal definition catalogue (id, name, priority, requirements, objectives).")]
		public static string ListGoalDefinitions(SimulationSnapshotProvider snapshots)
		{
			var goals = snapshots.Definitions.GetGoalDefinitions()
				.Select(g => new
				{
					g.Id,
					g.Name,
					Priority = g.PriorityValue,
					Requirements = g.RequirementExpressions.Select(r => FormatCondition(r.ToCondition())),
					Objectives = g.Goals.Select(FormatCondition),
				});
			return Serialize(goals);
		}

		static AgentSnapshot? FindAgent(SimulationSnapshotProvider snapshots, string agentName)
			=> snapshots.Current.Agents.FirstOrDefault(a =>
				string.Equals(a.Name, agentName, System.StringComparison.OrdinalIgnoreCase));

		static string AgentNotFound(string name)
			=> Serialize(new { Error = $"Agent '{name}' not found. Call list_agents to see valid names." });

		static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOpts);

		static string FormatCondition(GOAP.GoapCondition condition)
		{
			if (condition.Operand.Value is bool flag)
			{
				return condition.Comparison switch
				{
					GOAP.GoapComparison.EqualTo => flag ? condition.StateId : $"!{condition.StateId}",
					GOAP.GoapComparison.NotEqualTo => flag ? $"!{condition.StateId}" : condition.StateId,
					_ => $"{condition.StateId} {Symbol(condition.Comparison)} {FormatValue(condition.Operand)}",
				};
			}

			return $"{condition.StateId} {Symbol(condition.Comparison)} {FormatValue(condition.Operand)}";
		}

		static string FormatEffect(GOAP.GoapEffect effect)
		{
			if (effect.Operation == GOAP.GoapOperation.SetTo && effect.Operand.Value is bool flag)
			{
				return flag ? effect.StateId : $"!{effect.StateId}";
			}

			return $"{effect.StateId} {Symbol(effect.Operation)} {FormatValue(effect.Operand)}";
		}

		static string FormatValue(GOAP.GoapValue v) => v.Value switch
		{
			null => "(null)",
			bool b => b ? "true" : "false",
			double d => d.ToString("0.##"),
			float f => f.ToString("0.##"),
			_ => v.Value.ToString() ?? string.Empty,
		};

		static string Symbol(GOAP.GoapComparison c) => c switch
		{
			GOAP.GoapComparison.EqualTo => "==",
			GOAP.GoapComparison.NotEqualTo => "!=",
			GOAP.GoapComparison.LessThan => "<",
			GOAP.GoapComparison.LessThanOrEqualTo => "<=",
			GOAP.GoapComparison.GreaterThan => ">",
			GOAP.GoapComparison.GreaterThanOrEqualTo => ">=",
			_ => c.ToString(),
		};

		static string Symbol(GOAP.GoapOperation o) => o switch
		{
			GOAP.GoapOperation.SetTo => "=",
			GOAP.GoapOperation.IncreaseBy => "+=",
			GOAP.GoapOperation.DecreaseBy => "-=",
			GOAP.GoapOperation.MultiplyBy => "*=",
			GOAP.GoapOperation.DivideBy => "/=",
			GOAP.GoapOperation.ModuloBy => "%=",
			GOAP.GoapOperation.ExponentiateBy => "^=",
			_ => o.ToString(),
		};
	}
}
