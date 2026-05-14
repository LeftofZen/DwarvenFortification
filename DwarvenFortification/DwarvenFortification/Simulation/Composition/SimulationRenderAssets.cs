using Microsoft.Xna.Framework.Graphics;

namespace DwarvenFortification
{
	public sealed class SimulationRenderAssets
	{
		public SimulationRenderAssets(SpriteFont uiFont, Texture2D uiTexture)
		{
			UiFont = uiFont;
			UiTexture = uiTexture;
		}

		public SpriteFont UiFont { get; }
		public Texture2D UiTexture { get; }
	}
}