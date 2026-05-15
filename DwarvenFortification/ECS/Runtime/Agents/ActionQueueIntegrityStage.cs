using Arch.Core;
using DwarvenFortification.Logging;

namespace DwarvenFortification
{
	public sealed class ActionQueueIntegrityStage : IAgentUpdateStage
	{
		public void Update(AgentRuntimeContext context, Entity agent)
		{
			if (!agent.HasInvalidQueuedAction(context.World))
			{
				return;
			}

			context.Logger.Log(LogLevel.Debug, $"clearing stale queued plan for {agent.GetName()}");
			agent.ClearQueuedActions();
		}
	}
}