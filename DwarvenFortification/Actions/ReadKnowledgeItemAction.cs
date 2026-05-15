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
	public class ReadKnowledgeItemAction : BaseAgentAction
	{
		readonly Entity readableItem;
		readonly Point targetCell;

		public ReadKnowledgeItemAction(IActionRuntimeContext runtimeContext, Entity owner, Entity readableItem, Point targetCell) : base(runtimeContext, owner, "read-cookbook", 1)
		{
			this.readableItem = readableItem;
			this.targetCell = targetCell;
		}

		public override bool IsStillValid(ISimulationWorld world)
			=> owner.GetInventory().Contains(readableItem)
				|| (world.CoordsAtXY(owner.GetPosition()) == targetCell && world.TryGetItemEntity(targetCell, readableItem.GetItemDefinitionId(), out var worldItem) && worldItem.Equals(readableItem));

		protected override AgentActionStatus OnTick()
		{
			if (!owner.GetInventory().Contains(readableItem))
			{
				var currentCell = runtimeContext.World.CoordsAtXY(owner.GetPosition());
				if (currentCell != targetCell || !runtimeContext.World.TryGetItemEntity(targetCell, readableItem.GetItemDefinitionId(), out var worldItem) || !worldItem.Equals(readableItem))
				{
					return FailAction("The readable item is no longer available at the interaction location.");
				}
			}

			var learnedFacts = readableItem.Get<ItemDefinitionComponent>().LearnedFacts;
			foreach (var fact in learnedFacts)
			{
				owner.LearnFact(fact);
			}

			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb)
		{
			Draw(sb, new Point(7, 1));
		}
	}
}