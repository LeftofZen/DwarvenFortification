using Arch.Core;
using Arch.Core.Extensions;
using EpPathFinding.cs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using DwarvenFortification.Logging;

namespace DwarvenFortification
{
	public class GridWorld : ISimulationWorld
	{
		GridCell[,] world;
		BaseGrid navGrid;

		List<Entity> agents;
		int cellSize = 16;

		MouseState previousMouseState;
		readonly IAgentRuntime agentRuntime;
		readonly SimulationDefinitionRegistry definitions;
		readonly ISimulationEntityFactory entityFactory;
		readonly SimulationRenderAssets renderAssets;
		readonly ITaskRuntimeContext taskRuntimeContext;
		readonly ImGuiSimulationUi ui;

		public GridWorld(int width, int height, IAgentRuntime agentRuntime, SimulationDefinitionRegistry definitions, ISimulationEntityFactory entityFactory, SimulationRenderAssets renderAssets, ITaskRuntimeContext taskRuntimeContext, ImGuiSimulationUi ui)
		{
			this.agentRuntime = agentRuntime;
			this.definitions = definitions;
			this.entityFactory = entityFactory;
			this.renderAssets = renderAssets;
			this.taskRuntimeContext = taskRuntimeContext;
			this.ui = ui;
			agents = new List<Entity>();

			for (var i = 0; i < 10; ++i)
			{
				agents.Add(entityFactory.CreateAgent($"Agent{i}", new Point(100 + i, 100 + i)));
			}

			world = new GridCell[height, width];
			navGrid = new StaticGrid(width, height);

			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{

					// island
					if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
					{
						world[y, x] = new GridCell(CellType.Water, definitions, entityFactory, renderAssets);
						navGrid.SetWalkableAt(x, y, false);
					}
					else
					{
						world[y, x] = new GridCell(CellType.Dirt, definitions, entityFactory, renderAssets);
						navGrid.SetWalkableAt(x, y, world[y, x].IsWalkable);
					}
				}
			}
		}

		public Point CoordsAtXY(Point p)
			=> CoordsAtXY(p.X, p.Y);

		public Point CoordsAtXY(int x, int y)
		{
			var coords = new Point(x / cellSize, y / cellSize);
			if (coords.X >= 0 && coords.X < Width && coords.Y >= 0 && coords.Y < Height)
			{
				return coords;
			}
			return new Point(-1, -1);
		}

		public GridCell CellAtXY(int x, int y)
		{
			var cell = CoordsAtXY(x, y);
			if (cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height)
			{
				return world[cell.Y, cell.X];
			}
			return null;
		}

		public GridCell CellAtXY(Point location)
		{
			var cell = CoordsAtXY(location.X, location.Y);
			if (cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height)
			{
				return world[cell.Y, cell.X];
			}
			return null;
		}

		public Rectangle CellBoundsAt(int x, int y)
		{
			var cell = CoordsAtXY(x, y);
			if (cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height)
			{
				return new Rectangle(x - x % cellSize, y - y % cellSize, cellSize, cellSize);
			}
			return Rectangle.Empty;
		}

		public CellType CellTypeAtXY(int x, int y)
		{
			var cell = CellAtXY(x, y);
			if (cell != null)
				return cell.CellType;
			else
				return CellType.Null;
		}

