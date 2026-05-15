using Arch.Core;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public interface IAgentPlanningService
	{
		bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent);
	}
}