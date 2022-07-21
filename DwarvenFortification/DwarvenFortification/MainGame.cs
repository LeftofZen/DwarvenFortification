using DwarvenFortification.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DwarvenFortification
{
	public static class GameServices
	{
		public static readonly Dictionary<string, SpriteFont> Fonts = new();
		public static readonly Dictionary<string, Texture2D> Textures = new();
		public static readonly GameLogger Logger = new();
		public static readonly Dictionary<string, IGameEntity> EntityDefinitions = new();

		public static Game Game;
		public static int GameWidth => Game.GraphicsDevice.Viewport.Width;
		public static int GameHeight => Game.GraphicsDevice.Viewport.Height;
	}

	public class MainGame : Game
	{
		private GraphicsDeviceManager _graphics;
		private SpriteBatch _spriteBatch;
		//private TiledMap _tiledMap;
		//private TiledMapRenderer _tiledMapRenderer;
		//private OrthographicCamera _camera;
		//private Vector2 _cameraPosition;
		private GridWorld world;

		//Rectangle worldRenderRect;
		//Rectangle

		public MainGame()
		{
			_graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			GameServices.Game = this;
		}

		protected override void Initialize()
		{
			_graphics.PreferredBackBufferHeight = 1080;
			_graphics.PreferredBackBufferWidth = 1920;
			_graphics.ApplyChanges();

			//var viewportadapter = new BoxingViewportAdapter(Window, GraphicsDevice, 800, 600);
			//_camera = new OrthographicCamera(viewportadapter);
			world = new GridWorld(24, 16);

			base.Initialize();
		}

		protected override void LoadContent()
		{
			//_tiledMap = Content.Load<TiledMap>("tiles\\samplemap");
			//_tiledMapRenderer = new TiledMapRenderer(GraphicsDevice, _tiledMap);

			GameServices.Fonts.Add("Calibri", Content.Load<SpriteFont>("Calibri"));
			GameServices.Textures.Add("ui", Content.Load<Texture2D>("tiles/18x18_ui"));

			//var path = Content.Load<ItemDefinition[]>("config/items");
			var path = @"Content/config/items.json";
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			var entityDefs = JsonSerializer.Deserialize<ItemDefinitionJson>(File.ReadAllText(path), options);

			_spriteBatch = new SpriteBatch(GraphicsDevice);
		}

		protected override void Update(GameTime gameTime)
		{
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
			{
				Exit();
			}

			world.Update(gameTime);

			//_tiledMapRenderer.Update(gameTime);
			//MoveCamera(gameTime);
			//_camera.LookAt(_cameraPosition);

			base.Update(gameTime);
		}

		private Vector2 GetMovementDirection()
		{
			var movementDirection = Vector2.Zero;
			var state = Keyboard.GetState();
			if (state.IsKeyDown(Keys.Down))
			{
				movementDirection += Vector2.UnitY;
			}
			if (state.IsKeyDown(Keys.Up))
			{
				movementDirection -= Vector2.UnitY;
			}
			if (state.IsKeyDown(Keys.Left))
			{
				movementDirection -= Vector2.UnitX;
			}
			if (state.IsKeyDown(Keys.Right))
			{
				movementDirection += Vector2.UnitX;
			}

			// Can't normalize the zero vector so test for it before normalizing
			if (movementDirection != Vector2.Zero)
			{
				movementDirection.Normalize();
			}

			return movementDirection;
		}

		//private void MoveCamera(GameTime gameTime)
		//{
		//	var speed = 200;
		//	var seconds = gameTime.GetElapsedSeconds();
		//	var movementDirection = GetMovementDirection();
		//	_cameraPosition += speed * movementDirection * seconds;
		//}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.CornflowerBlue);

			_spriteBatch.Begin(blendState: BlendState.AlphaBlend);

			world.Draw(_spriteBatch);
			//_tiledMapRenderer.Draw(_camera.GetViewMatrix());

			_spriteBatch.End();

			base.Draw(gameTime);
		}
	}
}
