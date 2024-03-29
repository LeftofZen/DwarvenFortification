﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace DwarvenFortification
{
	public class Agent : ISelectableGameObject
	{
		public Agent(int x, int y, GridWorld world)
		{
			X = x;
			Y = y;
			this.world = world;
			taskQueue = new Queue<IAgentTask>();
			Inventory = new List<Item>();
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

		public int X;
		public int Y;
		public Point Position
		{
			get => new Point(X, Y);
			set { X = value.X; Y = value.Y; }
		}

		public float Speed = 5f;

		public void Update(GameTime gameTime)
		{
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

		public List<Item> Inventory { get; set; }

		public void AddTask(IAgentTask task)
		{
			taskQueue.Enqueue(task);
		}

		Queue<IAgentTask> taskQueue;

		public void Draw(SpriteBatch sb)
		{
			sb.DrawEllipse(new Vector2(X, Y), new Vector2(16), 8, Color.White, 3);

			if (CellBounds != Rectangle.Empty)
			{
				sb.DrawRectangle(CellBounds, Color.Blue, 1);
			}

			var previousMoveToPoint = Position;
			foreach (var task in taskQueue.ToArray())
			{
				if (task is MoveToTask mtTask)
				{
					sb.DrawLine(previousMoveToPoint.ToVector2(), mtTask.Goal.ToVector2(), Color.RosyBrown, 2);
					previousMoveToPoint = mtTask.Goal;
				}
			}
		}
	}
}
