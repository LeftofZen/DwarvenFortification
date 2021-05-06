using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace DwarvenFortification
{
	public enum CellType { Grass, Dirt, Stone, Ore, Water, Storage, Null }

	public class GridWorld
	{
		GridCell[,] world;
		List<Agent> agents;
		int cellSize = 32;

		MouseState previousMouseState;
		//Agent selectedAgent;
		DebugGui debugGui;

		public GridWorld(int width, int height)
		{
			agents = new List<Agent>();
			agents.Add(new Agent(100, 100, this));

			debugGui = new DebugGui();
			debugGui.Bounds = new Rectangle(width * cellSize, 0, 400, 800);

			world = new GridCell[height, width];
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
					}
					else
					{
						world[y, x] = new GridCell(CellType.Dirt);
					}
				}
			}
		}

		public GridCell CellAt(int x, int y)
		{
			var cell = new Point(x / cellSize, y / cellSize);
			if (cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height)
			{
				return world[cell.Y, cell.X];
			}
			return null;
		}

		public Rectangle CellBoundsAt(int x, int y)
		{
			var cell = new Point(x / cellSize, y / cellSize);
			if (cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height)
			{
				return new Rectangle(x - x % cellSize, y - y % cellSize, cellSize, cellSize);
			}
			return Rectangle.Empty;
		}

		public CellType CellTypeAtXY(int x, int y)
		{
			var cell = CellAt(x, y);
			if (cell != null)
				return cell.CellType;
			else
				return CellType.Null;
		}

		CellType selectedCellTypeForDraw = CellType.Grass;

		public Point ClosestCellXYOfType(int X, int Y, CellType type)
		{
			var matching = new List<(GridCell cell, Point xy)>();
			for (var y = 0; y < Height; ++y)
			{
				for (var x = 0; x < Width; ++x)
				{
					if (world[y, x].CellType == type)
						matching.Add((world[y, x], new Point(x * cellSize, y * cellSize)));
				}
			}

			var closest = matching.OrderBy(cell => (cell.xy.ToVector2() - new Vector2(X, Y)).LengthSquared()).FirstOrDefault();
			return closest.xy;
		}

		public void Update(GameTime gameTime)
		{
			var currMouseState = Mouse.GetState();
			bool debugGuiSetThisFrame = false;

			if (currMouseState.LeftButton == ButtonState.Pressed)
			{
				var clickedCell = new Point(currMouseState.X / cellSize, currMouseState.Y / cellSize);
				if (clickedCell.X >= 0 && clickedCell.X < Width && clickedCell.Y >= 0 && clickedCell.Y < Height)
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

					world[clickedCell.Y, clickedCell.X].CellType = selectedCellTypeForDraw;

					if (!debugGuiSetThisFrame)
					{
						debugGui.BoundObject = world[clickedCell.Y, clickedCell.X];
					}
				}

				if (new Rectangle(0, Height * cellSize, cellSize * 7, cellSize).Contains(currMouseState.Position))
				{
					selectedCellTypeForDraw = (CellType)(currMouseState.X / 32);
				}
			}

			if (currMouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton != ButtonState.Pressed)
			{
				var agent = agents.First();

				if (CellTypeAtXY(currMouseState.Position.X, currMouseState.Position.Y) == CellType.Ore)
				{
					var oreItem = new Item("ore");
					agent.AddTask(new MoveToTask(agent, currMouseState.Position));
					agent.AddTask(new PickUpTask(agent, oreItem));
					var closestDropoff = ClosestCellXYOfType(agent.X, agent.Y, CellType.Storage);
					agent.AddTask(new MoveToTask(agent, closestDropoff));
					agent.AddTask(new PutDownTask(agent, oreItem));
				}
				else
				{
					agent.AddTask(new MoveToTask(agent, currMouseState.Position));
				}
			}

			previousMouseState = currMouseState;

			var keyboard = Keyboard.GetState();

			if (keyboard.IsKeyDown(Keys.D1))
			{
				selectedCellTypeForDraw = (CellType)0;
			}
			if (keyboard.IsKeyDown(Keys.D2))
			{
				selectedCellTypeForDraw = (CellType)1;
			}
			if (keyboard.IsKeyDown(Keys.D3))
			{
				selectedCellTypeForDraw = (CellType)2;
			}
			if (keyboard.IsKeyDown(Keys.D4))
			{
				selectedCellTypeForDraw = (CellType)3;
			}
			if (keyboard.IsKeyDown(Keys.D5))
			{
				selectedCellTypeForDraw = (CellType)4;
			}
			if (keyboard.IsKeyDown(Keys.D6))
			{
				selectedCellTypeForDraw = (CellType)5;
			}
			if (keyboard.IsKeyDown(Keys.D7))
			{
				selectedCellTypeForDraw = (CellType)6;
			}

			foreach (var agent in agents)
			{
				agent.Update(gameTime);
			}

			//debugGui.BoundObject = agents.First();

			debugGui.Update(gameTime);
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

			// selection rect background
			//sb.FillRectangle(0, Height * cellSize, 5 * cellSize, cellSize, Color.Black);
			// selections
			for (var i = 0; i < 7; ++i)
			{
				sb.FillRectangle(i * cellSize, Height * cellSize, cellSize, cellSize, GridCell.CellLookup[(CellType)i]);
			}
			// selected cell
			sb.DrawRectangle((int)selectedCellTypeForDraw * cellSize, Height * cellSize, cellSize, cellSize, Color.Black, 3);

			// actual gui
			debugGui.Draw(sb);

		}
	}
}
