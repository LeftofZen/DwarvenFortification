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
	public sealed class CompleteConstructionAction : BaseAgentAction
	{
		readonly Entity targetSite;
		readonly Point targetCell;

		public CompleteConstructionAction(IActionRuntimeContext runtimeContext, Entity owner, Entity targetSite) : base(runtimeContext, owner, "complete-construction", 1)
		{
			this.targetSite = targetSite;
			targetCell = targetSite.GetCellReference();
		}

		public override bool IsStillValid(ISimulationWorld world)
			=> targetSite.IsConstructionSite()
				&& world.CellAtCoords(targetCell)?.TryGetWorldObject(out var worldObject) == true
				&& worldObject.Equals(targetSite);

		protected override bool CanStart()
			=> targetSite.IsConstructionSite() && targetSite.HasAllBuildMaterials();

		protected override string BuildCannotStartReason()
			=> "Construction site does not yet contain all required materials.";

		protected override AgentActionStatus OnTick()
		{
			if (runtimeContext.World is not GridWorld gridWorld)
			{
				return FailAction("Construction requires the concrete grid world implementation.");
			}

			var cell = gridWorld.CellAtCoords(targetCell);
			if (cell == null || !cell.TryGetWorldObject(out var currentWorldObject) || !currentWorldObject.Equals(targetSite))
			{
				return FailAction("Construction site is no longer present.");
			}

			if (!targetSite.Has<ConstructionSiteComponent>())
			{
				return FailAction("Target is not a construction site.");
			}

			var site = targetSite.Get<ConstructionSiteComponent>();
			if (!targetSite.HasAllBuildMaterials())
			{
				return FailAction("Construction site is missing required materials.");
			}

			var consumed = targetSite.ConsumeStoredMaterials(site.BuildCosts);
			foreach (var item in consumed)
			{
				gridWorld.DestroyEntity(item);
			}

			var finishedWorldObject = gridWorld.CreateCompletedWorldObject(site.TargetDefinitionId, targetCell);
			cell.ReplaceOccupant(finishedWorldObject);
			gridWorld.DestroyEntity(targetSite);

			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb) => Draw(sb, new Point(6, 2));
	}
}