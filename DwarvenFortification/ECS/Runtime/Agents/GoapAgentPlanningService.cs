using Arch.Core;
using DwarvenFortification.Actions;
using DwarvenFortification.GOAP;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.Composition;
using System.Linq;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class GoapAgentPlanningService : IAgentPlanningService
	{
		const int IdleDurationTicks = 60;

		readonly GoapPlanner planner;
		readonly IGoapPlanSelector planSelector;
		readonly IGoapPlanExecutor planExecutor;
		readonly IActionRuntimeContext runtimeContext;

		public GoapAgentPlanningService(GoapPlanner planner, IGoapPlanSelector planSelector, IGoapPlanExecutor planExecutor, IActionRuntimeContext runtimeContext)
		{
			this.planner = planner;
			this.planSelector = planSelector;
			this.planExecutor = planExecutor;
			this.runtimeContext = runtimeContext;
		}

		public bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent)
		{
			var planningSnapshot = planner.Inspect(agent);
			var plan = planSelector.SelectCandidatePlan(agent, planningSnapshot);
			if (plan == null)
			{
				// Log why no plan was found (once per N ticks to avoid spam).
				var eligibleGoals = planningSnapshot.Goals.Where(g => g.IsEligible && !g.IsSatisfied).ToArray();
				if (eligibleGoals.Length > 0)
				{
					var goalSummary = string.Join(", ", eligibleGoals.Select(g => g.Goal.Id));
					context.Logger.Log(LogLevel.Warning, $"{agent.GetName()} idle: eligible unsatisfied goals [{goalSummary}] but no plan found. Facts: [{string.Join(", ", planningSnapshot.CurrentState.Take(20))}]");
				}
				agent.EnqueueAction(new TimedAction(runtimeContext, agent, "idle", IdleDurationTicks));
				return false;
			}

			context.Logger.Log(LogLevel.Info, $"{agent.GetName()} enqueuing plan for goal '{plan.Goal.Id}' with {plan.Steps.Count} steps: [{string.Join(" -> ", plan.Steps.Select(s => s.Definition.Id))}]");

			if (planExecutor.Enqueue(agent, plan))
			{
				return true;
			}

			context.Logger.Log(LogLevel.Warning, $"planner produced an invalid plan for {agent.GetName()}.");
			agent.EnqueueAction(new TimedAction(runtimeContext, agent, "idle", IdleDurationTicks));
			return false;
		}
	}
}