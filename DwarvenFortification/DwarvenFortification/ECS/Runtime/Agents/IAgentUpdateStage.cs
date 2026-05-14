using Arch.Core;

namespace DwarvenFortification
{
	public interface IAgentUpdateStage
	{
		void Update(AgentRuntimeContext context, Entity agent);
	}
}