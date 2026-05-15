using Arch.Core;
using DwarvenFortification.Actions;
using DwarvenFortification.Logging;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class ActionExecutionStage : IAgentUpdateStage
	{
		public void Update(AgentRuntimeContext context, Entity agent)
		{
			if (!agent.TryPeekAction(out var currentAction))
			{
				return;
			}

			var status = currentAction.Tick();
			if (status is AgentActionStatus.Succeeded or AgentActionStatus.Failed or AgentActionStatus.Cancelled)
			{
				if (status == AgentActionStatus.Failed)
				{
					context.Logger.Log(LogLevel.Warning, $"action failed: {currentAction.Name}; actionId={currentAction.ActionId}; reason={currentAction.FailureReason}");
				}

				agent.DequeueAction();
			}
		}
	}
}