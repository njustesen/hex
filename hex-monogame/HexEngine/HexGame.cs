using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using HexEngine.Maps;

namespace HexEngine;

public class HexGame : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private PrimitiveDrawer _drawer = null!;
    private GridMap _map = null!;
    private Viewport _viewport = null!;
    private Minimap _minimap = null!;
    private InputManager _inputManager = null!;
    private DebugMenu _debugMenu = null!;
    private SpriteFont _debugFont = null!;
    private int _currentMapMode = 1;

    public HexGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        _graphics.PreferredBackBufferWidth = EngineConfig.Width;
        _graphics.PreferredBackBufferHeight = EngineConfig.Height;
        IsFixedTimeStep = true;
        TargetElapsedTime = System.TimeSpan.FromSeconds(1.0 / 60.0);
    }

    protected override void Initialize()
    {
        EngineConfig.Load();
        _inputManager = new InputManager();
        _debugMenu = new DebugMenu();
        SetupMap(1);
        base.Initialize();
    }

    private void SetupMap(int mode)
    {
        _currentMapMode = mode;

        _map = mode switch
        {
            2 => new TileGridMap(21, 11, tileWidth: 100f, tileHeight: 100f),
            3 => new IsometricTileGridMap(21, 11, tileWidth: 100f, tileHeight: 100f),
            _ => new HexGridMap(21, 11, hexRadius: 100f, hexVerticalScale: 0.7f, hexOrientation: "flat"),
        };

        if (mode != 1)
            EngineConfig.PerspectiveFactor = 0f;

        _map.Tiles[10][10].Unit = new Unit();

        // Varied terrain elevations
        _map.Tiles[5][10].Elevation = 1;
        _map.Tiles[5][11].Elevation = 1;
        _map.Tiles[4][10].Elevation = 1;
        _map.Tiles[4][11].Elevation = 2;
        _map.Tiles[4][12].Elevation = 1;
        _map.Tiles[3][11].Elevation = 3;
        _map.Tiles[3][12].Elevation = 2;
        _map.Tiles[6][8].Elevation = 1;
        _map.Tiles[7][7].Elevation = 1;
        _map.Tiles[7][8].Elevation = 2;
        _map.Tiles[8][8].Elevation = 1;

        float mapRatio = _map.Width / _map.Height;
        float minimapHeight = EngineConfig.Height / 5f;
        float minimapWidth = minimapHeight * mapRatio;

        _viewport = new Viewport(
            screenX1: 0, screenY1: 0,
            screenWidth: EngineConfig.Width, screenHeight: EngineConfig.Height,
            zoomLevel: 1f, map: _map,
            isMinimap: false, isPrimary: true);

        _minimap = new Minimap(
            screenX1: EngineConfig.Width - minimapWidth - 1,
            screenY1: EngineConfig.Height - minimapHeight - 1,
            screenWidth: minimapWidth, screenHeight: minimapHeight,
            zoomLevel: 1f, map: _map,
            primaryViewport: _viewport);

        _viewport.Minimap = _minimap;

        if (GraphicsDevice != null)
        {
            _viewport.CreateRenderTarget(GraphicsDevice);
            _minimap.CreateRenderTarget(GraphicsDevice);
        }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _drawer = new PrimitiveDrawer(GraphicsDevice);
        _debugFont = Content.Load<SpriteFont>("DebugFont");
        _viewport.CreateRenderTarget(GraphicsDevice);
        _minimap.CreateRenderTarget(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _inputManager.Update();
        _debugMenu.Update();

        if (_inputManager.MapModePressed != 0 && _inputManager.MapModePressed != _currentMapMode)
            SetupMap(_inputManager.MapModePressed);

        // Only pass input to viewport when debug menu isn't consuming arrow keys
        if (!_debugMenu.ConsumesArrowKeys)
            _viewport.Update(seconds, _inputManager);
        else
            _viewport.Update(seconds);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Draw viewport to its render target
        _viewport.Draw(GraphicsDevice, _drawer, grid: EngineConfig.ShowGrid ? 80 : null);

        // Draw minimap to its render target
        _minimap.DrawMinimap(GraphicsDevice, _drawer);

        // Compose to screen
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // Draw viewport
        if (_viewport.RenderTarget != null)
            _spriteBatch.Draw(_viewport.RenderTarget,
                new Vector2(_viewport.ScreenX1, _viewport.ScreenY1), Color.White);

        // Draw minimap
        if (_minimap.RenderTarget != null)
            _spriteBatch.Draw(_minimap.RenderTarget,
                new Vector2(_minimap.ScreenX1, _minimap.ScreenY1), Color.White);

        // Debug menu overlay
        _debugMenu.Draw(_spriteBatch, _debugFont);

        _spriteBatch.End();

        // Draw minimap border and cursor using primitives
        _drawer.UpdateProjection(EngineConfig.Width, EngineConfig.Height);
        _drawer.DrawRectOutline(_minimap.ScreenX1 - 1, _minimap.ScreenY1 - 1,
                                _minimap.ScreenWidth + 2, _minimap.ScreenHeight + 2, Colors.RED);

        // Draw mouse cursor
        var mouse = Mouse.GetState();
        _drawer.DrawFilledRect(mouse.X - 5, mouse.Y - 5, 10, 10, Colors.WHITE);

        base.Draw(gameTime);
    }
}
