using Arch.Core;

namespace DwarvenFortification.GOAP
{
	public interface IGoapPlanExecutor
	{
		bool Enqueue(Entity agent, GoapPlan plan);
	}
}