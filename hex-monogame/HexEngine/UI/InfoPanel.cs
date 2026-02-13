using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Core;
using HexEngine.Input;
using HexEngine.Rendering;

namespace HexEngine.UI;

public class InfoPanel : Panel
{
    public string? ProductionStartType { get; private set; }
    public bool ProductionCancelled { get; private set; }
    public bool MineRequested { get; private set; }
    public bool UpgradeMineRequested { get; private set; }

    private static readonly Color IronColor = new(220, 160, 80);
    private static readonly Color FissiumColor = new(80, 220, 80);

    private float _x, _y, _w, _h;
    private readonly List<Rectangle> _buildBtnRects = new();
    private readonly List<string> _buildBtnTypes = new();
    private readonly List<bool> _buildBtnAffordable = new();
    private Rectangle _cancelBtnRect;
    private Rectangle _upgradeBtnRect;
    private bool _upgradeBtnVisible;
    private bool _upgradeBtnEnabled;

    // Preview data for PrimitiveDrawer pass
    private readonly List<(string Type, float Cx, float Cy, float Size, bool Affordable)> _previewData = new();
    private Team _previewTeam;
    private string? _hoveredBuildType;

    public void Update(InputManager input, InteractionState state, float minimapX, int screenWidth, int screenHeight,
                       GameplayManager? gameplay = null)
    {
        ConsumesClick = false;
        ProductionStartType = null;
        ProductionCancelled = false;
        MineRequested = false;
        UpgradeMineRequested = false;
        _hoveredBuildType = null;

        _h = screenHeight / 5f;
        _w = minimapX - 1;
        _x = 0;
        _y = screenHeight - _h - 1;

        if (_w <= 0) return;

        var unit = state.SelectedUnit;
        if (unit == null) return;

        // Consume clicks in panel area
        if (input.MouseDown || input.MouseReleased)
        {
            float mx = input.MousePos.X;
            float my = input.MousePos.Y;
            if (mx >= _x && mx <= _x + _w && my >= _y && my <= _y + _h)
                ConsumesClick = true;
        }

        // Track hover for tooltip (uses rects from previous frame)
        {
            float hmx = input.MousePos.X;
            float hmy = input.MousePos.Y;
            for (int i = 0; i < _buildBtnRects.Count; i++)
            {
                if (InRect(hmx, hmy, _buildBtnRects[i]))
                {
                    _hoveredBuildType = _buildBtnTypes[i];
                    break;
                }
            }
        }

        if (!input.MouseReleased) return;

        float cmx = input.MousePos.X;
        float cmy = input.MousePos.Y;

        // Check build buttons (includes Mine)
        for (int i = 0; i < _buildBtnRects.Count; i++)
        {
            if (InRect(cmx, cmy, _buildBtnRects[i]) && _buildBtnAffordable[i])
            {
                if (_buildBtnTypes[i] == "Mine")
                    MineRequested = true;
                else
                    ProductionStartType = _buildBtnTypes[i];
                ConsumesClick = true;
                return;
            }
        }

        // Check upgrade button
        if (_upgradeBtnVisible && _upgradeBtnEnabled && InRect(cmx, cmy, _upgradeBtnRect))
        {
            UpgradeMineRequested = true;
            ConsumesClick = true;
            return;
        }

        // Check cancel button
        if (unit.IsProducing && InRect(cmx, cmy, _cancelBtnRect))
        {
            ProductionCancelled = true;
            ConsumesClick = true;
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, InteractionState state,
                     GameplayManager? gameplay = null)
    {
        var unit = state.SelectedUnit;
        if (unit == null) return;
        if (_w <= 0) return;

        _buildBtnRects.Clear();
        _buildBtnTypes.Clear();
        _buildBtnAffordable.Clear();
        _previewData.Clear();
        _upgradeBtnVisible = false;
        _previewTeam = unit.Team;

        // Background
        DrawBg(spriteBatch, pixel, _x, _y, _w, _h);

        float pad = Padding;
        float lineH = 16f;
        float topY = _y + pad;

        // --- Left section: portrait + unit stats ---
        float portraitSize = _h - pad * 2;
        float portraitX = _x + pad;
        float statsTextW = font.MeasureString("HP 00/00  Armor 0").X;
        float leftSectionW = pad + portraitSize + pad + statsTextW + pad;
        float portraitY = topY;
        spriteBatch.Draw(pixel, new Rectangle((int)portraitX, (int)portraitY, (int)portraitSize, (int)portraitSize),
            new Color(30, 30, 40, 200));
        // Portrait border
        DrawHLine(spriteBatch, pixel, portraitX, portraitY, portraitSize);
        DrawHLine(spriteBatch, pixel, portraitX, portraitY + portraitSize, portraitSize);
        DrawVLine(spriteBatch, pixel, portraitX, portraitY, portraitSize);
        DrawVLine(spriteBatch, pixel, portraitX + portraitSize, portraitY, portraitSize);

        // Unit stats (right of portrait)
        float statsX = portraitX + portraitSize + pad;
        var def = UnitDefs.Get(unit.Type);
        float cy = topY;

        string typeLabel = UnitDefs.DisplayName(unit.Type);
        if (unit.Type == "Mine" && unit.MineLevel > 1)
            typeLabel = $"Mine Lv.{unit.MineLevel}";
        DrawText(spriteBatch, font, statsX, cy, typeLabel, Color.White);
        cy += lineH;

        DrawText(spriteBatch, font, statsX, cy, $"HP {unit.Health}/{unit.MaxHealth}  Armor {unit.Armor}", Color.LightGray);
        cy += lineH;

        if (def.Damage > 0 || def.Range > 0)
        {
            DrawText(spriteBatch, font, statsX, cy, $"Dmg {unit.Damage}  Rng {unit.Range}", Color.LightGray);
            cy += lineH;
        }

        if (def.Movement > 0)
        {
            DrawText(spriteBatch, font, statsX, cy, $"Move {unit.MovementPoints}/{unit.MaxMovementPoints}", Color.LightGray);
            cy += lineH;
        }

        DrawText(spriteBatch, font, statsX, cy, $"Sight {unit.Sight}", Color.LightGray);

        // Mine production info
        if (unit.Type == "Mine" && state.SelectedUnitTile != null)
        {
            var resTile = state.SelectedUnitTile;
            if (resTile.Resource != Tiles.ResourceType.None)
            {
                cy += lineH;
                int prod = 2 + unit.MineLevel;
                string resName = resTile.Resource == Tiles.ResourceType.Iron ? "Iron" : "Fissium";
                Color resColor = resTile.Resource == Tiles.ResourceType.Iron ? IronColor : FissiumColor;
                string prodLabel = $"+{prod} ";
                float prodLabelW = font.MeasureString(prodLabel).X;
                spriteBatch.DrawString(font, prodLabel, new Vector2(statsX, cy), resColor);
                spriteBatch.DrawString(font, resName, new Vector2(statsX + prodLabelW, cy), resColor);
            }
        }

        // Mine upgrade button (below stats)
        if (unit.Type == "Mine" && unit.MineLevel < 3 && gameplay != null)
        {
            cy += lineH + 4;
            bool canUpgrade = gameplay.CanAffordMineUpgrade(unit.Team);
            string upgradeLabel = "Upgrade ";
            float upgradeLabelW = font.MeasureString(upgradeLabel).X;
            string costPart = "3";
            float costW = font.MeasureString(costPart).X;
            float totalUpgradeW = upgradeLabelW + costW + 12;
            _upgradeBtnRect = new Rectangle((int)statsX, (int)cy, (int)totalUpgradeW, (int)BtnHeight);
            _upgradeBtnVisible = true;
            _upgradeBtnEnabled = canUpgrade;
            Color upgColor = canUpgrade ? new Color(60, 100, 60, 200) : new Color(50, 50, 50, 200);
            Color upgTextColor = canUpgrade ? Color.White : Color.Gray;
            spriteBatch.Draw(pixel, _upgradeBtnRect, upgColor);
            float upgTx = _upgradeBtnRect.X + 6;
            float upgTy = _upgradeBtnRect.Y + (_upgradeBtnRect.Height - font.LineSpacing) / 2f;
            spriteBatch.DrawString(font, upgradeLabel, new Vector2(upgTx, upgTy), upgTextColor);
            spriteBatch.DrawString(font, costPart, new Vector2(upgTx + upgradeLabelW, upgTy),
                canUpgrade ? IronColor : Color.Gray);
        }

        // --- Separator line ---
        float sepX = _x + leftSectionW;
        spriteBatch.Draw(pixel, new Rectangle((int)sepX, (int)(_y + pad), 1, (int)(_h - pad * 2)),
            new Color(100, 100, 100, 180));

        // --- Right section: production controls ---
        float rightX = sepX + pad;
        float ry = topY;

        if (!unit.CanProduce) return;

        if (unit.IsProducing)
        {
            // Show current production
            var prodDef = UnitDefs.Get(unit.ProducingType!);
            int totalTurns = prodDef.ProductionTime;
            int elapsed = totalTurns - unit.ProductionTurnsLeft;

            DrawText(spriteBatch, font, rightX, ry, $"Building: {UnitDefs.DisplayName(unit.ProducingType!)}", Color.Yellow);
            ry += lineH;

            DrawText(spriteBatch, font, rightX, ry, $"Turns: {elapsed}/{totalTurns}", Color.LightGray);
            ry += lineH + 4;

            // Cancel button
            float cancelW = font.MeasureString("Cancel").X + 12;
            _cancelBtnRect = new Rectangle((int)rightX, (int)ry, (int)cancelW, (int)BtnHeight);
            DrawBtn(spriteBatch, pixel, font, _cancelBtnRect, "Cancel", new Color(160, 40, 40, 200));
        }
        else
        {
            // Build grid: square tiles with unit previews
            float sqSize = MathF.Floor(_h * 2f / 3f);
            float costTextH = font.LineSpacing;
            float sqGap = 6f;
            float sqX = rightX;
            float sqY = topY;
            float maxSqX = _x + _w - pad;

            foreach (var typeName in UnitDefs.TypeNames)
            {
                if (typeName == "CommandCenter") continue;
                var tDef = UnitDefs.Get(typeName);

                // Wrap to next row
                if (sqX + sqSize > maxSqX && sqX > rightX)
                {
                    sqX = rightX;
                    sqY += sqSize + costTextH + sqGap;
                }

                bool affordable;
                if (typeName == "Mine")
                {
                    bool mineAfford = gameplay != null && gameplay.CanAfford("Mine", unit.Team);
                    bool hasRes = gameplay != null && _selectedCCTile != null && gameplay.HasAdjacentResourceTiles(_selectedCCTile, _map!);
                    affordable = mineAfford && hasRes;
                }
                else
                {
                    affordable = gameplay != null && gameplay.CanAfford(typeName, unit.Team);
                }

                var rect = new Rectangle((int)sqX, (int)sqY, (int)sqSize, (int)sqSize);
                _buildBtnRects.Add(rect);
                _buildBtnTypes.Add(typeName);
                _buildBtnAffordable.Add(affordable);

                // Square background with hover highlight
                bool hovered = _hoveredBuildType == typeName;
                Color sqBg = affordable
                    ? (hovered ? new Color(55, 70, 55, 220) : new Color(40, 50, 40, 200))
                    : (hovered ? new Color(45, 45, 45, 220) : new Color(30, 30, 30, 200));
                spriteBatch.Draw(pixel, rect, sqBg);

                // Square border
                Color borderColor = hovered ? new Color(140, 140, 160, 220) : new Color(80, 80, 100, 180);
                DrawHLine(spriteBatch, pixel, sqX, sqY, sqSize, borderColor);
                DrawHLine(spriteBatch, pixel, sqX, sqY + sqSize, sqSize, borderColor);
                DrawVLine(spriteBatch, pixel, sqX, sqY, sqSize, borderColor);
                DrawVLine(spriteBatch, pixel, sqX + sqSize, sqY, sqSize, borderColor);

                // Store preview data for PrimitiveDrawer pass
                _previewData.Add((typeName, sqX + sqSize / 2f, sqY + sqSize / 2f, sqSize, affordable));

                // Cost text centered below square
                float costTotalW = MeasureCostWidth(font, tDef);
                float costX = sqX + (sqSize - costTotalW) / 2f;
                float costY = sqY + sqSize + 2;
                DrawColoredCost(spriteBatch, font, costX, costY, tDef, affordable);

                sqX += sqSize + sqGap;
            }
        }
    }

    /// Draw unit shape previews inside build squares (call after SpriteBatch.End).
    public void DrawUnitPreviews(PrimitiveDrawer drawer)
    {
        foreach (var (type, cx, cy, size, affordable) in _previewData)
        {
            float brightness = affordable ? 1f : 0.4f;
            UnitRenderer.DrawPreview(drawer, type, _previewTeam, cx, cy, size, brightness);
        }
    }

    /// Draw tooltip for hovered build option (call in separate SpriteBatch pass on top).
    public void DrawTooltip(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel)
    {
        if (_hoveredBuildType == null) return;

        int idx = _buildBtnTypes.IndexOf(_hoveredBuildType);
        if (idx < 0 || idx >= _buildBtnRects.Count) return;

        var rect = _buildBtnRects[idx];
        var tDef = UnitDefs.Get(_hoveredBuildType);

        var lines = new List<(string Text, Color Color)>();
        lines.Add((UnitDefs.DisplayName(_hoveredBuildType), Color.White));
        lines.Add(($"HP {tDef.Health}  Armor {tDef.Armor}", Color.LightGray));
        if (tDef.Damage > 0 || tDef.Range > 0)
            lines.Add(($"Dmg {tDef.Damage}  Rng {tDef.Range}", Color.LightGray));
        if (tDef.Movement > 0)
            lines.Add(($"Move {tDef.Movement}  Sight {tDef.Sight}", Color.LightGray));
        else
            lines.Add(($"Sight {tDef.Sight}", Color.LightGray));
        if (tDef.ProductionTime > 1)
            lines.Add(($"Build: {tDef.ProductionTime} turns", Color.LightGray));

        float lineH = font.LineSpacing;
        float tipPad = 6f;
        float tipW = 0;
        foreach (var line in lines)
            tipW = MathF.Max(tipW, font.MeasureString(line.Text).X);
        tipW += tipPad * 2;
        float tipH = lines.Count * lineH + tipPad * 2;

        float tipX = rect.X;
        float tipY = rect.Y - tipH - 4;

        // Clamp to screen
        if (tipX + tipW > _x + _w)
            tipX = _x + _w - tipW;
        if (tipY < 0)
            tipY = rect.Y + rect.Height + 4;

        // Background
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)tipY, (int)tipW, (int)tipH), new Color(0, 0, 0, 240));

        // Border
        Color tipBorder = new Color(100, 100, 120, 200);
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)tipY, (int)tipW, 1), tipBorder);
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)(tipY + tipH), (int)tipW, 1), tipBorder);
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)tipY, 1, (int)tipH), tipBorder);
        spriteBatch.Draw(pixel, new Rectangle((int)(tipX + tipW), (int)tipY, 1, (int)tipH), tipBorder);

        // Text
        float ty = tipY + tipPad;
        foreach (var (text, color) in lines)
        {
            spriteBatch.DrawString(font, text, new Vector2(tipX + tipPad, ty), color);
            ty += lineH;
        }
    }

    private static float MeasureCostWidth(SpriteFont font, UnitDef def)
    {
        float w = 0;
        if (def.CostIron > 0)
            w += font.MeasureString($"{def.CostIron}").X;
        if (def.CostIron > 0 && def.CostFissium > 0)
            w += font.MeasureString(" ").X;
        if (def.CostFissium > 0)
            w += font.MeasureString($"{def.CostFissium}").X;
        return w;
    }

    private static void DrawColoredCost(SpriteBatch spriteBatch, SpriteFont font, float x, float y,
                                         UnitDef def, bool enabled)
    {
        float cx = x;
        if (def.CostIron > 0)
        {
            string ironStr = $"{def.CostIron}";
            spriteBatch.DrawString(font, ironStr, new Vector2(cx, y), enabled ? IronColor : Color.Gray);
            cx += font.MeasureString(ironStr).X;
        }
        if (def.CostIron > 0 && def.CostFissium > 0)
        {
            spriteBatch.DrawString(font, " ", new Vector2(cx, y), Color.Gray);
            cx += font.MeasureString(" ").X;
        }
        if (def.CostFissium > 0)
        {
            string fissStr = $"{def.CostFissium}";
            spriteBatch.DrawString(font, fissStr, new Vector2(cx, y), enabled ? FissiumColor : Color.Gray);
        }
    }

    private static void DrawHLine(SpriteBatch spriteBatch, Texture2D pixel, float x, float y, float w,
                                   Color? color = null)
    {
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)w, 1), color ?? new Color(80, 80, 100, 180));
    }

    private static void DrawVLine(SpriteBatch spriteBatch, Texture2D pixel, float x, float y, float h,
                                   Color? color = null)
    {
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, 1, (int)h), color ?? new Color(80, 80, 100, 180));
    }

    // Cache for mine button adjacency check
    private Tiles.Tile? _selectedCCTile;
    private Maps.GridMap? _map;

    public void SetContext(Tiles.Tile? selectedTile, Maps.GridMap? map)
    {
        _selectedCCTile = selectedTile;
        _map = map;
    }

    private static void DrawText(SpriteBatch spriteBatch, SpriteFont font, float x, float y, string text, Color color)
    {
        spriteBatch.DrawString(font, text, new Vector2(x, y), color);
    }
}
