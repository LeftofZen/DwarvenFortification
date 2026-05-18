using EpPathFinding.cs;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.Simulation.Pathfinding
{
	public sealed class EpPathFindingGridPathfinder : IGridPathfinder
	{
		public bool TryFindPath(GridPathRequest request, out IReadOnlyList<Point> pathCells)
		{
			pathCells = Array.Empty<Point>();

			var walkableCells = request.WalkableCells;
			ArgumentNullException.ThrowIfNull(walkableCells);

			var height = walkableCells.GetLength(0);
			var width = walkableCells.GetLength(1);
			if (!IsWithinBounds(request.StartCell, width, height)
				|| !IsWithinBounds(request.DestinationCell, width, height))
			{
				return false;
			}

			var navGrid = new StaticGrid(width, height);
			for (var y = 0; y < height; ++y)
			{
				for (var x = 0; x < width; ++x)
				{
					navGrid.SetWalkableAt(x, y, walkableCells[y, x]);
				}
			}

			var jpsParam = new JumpPointParam(
				navGrid,
				new GridPos(request.StartCell.X, request.StartCell.Y),
				new GridPos(request.DestinationCell.X, request.DestinationCell.Y),
				EndNodeUnWalkableTreatment.Disallow,
				DiagonalMovement.Always,
				HeuristicMode.EuclideanSquared);

			var path = JumpPointFinder.FindPath(jpsParam);
			if (path == null || path.Count == 0)
			{
				return false;
			}

			pathCells = [.. path.Select(node => new Point(node.X, node.Y))];
			return true;
		}

		static bool IsWithinBounds(Point cell, int width, int height)
			=> cell.X >= 0 && cell.X < width && cell.Y >= 0 && cell.Y < height;
	}
}
