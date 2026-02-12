using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Maps;
using HexEngine.Input;
using HexEngine.Config;
using HexEngine.Editor;
using HexEngine.Core;

namespace HexEngine.UI;

public enum MenuScreen { Main, PlaySelectMap, PlaySelectMode, EditorMenu, NewMap, EditorLoadMap }

public class StartupMenu
{
    public MenuScreen Screen { get; private set; } = MenuScreen.Main;
    public GridMap? ResultMap { get; private set; }
    public string? ResultMapPath { get; private set; }
    public GameMode ResultMode { get; private set; }
    public bool Done { get; private set; }
    public bool Quit { get; private set; }

    // Main menu
    private int _mainIndex;
    private readonly string[] _mainOptions = { "Play Game", "Map Editor" };

    // Editor sub-menu
    private int _editorIndex;
    private readonly string[] _editorOptions = { "New Hex Map", "Load Map" };

    // Play mode selection
    private int _modeIndex;
    private readonly string[] _modeOptions = { "Hot Seat", "Multiplayer" };

    // New map settings
    private int _newMapField; // 0 = cols, 1 = rows
    private readonly int[] _sizeOptions = { 8, 16, 24, 32, 48 };
    private int _colsIndex = 1; // default 16
    private int _rowsIndex = 1; // default 16

    // Load map
    private string[] _mapFiles = Array.Empty<string>();
    private int _loadIndex;

    public void Update(InputManager input)
    {
        if (Done) return;

        switch (Screen)
        {
            case MenuScreen.Main:
                UpdateMain(input);
                break;
            case MenuScreen.PlaySelectMap:
                UpdatePlaySelectMap(input);
                break;
            case MenuScreen.PlaySelectMode:
                UpdatePlaySelectMode(input);
                break;
            case MenuScreen.EditorMenu:
                UpdateEditorMenu(input);
                break;
            case MenuScreen.NewMap:
                UpdateNewMap(input);
                break;
            case MenuScreen.EditorLoadMap:
                UpdateEditorLoadMap(input);
                break;
        }
    }

    private void UpdateMain(InputManager input)
    {
        if (input.UpPressed)
            _mainIndex = (_mainIndex - 1 + _mainOptions.Length) % _mainOptions.Length;
        if (input.DownPressed)
            _mainIndex = (_mainIndex + 1) % _mainOptions.Length;

        if (input.EnterPressed)
        {
            if (_mainIndex == 0) // Play Game
            {
                _mapFiles = MapSerializer.GetMapFiles();
                _loadIndex = 0;
                Screen = MenuScreen.PlaySelectMap;
            }
            else if (_mainIndex == 1) // Map Editor
            {
                _editorIndex = 0;
                Screen = MenuScreen.EditorMenu;
            }
        }

        if (input.EscapePressed)
            Quit = true;
    }

    private void UpdatePlaySelectMap(InputManager input)
    {
        if (_mapFiles.Length > 0)
        {
            if (input.UpPressed)
                _loadIndex = (_loadIndex - 1 + _mapFiles.Length) % _mapFiles.Length;
            if (input.DownPressed)
                _loadIndex = (_loadIndex + 1) % _mapFiles.Length;

            if (input.EnterPressed)
            {
                string path = System.IO.Path.Combine(MapSerializer.GetMapsDirectory(), _mapFiles[_loadIndex]);
                ResultMap = MapSerializer.Load(path);
                ResultMapPath = path;
                _modeIndex = 0;
                Screen = MenuScreen.PlaySelectMode;
            }
        }

        if (input.EscapePressed)
            Screen = MenuScreen.Main;
    }

    private void UpdatePlaySelectMode(InputManager input)
    {
        if (input.UpPressed)
            _modeIndex = (_modeIndex - 1 + _modeOptions.Length) % _modeOptions.Length;
        if (input.DownPressed)
            _modeIndex = (_modeIndex + 1) % _modeOptions.Length;

        if (input.EnterPressed)
        {
            ResultMode = _modeIndex == 0 ? GameMode.HotSeat : GameMode.Multiplayer;
            Done = true;
        }

        if (input.EscapePressed)
        {
            ResultMap = null;
            ResultMapPath = null;
            _mapFiles = MapSerializer.GetMapFiles();
            _loadIndex = 0;
            Screen = MenuScreen.PlaySelectMap;
        }
    }

