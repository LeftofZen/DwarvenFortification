using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification.Simulation.Composition
{
	public sealed class SimulationRenderAssets
	{
		public SimulationRenderAssets(SpriteFont uiFont) => UiFont = uiFont;

		public SpriteFont UiFont { get; }
	}
}