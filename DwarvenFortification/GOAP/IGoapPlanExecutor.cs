using Arch.Core;

namespace DwarvenFortification
{
	public interface IGoapPlanExecutor
	{
		bool Enqueue(Entity agent, GoapPlan plan);
	}
}