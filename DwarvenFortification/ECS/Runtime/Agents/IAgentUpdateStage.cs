using Arch.Core;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public interface IAgentUpdateStage
	{
		void Update(AgentRuntimeContext context, Entity agent);
	}
}