using Arch.Core;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using DwarvenFortification.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System;

namespace DwarvenFortification.Actions
{
	public abstract class BaseAgentAction : IAgentAction
	{
		public BaseAgentAction(IActionRuntimeContext runtimeContext, Entity owner, string actionId, int cost = 0)
		{
			this.runtimeContext = runtimeContext;
			this.owner = owner;
			ActionId = actionId;
			Metadata = new AgentActionMetadata(AgentActionSource.Autonomous, "autonomous");
			this.Cost = cost;
			this.Progress = 0;

			runtimeContext.Logger.Log(Logging.LogLevel.Debug, $"new action created: {this}");
		}

		protected readonly IActionRuntimeContext runtimeContext;
		protected int Progress;
		protected int Cost;

		protected Entity owner;

		public string Name => GetType().Name;
		public string ActionId { get; }
		public AgentActionMetadata Metadata { get; }
		public AgentActionStatus Status { get; protected set; } = AgentActionStatus.Pending;
		public string FailureReason { get; protected set; } = string.Empty;

		public override string ToString()
			=> $"Action={Name} ActionId={ActionId} Agent={owner.GetName()} Source={Metadata.Origin} Status={Status} Cost={Cost} Progress={Progress}";

		public void ApplyActionMetadata(AgentActionMetadata metadata)
		{
			if (metadata == null)
			{
				return;
			}

			Metadata.Origin = metadata.Origin;
			Metadata.Tags.Clear();
			foreach (var tag in metadata.Tags)
			{
				Metadata.Tags.Add(tag);
			}
		}

		public AgentActionStatus Tick()
		{
			if (Status is AgentActionStatus.Succeeded or AgentActionStatus.Failed or AgentActionStatus.Cancelled)
			{
				return Status;
			}

			if (Status == AgentActionStatus.Pending)
			{
				if (!CanStart())
				{
					Status = AgentActionStatus.Failed;
					FailureReason = BuildCannotStartReason();
					runtimeContext.Logger.Log(Logging.LogLevel.Warning, $"action failed before start: {this}; reason={FailureReason}");
					return Status;
				}

				OnStarted();
				Status = AgentActionStatus.Running;
			}

			Status = OnTick();

			runtimeContext.Logger.Log(Logging.LogLevel.Debug, $"{this}");

			if (Status == AgentActionStatus.Failed && string.IsNullOrWhiteSpace(FailureReason))
			{
				FailureReason = "Action failed without reporting a reason.";
			}

			return Status;
		}

		public virtual bool IsStillValid(ISimulationWorld world)
			=> true;

		public abstract void Draw(SpriteBatch sb);

		protected virtual bool CanStart()
			=> true;

		protected virtual string BuildCannotStartReason()
			=> $"Cannot start action {Name}.";

		protected virtual void OnStarted()
		{
		}

		protected abstract AgentActionStatus OnTick();

		protected void AdvanceProgress(int amount = 1)
			=> Progress = Math.Clamp(Progress + amount, 0, Math.Max(0, Cost));

		protected AgentActionStatus CompleteAction()
		{
			Progress = Math.Max(Progress, Cost);
			FailureReason = string.Empty;
			return AgentActionStatus.Succeeded;
		}

		protected AgentActionStatus FailAction(string reason)
		{
			FailureReason = reason;
			return AgentActionStatus.Failed;
		}

		protected void Draw(SpriteBatch sb, Point tileIndex)
		{
			//const int tileSize = 16;

			// action icon - specific to the action being performed
			//sb.FillRectangle(owner.GetPosition().ToVector2() + new Vector2(tileSize / 2, -tileSize), new Vector2(tileSize, tileSize), Color.Blue);

			// progress bar to goal
			var goalPercent = Cost == 0 ? (Status == AgentActionStatus.Succeeded ? 1f : 0f) : Progress / (float)Cost;
			const int borderThickness = 2;
			var barHeight = owner.GetHeight() / 4;
			sb.FillRectangle(owner.GetLeft(), owner.GetTop() - barHeight, owner.GetWidth(), barHeight, Color.Black); // border
			sb.FillRectangle(owner.GetLeft() + borderThickness, owner.GetTop() - barHeight + borderThickness, (owner.GetWidth() - (borderThickness * 2)) * goalPercent, barHeight - (borderThickness * 2), Color.White); // inside
		}
	}
}
