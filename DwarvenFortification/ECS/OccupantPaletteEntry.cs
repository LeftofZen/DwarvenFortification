using Microsoft.Xna.Framework;

namespace DwarvenFortification.ECS
{
	public enum OccupantPaletteKind
	{
		WorldObject,
		ResourceNode,
	}

	public readonly record struct OccupantPaletteEntry(string Id, string Name, Color Color, OccupantPaletteKind Kind);
}