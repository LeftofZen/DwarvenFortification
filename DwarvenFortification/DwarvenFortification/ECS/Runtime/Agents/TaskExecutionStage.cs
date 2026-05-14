using Arch.Core;
using DwarvenFortification.Logging;

namespace DwarvenFortification
{
	public sealed class TaskExecutionStage : IAgentUpdateStage
	{
		public void Update(AgentRuntimeContext context, Entity agent)
		{
			if (!agent.TryPeekTask(out var currentTask))
			{
				return;
			}

			var status = currentTask.Tick();
			if (status is AgentTaskStatus.Succeeded or AgentTaskStatus.Failed or AgentTaskStatus.Cancelled)
			{
				if (status == AgentTaskStatus.Failed)
				{
					context.Logger.Log(LogLevel.Warning, $"task failed: {currentTask.Name}; action={currentTask.ActionId}; reason={currentTask.FailureReason}");
				}

				agent.DequeueTask();
			}
		}
	}
}