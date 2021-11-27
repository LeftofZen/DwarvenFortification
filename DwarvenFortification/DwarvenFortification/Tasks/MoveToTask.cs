using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public class MoveToTask : BaseAgentTask
	{
		const int _cost = 0;

		public MoveToTask(Agent owner, Point goal) : base(owner, _cost)
		{
			this.Goal = goal;
		}

		public Point Goal;

		public override bool Update()
		{
			if (!success)
			{
				var direction = (Goal - owner.Position).ToVector2();
				var distance = direction.Length();

				if (Cost == 0)
				{
					Cost = (int)distance;
				}

				direction.Normalize();
				if (distance < owner.Speed)
				{
					owner.Position = Goal;
					success = true;
					Progress = Cost;
				}
				else
				{
					var newPos = owner.Position + (direction * owner.Speed).ToPoint();

					//if (owner.world.CellTypeAtXY(newPos.X, newPos.Y) != CellType.Water)
					{
						owner.Position = newPos;
						Progress = Cost - (int)(Goal - owner.Position).ToVector2().Length();
					}
				}

			}

			return base.Update();
		}

		public override void Draw(SpriteBatch sb)
		{
			var tileIndex = new Point(8, 0);
			Draw(sb, tileIndex);
		}
	}
}
