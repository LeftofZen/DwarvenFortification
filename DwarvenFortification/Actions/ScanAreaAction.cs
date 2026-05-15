using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace DwarvenFortification
{
	public class ScanAreaAction : BaseAgentAction
	{
		readonly int radiusCells;
		readonly int memoryDurationTicks;

		public ScanAreaAction(IActionRuntimeContext runtimeContext, Entity owner, int radiusCells, int memoryDurationTicks) : base(runtimeContext, owner, "scan-area", 1)
		{
			this.radiusCells = radiusCells;
			this.memoryDurationTicks = memoryDurationTicks;
		}

		protected override AgentActionStatus OnTick()
		{
			var world = runtimeContext.World;
			var ownerCell = world.CoordsAtXY(owner.GetPosition());
			var enemy = world.GetAgents()
				.Where(other => !other.Equals(owner) && !string.Equals(other.GetFactionId(), owner.GetFactionId(), System.StringComparison.OrdinalIgnoreCase))
				.OrderBy(other => Vector2.DistanceSquared(world.CoordsAtXY(other.GetPosition()).ToVector2(), ownerCell.ToVector2()))
				.FirstOrDefault();

			if (!enemy.Equals(default(Entity)))
			{
				var enemyCell = world.CoordsAtXY(enemy.GetPosition());
				if (Vector2.DistanceSquared(enemyCell.ToVector2(), ownerCell.ToVector2()) <= radiusCells * radiusCells)
				{
					owner.SetEnemyVisible(true, enemyCell, memoryDurationTicks);
				}
				else
				{
					owner.SetEnemyVisible(false, new Point(-1, -1), memoryDurationTicks);
				}
			}
			else
			{
				owner.SetEnemyVisible(false, new Point(-1, -1), memoryDurationTicks);
			}

			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(4, 0));
		}
	}
}