using DwarvenFortification.ECS;
using DwarvenFortification.Logging;
using DwarvenFortification.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace DwarvenFortification
{
	public static class GameServices
	{
		public static readonly Dictionary<string, SpriteFont> Fonts = [];
		public static readonly Dictionary<string, Texture2D> Textures = [];
		public static readonly GameLogger Logger = new();

		public static SimulationDefinitionRegistry Definitions { get; set; }

		public static Game Game;
		public static int GameWidth => Game.GraphicsDevice.Viewport.Width;
		public static int GameHeight => Game.GraphicsDevice.Viewport.Height;

		public static GridWorld GridWorld;
	}
}
