using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine;

public enum EditorTool { Elevation, Ramp }

public class MapEditor
{
    public bool Active { get; set; }
    public bool ConsumesClick { get; private set; }
    public EditorTool CurrentTool { get; private set; } = EditorTool.Elevation;
    public int? HoveredEdge { get; private set; }
    public Tile? HoveredEdgeTile { get; private set; }
    public float PanelBottom { get; private set; }

    private string? _lastSavePath;
    private string? _statusMessage;
    private float _statusTimer;

    // Layout constants
    private const float Padding = 8f;
    private const float ButtonHeight = 20f;
    private const float ButtonGap = 4f;
    private const float RowHeight = 24f;
    private const float SmallBtnWidth = 28f;

    // Cached panel layout
    private float _panelX, _panelY, _panelWidth, _panelHeight;
    private Rectangle? _elevBtn, _rampBtn;
    private Rectangle? _saveBtn;

    public void Update(InputManager input, Viewport viewport, bool externalClickConsumed)
    {
        ConsumesClick = false;
        if (!Active) return;

        // Keyboard shortcuts for tool switching
        if (input.EPressed)
            CurrentTool = EditorTool.Elevation;
        if (input.RPressed)
            CurrentTool = EditorTool.Ramp;

        // Panel button clicks
        if (input.MouseReleased && !externalClickConsumed)
        {
            float mx = input.MousePos.X;
            float my = input.MousePos.Y;

            if (mx >= _panelX && mx <= _panelX + _panelWidth &&
                my >= _panelY && my <= _panelY + _panelHeight)
            {
                ConsumesClick = true;

                if (_elevBtn.HasValue && InRect(mx, my, _elevBtn.Value))
                    CurrentTool = EditorTool.Elevation;
                if (_rampBtn.HasValue && InRect(mx, my, _rampBtn.Value))
                    CurrentTool = EditorTool.Ramp;
                if (_saveBtn.HasValue && InRect(mx, my, _saveBtn.Value))
                    DoSave(viewport);
            }
        }

        // Click-through prevention
        if (input.MouseDown && !externalClickConsumed)
        {
            float mx = input.MousePos.X;
            float my = input.MousePos.Y;
            if (mx >= _panelX && mx <= _panelX + _panelWidth &&
                my >= _panelY && my <= _panelY + _panelHeight)
                ConsumesClick = true;
        }

        // Tile interaction (only if no click consumed)
        bool clickConsumed = externalClickConsumed || ConsumesClick;
        if (CurrentTool == EditorTool.Elevation)
            UpdateElevationTool(input, viewport, clickConsumed);
        else
            UpdateRampTool(input, viewport, clickConsumed);

        // Ctrl+S save shortcut
        if (input.CtrlS)
            DoSave(viewport);

        if (_statusTimer > 0)
            _statusTimer -= 1f / 60f;
    }

    private void DoSave(Viewport viewport)
    {
        if (_lastSavePath == null)
            _lastSavePath = Path.Combine(MapSerializer.GetMapsDirectory(), "map.json");
        MapSerializer.Save(viewport.Map, _lastSavePath);
        _statusMessage = $"Saved to {_lastSavePath}";
        _statusTimer = 2f;
    }

    private void UpdateElevationTool(InputManager input, Viewport viewport, bool clickConsumed)
    {
        HoveredEdge = null;
        HoveredEdgeTile = null;

        if (viewport.HoverTile == null) return;
        if (clickConsumed) return;

        if (input.MouseReleased)
        {
            var tile = viewport.HoverTile;
            tile.Elevation++;
            RemoveInvalidRamps(tile, viewport.Map);
        }

        if (input.RightMouseReleased && viewport.HoverTile.Elevation > 0)
        {
            var tile = viewport.HoverTile;
            tile.Elevation--;
            RemoveInvalidRamps(tile, viewport.Map);
        }
    }

    private static void RemoveInvalidRamps(Tile tile, GridMap map)
    {
        var edgesToRemove = new System.Collections.Generic.List<int>();
        foreach (int edge in tile.Ramps)
        {
            var neighbor = map.GetNeighbor(tile, edge);
            if (neighbor == null || tile.Elevation == neighbor.Elevation)
                edgesToRemove.Add(edge);
        }
        foreach (int edge in edgesToRemove)
            map.RemoveRamp(tile, edge);

        for (int e = 0; e < map.EdgeCount; e++)
        {
            var neighbor = map.GetNeighbor(tile, e);
            if (neighbor == null) continue;
            int opposite = map.GetOppositeEdge(e);
            if (neighbor.Ramps.Contains(opposite) && tile.Elevation == neighbor.Elevation)
                map.RemoveRamp(neighbor, opposite);
        }
    }

