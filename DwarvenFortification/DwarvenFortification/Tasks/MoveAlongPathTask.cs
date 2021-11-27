using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public class MoveAlongPathTask : BaseAgentTask
	{

		public MoveAlongPathTask(Agent owner, IEnumerable<Point> path) : base(owner)
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

		public override bool Update()
		{
			if (currentGoal == Point.Zero)
				currentGoal = Path.Peek();

			if (!success)
			{
				var direction = (currentGoal - owner.Position).ToVector2();
				var distance = direction.Length();

				if (Cost == 0)
				{
					Cost = (int)distance;
				}

				direction.Normalize();
				if (distance < owner.Speed)
				{
					Path.Dequeue();
					owner.Position = currentGoal; // need to account for this extra distance in the % to goal
					if (Path.TryPeek(out Point node))
					{
						currentGoal = node;
					}
					else
					{
						// at the very end
						success = true;
						Progress = Cost;
					}
				}
				else
				{
					var distanceTravelled = direction * owner.Speed;
					var newPos = owner.Position + distanceTravelled.ToPoint();
					totalDistanceAlongPath += distanceTravelled.Length();

					//if (owner.world.CellTypeAtXY(newPos.X, newPos.Y) != CellType.Water)
					{
						owner.Position = newPos;
						Progress = (int)totalDistanceAlongPath; // / totalPathDistance;
					}
				}

			}

			return base.Update();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(8, 0);
			Draw(sb, tileIndex);

			// draw path if moving
			var previousMoveToPoint = owner.Position;
			foreach (var node in Path)
			{
				sb.DrawLine(previousMoveToPoint.ToVector2(), node.ToVector2(), Color.RosyBrown, 2);
				previousMoveToPoint = node;
			}
		}
	}
}
