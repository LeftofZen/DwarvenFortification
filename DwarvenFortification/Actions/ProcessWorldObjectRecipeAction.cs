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
	public sealed class ProcessWorldObjectRecipeAction : BaseAgentAction
	{
		readonly Entity targetWorldObject;
		readonly string outputItemId;
		readonly Point targetCell;

		public ProcessWorldObjectRecipeAction(IActionRuntimeContext runtimeContext, Entity owner, Entity targetWorldObject, string outputItemId) : base(runtimeContext, owner, "process-recipe", 1)
		{
			this.targetWorldObject = targetWorldObject;
			this.outputItemId = outputItemId;
			targetCell = targetWorldObject.GetCellReference();
		}

		public override bool IsStillValid(ISimulationWorld world)
			=> world.CellAtCoords(targetCell)?.TryGetWorldObject(out var currentWorldObject) == true
				&& currentWorldObject.Equals(targetWorldObject)
				&& targetWorldObject.TryGetRecipeForOutput(outputItemId, out _);

		protected override bool CanStart()
			=> targetWorldObject.TryGetRecipeForOutput(outputItemId, out var recipe)
				&& targetWorldObject.HasStoredMaterials(recipe.Inputs);

		protected override string BuildCannotStartReason()
			=> $"Target workstation does not contain the stored materials needed for '{outputItemId}'.";

		protected override AgentActionStatus OnTick()
		{
			if (runtimeContext.World is not GridWorld gridWorld)
			{
				return FailAction("Recipe processing requires the concrete grid world implementation.");
			}

			var cell = gridWorld.CellAtCoords(targetCell);
			if (cell == null || !cell.TryGetWorldObject(out var currentWorldObject) || !currentWorldObject.Equals(targetWorldObject))
			{
				return FailAction("Target workstation is no longer present.");
			}

			if (!targetWorldObject.TryGetRecipeForOutput(outputItemId, out var recipe))
			{
				return FailAction($"No recipe is available for '{outputItemId}' on this workstation.");
			}

			if (!targetWorldObject.HasStoredMaterials(recipe.Inputs))
			{
				return FailAction("Target workstation is missing required stored materials.");
			}

			var consumed = targetWorldObject.ConsumeStoredMaterials(recipe.Inputs);
			foreach (var item in consumed)
			{
				gridWorld.DestroyEntity(item);
			}

			for (var i = 0; i < recipe.OutputQuantity; ++i)
			{
				var createdItem = gridWorld.CreateItem(recipe.OutputItemId);
				if (targetWorldObject.HasInventorySpace())
				{
					targetWorldObject.AddStoredItem(createdItem);
				}
				else
				{
					cell.ItemsInCell.Add(createdItem);
				}
			}

			if (targetWorldObject.HasActiveProductionOrder())
			{
				var order = targetWorldObject.GetProductionOrder();
				if (string.Equals(order.ActiveRecipeId, recipe.Id, System.StringComparison.OrdinalIgnoreCase))
				{
					targetWorldObject.IncrementProductionBatch();
				}
			}

			AdvanceProgress(Cost);
			return CompleteAction();
		}

		public override void Draw(SpriteBatch sb) => Draw(sb, new Point(7, 2));
	}
}