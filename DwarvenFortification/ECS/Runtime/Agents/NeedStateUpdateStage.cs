using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS.Components;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class NeedStateUpdateStage : IAgentUpdateStage
	{
		public void Update(AgentRuntimeContext context, Entity agent)
		{
			agent.DecayRest();
			agent.TickBodyNutrition();
		}
	}
}