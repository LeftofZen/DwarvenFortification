using Arch.Core;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.Actions
{
	public class SearchForItemAction : BaseAgentAction
	{
		readonly string itemDefinitionId;
		readonly Point targetCell;

		public SearchForItemAction(IActionRuntimeContext runtimeContext, Entity owner, string itemDefinitionId, Point targetCell) : base(runtimeContext, owner, "search-for-item", 1)
		{
			this.itemDefinitionId = itemDefinitionId;
			this.targetCell = targetCell;
		}

		public override bool IsStillValid(ISimulationWorld world)
			=> world.CellContainsItem(targetCell, itemDefinitionId);

		protected override AgentActionStatus OnTick()
		{
			if (!runtimeContext.World.CellContainsItem(targetCell, itemDefinitionId))
			{
				return FailAction($"Could not find '{itemDefinitionId}' while searching.");
			}

			owner.RememberItemLocation(itemDefinitionId, targetCell);
			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(4, 1));
		}
	}
}