    private void UpdateEditorMenu(InputManager input)
    {
        if (input.UpPressed)
            _editorIndex = (_editorIndex - 1 + _editorOptions.Length) % _editorOptions.Length;
        if (input.DownPressed)
            _editorIndex = (_editorIndex + 1) % _editorOptions.Length;

        if (input.EnterPressed)
        {
            if (_editorIndex == 0) // New Hex Map
            {
                Screen = MenuScreen.NewMap;
            }
            else if (_editorIndex == 1) // Load Map
            {
                _mapFiles = MapSerializer.GetMapFiles();
                _loadIndex = 0;
                Screen = MenuScreen.EditorLoadMap;
            }
        }

        if (input.EscapePressed)
            Screen = MenuScreen.Main;
    }

    private void UpdateNewMap(InputManager input)
    {
        if (input.UpPressed || input.DownPressed)
            _newMapField = _newMapField == 0 ? 1 : 0;

        if (input.LeftPressed)
        {
            if (_newMapField == 0)
                _colsIndex = (_colsIndex - 1 + _sizeOptions.Length) % _sizeOptions.Length;
            else
                _rowsIndex = (_rowsIndex - 1 + _sizeOptions.Length) % _sizeOptions.Length;
        }
        if (input.RightPressed)
        {
            if (_newMapField == 0)
                _colsIndex = (_colsIndex + 1) % _sizeOptions.Length;
            else
                _rowsIndex = (_rowsIndex + 1) % _sizeOptions.Length;
        }

        if (input.EnterPressed)
        {
            int cols = _sizeOptions[_colsIndex];
            int rows = _sizeOptions[_rowsIndex];
            ResultMap = new HexGridMap(cols, rows, hexRadius: 100f, hexVerticalScale: 0.7f, hexOrientation: "flat");
            ResultMode = GameMode.Editor;
            Done = true;
        }

        if (input.EscapePressed)
            Screen = MenuScreen.EditorMenu;
    }

    private void UpdateEditorLoadMap(InputManager input)
    {
        if (_mapFiles.Length > 0)
        {
            if (input.UpPressed)
                _loadIndex = (_loadIndex - 1 + _mapFiles.Length) % _mapFiles.Length;
            if (input.DownPressed)
                _loadIndex = (_loadIndex + 1) % _mapFiles.Length;

            if (input.EnterPressed)
            {
                string path = System.IO.Path.Combine(MapSerializer.GetMapsDirectory(), _mapFiles[_loadIndex]);
                ResultMap = MapSerializer.Load(path);
                ResultMapPath = path;
                ResultMode = GameMode.Editor;
                Done = true;
            }
        }

        if (input.EscapePressed)
            Screen = MenuScreen.EditorMenu;
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font)
    {
        float centerX = EngineConfig.Width / 2f;
        float lineHeight = font.LineSpacing + 6;

        switch (Screen)
        {
            case MenuScreen.Main:
                DrawMain(spriteBatch, font, centerX, lineHeight);
                break;
            case MenuScreen.PlaySelectMap:
                DrawMapList(spriteBatch, font, centerX, lineHeight, "SELECT MAP");
                break;
            case MenuScreen.PlaySelectMode:
                DrawPlaySelectMode(spriteBatch, font, centerX, lineHeight);
                break;
            case MenuScreen.EditorMenu:
                DrawEditorMenu(spriteBatch, font, centerX, lineHeight);
                break;
            case MenuScreen.NewMap:
                DrawNewMap(spriteBatch, font, centerX, lineHeight);
                break;
            case MenuScreen.EditorLoadMap:
                DrawMapList(spriteBatch, font, centerX, lineHeight, "LOAD MAP");
                break;
        }
    }

    private void DrawMain(SpriteBatch spriteBatch, SpriteFont font, float centerX, float lineHeight)
    {
        float y = EngineConfig.Height / 3f;

        DrawCentered(spriteBatch, font, "HEX ENGINE", centerX, y, Color.Yellow);
        y += lineHeight * 2;

        for (int i = 0; i < _mainOptions.Length; i++)
        {
            string prefix = i == _mainIndex ? "> " : "  ";
            Color color = i == _mainIndex ? Color.Cyan : Color.White;
            DrawCentered(spriteBatch, font, prefix + _mainOptions[i], centerX, y, color);
            y += lineHeight;
        }

        y += lineHeight;
        DrawCentered(spriteBatch, font, "Up/Down: Navigate | Enter: Select | Esc: Quit", centerX, y, Color.Gray);
    }