    private void UpdateRampTool(InputManager input, Viewport viewport, bool clickConsumed)
    {
        HoveredEdge = null;
        HoveredEdgeTile = null;

        if (viewport.HoverTile == null) return;

        var tile = viewport.HoverTile;
        var screenPoints = viewport.GetTileScreenPoints(tile);
        var mousePos = new Vector2(input.MousePos.X - viewport.ScreenX1, input.MousePos.Y - viewport.ScreenY1);

        int edgeCount = viewport.Map.EdgeCount;
        float bestDist = float.MaxValue;
        int bestEdge = 0;

        for (int e = 0; e < edgeCount; e++)
        {
            int nextIdx = (e + 1) % screenPoints.Length;
            var mid = (screenPoints[e] + screenPoints[nextIdx]) / 2f;
            float dist = Vector2.Distance(mousePos, mid);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestEdge = e;
            }
        }

        HoveredEdge = bestEdge;
        HoveredEdgeTile = tile;

        if (clickConsumed) return;

        if (input.MouseReleased)
        {
            if (tile.Ramps.Contains(bestEdge))
                viewport.Map.RemoveRamp(tile, bestEdge);
            else
                viewport.Map.AddRamp(tile, bestEdge);
        }
    }

    public void SetLastSavePath(string? path) => _lastSavePath = path;

    private static bool InRect(float x, float y, Rectangle r)
        => x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height;

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Viewport viewport, float startY)
    {
        PanelBottom = startY;
        if (!Active) return;

        float x = 10;
        float y = startY;

        // Row 1: Tool buttons
        float elevBtnW = font.MeasureString("Elev").X + Padding;
        float rampBtnW = font.MeasureString("Ramp").X + Padding;

        // Row 2: Tile info
        string tileInfo = viewport.HoverTile != null
            ? $"Tile [{viewport.HoverTile.X},{viewport.HoverTile.Y}] Elev: {viewport.HoverTile.Elevation}"
            : "Tile: -";

        // Row 3: Save
        float saveBtnW = font.MeasureString("Save").X + Padding;

        // Row 4: Help text
        string helpText = CurrentTool == EditorTool.Elevation
            ? "LMB: Raise | RMB: Lower"
            : "LMB: Toggle Ramp";

        // Compute panel width
        float maxWidth = Math.Max(elevBtnW + ButtonGap + rampBtnW,
                         Math.Max(font.MeasureString(tileInfo).X,
                         Math.Max(saveBtnW, font.MeasureString(helpText).X)));

        int rows = 4;
        if (_statusTimer > 0) rows++;
        float panelWidth = maxWidth + Padding * 2;
        float panelHeight = rows * RowHeight + Padding * 2;

        _panelX = x;
        _panelY = y;
        _panelWidth = panelWidth;
        _panelHeight = panelHeight;
        PanelBottom = y + panelHeight;

        // Background
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)panelWidth, (int)panelHeight), new Color(0, 0, 0, 200));

        float cy = y + Padding;

        // Row 1: Tool buttons
        _elevBtn = new Rectangle((int)(x + Padding), (int)cy, (int)elevBtnW, (int)ButtonHeight);
        _rampBtn = new Rectangle((int)(x + Padding + elevBtnW + ButtonGap), (int)cy, (int)rampBtnW, (int)ButtonHeight);
        DrawButton(spriteBatch, pixel, font, _elevBtn.Value, "Elev",
            CurrentTool == EditorTool.Elevation ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        DrawButton(spriteBatch, pixel, font, _rampBtn.Value, "Ramp",
            CurrentTool == EditorTool.Ramp ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        cy += RowHeight;

        // Row 2: Tile info
        spriteBatch.DrawString(font, tileInfo, new Vector2(x + Padding, cy), Color.White);
        cy += RowHeight;

        // Row 3: Save
        _saveBtn = new Rectangle((int)(x + Padding), (int)cy, (int)saveBtnW, (int)ButtonHeight);
        DrawButton(spriteBatch, pixel, font, _saveBtn.Value, "Save", new Color(40, 80, 40, 200));
        cy += RowHeight;

        // Row 5: Status (if any)
        if (_statusTimer > 0 && _statusMessage != null)
        {
            spriteBatch.DrawString(font, _statusMessage, new Vector2(x + Padding, cy), Color.LimeGreen);
            cy += RowHeight;
        }

        // Row 6: Help text
        spriteBatch.DrawString(font, helpText, new Vector2(x + Padding, cy), Color.Gray);
    }

    private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font,
                                    Rectangle rect, string text, Color bgColor)
    {
        spriteBatch.Draw(pixel, rect, bgColor);
        var textSize = font.MeasureString(text);
        float tx = rect.X + (rect.Width - textSize.X) / 2f;
        float ty = rect.Y + (rect.Height - textSize.Y) / 2f;
        spriteBatch.DrawString(font, text, new Vector2(tx, ty), Color.White);
    }
}
