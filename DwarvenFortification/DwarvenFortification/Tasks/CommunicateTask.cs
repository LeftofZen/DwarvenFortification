using Arch.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public class CommunicateTask : BaseAgentTask
	{
		readonly Entity otherAgent;
		readonly string requestedFact;

		public CommunicateTask(ITaskRuntimeContext runtimeContext, Entity owner, Entity otherAgent, string requestedFact) : base(runtimeContext, owner, "communicate", 1)
		{
			this.otherAgent = otherAgent;
			this.requestedFact = requestedFact;
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			var ownerCell = world.CoordsAtXY(owner.GetPosition());
			var otherCell = world.CoordsAtXY(otherAgent.GetPosition());
			if (Vector2.DistanceSquared(ownerCell.ToVector2(), otherCell.ToVector2()) > 4f)
			{
				return false;
			}

			return requestedFact.StartsWith("knows.item-location.", System.StringComparison.OrdinalIgnoreCase)
				? otherAgent.TryRecallItemLocation(requestedFact["knows.item-location.".Length..], out _)
				: otherAgent.KnowsFact(requestedFact);
		}

		protected override AgentTaskStatus OnTick()
		{
			if (requestedFact.StartsWith("knows.item-location.", System.StringComparison.OrdinalIgnoreCase))
			{
				owner.ShareKnownItemLocation(otherAgent, requestedFact["knows.item-location.".Length..]);
			}
			else
			{
				owner.ShareKnownFact(otherAgent, requestedFact);
			}

			AdvanceProgress(Cost);
			return CompleteTask();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(6, 1));
		}
	}
}