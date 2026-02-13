using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public enum EditorTool { Elevation, Ramp, Unit, Resource, StartLoc }

public class MapEditor : Panel
{
    public EditorTool CurrentTool { get; private set; } = EditorTool.Elevation;

    private int _selectedUnitIndex;
    private Team _selectedTeam = Team.Red;
    private ResourceType _selectedResource = ResourceType.Iron;
    private string? _lastSavePath;
    private GridMap? _lastMap;
    private string? _statusMessage;
    private float _statusTimer;

    private string SelectedUnitType
    {
        get
        {
            var names = UnitDefs.TypeNames;
            if (names.Count == 0) return "Marine";
            return names[Math.Clamp(_selectedUnitIndex, 0, names.Count - 1)];
        }
    }

    // Cached panel layout
    private float _panelX, _panelY, _panelWidth, _panelHeight;
    private Rectangle? _elevBtn, _rampBtn, _unitBtn, _resBtn, _startLocBtn;
    private List<(Rectangle rect, string typeName)> _unitTypeButtons = new();
    private Rectangle? _redBtn, _blueBtn;
    private Rectangle? _ironBtn, _fissiumBtn;
    private Rectangle? _saveBtn;

    public void Update(InputManager input, Viewport viewport, InteractionState state, bool externalClickConsumed)
    {
        ConsumesClick = false;
        state.HighlightedEdgeIndex = null;
        state.HighlightedEdgeTile = null;
        _lastMap = viewport.Map;
        if (!Visible) return;

        // Keyboard shortcuts for tool switching
        if (input.EPressed)
            CurrentTool = EditorTool.Elevation;
        if (input.RPressed)
            CurrentTool = EditorTool.Ramp;
        if (input.UPressed)
            CurrentTool = EditorTool.Unit;
        if (input.GPressed)
            CurrentTool = EditorTool.Resource;
        if (input.SPressed)
            CurrentTool = EditorTool.StartLoc;

        // Cycle unit type with [ / ]
        if (CurrentTool == EditorTool.Unit)
        {
            int count = UnitDefs.TypeNames.Count;
            if (count > 0)
            {
                if (input.BracketRightPressed)
                    _selectedUnitIndex = (_selectedUnitIndex + 1) % count;
                if (input.BracketLeftPressed)
                    _selectedUnitIndex = (_selectedUnitIndex - 1 + count) % count;
            }
            if (input.TPressed)
                _selectedTeam = _selectedTeam == Team.Red ? Team.Blue : Team.Red;
        }

        // Cycle resource type with [ / ]
        if (CurrentTool == EditorTool.Resource)
        {
            if (input.BracketRightPressed)
                _selectedResource = _selectedResource == ResourceType.Iron ? ResourceType.Fissium : ResourceType.Iron;
            if (input.BracketLeftPressed)
                _selectedResource = _selectedResource == ResourceType.Iron ? ResourceType.Fissium : ResourceType.Iron;
        }

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
                if (_unitBtn.HasValue && InRect(mx, my, _unitBtn.Value))
                    CurrentTool = EditorTool.Unit;
                if (_resBtn.HasValue && InRect(mx, my, _resBtn.Value))
                    CurrentTool = EditorTool.Resource;
                if (_startLocBtn.HasValue && InRect(mx, my, _startLocBtn.Value))
                    CurrentTool = EditorTool.StartLoc;
                for (int i = 0; i < _unitTypeButtons.Count; i++)
                {
                    if (InRect(mx, my, _unitTypeButtons[i].rect))
                    {
                        _selectedUnitIndex = i;
                        break;
                    }
                }
                if (_redBtn.HasValue && InRect(mx, my, _redBtn.Value))
                    _selectedTeam = Team.Red;
                if (_blueBtn.HasValue && InRect(mx, my, _blueBtn.Value))
                    _selectedTeam = Team.Blue;
                if (_ironBtn.HasValue && InRect(mx, my, _ironBtn.Value))
                    _selectedResource = ResourceType.Iron;
                if (_fissiumBtn.HasValue && InRect(mx, my, _fissiumBtn.Value))
                    _selectedResource = ResourceType.Fissium;
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
        else if (CurrentTool == EditorTool.Ramp)
            DoRamp(input, viewport, state, clickConsumed);
        else if (CurrentTool == EditorTool.Unit)
            DoUnit(input, state, clickConsumed);
        else if (CurrentTool == EditorTool.Resource)
            DoResource(input, state, clickConsumed);
        else if (CurrentTool == EditorTool.StartLoc)
            DoStartLoc(input, state, clickConsumed);

        // Ctrl+S save shortcut
        if (input.CtrlS)
            DoSave(viewport);

        if (_statusTimer > 0)
            _statusTimer -= 1f / 60f;
    }

    private void DoUnit(InputManager input, InteractionState state, bool clickConsumed)
    {
        if (state.HoverTile == null) return;
        if (clickConsumed) return;

        if (input.MouseReleased)
            state.HoverTile.Unit = new Unit(SelectedUnitType) { Team = _selectedTeam };

        if (input.RightMouseReleased)
            state.HoverTile.Unit = null;
    }

