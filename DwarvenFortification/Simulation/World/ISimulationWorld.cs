using Arch.Core;
using DwarvenFortification.UI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace DwarvenFortification.Simulation.World
{
	public interface ISimulationWorld
	{
		Point CoordsAtXY(Point point);
		Point CoordsAtXY(int x, int y);
		Point CentreOfCellWithCoords(Point point);
		Point CentreOfCellWithCoords(int x, int y);
		GridCell CellAtXY(Point location);
		GridCell CellAtXY(int x, int y);
		GridCell CellAtCoords(Point coords);
		Rectangle CellBoundsAt(int x, int y);
		IEnumerable<(GridCell cell, Point point, Point coords)> EnumerateCells();
		bool CellContainsItem(Point coords, string itemId);
		bool TryGetItemEntity(Point coords, string itemId, out Entity itemEntity);
		bool TryFindNearestItemLocation(string itemId, Point origin, out Point itemCell);
		bool TryFindNearestItemLocationByTag(string[] tags, Point origin, out Point itemCell, out string matchedItemId);
		IReadOnlyList<Entity> GetAgents();
		bool TryFindPatrolRoute(Point origin, int radius, int waypointCount, out IReadOnlyList<Point> route);
		bool TryFindHideDestination(Point origin, Point dangerCell, int radius, out Point hideCell);
		bool TryFindActionDestinationCell(Point agentCell, Point targetCell, string destinationMode, out Point destinationCell);
		void PlotPath(Entity agent, Point destinationCell, AgentActionMetadata metadata = null);
	}
	}