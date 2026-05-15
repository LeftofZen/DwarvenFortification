using DwarvenFortification.Simulation.World;
using DwarvenFortification.UI;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.Actions
{
	public interface IAgentAction
	{
		string Name { get; }
		string ActionId { get; }
		AgentActionMetadata Metadata { get; }
		AgentActionStatus Status { get; }
		string FailureReason { get; }
		void ApplyActionMetadata(AgentActionMetadata metadata);
		bool IsStillValid(ISimulationWorld world);
		AgentActionStatus Tick();
		void Draw(SpriteBatch sb);
	}
}