    private void DoResource(InputManager input, InteractionState state, bool clickConsumed)
    {
        if (state.HoverTile == null) return;
        if (clickConsumed) return;

        if (input.MouseReleased)
            state.HoverTile.Resource = _selectedResource;

        if (input.RightMouseReleased)
            state.HoverTile.Resource = ResourceType.None;
    }

    private void DoStartLoc(InputManager input, InteractionState state, bool clickConsumed)
    {
        if (state.HoverTile == null) return;
        if (clickConsumed) return;

        if (input.MouseReleased)
        {
            // Enforce max 2: if placing a 3rd, clear the oldest
            if (!state.HoverTile.IsStartingLocation)
            {
                var existing = new List<Tile>();
                var map = _lastMap;
                if (map != null)
                {
                    for (int y = 0; y < map.Rows; y++)
                        for (int x = 0; x < map.Cols; x++)
                            if (map.Tiles[y][x].IsStartingLocation)
                                existing.Add(map.Tiles[y][x]);
                }
                if (existing.Count >= 2)
                    existing[0].IsStartingLocation = false;
            }
            state.HoverTile.Unit = null;
            state.HoverTile.IsStartingLocation = true;
        }

        if (input.RightMouseReleased)
            state.HoverTile.IsStartingLocation = false;
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
        var edgesToRemove = new List<int>();
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

    private static string ShortName(string typeName, int maxLen = 6)
    {
        if (typeName.Length <= maxLen) return typeName;
        return typeName.Substring(0, maxLen - 1) + ".";
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, InteractionState state, float startY)
    {
        PanelBottom = startY;
        if (!Visible) return;

        float x = 10;
        float y = startY;

        // Row 1: Tool buttons
        float elevBtnW = font.MeasureString("Elev").X + Padding;
        float rampBtnW = font.MeasureString("Ramp").X + Padding;
        float unitBtnW = font.MeasureString("Unit").X + Padding;
        float resBtnW = font.MeasureString("Res").X + Padding;
        float startLocBtnW = font.MeasureString("Start").X + Padding;

        // Unit type sub-row widths (dynamic)
        var typeNames = UnitDefs.TypeNames;
        float unitTypeRowW = 0;
        var typeWidths = new List<float>();
        for (int i = 0; i < typeNames.Count; i++)
        {
            string shortName = ShortName(typeNames[i]);
            float w = font.MeasureString(shortName).X + Padding;
            typeWidths.Add(w);
            unitTypeRowW += w;
            if (i > 0) unitTypeRowW += BtnGap;
        }

        // Team sub-row widths
        float redBtnW = font.MeasureString("Red").X + Padding;
        float blueBtnW = font.MeasureString("Blue").X + Padding;
        float teamRowW = redBtnW + BtnGap + blueBtnW;

        // Resource sub-row widths
        float ironBtnW = font.MeasureString("Iron").X + Padding;
        float fissBtnW = font.MeasureString("Fissium").X + Padding;
        float resTypeRowW = ironBtnW + BtnGap + fissBtnW;

        // Row 2: Tile info
        string tileInfo = state.HoverTile != null
            ? $"Tile [{state.HoverTile.X},{state.HoverTile.Y}] Elev: {state.HoverTile.Elevation}" +
              (state.HoverTile.Resource != ResourceType.None ? $" Res: {state.HoverTile.Resource}" : "")
            : "Tile: -";

        // Row 3: Save
        float saveBtnW = font.MeasureString("Save").X + Padding;

        // Row 4: Help text
        string helpText = CurrentTool switch
        {
            EditorTool.Elevation => "LMB: Raise | RMB: Lower",
            EditorTool.Ramp => "LMB: Toggle Ramp",
            EditorTool.Unit => $"LMB: Place {_selectedTeam} {SelectedUnitType} | RMB: Remove | [/]: Cycle | T: Team",
            EditorTool.Resource => $"LMB: Place {_selectedResource} | RMB: Remove | [/]: Cycle",
            EditorTool.StartLoc => "LMB: Place Start | RMB: Remove (max 2)",
            _ => ""
        };

        // Compute panel width
        float toolRowW = elevBtnW + BtnGap + rampBtnW + BtnGap + unitBtnW + BtnGap + resBtnW + BtnGap + startLocBtnW;
        float subRowW = 0;
        if (CurrentTool == EditorTool.Unit) subRowW = Math.Max(unitTypeRowW, teamRowW);
        else if (CurrentTool == EditorTool.Resource) subRowW = resTypeRowW;
        float maxWidth = Math.Max(toolRowW,
                         Math.Max(subRowW,
                         Math.Max(font.MeasureString(tileInfo).X,
                         Math.Max(saveBtnW, font.MeasureString(helpText).X))));

        int rows = 4;
        if (CurrentTool == EditorTool.Unit) rows += 2;
        if (CurrentTool == EditorTool.Resource) rows += 1;
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
        _unitBtn = new Rectangle((int)(x + Padding + elevBtnW + BtnGap + rampBtnW + BtnGap), (int)cy, (int)unitBtnW, (int)BtnHeight);
        _resBtn = new Rectangle((int)(x + Padding + elevBtnW + BtnGap + rampBtnW + BtnGap + unitBtnW + BtnGap), (int)cy, (int)resBtnW, (int)BtnHeight);
        _startLocBtn = new Rectangle((int)(x + Padding + elevBtnW + BtnGap + rampBtnW + BtnGap + unitBtnW + BtnGap + resBtnW + BtnGap), (int)cy, (int)startLocBtnW, (int)BtnHeight);
        DrawBtn(spriteBatch, pixel, font, _elevBtn.Value, "Elev",
            CurrentTool == EditorTool.Elevation ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        DrawBtn(spriteBatch, pixel, font, _rampBtn.Value, "Ramp",
            CurrentTool == EditorTool.Ramp ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        DrawBtn(spriteBatch, pixel, font, _unitBtn.Value, "Unit",
            CurrentTool == EditorTool.Unit ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        DrawBtn(spriteBatch, pixel, font, _resBtn.Value, "Res",
            CurrentTool == EditorTool.Resource ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        DrawBtn(spriteBatch, pixel, font, _startLocBtn.Value, "Start",
            CurrentTool == EditorTool.StartLoc ? new Color(40, 100, 40, 200) : new Color(60, 60, 60, 200));
        cy += RowHeight;

        // Sub-rows for Unit tool
        _unitTypeButtons.Clear();
        _ironBtn = null;
        _fissiumBtn = null;
        if (CurrentTool == EditorTool.Unit)
        {
            float btnX = x + Padding;
            for (int i = 0; i < typeNames.Count; i++)
            {
                string shortName = ShortName(typeNames[i]);
                var rect = new Rectangle((int)btnX, (int)cy, (int)typeWidths[i], (int)BtnHeight);
                _unitTypeButtons.Add((rect, typeNames[i]));
                DrawBtn(spriteBatch, pixel, font, rect, shortName,
                    _selectedUnitIndex == i ? new Color(40, 80, 100, 200) : new Color(60, 60, 60, 200));
                btnX += typeWidths[i] + BtnGap;
            }
            cy += RowHeight;

            // Team sub-row
            _redBtn = new Rectangle((int)(x + Padding), (int)cy, (int)redBtnW, (int)BtnHeight);
            _blueBtn = new Rectangle((int)(x + Padding + redBtnW + BtnGap), (int)cy, (int)blueBtnW, (int)BtnHeight);
            DrawBtn(spriteBatch, pixel, font, _redBtn.Value, "Red",
                _selectedTeam == Team.Red ? new Color(140, 40, 40, 200) : new Color(60, 60, 60, 200));
            DrawBtn(spriteBatch, pixel, font, _blueBtn.Value, "Blue",
                _selectedTeam == Team.Blue ? new Color(40, 40, 140, 200) : new Color(60, 60, 60, 200));
            cy += RowHeight;
        }
        else if (CurrentTool == EditorTool.Resource)
        {
            // Resource type sub-row
            _ironBtn = new Rectangle((int)(x + Padding), (int)cy, (int)ironBtnW, (int)BtnHeight);
            _fissiumBtn = new Rectangle((int)(x + Padding + ironBtnW + BtnGap), (int)cy, (int)fissBtnW, (int)BtnHeight);
            DrawBtn(spriteBatch, pixel, font, _ironBtn.Value, "Iron",
                _selectedResource == ResourceType.Iron ? new Color(140, 90, 45, 200) : new Color(60, 60, 60, 200));
            DrawBtn(spriteBatch, pixel, font, _fissiumBtn.Value, "Fissium",
                _selectedResource == ResourceType.Fissium ? new Color(40, 140, 40, 200) : new Color(60, 60, 60, 200));
            cy += RowHeight;
        }
        else
        {
            _redBtn = null;
            _blueBtn = null;
        }

        // Row 2: Tile info
        spriteBatch.DrawString(font, tileInfo, new Vector2(x + Padding, cy), Color.White);
        cy += RowHeight;

        // Row 3: Save
        _saveBtn = new Rectangle((int)(x + Padding), (int)cy, (int)saveBtnW, (int)BtnHeight);
        DrawBtn(spriteBatch, pixel, font, _saveBtn.Value, "Save", new Color(40, 80, 40, 200));
        cy += RowHeight;

        // Status (if any)
        if (_statusTimer > 0 && _statusMessage != null)
        {
            spriteBatch.DrawString(font, _statusMessage, new Vector2(x + Padding, cy), Color.LimeGreen);
            cy += RowHeight;
        }

        // Help text
        spriteBatch.DrawString(font, helpText, new Vector2(x + Padding, cy), Color.Gray);
    }

}
