using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public interface IAgentTask
	{
		string Name { get; }
		string ActionId { get; }
		AgentTaskStatus Status { get; }
		string FailureReason { get; }
		bool IsStillValid(ISimulationWorld world);
		AgentTaskStatus Tick();
		void Draw(SpriteBatch sb);
	}
}
