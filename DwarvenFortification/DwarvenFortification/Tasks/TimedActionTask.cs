using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public class TimedActionTask : BaseAgentTask
	{
		public TimedActionTask(ITaskRuntimeContext runtimeContext, Entity owner, string actionId, int durationTicks) : base(runtimeContext, owner, actionId, durationTicks)
		{
		}

		protected override AgentTaskStatus OnTick()
		{
			AdvanceProgress();
			return Progress >= Cost ? CompleteTask() : AgentTaskStatus.Running;
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(1, 1));
		}
	}
}