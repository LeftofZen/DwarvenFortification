using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Components;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.Simulation.Composition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification.Simulation.World
{
	public class GridCell
	{
		readonly SimulationDefinitionRegistry definitions;
		readonly ISimulationEntityFactory entityFactory;
		readonly SimulationRenderAssets renderAssets;
		readonly List<Entity> occupants = [];

		public GridCell(CellType type, SimulationDefinitionRegistry definitions, ISimulationEntityFactory entityFactory, SimulationRenderAssets renderAssets)
		{
			this.definitions = definitions;
			this.entityFactory = entityFactory;
			this.renderAssets = renderAssets;
			CellType = type;
		}

		public void Draw(SpriteBatch sb, Point xy, int cellSize)
		{
			var cellColour = CellLookup[CellType];

			sb.FillRectangle(xy.X * cellSize, xy.Y * cellSize, cellSize, cellSize, cellColour);

			if (TryGetDisplayOccupant(out var displayOccupant) && displayOccupant.Has<OccupantVisualComponent>())
			{
				var occupantColor = displayOccupant.Get<OccupantVisualComponent>().Color;
				sb.FillRectangle((xy.X * cellSize) + 2, (xy.Y * cellSize) + 2, cellSize - 4, cellSize - 4, occupantColor);
			}

			if (TryGetBlockingOccupant(out _))
			{
				sb.DrawRectangle(xy.X * cellSize, xy.Y * cellSize, cellSize, cellSize, Color.Black, 2);
			}

			if (ItemsInCell.Count > 0)
			{
				sb.DrawString(renderAssets.UiFont, ItemsInCell.Count.ToString(), new Vector2(xy.X * cellSize, xy.Y * cellSize), Color.Black);
			}

			if (TryGetWorldObject(out var placedWorldObject) && placedWorldObject.Has<InventoryComponent>())
			{
				var count = placedWorldObject.Get<InventoryComponent>().Items.Count;
				if (count > 0)
				{
					sb.DrawString(renderAssets.UiFont, $"[{count}]", new Vector2(xy.X * cellSize, (xy.Y * cellSize) + (cellSize / 2f)), Color.Black);
				}
			}
		}

		// 0 -> 100
		public int Durability { get; set; } = InitialDurability;
		public const int InitialDurability = 10000;

		public override string ToString()
			=> $"{CellType}";

		public static Dictionary<CellType, Color> CellLookup = new()
		{
			{ CellType.Null, Color.Black },
			{ CellType.Dirt, Color.SandyBrown },
			{ CellType.Stone, Color.Gray },
			{ CellType.Water, Color.Blue },
		};

		public List<Entity> ItemsInCell = [];

		public IReadOnlyList<Entity> Occupants => occupants;

		public void AddOccupant(Entity entity)
			=> occupants.Add(entity);

		public bool RemoveOccupant(Entity entity)
			=> occupants.Remove(entity);

		public bool TryGetWorldObject(out Entity entity)
		{
			entity = occupants.FirstOrDefault(occupant => occupant.Has<WorldObjectDefinitionComponent>() && !occupant.Has<ResourceNodeDefinitionComponent>());
			if (!entity.Equals(default(Entity)))
			{
				return true;
			}

			entity = default;
			return false;
		}

		public bool TryGetResourceNode(out Entity entity)
		{
			entity = occupants.FirstOrDefault(occupant => occupant.Has<ResourceNodeDefinitionComponent>());
			return !entity.Equals(default(Entity));
		}

		public bool TryGetDisplayOccupant(out Entity entity)
		{
			entity = occupants.FirstOrDefault();
			return !entity.Equals(default(Entity));
		}

		public bool TryGetBlockingOccupant(out Entity entity)
		{
			entity = occupants.FirstOrDefault(occupant =>
				(occupant.Has<WorldObjectDefinitionComponent>() && occupant.Get<WorldObjectDefinitionComponent>().BlocksMovement)
				|| (occupant.Has<ResourceNodeDefinitionComponent>() && occupant.Get<ResourceNodeDefinitionComponent>().BlocksMovement));
			return !entity.Equals(default(Entity));
		}

		public bool TryGetStorageOccupant(out Entity entity)
		{
			entity = occupants.FirstOrDefault(occupant => occupant.IsStorageObject());
			return !entity.Equals(default(Entity));
		}

		public bool TryExtractResource(out string yieldItemId, out int yieldCount, out Entity resourceNode)
		{
			yieldItemId = string.Empty;
			yieldCount = 0;
			resourceNode = default;
			if (!TryGetResourceNode(out resourceNode))
			{
				return false;
			}

			var definition = resourceNode.Get<ResourceNodeDefinitionComponent>();
			yieldItemId = definition.YieldItemId;
			yieldCount = definition.YieldCount;
			return !string.IsNullOrWhiteSpace(yieldItemId) && yieldCount > 0;
		}

		public void ClearOccupants()
			=> occupants.Clear();

		public void ReplaceOccupant(Entity? entity)
			{
			occupants.Clear();
			if (entity.HasValue)
			{
				occupants.Add(entity.Value);
			}
			}

		public bool IsStorageCell
			=> TryGetStorageOccupant(out _);

		public bool CanStore(Entity item)
			=> TryGetStorageOccupant(out var entity) && entity.CanStore(item) && entity.HasInventorySpace();

		public bool IsWalkable
		{
			get
			{
				return definitions.IsCellTypeWalkable(CellType) && !TryGetBlockingOccupant(out _);
			}
		}

		public CellType CellType
		{
			get => cellType;
			set
			{
				cellType = value;
			}
		}
		CellType cellType;
	}
}