using Arch.Core;
using DwarvenFortification.Logging;

namespace DwarvenFortification
{
	public sealed class TaskQueueIntegrityStage : IAgentUpdateStage
	{
		public void Update(AgentRuntimeContext context, Entity agent)
		{
			if (!agent.HasInvalidQueuedTask(context.World))
			{
				return;
			}

			context.Logger.Log(LogLevel.Debug, $"clearing stale queued plan for {agent.GetName()}");
			agent.ClearQueuedTasks();
		}
	}
}