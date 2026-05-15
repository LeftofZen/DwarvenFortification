using Arch.Core;

namespace DwarvenFortification.GOAP.Plans
{
	public interface IPlanExecutor
	{
		bool Enqueue(Entity agent, Plan plan);
	}
}
