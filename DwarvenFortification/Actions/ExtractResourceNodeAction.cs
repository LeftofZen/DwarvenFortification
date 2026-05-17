using Arch.Core;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace DwarvenFortification.Actions
{
	public sealed class ExtractResourceNodeAction : BaseAgentAction
	{
		readonly Point targetCell;
		readonly float yieldMultiplier;

		public ExtractResourceNodeAction(IActionRuntimeContext runtimeContext, Entity owner, Point targetCell, float yieldMultiplier = 1f) : base(runtimeContext, owner, "extract-resource-node", 1)
		{
			this.targetCell = targetCell;
			this.yieldMultiplier = yieldMultiplier;
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			var cell = world.CellAtCoords(targetCell);
			return cell != null && cell.TryGetResourceNode(out _);
		}

		protected override AgentActionStatus OnTick()
		{
			var cell = runtimeContext.World.CellAtCoords(targetCell);
			if (cell == null)
			{
				return FailAction("Target cell for extraction does not exist.");
			}

			if (!cell.TryExtractResource(out var yieldItemId, out var yieldCount, out var resourceNode))
			{
				return FailAction("No extractable resource node was present in the target cell.");
			}

			cell.RemoveOccupant(resourceNode);
			var effectiveYieldCount = Math.Max(1, (int)Math.Round(yieldCount * yieldMultiplier));
			for (var i = 0; i < effectiveYieldCount; ++i)
			{
				cell.ItemsInCell.Add(runtimeContext.World is GridWorld gridWorld
					? gridWorld.CreateItem(yieldItemId)
					: throw new System.InvalidOperationException("Resource extraction requires the concrete world item factory."));
			}

			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(1, 0));
		}
	}
}