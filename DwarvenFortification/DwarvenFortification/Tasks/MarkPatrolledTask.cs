using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public class MarkPatrolledTask : BaseAgentTask
	{
		readonly int durationTicks;

		public MarkPatrolledTask(ITaskRuntimeContext runtimeContext, Entity owner, int durationTicks) : base(runtimeContext, owner, "mark-patrolled", 1)
		{
			this.durationTicks = durationTicks;
		}

		protected override AgentTaskStatus OnTick()
		{
			owner.SetPatrolled(durationTicks);
			AdvanceProgress(Cost);
			return CompleteTask();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(6, 0));
		}
	}
}