		public IEnumerable<(GridCell cell, Point point)> ClosestNCellsXYOfType(int X, int Y, Func<GridCell, bool> func, int count = 1)
		{
			var matching = new List<(GridCell cell, Point xy)>();
			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					if (func(world[y, x]))
					{
						matching.Add((world[y, x], new Point(x * cellSize, y * cellSize)));
					}
				}
			}

			return matching.OrderBy(cell => (cell.xy.ToVector2() - new Vector2(X, Y)).LengthSquared()).Take(count);
		}

		public Point CentreOfCellWithCoords(int x, int y)
		{
			return new Point(x * cellSize + cellSize / 2, y * cellSize + cellSize / 2);
		}

		public Point CentreOfCellWithCoords(Point p)
		{
			return CentreOfCellWithCoords(p.X, p.Y);
		}
		public Point CentreOfCellWithXY(Point p)
		{
			return CentreOfCellWithCoords(CoordsAtXY(p));
		}

		internal Entity CreateItem(string itemId)
			=> entityFactory.CreateItem(itemId);

		public GridCell CellAtCoords(Point coords)
		{
			if (coords.X >= 0 && coords.X < Width && coords.Y >= 0 && coords.Y < Height)
			{
				return world[coords.Y, coords.X];
			}

			return null;
		}

		public IEnumerable<(GridCell cell, Point point, Point coords)> EnumerateCells()
		{
			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					yield return (world[y, x], CentreOfCellWithCoords(x, y), new Point(x, y));
				}
			}
		}

		public bool CellContainsItem(Point coords, string itemId)
		{
			var cell = CellAtCoords(coords);
			if (cell == null)
			{
				return false;
			}

			if (cell.ItemsInCell.Any(item => string.Equals(item.GetItemDefinitionId(), itemId, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}

			if (cell.TryGetStorageOccupant(out var worldObject) && worldObject.Has<InventoryComponent>())
			{
				return worldObject.Get<InventoryComponent>().Items.Any(item => string.Equals(item.GetItemDefinitionId(), itemId, StringComparison.OrdinalIgnoreCase));
			}

			return false;
		}

		public bool TryGetItemEntity(Point coords, string itemId, out Entity itemEntity)
		{
			itemEntity = default;
			var cell = CellAtCoords(coords);
			if (cell == null)
			{
				return false;
			}

			itemEntity = cell.ItemsInCell.FirstOrDefault(item => string.Equals(item.GetItemDefinitionId(), itemId, StringComparison.OrdinalIgnoreCase));
			if (!itemEntity.Equals(default(Entity)))
			{
				return true;
			}

			if (cell.TryGetStorageOccupant(out var worldObject) && worldObject.Has<InventoryComponent>())
			{
				itemEntity = worldObject.Get<InventoryComponent>().Items.FirstOrDefault(item => string.Equals(item.GetItemDefinitionId(), itemId, StringComparison.OrdinalIgnoreCase));
				return !itemEntity.Equals(default(Entity));
			}

			return false;
		}

		public bool TryFindNearestItemLocation(string itemId, Point origin, out Point itemCell)
		{
			var match = EnumerateCells()
				.Where(entry => CellContainsItem(entry.coords, itemId))
				.OrderBy(entry => Vector2.DistanceSquared(entry.coords.ToVector2(), origin.ToVector2()))
				.FirstOrDefault();

			if (match.cell == null)
			{
				itemCell = Point.Zero;
				return false;
			}

			itemCell = match.coords;
			return true;
		}

		public IReadOnlyList<Entity> GetAgents()
			=> agents;

		public IEnumerable<Point> GetNearbyWalkableCells(Point origin, int radius)
		{
			for (var y = Math.Max(0, origin.Y - radius); y <= Math.Min(Height - 1, origin.Y + radius); ++y)
			{
				for (var x = Math.Max(0, origin.X - radius); x <= Math.Min(Width - 1, origin.X + radius); ++x)
				{
					if (world[y, x].IsWalkable)
					{
						yield return new Point(x, y);
					}
				}
			}
		}

		public bool TryFindPatrolRoute(Point origin, int radius, int waypointCount, out IReadOnlyList<Point> route)
		{
			var candidates = GetNearbyWalkableCells(origin, radius)
				.Where(cell => cell != origin)
				.OrderByDescending(cell => Vector2.DistanceSquared(cell.ToVector2(), origin.ToVector2()))
				.Take(waypointCount)
				.ToArray();

			route = candidates;
			return candidates.Length > 0;
		}

		public bool TryFindHideDestination(Point origin, Point dangerCell, int radius, out Point hideCell)
		{
			var candidate = GetNearbyWalkableCells(origin, radius)
				.OrderByDescending(cell => Vector2.DistanceSquared(cell.ToVector2(), dangerCell.ToVector2()))
				.FirstOrDefault();

			if (candidate == default && !CellAtCoords(origin).IsWalkable)
			{
				hideCell = Point.Zero;
				return false;
			}

			hideCell = candidate == default ? origin : candidate;
			return true;
		}

		public bool TryFindActionDestinationCell(Point agentCell, Point targetCell, string destinationMode, out Point destinationCell)
		{
			if (string.Equals(destinationMode, "adjacent", StringComparison.OrdinalIgnoreCase))
			{
				var candidates = new List<Point>();
				for (var y = targetCell.Y - 1; y <= targetCell.Y + 1; ++y)
				{
					for (var x = targetCell.X - 1; x <= targetCell.X + 1; ++x)
					{
						if ((x == targetCell.X && y == targetCell.Y) || x < 0 || x >= Width || y < 0 || y >= Height)
						{
							continue;
						}

						if (world[y, x].IsWalkable)
						{
							candidates.Add(new Point(x, y));
						}
					}
				}

				if (candidates.Count == 0)
				{
					destinationCell = Point.Zero;
					return false;
				}

				destinationCell = candidates
					.OrderBy(cell => Vector2.DistanceSquared(cell.ToVector2(), agentCell.ToVector2()))
					.First();
				return true;
			}

			destinationCell = targetCell;
			return destinationCell.X >= 0 && destinationCell.X < Width && destinationCell.Y >= 0 && destinationCell.Y < Height && world[destinationCell.Y, destinationCell.X].IsWalkable;
		}

		public void Update(GameTime gameTime)
		{
			var currMouseState = Mouse.GetState();
			bool selectionBoundThisFrame = false;

			if (!ui.WantsMouseCapture && currMouseState.LeftButton == ButtonState.Pressed)
			{
				var clickedCell = new Point(currMouseState.X / cellSize, currMouseState.Y / cellSize);
				if (clickedCell.X >= 0 && clickedCell.X < Width && clickedCell.Y >= 0 && clickedCell.Y < Height)
				{
					if (ui.SelectedMouseClickMode == MouseClickMode.Select)
					{
						// check if we clicked on agent
						foreach (var a in agents)
						{
							if (a.GetCurrentCell(this) == world[clickedCell.Y, clickedCell.X])
							{
								ui.BindEntity(a);
								selectionBoundThisFrame = true;
								break;
							}
						}
						if (!selectionBoundThisFrame && world[clickedCell.Y, clickedCell.X].TryGetDisplayOccupant(out var worldObject))
						{
							ui.BindEntity(worldObject);
							selectionBoundThisFrame = true;
						}
						if (!selectionBoundThisFrame)
						{
							ui.BindObject(world[clickedCell.Y, clickedCell.X]);
						}
					}
					else if (ui.SelectedMouseClickMode == MouseClickMode.Paint)
					{
						var cell = world[clickedCell.Y, clickedCell.X];
						if (!string.IsNullOrWhiteSpace(ui.SelectedOccupantId))
						{
							if (cell.TryGetDisplayOccupant(out var existingWorldObject))
							{
								definitions.RuntimeWorld.Destroy(existingWorldObject);
								cell.ClearOccupants();
							}

							var cellPoint = new Point(clickedCell.X, clickedCell.Y);
							if (definitions.TryGetWorldObjectDefinitionEntity(ui.SelectedOccupantId, out _))
							{
								cell.AddOccupant(entityFactory.CreateWorldObject(ui.SelectedOccupantId, CentreOfCellWithCoords(cellPoint), cellPoint));
							}
							else if (definitions.TryGetResourceNodeDefinitionEntity(ui.SelectedOccupantId, out _))
							{
								cell.AddOccupant(entityFactory.CreateResourceNode(ui.SelectedOccupantId, CentreOfCellWithCoords(cellPoint), cellPoint));
							}
						}
						else
						{
							if (cell.TryGetDisplayOccupant(out var existingWorldObject))
							{
								definitions.RuntimeWorld.Destroy(existingWorldObject);
								cell.ClearOccupants();
							}

							cell.CellType = ui.SelectedCellType;
						}

						navGrid.SetWalkableAt(clickedCell.X, clickedCell.Y, cell.IsWalkable);
					}
				}
			}

			if (!ui.WantsMouseCapture && currMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton != ButtonState.Pressed)
			{
				if (ui.TryGetBoundEntity(out var agent) && agent.IsAgent())
				{
					var clickedCell = CoordsAtXY(currMouseState.Position.X, currMouseState.Position.Y);
					var agentPosition = agent.GetPosition();
					var agentCell = CoordsAtXY(agentPosition.X, agentPosition.Y);
					if (clickedCell.X != -1 && clickedCell.Y != -1 && agentCell.X != -1 && agentCell.Y != -1)
					{
						PlotPath(agent, clickedCell);

						var cell = CellAtXY(currMouseState.Position.X, currMouseState.Position.Y);
						if (cell != null && cell.ItemsInCell.Count > 0 && !cell.IsStorageCell)
						{
							agent.EnqueueTask(new PickUpTask(taskRuntimeContext, agent, cell.ItemsInCell.First()));
						}
						else if (cell != null && cell.IsStorageCell)
						{
							var inventory = agent.GetInventory();
							if (inventory.Count > 0)
							{
								var storableItems = inventory.Where(cell.CanStore).ToList();
								if (storableItems.Count > 0)
								{
									agent.EnqueueTask(new PutDownTask(taskRuntimeContext, agent, storableItems));
								}
							}
						}
					}

					//if (CellTypeAtXY(currMouseState.Position.X, currMouseState.Position.Y) == CellType.Ore)
					//{
					//	var oreItem = new Item("ore");
					//	agent.AddTask(new MoveToTask(agent, currMouseState.Position));
					//	agent.AddTask(new PickUpTask(agent, oreItem));
					//	var closestDropoff = ClosestCellXYOfType(agent.X, agent.Y, CellType.Storage);
					//	agent.AddTask(new MoveToTask(agent, closestDropoff));
					//	agent.AddTask(new PutDownTask(agent, oreItem));
					//}
					//else
					//{
					//	agent.AddTask(new MoveToTask(agent, currMouseState.Position));
					//}
				}
			}

			previousMouseState = currMouseState;

			var keyboard = Keyboard.GetState();

			if (keyboard.IsKeyDown(Keys.D1))
			{
				ui.SelectedCellType = (CellType)0;
				ui.SelectedOccupantId = string.Empty;
			}
			if (keyboard.IsKeyDown(Keys.D2))
			{
				ui.SelectedCellType = (CellType)1;
				ui.SelectedOccupantId = string.Empty;
			}
			if (keyboard.IsKeyDown(Keys.D3))
			{
				ui.SelectedCellType = (CellType)2;
				ui.SelectedOccupantId = string.Empty;
			}
			if (keyboard.IsKeyDown(Keys.D4))
			{
				ui.SelectedCellType = (CellType)3;
				ui.SelectedOccupantId = string.Empty;
			}

			foreach (var agent in agents)
			{
				agentRuntime.Update(this, agent);
			}
		}

		public void PlotPath(Entity agent, Point dstCell)
		{
			var agentPosition = agent.GetPosition();
			var agentCell = CoordsAtXY(agentPosition.X, agentPosition.Y);
			// recreate nav grid every time (!)
			var newg = navGrid.Clone();
			newg.Reset();
			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					newg.SetWalkableAt(x, y, world[y, x].IsWalkable);
				}
			}
			navGrid = newg;

			var last = agent.GetCurrentPathGoal();
			var lastCell = CoordsAtXY(last.X, last.Y);

			var jpsParam = new JumpPointParam(
				navGrid,
				last == Point.Zero ? new GridPos(agentCell.X, agentCell.Y) : new GridPos(lastCell.X, lastCell.Y),
				new GridPos(dstCell.X, dstCell.Y),
				EndNodeUnWalkableTreatment.Disallow,
				DiagonalMovement.Always,
				HeuristicMode.EuclideanSquared);

			var path = JumpPointFinder.FindPath(jpsParam);
			var pathPoints = path.Select(node => CentreOfCellWithCoords(node.X, node.Y));
			agent.EnqueueTask(new MoveAlongPathTask(taskRuntimeContext, agent, pathPoints));
		}

		public int Height => world.GetLength(0);

		public int Width => world.GetLength(1);

		public void Draw(SpriteBatch sb)
		{
			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					world[y, x].Draw(sb, new Point(x, y), cellSize);
				}
			}

			foreach (var agent in agents)
			{
				agentRuntime.Draw(sb, this, agent);
			}

		}
	}
}
