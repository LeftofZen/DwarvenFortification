using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.Actions
{
	public class SleepAction : BaseAgentAction
	{
		readonly Entity targetWorldObject;
		readonly Point targetCell;

		public SleepAction(IActionRuntimeContext runtimeContext, Entity owner, Entity targetWorldObject, int durationTicks) : base(runtimeContext, owner, "sleep", durationTicks)
		{
			this.targetWorldObject = targetWorldObject;
			this.targetCell = targetWorldObject.GetCellReference();
		}

		public override bool IsStillValid(ISimulationWorld world)
		{
			var cell = world.CellAtCoords(targetCell);
			return cell != null && cell.TryGetWorldObject(out var currentWorldObject) && currentWorldObject.Equals(targetWorldObject);
		}

		protected override bool CanStart()
			=> targetWorldObject.IsWorldObject() && targetWorldObject.Get<TagCollectionComponent>().Contains("bed");

		protected override string BuildCannotStartReason()
			=> "Cannot sleep because the target world object is not a bed.";

		protected override AgentActionStatus OnTick()
		{
			owner.RestoreRest();
			AdvanceProgress();
			return Progress >= Cost || !owner.IsRestLow() ? CompleteAction() : AgentActionStatus.Running;
		}

		public override void Draw(SpriteBatch sb) => Draw(sb, new Point(0, 1));
	}
}