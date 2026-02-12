using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Maps;
using HexEngine.Tiles;
using Viewport = HexEngine.View.Viewport;
using HexEngine.Core;
using HexEngine.Input;
using HexEngine.UI;
using HexEngine.Config;
using HexEngine.Rendering;

namespace HexEngine.Editor;

public enum EditorTool { Elevation, Ramp }

public class MapEditor : Panel
{
    public EditorTool CurrentTool { get; private set; } = EditorTool.Elevation;

    private string? _lastSavePath;
    private string? _statusMessage;
    private float _statusTimer;

    // Cached panel layout
    private float _panelX, _panelY, _panelWidth, _panelHeight;
    private Rectangle? _elevBtn, _rampBtn;
    private Rectangle? _saveBtn;

    public void Update(InputManager input, Viewport viewport, InteractionState state, bool externalClickConsumed)
    {
        ConsumesClick = false;
        state.HighlightedEdgeIndex = null;
        state.HighlightedEdgeTile = null;
        if (!Visible) return;

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

            if (ConsumeIfHit(mx, my, _panelX, _panelY, _panelWidth, _panelHeight))
            {

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
            ConsumeIfHit(input.MousePos.X, input.MousePos.Y, _panelX, _panelY, _panelWidth, _panelHeight);

        // Tile interaction (only if no click consumed)
        bool clickConsumed = externalClickConsumed || ConsumesClick;
        if (CurrentTool == EditorTool.Elevation)
            DoElevation(input, viewport, state, clickConsumed);
        else
            DoRamp(input, viewport, state, clickConsumed);

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

    private void DoElevation(InputManager input, Viewport viewport, InteractionState state, bool clickConsumed)
    {
        if (state.HoverTile == null) return;
        if (clickConsumed) return;

        if (input.MouseReleased)
        {
            var tile = state.HoverTile;
            tile.Elevation++;
            RemoveInvalidRamps(tile, viewport.Map);
        }

        if (input.RightMouseReleased && state.HoverTile.Elevation > 0)
        {
            var tile = state.HoverTile;
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

    private void DoRamp(InputManager input, Viewport viewport, InteractionState state, bool clickConsumed)
    {
        if (state.HoverTile == null) return;

        var tile = state.HoverTile;
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

        state.HighlightedEdgeIndex = bestEdge;
        state.HighlightedEdgeTile = tile;

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

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, InteractionState state, float startY)
    {
        PanelBottom = startY;
        if (!Visible) return;

        float x = 10;
        float y = startY;

        // Row 1: Tool buttons
        float elevBtnW = font.MeasureString("Elev").X + Padding;
        float rampBtnW = font.MeasureString("Ramp").X + Padding;

        // Row 2: Tile info
        string tileInfo = state.HoverTile != null
            ? $"Tile [{state.HoverTile.X},{state.HoverTile.Y}] Elev: {state.HoverTile.Elevation}"
            : "Tile: -";

        // Row 3: Save
        float saveBtnW = font.MeasureString("Save").X + Padding;

        // Row 4: Help text
        string helpText = CurrentTool == EditorTool.Elevation
            ? "LMB: Raise | RMB: Lower"
            : "LMB: Toggle Ramp";

        // Compute panel width
        float maxWidth = Math.Max(elevBtnW + BtnGap + rampBtnW,
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

        DrawBg(spriteBatch, pixel, x, y, panelWidth, panelHeight);

        float cy = y + Padding;

        // Row 1: Tool buttons
        _elevBtn = new Rectangle((int)(x + Padding), (int)cy, (int)elevBtnW, (int)BtnHeight);
        _rampBtn = new Rectangle((int)(x + Padding + elevBtnW + BtnGap), (int)cy, (int)rampBtnW, (int)BtnHeight);
        DrawBtn(spriteBatch, pixel, font, _elevBtn.Value, "Elev",
            CurrentTool == EditorTool.Elevation ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        DrawBtn(spriteBatch, pixel, font, _rampBtn.Value, "Ramp",
            CurrentTool == EditorTool.Ramp ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        cy += RowHeight;

        // Row 2: Tile info
        spriteBatch.DrawString(font, tileInfo, new Vector2(x + Padding, cy), Color.White);
        cy += RowHeight;

        // Row 3: Save
        _saveBtn = new Rectangle((int)(x + Padding), (int)cy, (int)saveBtnW, (int)BtnHeight);
        DrawBtn(spriteBatch, pixel, font, _saveBtn.Value, "Save", new Color(40, 80, 40, 200));
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

}
