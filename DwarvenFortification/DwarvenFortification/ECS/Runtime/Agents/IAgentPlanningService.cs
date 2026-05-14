using Arch.Core;

namespace DwarvenFortification
{
	public interface IAgentPlanningService
	{
		bool TryEnqueuePlan(AgentRuntimeContext context, Entity agent);
	}
}