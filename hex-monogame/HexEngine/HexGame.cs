using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using HexEngine.Maps;
using HexEngine.Rendering;
using HexEngine.View;
using Viewport = HexEngine.View.Viewport;
using HexEngine.UI;
using HexEngine.Editor;
using HexEngine.Input;
using HexEngine.Config;
using HexEngine.Core;

namespace HexEngine;

public enum GameState { Menu, Playing }

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
    private MapEditor _editor = null!;
    private StartupMenu _startupMenu = null!;
    private MapRenderer _renderer = new();
    private InteractionState _interaction = new();
    private Toolbar _toolbar = new();
    private InfoPanel _infoPanel = new();
    private GameplayManager _gameplay = new();
    private SpriteFont _debugFont = null!;
    private Texture2D _pixel = null!;
    private int _currentMapMode = 1;
    private GameState _state = GameState.Menu;
    private int _windowedWidth;
    private int _windowedHeight;

    public HexGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        _graphics.PreferredBackBufferWidth = EngineConfig.Width;
        _graphics.PreferredBackBufferHeight = EngineConfig.Height;
        IsFixedTimeStep = true;
        TargetElapsedTime = System.TimeSpan.FromSeconds(1.0 / 60.0);
        _windowedWidth = EngineConfig.Width;
        _windowedHeight = EngineConfig.Height;
    }

    protected override void Initialize()
    {
        EngineConfig.Load();
        UnitDefs.Load();
        _inputManager = new InputManager();
        _debugMenu = new DebugMenu();
        _editor = new MapEditor();
        _startupMenu = new StartupMenu();

        // Create a default map (used during menu for background, replaced when playing)
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

        SetupViewport();
    }

    private void SetupMapFromGrid(GridMap map)
    {
        _map = map;
        _currentMapMode = map switch
        {
            HexGridMap => 1,
            TileGridMap => 2,
            IsometricTileGridMap => 3,
            _ => 1
        };
        SetupViewport();
    }

    private void ApplyWindowSettings()
    {
        if (EngineConfig.Fullscreen)
        {
            // Save windowed size before going fullscreen
            if (!_graphics.IsFullScreen)
            {
                _windowedWidth = EngineConfig.Width;
                _windowedHeight = EngineConfig.Height;
            }
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = displayMode.Width;
            _graphics.PreferredBackBufferHeight = displayMode.Height;
        }
        else
        {
            // Restore windowed size when leaving fullscreen
            if (_graphics.IsFullScreen)
            {
                EngineConfig.Width = _windowedWidth;
                EngineConfig.Height = _windowedHeight;
            }
            _graphics.PreferredBackBufferWidth = EngineConfig.Width;
            _graphics.PreferredBackBufferHeight = EngineConfig.Height;
        }

        _graphics.IsFullScreen = EngineConfig.Fullscreen;
        _graphics.ApplyChanges();

        // Config reflects actual screen size for rendering
        EngineConfig.Width = GraphicsDevice.Viewport.Width;
        EngineConfig.Height = GraphicsDevice.Viewport.Height;

        SetupViewport();
        _viewport.CreateRenderTarget(GraphicsDevice);
        _minimap.CreateRenderTarget(GraphicsDevice);
    }

    private void SetupViewport()
    {
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
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _viewport.CreateRenderTarget(GraphicsDevice);
        _minimap.CreateRenderTarget(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        float seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _inputManager.Update();

        switch (_state)
        {
            case GameState.Menu:
                UpdateMenu();
                break;
            case GameState.Playing:
                UpdatePlaying(seconds);
                break;
        }

        base.Update(gameTime);
    }

    private void UpdateMenu()
    {
        _startupMenu.Update(_inputManager);

        if (_startupMenu.Quit)
            Exit();

        if (_startupMenu.Done && _startupMenu.ResultMap != null)
        {
            SetupMapFromGrid(_startupMenu.ResultMap);
            _editor.SetLastSavePath(_startupMenu.ResultMapPath);
            _gameplay.StartGame(_map, _startupMenu.ResultMode);
            _state = GameState.Playing;

            // In editor mode, open editor automatically
            if (_startupMenu.ResultMode == GameMode.Editor)
                _editor.Visible = true;
        }
    }

    private void UpdatePlaying(float seconds)
    {
        _interaction.InnerShapeScale = EngineConfig.InnerShapeScale;

        _gameplay.Tick(seconds);
        if (_gameplay.IsAnimating)
        {
            _gameplay.Update(_interaction);
            return;
        }

        _toolbar.Update(_inputManager);
        if (_toolbar.DebugToggled) _debugMenu.Visible = !_debugMenu.Visible;
        if (_toolbar.EditorToggled) _editor.Visible = !_editor.Visible;
        if (_toolbar.EndTurnPressed) _gameplay.EndTurn(_map);

        _debugMenu.Update(_inputManager);

        if (_debugMenu.WindowChanged)
        {
            _debugMenu.WindowChanged = false;
            ApplyWindowSettings();
        }

        if (_inputManager.F2Pressed)
            _editor.Visible = !_editor.Visible;

        if (_inputManager.EscapePressed)
        {
            if (_editor.Visible)
            {
                _editor.Visible = false;
            }
            else
            {
                _state = GameState.Menu;
                _startupMenu = new StartupMenu();
                return;
            }
        }

        if (!_editor.Visible && _inputManager.MapModePressed != 0 && _inputManager.MapModePressed != _currentMapMode)
            SetupMap(_inputManager.MapModePressed);

        _viewport.Update(seconds, _inputManager, _interaction);

        float minimapX = _minimap.ScreenX1;
        _infoPanel.Update(_inputManager, _interaction, minimapX, EngineConfig.Width, EngineConfig.Height);

        if (_infoPanel.ProductionStartType != null)
            _gameplay.StartProduction(_infoPanel.ProductionStartType);
        if (_infoPanel.ProductionCancelled)
            _gameplay.CancelProduction();

        bool uiConsumed = _toolbar.ConsumesClick || _debugMenu.ConsumesClick || _infoPanel.ConsumesClick;

        if (_editor.Visible)
        {
            _gameplay.Deselect();
            _gameplay.Update(_interaction);
            _editor.Update(_inputManager, _viewport, _interaction, uiConsumed);
        }
        else
        {
            if (!uiConsumed && _viewport.LastClickedTile != null)
                _gameplay.OnTileClicked(_viewport.LastClickedTile, _map);
            _gameplay.Update(_interaction);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        switch (_state)
        {
            case GameState.Menu:
                DrawMenu();
                break;
            case GameState.Playing:
                DrawPlaying();
                break;
        }

        base.Draw(gameTime);
    }

    private void DrawMenu()
    {
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        _startupMenu.Draw(_spriteBatch, _debugFont);
        _spriteBatch.End();

        // Draw mouse cursor
        _drawer.UpdateProjection(EngineConfig.Width, EngineConfig.Height);
        var mouse = Mouse.GetState();
        _drawer.DrawFilledRect(mouse.X - 5, mouse.Y - 5, 10, 10, Colors.WHITE);
    }

    private void DrawPlaying()
    {
        // Draw viewport to its render target
        _viewport.Draw(GraphicsDevice, _drawer, _renderer, _interaction, EngineConfig.ShowGrid);

        // Draw minimap to its render target
        _minimap.Draw(GraphicsDevice, _drawer, _renderer);

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

        _infoPanel.Draw(_spriteBatch, _debugFont, _pixel, _interaction);

        _toolbar.SetState(_debugMenu.Visible, _editor.Visible);
        _toolbar.Draw(_spriteBatch, _debugFont, _pixel, _gameplay.CurrentTeam);

        float panelY = _toolbar.Bottom;
        _debugMenu.Draw(_spriteBatch, _debugFont, _pixel, panelY);
        if (_debugMenu.Visible)
            panelY = _debugMenu.PanelBottom + Panel.BtnGap;
        _editor.Draw(_spriteBatch, _debugFont, _pixel, _interaction, panelY);

        _spriteBatch.End();

        // Draw minimap border and cursor using primitives
        _drawer.UpdateProjection(EngineConfig.Width, EngineConfig.Height);
        _drawer.DrawRectOutline(_minimap.ScreenX1 - 1, _minimap.ScreenY1 - 1,
                                _minimap.ScreenWidth + 2, _minimap.ScreenHeight + 2, Colors.RED);

        // Draw mouse cursor
        var mouse = Mouse.GetState();
        _drawer.DrawFilledRect(mouse.X - 5, mouse.Y - 5, 10, 10, Colors.WHITE);
    }

}