    private void DrawMapList(SpriteBatch spriteBatch, SpriteFont font, float centerX, float lineHeight, string title)
    {
        float y = EngineConfig.Height / 3f;

        DrawCentered(spriteBatch, font, title, centerX, y, Color.Yellow);
        y += lineHeight * 2;

        if (_mapFiles.Length == 0)
        {
            DrawCentered(spriteBatch, font, "No map files found in maps/", centerX, y, Color.Gray);
            y += lineHeight * 2;
            DrawCentered(spriteBatch, font, "Esc: Back", centerX, y, Color.Gray);
            return;
        }

        for (int i = 0; i < _mapFiles.Length; i++)
        {
            string prefix = i == _loadIndex ? "> " : "  ";
            Color color = i == _loadIndex ? Color.Cyan : Color.White;
            DrawCentered(spriteBatch, font, prefix + _mapFiles[i], centerX, y, color);
            y += lineHeight;
        }

        y += lineHeight;
        DrawCentered(spriteBatch, font, "Up/Down: Navigate | Enter: Select | Esc: Back", centerX, y, Color.Gray);
    }

    private void DrawPlaySelectMode(SpriteBatch spriteBatch, SpriteFont font, float centerX, float lineHeight)
    {
        float y = EngineConfig.Height / 3f;

        DrawCentered(spriteBatch, font, "SELECT MODE", centerX, y, Color.Yellow);
        y += lineHeight * 2;

        for (int i = 0; i < _modeOptions.Length; i++)
        {
            string prefix = i == _modeIndex ? "> " : "  ";
            Color color = i == _modeIndex ? Color.Cyan : Color.White;
            DrawCentered(spriteBatch, font, prefix + _modeOptions[i], centerX, y, color);
            y += lineHeight;
        }

        y += lineHeight;
        DrawCentered(spriteBatch, font, "Up/Down: Navigate | Enter: Select | Esc: Back", centerX, y, Color.Gray);
    }

    private void DrawEditorMenu(SpriteBatch spriteBatch, SpriteFont font, float centerX, float lineHeight)
    {
        float y = EngineConfig.Height / 3f;

        DrawCentered(spriteBatch, font, "MAP EDITOR", centerX, y, Color.Yellow);
        y += lineHeight * 2;

        for (int i = 0; i < _editorOptions.Length; i++)
        {
            string prefix = i == _editorIndex ? "> " : "  ";
            Color color = i == _editorIndex ? Color.Cyan : Color.White;
            DrawCentered(spriteBatch, font, prefix + _editorOptions[i], centerX, y, color);
            y += lineHeight;
        }

        y += lineHeight;
        DrawCentered(spriteBatch, font, "Up/Down: Navigate | Enter: Select | Esc: Back", centerX, y, Color.Gray);
    }

    private void DrawNewMap(SpriteBatch spriteBatch, SpriteFont font, float centerX, float lineHeight)
    {
        float y = EngineConfig.Height / 3f;

        DrawCentered(spriteBatch, font, "NEW HEX MAP", centerX, y, Color.Yellow);
        y += lineHeight * 2;

        string colsText = $"Cols: < {_sizeOptions[_colsIndex]} >";
        string rowsText = $"Rows: < {_sizeOptions[_rowsIndex]} >";

        DrawCentered(spriteBatch, font, colsText, centerX, y,
            _newMapField == 0 ? Color.Cyan : Color.White);
        y += lineHeight;
        DrawCentered(spriteBatch, font, rowsText, centerX, y,
            _newMapField == 1 ? Color.Cyan : Color.White);
        y += lineHeight * 2;

        DrawCentered(spriteBatch, font, "Up/Down: Select Field | Left/Right: Change Size", centerX, y, Color.Gray);
        y += lineHeight;
        DrawCentered(spriteBatch, font, "Enter: Create | Esc: Back", centerX, y, Color.Gray);
    }

    private static void DrawCentered(SpriteBatch spriteBatch, SpriteFont font, string text, float centerX, float y, Color color)
    {
        var size = font.MeasureString(text);
        spriteBatch.DrawString(font, text, new Vector2(centerX - size.X / 2f, y), color);
    }
}
