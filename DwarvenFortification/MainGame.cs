using DwarvenFortification.ECS;
using DwarvenFortification.GOAP;
using DwarvenFortification.GOAP.Plans;
using DwarvenFortification.Simulation.Composition;
using DwarvenFortification.Simulation.World;
using DwarvenFortification.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monogame.Imgui.Renderer;

namespace DwarvenFortification
{
	public class MainGame : Game
	{
		private readonly GraphicsDeviceManager _graphics;
		private SpriteBatch _spriteBatch;
		private ImGuiRenderer _imGuiRenderer;
		private SimulationRuntime simulationRuntime;
		private ImGuiSimulationUi simulationUi;
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

			base.Initialize();

			_imGuiRenderer = new ImGuiRenderer(this);
			_imGuiRenderer.RebuildFontAtlas();
		}

		protected override void LoadContent()
		{
			//_tiledMap = Content.Load<TiledMap>("tiles\\samplemap");
			//_tiledMapRenderer = new TiledMapRenderer(GraphicsDevice, _tiledMap);

			GameServices.Fonts.Add("Calibri", Content.Load<SpriteFont>("Calibri"));
			//GameServices.Textures.Add("ui", Content.Load<Texture2D>("tiles/18x18_ui"));

			var definitions = SimulationDefinitionRegistry.LoadFromContentDirectory(@"Content/config");
			GameServices.Definitions = definitions;
			simulationUi = new ImGuiSimulationUi(definitions, GameServices.Logger);
			var inspectorWorldQueryService = new GoapWorldQueryService(definitions, () => world);
			var inspectorPlanner = new Planner(definitions, inspectorWorldQueryService);
			simulationUi.PlanningSnapshotProvider = inspectorPlanner.Inspect;

			var renderAssets = new SimulationRenderAssets(
				GameServices.Fonts["Calibri"]);
			simulationRuntime = SimulationCompositionRoot.Create(
				definitions,
				renderAssets,
				GameServices.Logger,
				simulationUi);
			world = simulationRuntime.World;
			GameServices.GridWorld = world;

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

			_imGuiRenderer.BeforeLayout(this, gameTime);
			simulationUi.Render();
			_imGuiRenderer.AfterLayout();

			base.Draw(gameTime);
		}
	}
}
