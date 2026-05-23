using DwarvenFortification.UI;
using Arch.Core;

namespace DwarvenFortification.GOAP
{
	public interface IGoapPlanExecutor
	{
		bool Enqueue(Entity agent, GoapPlan plan, AgentActionMetadata metadata = null);
		bool Enqueue(Entity agent, GoapAction step, GoapAgent goapAgent, AgentActionMetadata metadata = null);
	}
}
