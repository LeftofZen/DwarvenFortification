using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public class MoveAlongPathAction : BaseAgentAction
	{
		const string _actionId = "move-path";

		public MoveAlongPathAction(IActionRuntimeContext runtimeContext, Entity owner, IEnumerable<Point> path) : base(runtimeContext, owner, _actionId)
		{
			this.Path = new Queue<Point>(path);
			var distances = Path.Zip(Path.Skip(1), Distance);
			Cost = (int)distances.Sum();

			totalDistanceAlongPath = 0f;
		}

		float Distance(Point p1, Point p2)
		{
			float dx = p1.X - p2.X;
			float dy = p1.Y - p2.Y;
			return (float)Math.Sqrt(dx * dx + dy * dy);
		}

		public Queue<Point> Path;
		Point currentGoal;
		//float totalPathDistance;
		float totalDistanceAlongPath;

		public Point Destination => Path.Count > 0 ? Path.Last() : currentGoal;

		protected override bool CanStart()
			=> Path.Count > 0;

		protected override string BuildCannotStartReason()
			=> "MoveAlongPathTask requires a non-empty path.";

		protected override AgentActionStatus OnTick()
		{
			if (currentGoal == Point.Zero)
				currentGoal = Path.Peek();

			var direction = (currentGoal - owner.GetPosition()).ToVector2();
			var distance = direction.Length();

			if (distance <= float.Epsilon)
			{
				Path.Dequeue();
				owner.SetPosition(currentGoal);
				if (!Path.TryPeek(out var nextNode))
				{
					return CompleteAction();
				}

				currentGoal = nextNode;
				return AgentActionStatus.Running;
			}

			if (Cost == 0)
			{
				Cost = (int)distance;
			}

			direction.Normalize();
			if (distance < owner.GetSpeed())
			{
				Path.Dequeue();
				owner.SetPosition(currentGoal);
				if (Path.TryPeek(out var node))
				{
					currentGoal = node;
					return AgentActionStatus.Running;
				}

				return CompleteAction();
			}

			var distanceTravelled = direction * owner.GetSpeed();
			var newPos = owner.GetPosition() + distanceTravelled.ToPoint();
			totalDistanceAlongPath += distanceTravelled.Length();
			owner.SetPosition(newPos);
			Progress = Math.Clamp((int)totalDistanceAlongPath, 0, Cost);

			return AgentActionStatus.Running;
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(8, 0);
			Draw(sb, tileIndex);

			// draw path if moving
			var previousMoveToPoint = owner.GetPosition();
			foreach (var node in Path)
			{
				sb.DrawLine(previousMoveToPoint.ToVector2(), node.ToVector2(), Color.RosyBrown, 2);
				previousMoveToPoint = node;
			}
		}
	}
}
