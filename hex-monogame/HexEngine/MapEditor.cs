using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine;

public class MapEditor
{
    public bool Active { get; set; }

    private string? _lastSavePath;
    private string? _statusMessage;
    private float _statusTimer;

    public void Update(InputManager input, Viewport viewport)
    {
        if (!Active) return;

        // Elevation tool: left-click raise, right-click lower
        if (input.MouseReleased && viewport.HoverTile != null)
            viewport.HoverTile.Elevation++;

        if (input.RightMouseReleased && viewport.HoverTile != null && viewport.HoverTile.Elevation > 0)
            viewport.HoverTile.Elevation--;

        // Ctrl+S: Save
        if (input.CtrlS)
        {
            if (_lastSavePath == null)
                _lastSavePath = Path.Combine(MapSerializer.GetMapsDirectory(), "map.json");
            MapSerializer.Save(viewport.Map, _lastSavePath);
            _statusMessage = $"Saved to {_lastSavePath}";
            _statusTimer = 2f;
        }

        if (_statusTimer > 0)
            _statusTimer -= 1f / 60f;
    }

    public void SetLastSavePath(string? path) => _lastSavePath = path;

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Viewport viewport)
    {
        if (!Active) return;

        float x = 10;
        float y = 10;
        float lineHeight = font.LineSpacing + 4;
        float padding = 8;

        string line1 = "EDITOR: Elevation Tool";
        string line2 = viewport.HoverTile != null
            ? $"Tile [{viewport.HoverTile.X},{viewport.HoverTile.Y}] Elev: {viewport.HoverTile.Elevation}"
            : "Tile: -";
        string line3 = "LMB: Raise | RMB: Lower | Ctrl+S: Save | F2: Exit";

        float maxWidth = Math.Max(font.MeasureString(line1).X,
                         Math.Max(font.MeasureString(line2).X, font.MeasureString(line3).X));

        int lines = 3;
        if (_statusTimer > 0) lines++;

        float bgWidth = maxWidth + padding * 2;
        float bgHeight = lines * lineHeight + padding * 2;

        var pixel = new Texture2D(spriteBatch.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { new Color(0, 0, 0, 180) });
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)bgWidth, (int)bgHeight), Color.White);

        spriteBatch.DrawString(font, line1, new Vector2(x + padding, y + padding), Color.Yellow);
        spriteBatch.DrawString(font, line2, new Vector2(x + padding, y + padding + lineHeight), Color.White);
        spriteBatch.DrawString(font, line3, new Vector2(x + padding, y + padding + lineHeight * 2), Color.Gray);

        if (_statusTimer > 0 && _statusMessage != null)
            spriteBatch.DrawString(font, _statusMessage, new Vector2(x + padding, y + padding + lineHeight * 3), Color.LimeGreen);

        pixel.Dispose();
    }
}
