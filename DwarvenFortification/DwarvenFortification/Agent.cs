using DwarvenFortification.GOAP;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public class Agent : ISelectableGameObject
	{
		static Random r = new(1);

		public Agent(int x, int y, GridWorld world)
		{
			X = x;
			Y = y;
			this.world = world;
			taskQueue = new Queue<IAgentTask>();
			Inventory = new List<Item>();
			Speed = ((float)r.NextDouble() * 2) + 4f;
		}

		public GridCell CurrentCell => world.CellAtXY(X, Y);
		public Rectangle CellBounds => world.CellBoundsAt(X, Y);

		internal GridWorld world;

		public Point LastPointInPath()
		{
			var moveTasks = taskQueue.Where(t => t is MoveToTask);
			return moveTasks.Any()
				? (moveTasks.Last() as MoveToTask).Goal
				: Point.Zero;
		}

		public List<GOAPAction> PossibleActions;

		public int X { get; set; }
		public int Y { get; set; }
		public Point Position
		{
			get => new(X, Y);
			set { X = value.X; Y = value.Y; }
		}

		public float Speed
		{
			get => speed * (1 - (0.25f * ((float)Inventory.Count / InventorySize)));
			set => speed = value;
		}
		float speed;

		public void Update(GameTime gameTime)
		{
			// think
			Think();

			// do
			if (taskQueue.Count > 0)
			{
				var currentTask = taskQueue.Peek();
				var result = currentTask.Update();
				if (result)
				{
					_ = taskQueue.Dequeue();
				}
			}
		}

		public void Think()
		{
			if (taskQueue.Count > 0)
			{
				return;
			}

			// if inventory not empty
			if (Inventory.Count > 0)
			{
				var closestStorage = world.ClosestCellXYOfType(X, Y, cell => cell.CellType == CellType.Storage);
				if (closestStorage.cell != null)
				{
					// move to storage
					world.PlotPath(this, world.CoordsAtXY(closestStorage.point));

					// deposit
					AddTask(new PutDownTask(this, Inventory));
					return;
				}
			}

			// if can find any ores
			var closestOre = world.ClosestCellXYOfType(X, Y, cell => cell.CellType != CellType.Storage && cell.ItemsInCell.Count > 0);
			if (closestOre.cell != null && closestOre.cell.ItemsInCell.Count > 0)
			{
				// move to ore
				world.PlotPath(this, world.CoordsAtXY(closestOre.point));

				// pick up ores
				AddTask(new PickUpTask(this, closestOre.cell.ItemsInCell.First()), InventorySize - Inventory.Count);
				return;
			}
		}

		public List<Item> Inventory { get; set; }

		public int InventorySize => 5;

		public void AddTask(IAgentTask task, int count = 1)
		{
			for (int i = 0; i < count; ++i)
			{
				taskQueue.Enqueue(task);
			}
		}

		Queue<IAgentTask> taskQueue;

		public void Draw(SpriteBatch sb)
		{
			sb.DrawEllipse(new Vector2(X, Y), new Vector2(16), 8, Color.White, 3);

			if (CellBounds != Rectangle.Empty)
			{
				sb.DrawRectangle(CellBounds, Color.Blue, 1);
			}

			// draw path if moving
			var previousMoveToPoint = Position;
			foreach (var task in taskQueue.ToArray())
			{
				if (task is MoveToTask mtTask)
				{
					sb.DrawLine(previousMoveToPoint.ToVector2(), mtTask.Goal.ToVector2(), Color.RosyBrown, 2);
					previousMoveToPoint = mtTask.Goal;
				}
			}

			// draw current task (debug)
			if (taskQueue.TryPeek(out IAgentTask agentTask))
			{

			}
		}
	}
}
