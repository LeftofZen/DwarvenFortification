using Arch.Core;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.Actions
{
	public class SetHiddenAction : BaseAgentAction
	{
		readonly int durationTicks;

		public SetHiddenAction(IActionRuntimeContext runtimeContext, Entity owner, int durationTicks) : base(runtimeContext, owner, "set-hidden", 1)
		{
			this.durationTicks = durationTicks;
		}

		protected override AgentActionStatus OnTick()
		{
			owner.SetHidden(durationTicks);
			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(5, 0));
		}
	}
}