using Arch.Core;
using DwarvenFortification.Simulation.Composition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.Actions
{
	public class TimedAction : BaseAgentAction
	{
		public TimedAction(IActionRuntimeContext runtimeContext, Entity owner, string actionId, int durationTicks) : base(runtimeContext, owner, actionId, durationTicks)
		{
		}

		protected override AgentActionStatus OnTick()
		{
			AdvanceProgress();
			return Progress >= Cost ? CompleteAction() : AgentActionStatus.Running;
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(1, 1));
		}
	}
}