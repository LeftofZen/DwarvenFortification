using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public class GridCell : ISelectableGameObject
	{
		public GridCell(CellType type)
			=> CellType = type;

		public void Draw(SpriteBatch sb, Point2 xy, int cellSize)
		{
			sb.FillRectangle(xy.X * cellSize, xy.Y * cellSize, cellSize, cellSize, CellLookup[CellType]);
			if (ItemsInCell.Count > 0)
			{
				sb.DrawString(GameServices.Fonts["Calibri"], ItemsInCell.Count.ToString(), new Vector2(xy.X * cellSize, xy.Y * cellSize), Color.Black);
			}
		}

		public override string ToString()
			=> $"{CellType}";

		public static Dictionary<CellType, Color> CellLookup = new()
		{
			{ CellType.Grass, Color.Green },
			{ CellType.Dirt, Color.SandyBrown },
			{ CellType.Stone, Color.Gray },
			{ CellType.Ore, Color.OrangeRed },
			{ CellType.Water, Color.Blue },
			{ CellType.Storage, Color.Purple },
			{ CellType.Null, Color.Black },
		};

		public List<Item> ItemsInCell = new();

		public CellType CellType
		{
			get => cellType;
			set
			{
				cellType = value;
				if (cellType == CellType.Ore)
				{
					ItemsInCell.Clear();
					ItemsInCell.AddRange(Enumerable.Repeat(new Item("ore"), 50));
				}
				if (cellType == CellType.Stone)
				{
					ItemsInCell.Clear();
					ItemsInCell.AddRange(Enumerable.Repeat(new Item("stone"), 100));
				}
			}
		}
		CellType cellType;
	}
}
