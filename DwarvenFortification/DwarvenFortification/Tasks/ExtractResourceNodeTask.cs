using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public sealed class ExtractResourceNodeTask : BaseAgentTask
	{
		readonly Point targetCell;

		public ExtractResourceNodeTask(ITaskRuntimeContext runtimeContext, Entity owner, Point targetCell) : base(runtimeContext, owner, "extract-resource-node", 1)
		{
			this.targetCell = targetCell;
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			var cell = world.CellAtCoords(targetCell);
			return cell != null && cell.TryGetResourceNode(out _);
		}

		protected override AgentTaskStatus OnTick()
		{
			var cell = runtimeContext.World.CellAtCoords(targetCell);
			if (cell == null)
			{
				return FailTask("Target cell for extraction does not exist.");
			}

			if (!cell.TryExtractResource(out var yieldItemId, out var yieldCount, out var resourceNode))
			{
				return FailTask("No extractable resource node was present in the target cell.");
			}

			cell.RemoveOccupant(resourceNode);
			for (var i = 0; i < yieldCount; ++i)
			{
				cell.ItemsInCell.Add(runtimeContext.World is GridWorld gridWorld
					? gridWorld.CreateItem(yieldItemId)
					: throw new System.InvalidOperationException("Resource extraction requires the concrete world item factory."));
			}

			AdvanceProgress(Cost);
			return CompleteTask();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(1, 0));
		}
	}
}