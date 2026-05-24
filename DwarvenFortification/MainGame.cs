using DwarvenFortification.Camera;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Runtime;
using DwarvenFortification.GOAP;
using DwarvenFortification.Mcp;
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
		private Camera2D _camera;
		private GridWorld world;
		private SimulationSnapshotProvider snapshotProvider;
		private SimulationMcpHost mcpHost;
		private long simulationTick;

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

			Window.AllowUserResizing = true;
			Window.ClientSizeChanged += (_, _) =>
			{
				_graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
				_graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
				_graphics.ApplyChanges();
			};

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
			simulationUi.PlanningAgentProvider = entity => {
				var state = inspectorWorldQueryService.BuildCurrentState(entity);
				var numericState = inspectorWorldQueryService.BuildNumericState(entity);
				return SimulationGoapAgentFactory.CreateAgent(definitions, entity.GetName(), state, numericState);
			};

			var renderAssets = new SimulationRenderAssets(
				GameServices.Fonts["Calibri"]);
			_camera = new Camera2D(GraphicsDevice.Viewport);
			simulationRuntime = SimulationCompositionRoot.Create(
				definitions,
				renderAssets,
				GameServices.Logger,
				simulationUi,
				_camera);
			world = simulationRuntime.World;
			_camera.Position = world.WorldCenter;
			world.PlanningAgentProvider = entity => {
				var state = inspectorWorldQueryService.BuildCurrentState(entity);
				var numericState = inspectorWorldQueryService.BuildNumericState(entity);
				return SimulationGoapAgentFactory.CreateAgent(definitions, entity.GetName(), state, numericState);
			};
			GameServices.GridWorld = world;

			// Start the embedded MCP server so external AI clients can introspect the GOAP/world state.
			// Snapshots are captured on the main thread each Update; MCP tools read them lock-free.
			snapshotProvider = new SimulationSnapshotProvider(definitions, inspectorWorldQueryService);
			mcpHost = SimulationMcpHost.Start(snapshotProvider);

			_spriteBatch = new SpriteBatch(GraphicsDevice);
		}

		protected override void Update(GameTime gameTime)
		{
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
			{
				Exit();
			}

			world.Update(gameTime);
			snapshotProvider?.Capture(world, ++simulationTick);

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

			// World-space pass — camera transform applied
			_spriteBatch.Begin(blendState: BlendState.AlphaBlend, transformMatrix: _camera.GetTransformMatrix());
			world.Draw(_spriteBatch);
			_spriteBatch.End();

			// Screen-space pass — no transform (tooltips, HUD)
			_spriteBatch.Begin(blendState: BlendState.AlphaBlend);
			world.DrawScreenOverlays(_spriteBatch);
			_spriteBatch.End();

			_imGuiRenderer.BeforeLayout(this, gameTime);
			simulationUi.Render();
			_imGuiRenderer.AfterLayout();

			base.Draw(gameTime);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				try
				{
					mcpHost?.DisposeAsync().AsTask().GetAwaiter().GetResult();
				}
				catch
				{
					// swallow shutdown errors
				}

				mcpHost = null;
			}

			base.Dispose(disposing);
		}
	}
}
