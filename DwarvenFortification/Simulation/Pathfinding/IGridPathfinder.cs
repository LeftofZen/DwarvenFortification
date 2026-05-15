using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace DwarvenFortification.Simulation.Pathfinding
{
	public readonly record struct GridPathRequest(
		bool[,] WalkableCells,
		Point StartCell,
		Point DestinationCell);

	public interface IGridPathfinder
	{
		bool TryFindPath(GridPathRequest request, out IReadOnlyList<Point> pathCells);
	}
}