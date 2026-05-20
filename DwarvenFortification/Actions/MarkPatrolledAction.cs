using Arch.Core;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.Actions
{
	public class MarkPatrolledAction : BaseAgentAction
	{
		readonly int durationTicks;

		public MarkPatrolledAction(IActionRuntimeContext runtimeContext, Entity owner, int durationTicks) : base(runtimeContext, owner, "mark-patrolled", 1) => this.durationTicks = durationTicks;

		protected override AgentActionStatus OnTick()
		{
			owner.SetPatrolled(durationTicks);
			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb) => Draw(sb, new Point(6, 0));
	}
}