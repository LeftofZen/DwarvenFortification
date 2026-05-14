using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace DwarvenFortification
{
	public class MoveToTask : BaseAgentTask
	{
		const string _actionId = "move-to";

		public MoveToTask(ITaskRuntimeContext runtimeContext, Entity owner, Point goal) : base(runtimeContext, owner, _actionId)
		{
			this.Goal = goal;
		}

		public Point Goal;

		protected override AgentTaskStatus OnTick()
		{
			var direction = (Goal - owner.GetPosition()).ToVector2();
			var distance = direction.Length();

			if (distance <= float.Epsilon)
			{
				owner.SetPosition(Goal);
				return CompleteTask();
			}

			if (Cost == 0)
			{
				Cost = (int)distance;
			}

			direction.Normalize();
			if (distance < owner.GetSpeed())
			{
				owner.SetPosition(Goal);
				return CompleteTask();
			}

			var newPos = owner.GetPosition() + (direction * owner.GetSpeed()).ToPoint();
			owner.SetPosition(newPos);
			Progress = Math.Clamp(Cost - (int)(Goal - owner.GetPosition()).ToVector2().Length(), 0, Cost);
			return AgentTaskStatus.Running;
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(8, 0);
			Draw(sb, tileIndex);
		}
	}
}
