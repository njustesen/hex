using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Maps;

namespace HexEngine;

public enum MenuScreen { Main, NewMap, LoadMap }

public class StartupMenu
{
    public MenuScreen Screen { get; private set; } = MenuScreen.Main;
    public GridMap? ResultMap { get; private set; }
    public string? ResultMapPath { get; private set; }
    public bool Done { get; private set; }
    public bool Quit { get; private set; }

    // Main menu
    private int _mainIndex;
    private readonly string[] _mainOptions = { "New Hex Map", "Load Map" };

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
            case MenuScreen.NewMap:
                UpdateNewMap(input);
                break;
            case MenuScreen.LoadMap:
                UpdateLoadMap(input);
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
            if (_mainIndex == 0)
            {
                Screen = MenuScreen.NewMap;
            }
            else if (_mainIndex == 1)
            {
                _mapFiles = MapSerializer.GetMapFiles();
                _loadIndex = 0;
                Screen = MenuScreen.LoadMap;
            }
        }

        if (input.EscapePressed)
            Quit = true;
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
            Done = true;
        }

        if (input.EscapePressed)
            Screen = MenuScreen.Main;
    }

    private void UpdateLoadMap(InputManager input)
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
                Done = true;
            }
        }

        if (input.EscapePressed)
            Screen = MenuScreen.Main;
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
            case MenuScreen.NewMap:
                DrawNewMap(spriteBatch, font, centerX, lineHeight);
                break;
            case MenuScreen.LoadMap:
                DrawLoadMap(spriteBatch, font, centerX, lineHeight);
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

    private void DrawLoadMap(SpriteBatch spriteBatch, SpriteFont font, float centerX, float lineHeight)
    {
        float y = EngineConfig.Height / 3f;

        DrawCentered(spriteBatch, font, "LOAD MAP", centerX, y, Color.Yellow);
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
        DrawCentered(spriteBatch, font, "Up/Down: Navigate | Enter: Load | Esc: Back", centerX, y, Color.Gray);
    }

    private static void DrawCentered(SpriteBatch spriteBatch, SpriteFont font, string text, float centerX, float y, Color color)
    {
        var size = font.MeasureString(text);
        spriteBatch.DrawString(font, text, new Vector2(centerX - size.X / 2f, y), color);
    }
}
