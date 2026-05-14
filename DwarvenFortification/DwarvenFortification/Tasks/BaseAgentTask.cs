using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;

namespace DwarvenFortification
{
	public abstract class BaseAgentTask : IAgentTask
	{
		public BaseAgentTask(ITaskRuntimeContext runtimeContext, Entity owner, string actionId, int cost = 0)
		{
			this.runtimeContext = runtimeContext;
			this.owner = owner;
			ActionId = actionId;
			this.Cost = cost;
			this.Progress = 0;

			runtimeContext.Logger.Log(Logging.LogLevel.Debug, $"new task created: {this.ToString()}");
		}

		protected readonly ITaskRuntimeContext runtimeContext;
		protected int Progress;
		protected int Cost;

		protected Entity owner;

		public string Name => GetType().Name;
		public string ActionId { get; }
		public AgentTaskStatus Status { get; protected set; } = AgentTaskStatus.Pending;
		public string FailureReason { get; protected set; } = string.Empty;

		public override string ToString()
			=> $"Task={Name} Action={ActionId} Agent={owner.GetName()} Status={Status} Cost={Cost} Progress={Progress}";

		public AgentTaskStatus Tick()
		{
			if (Status is AgentTaskStatus.Succeeded or AgentTaskStatus.Failed or AgentTaskStatus.Cancelled)
			{
				return Status;
			}

			if (Status == AgentTaskStatus.Pending)
			{
				if (!CanStart())
				{
					Status = AgentTaskStatus.Failed;
					FailureReason = BuildCannotStartReason();
					runtimeContext.Logger.Log(Logging.LogLevel.Warning, $"task failed before start: {this}; reason={FailureReason}");
					return Status;
				}

				OnStarted();
				Status = AgentTaskStatus.Running;
			}

			Status = OnTick();

			runtimeContext.Logger.Log(Logging.LogLevel.Debug, $"{this}");

			if (Status == AgentTaskStatus.Failed && string.IsNullOrWhiteSpace(FailureReason))
			{
				FailureReason = "Task failed without reporting a reason.";
			}

			return Status;
		}

		public virtual bool IsStillValid(ISimulationWorld world)
			=> true;

		public abstract void Draw(SpriteBatch sb);

		protected virtual bool CanStart()
			=> true;

		protected virtual string BuildCannotStartReason()
			=> $"Cannot start task {Name}.";

		protected virtual void OnStarted()
		{
		}

		protected abstract AgentTaskStatus OnTick();

		protected void AdvanceProgress(int amount = 1)
			=> Progress = Math.Clamp(Progress + amount, 0, Math.Max(0, Cost));

		protected AgentTaskStatus CompleteTask()
		{
			Progress = Math.Max(Progress, Cost);
			FailureReason = string.Empty;
			return AgentTaskStatus.Succeeded;
		}

		protected AgentTaskStatus FailTask(string reason)
		{
			FailureReason = reason;
			return AgentTaskStatus.Failed;
		}

		protected void Draw(SpriteBatch sb, Point tileIndex)
		{
			const int tileSize = 18;

			var srcRect = new Rectangle(
				tileSize * tileIndex.X,
				tileSize * tileIndex.Y,
				tileSize,
				tileSize);

			// task icon
			sb.Draw(runtimeContext.RenderAssets.UiTexture, owner.GetPosition().ToVector2() + new Vector2(9, -22), srcRect, Color.White);

			// progress bar to goal
			var goalPercent = Cost == 0 ? (Status == AgentTaskStatus.Succeeded ? 1f : 0f) : Progress / (float)Cost;
			const int borderThickness = 2;
			int barHeight = owner.GetHeight() / 4;
			sb.FillRectangle(owner.GetLeft(), owner.GetTop() - barHeight, owner.GetWidth(), barHeight, Color.Black); // border
			sb.FillRectangle(owner.GetLeft() + borderThickness, owner.GetTop() - barHeight + borderThickness, (owner.GetWidth() - borderThickness * 2) * goalPercent, (barHeight - (borderThickness * 2)), Color.White); // inside
		}
	}
}
