using EpPathFinding.cs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public enum CellType { Grass, Dirt, Stone, Ore, Water, Storage, Null }

	public enum MouseClickMode { None, Paint, Select }

	public interface IUIControl
	{
		public Rectangle Bounds { get; set; }
		public void Update(GameTime gameTime, ref object param);
		public void Draw(SpriteBatch sb, ref object param);
	}

	public interface IEnumUIControl : IUIControl
	{ }

	public class EnumUIControl<T> where T : Enum
	{
		public Rectangle Bounds { get; set; }
		public Size Size;
		public int Count => Enum.GetNames(typeof(T)).Length;
		Dictionary<T, Color> Colours;

		public EnumUIControl(Rectangle bounds, Dictionary<T, Color> colours = null)
		{
			Bounds = bounds;
			Colours = colours;
		}

		public void Update(GameTime gameTime, ref T param)
		{
			var mouse = Mouse.GetState();
			if (Bounds.Contains(mouse.Position) && mouse.LeftButton == ButtonState.Pressed)
			{
				var offset = mouse.Position - Bounds.Location;
				var index = offset.X / (Bounds.Width / Count);
				param = (T)(object)index;
			}
		}

		public void Draw(SpriteBatch sb, T param)
		{
			var cellWidth = Bounds.Width / Count;
			var cellHeight = Bounds.Height;
			for (var i = 0; i < Count; ++i)
			{
				var colour = ((T)(object)i).CompareTo(param) == 0 ? Color.LightGray : Color.White;
				if (Colours != null)
				{
					colour = Colours[(T)(object)i];
				}
				sb.FillRectangle(Bounds.X + i * cellWidth, Bounds.Y, cellWidth, cellHeight, colour);
				sb.DrawRectangle(Bounds.X + i * cellWidth, Bounds.Y, cellWidth, cellHeight, Color.Black);
				sb.DrawString(
					GameServices.Fonts["Calibri"],
					((T)(object)i).ToString(),
					new Vector2(Bounds.X + (i * cellWidth) + 4, Bounds.Y + 4),
					Color.Black);
			}

			// selected cell
			sb.DrawRectangle(Bounds.X + ((int)(object)param) * cellWidth, Bounds.Y, cellWidth, cellHeight, Color.Red, 3);
		}
	}

	public class GridWorld
	{
		GridCell[,] world;
		BaseGrid navGrid;

		List<Agent> agents;
		int cellSize = 32;

		MouseState previousMouseState;
		DebugGui debugGui;

		CellType selectedCellType = CellType.Dirt;
		MouseClickMode selectedMouseClickMode = MouseClickMode.None;

		EnumUIControl<MouseClickMode> mouseClickModeUI;
		EnumUIControl<CellType> cellTypeUI;

		public GridWorld(int width, int height)
		{
			agents = new List<Agent>();

			agents.Add(new Agent(100, 100, this));
			//agents.Add(new Agent(150, 100, this));
			//agents.Add(new Agent(190, 100, this));
			//agents.Add(new Agent(230, 100, this));

			debugGui = new DebugGui();
			debugGui.Bounds = new Rectangle(width * cellSize, 0, 400, 800);

			world = new GridCell[height, width];
			navGrid = new StaticGrid(width, height);

			//var rnd = new Random();

			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					// random
					//world[y, x] = new GridCell((CellType)rnd.Next(0, 5));

					// island
					if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
					{
						world[y, x] = new GridCell(CellType.Water);
						navGrid.SetWalkableAt(x, y, false);
					}
					else
					{
						world[y, x] = new GridCell(CellType.Dirt);
						navGrid.SetWalkableAt(x, y, true);
					}
				}
			}

			// ui setup
			var currentY = Height * cellSize;
			mouseClickModeUI = new EnumUIControl<MouseClickMode>(new Rectangle(0, currentY, Enum.GetNames(typeof(MouseClickMode)).Length * 2 * cellSize, cellSize));
			currentY += mouseClickModeUI.Bounds.Height;
			cellTypeUI = new EnumUIControl<CellType>(new Rectangle(0, currentY, Enum.GetNames(typeof(CellType)).Length * 2 * cellSize, cellSize), GridCell.CellLookup);
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

		public void Update(GameTime gameTime)
		{
			var currMouseState = Mouse.GetState();
			bool debugGuiSetThisFrame = false;

			mouseClickModeUI.Update(gameTime, ref selectedMouseClickMode);
			cellTypeUI.Update(gameTime, ref selectedCellType);

			if (currMouseState.LeftButton == ButtonState.Pressed)
			{
				var clickedCell = new Point(currMouseState.X / cellSize, currMouseState.Y / cellSize);
				if (clickedCell.X >= 0 && clickedCell.X < Width && clickedCell.Y >= 0 && clickedCell.Y < Height)
				{
					if (selectedMouseClickMode == MouseClickMode.Select)
					{
						// check if we clicked on agent
						foreach (var a in agents)
						{
							if (a.CurrentCell == world[clickedCell.Y, clickedCell.X])
							{
								debugGui.BoundObject = a;
								debugGuiSetThisFrame = true;
								break;
							}
						}
						if (!debugGuiSetThisFrame)
						{
							debugGui.BoundObject = world[clickedCell.Y, clickedCell.X];
						}
					}
					else if (selectedMouseClickMode == MouseClickMode.Paint)
					{
						world[clickedCell.Y, clickedCell.X].CellType = selectedCellType;
						navGrid.SetWalkableAt(clickedCell.X, clickedCell.Y, selectedCellType != CellType.Water);
					}
				}
			}

			if (currMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton != ButtonState.Pressed)
			{
				if (debugGui.BoundObject != null && debugGui.BoundObject.GetType().IsAssignableFrom(typeof(Agent)))
				{
					var agent = (Agent)debugGui.BoundObject;
					var clickedCell = CoordsAtXY(currMouseState.Position.X, currMouseState.Position.Y);
					var agentCell = CoordsAtXY(agent.X, agent.Y);
					if (clickedCell.X != -1 && clickedCell.Y != -1 && agentCell.X != -1 && agentCell.Y != -1)
					{
						PlotPath(agent, clickedCell);

						var cell = CellAtXY(currMouseState.Position.X, currMouseState.Position.Y);
						if (cell.CellType == CellType.Ore || cell.CellType == CellType.Stone)
						{
							if (cell.ItemsInCell.Count > 0)
							{
								agent.AddTask(new PickUpTask(agent, cell.ItemsInCell.First()));
							}
						}
						else if (cell.CellType == CellType.Storage)
						{
							if (agent.Inventory.Count > 0)
							{
								// puts down all items of same type at once
								var first = agent.Inventory.First();
								var allFirst = agent.Inventory.Where(i => i.Name == first.Name);
								agent.AddTask(new PutDownTask(agent, allFirst));
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
				selectedCellType = (CellType)0;
			}
			if (keyboard.IsKeyDown(Keys.D2))
			{
				selectedCellType = (CellType)1;
			}
			if (keyboard.IsKeyDown(Keys.D3))
			{
				selectedCellType = (CellType)2;
			}
			if (keyboard.IsKeyDown(Keys.D4))
			{
				selectedCellType = (CellType)3;
			}
			if (keyboard.IsKeyDown(Keys.D5))
			{
				selectedCellType = (CellType)4;
			}
			if (keyboard.IsKeyDown(Keys.D6))
			{
				selectedCellType = (CellType)5;
			}
			if (keyboard.IsKeyDown(Keys.D7))
			{
				selectedCellType = (CellType)6;
			}

			foreach (var agent in agents)
			{
				agent.Update(gameTime);
			}

			//debugGui.BoundObject = agents.First();

			debugGui.Update(gameTime);
		}

		public void PlotPath(Agent agent, Point dstCell)
		{
			var agentCell = CoordsAtXY(agent.X, agent.Y);
			// recreate nav grid every time (!)
			var newg = navGrid.Clone();
			newg.Reset();
			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					newg.SetWalkableAt(x, y, world[y, x].CellType != CellType.Water);
				}
			}
			navGrid = newg;

			var last = agent.LastPointInPath();
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
			agent.AddTask(new MoveAlongPathTask(agent, pathPoints));
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
				agent.Draw(sb);
			}

			// gui


			mouseClickModeUI.Draw(sb, selectedMouseClickMode);
			cellTypeUI.Draw(sb, selectedCellType);

			//// selection rect background
			////sb.FillRectangle(0, Height * cellSize, 5 * cellSize, cellSize, Color.Black);
			//// selections
			//for (var i = 0; i < 7; ++i)
			//{
			//	sb.FillRectangle(i * cellSize, Height * cellSize, cellSize, cellSize, GridCell.CellLookup[(CellType)i]);
			//}
			//// selected cell
			//sb.DrawRectangle((int)selectedCellTypeForDraw * cellSize, Height * cellSize, cellSize, cellSize, Color.Black, 3);

			//// mouse click mode
			//for (var i = 0; i < 3; ++i)
			//{
			//	sb.FillRectangle(i * (cellSize * 2), Height * cellSize + cellSize, cellSize * 2, cellSize, (MouseClickMode)i == mouseClickMode ? Color.LightGray : Color.White);
			//	sb.DrawRectangle(i * (cellSize * 2), Height * cellSize + cellSize, cellSize * 2, cellSize, Color.Black);
			//	sb.DrawString(
			//		GameServices.Fonts["Calibri"],
			//		((MouseClickMode)i).ToString(),
			//		new Vector2(i * (cellSize * 2) + 4, Height * cellSize + cellSize + 4),
			//		Color.Black);
			//}
			//// selected cell
			//sb.DrawRectangle((int)mouseClickMode * (cellSize * 2), Height * cellSize, cellSize * 2, cellSize, Color.Black, 3);

			// actual gui
			debugGui.Draw(sb);

		}
	}
}
