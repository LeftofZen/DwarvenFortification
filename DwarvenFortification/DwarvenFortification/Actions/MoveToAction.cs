using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace DwarvenFortification
{
	public class MoveToAction : BaseAgentAction
	{
		const string _actionId = "move-to";

		public MoveToAction(IActionRuntimeContext runtimeContext, Entity owner, Point goal) : base(runtimeContext, owner, _actionId)
		{
			this.Goal = goal;
		}

		public Point Goal;

		protected override AgentActionStatus OnTick()
		{
			var direction = (Goal - owner.GetPosition()).ToVector2();
			var distance = direction.Length();

			if (distance <= float.Epsilon)
			{
				owner.SetPosition(Goal);
				return CompleteAction();
			}

			if (Cost == 0)
			{
				Cost = (int)distance;
			}

			direction.Normalize();
			if (distance < owner.GetSpeed())
			{
				owner.SetPosition(Goal);
				return CompleteAction();
			}

			var newPos = owner.GetPosition() + (direction * owner.GetSpeed()).ToPoint();
			owner.SetPosition(newPos);
			Progress = Math.Clamp(Cost - (int)(Goal - owner.GetPosition()).ToVector2().Length(), 0, Cost);
			return AgentActionStatus.Running;
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(8, 0);
			Draw(sb, tileIndex);
		}
	}
}
