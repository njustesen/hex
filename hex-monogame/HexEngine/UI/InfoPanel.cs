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
    public string? BuildingPlacementType { get; private set; }
    public bool BuildingCancelled { get; private set; }

    private static readonly Color IronColor = new(220, 160, 80);
    private static readonly Color FissiumColor = new(80, 220, 80);

    // Icon colors
    private static readonly Color HeartColor = new(200, 60, 60);
    private static readonly Color ShieldColor = new(120, 150, 200);
    private static readonly Color DmgColor = new(220, 140, 40);
    private static readonly Color RangeColor = new(180, 180, 180);
    private static readonly Color MoveColor = new(100, 160, 220);
    private static readonly Color SightColor = new(200, 200, 220);

    private const float IconSize = 18f;
    private const float IconGap = 3f;
    private const float StatGap = 10f;
    private const float LineH = 16f;

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

    // Icon data for PrimitiveDrawer pass
    private readonly List<(IconType Type, float Cx, float Cy, float Size, Color Color)> _iconData = new();
    private readonly List<(IconType Type, float Cx, float Cy, float Size, Color Color)> _tooltipIconData = new();

    public void Update(InputManager input, InteractionState state, float minimapX, int screenWidth, int screenHeight,
                       GameplayManager? gameplay = null)
    {
        ConsumesClick = false;
        ProductionStartType = null;
        ProductionCancelled = false;
        MineRequested = false;
        UpgradeMineRequested = false;
        BuildingPlacementType = null;
        BuildingCancelled = false;
        _hoveredBuildType = null;

        _h = screenHeight / 5f;
        _w = minimapX - 1;
        _x = 0;
        _y = screenHeight - _h - 1;

        if (_w <= 0) return;

        bool hasBuildTile = state.SelectedBuildTile != null;
        var unit = state.SelectedUnit;

        // Always consume clicks in panel area
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

        // Check build buttons (includes Mine, build tile menu items)
        for (int i = 0; i < _buildBtnRects.Count; i++)
        {
            if (InRect(cmx, cmy, _buildBtnRects[i]) && _buildBtnAffordable[i])
            {
                if (hasBuildTile)
                {
                    BuildingPlacementType = _buildBtnTypes[i];
                }
                else if (_buildBtnTypes[i] == "Mine")
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

        // Check cancel button (production or under-construction)
        if (unit != null && (unit.IsProducing || unit.IsUnderConstruction) && InRect(cmx, cmy, _cancelBtnRect))
        {
            if (unit.IsUnderConstruction)
                BuildingCancelled = true;
            else
                ProductionCancelled = true;
            ConsumesClick = true;
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, InteractionState state,
                     GameplayManager? gameplay = null)
    {
        _previewData.Clear();
        _iconData.Clear();

        bool hasBuildTile = state.SelectedBuildTile != null;
        var unit = state.SelectedUnit;
        if (_w <= 0) return;

        _buildBtnRects.Clear();
        _buildBtnTypes.Clear();
        _buildBtnAffordable.Clear();
        _upgradeBtnVisible = false;
        _previewTeam = unit?.Team ?? state.CurrentTeam;

        // Background (always visible)
        DrawBg(spriteBatch, pixel, _x, _y, _w, _h);

        float pad = Padding;
        float topY = _y + pad;

        // === Build tile menu (no unit selected, empty tile) ===
        if (hasBuildTile && unit == null)
        {
            DrawBuildTileMenu(spriteBatch, font, pixel, state, gameplay, pad, topY);
            return;
        }

        if (unit == null) return;

        // --- Left section: portrait + unit stats ---
        float portraitSize = _h - pad * 2;
        float portraitX = _x + pad;
        float statsTextW = font.MeasureString("HP 00/00  Armor 0").X;
        float leftSectionW = pad + portraitSize + pad + statsTextW + pad;
        float portraitY = topY;
        spriteBatch.Draw(pixel, new Rectangle((int)portraitX, (int)portraitY, (int)portraitSize, (int)portraitSize),
            new Color(30, 30, 40, 200));
        DrawHLine(spriteBatch, pixel, portraitX, portraitY, portraitSize);
        DrawHLine(spriteBatch, pixel, portraitX, portraitY + portraitSize, portraitSize);
        DrawVLine(spriteBatch, pixel, portraitX, portraitY, portraitSize);
        DrawVLine(spriteBatch, pixel, portraitX + portraitSize, portraitY, portraitSize);

        // Unit stats (right of portrait)
        float statsX = portraitX + portraitSize + pad;
        var def = UnitDefs.Get(unit.Type);
        float cy = topY;

        // Name
        string typeLabel = UnitDefs.DisplayName(unit.Type);
        if (unit.Type == "Mine" && unit.MineLevel > 1)
            typeLabel = $"Mine Lv.{unit.MineLevel}";
        DrawText(spriteBatch, font, statsX, cy, typeLabel, Color.White);
        cy += LineH + 2;

        // Separator line below name
        float sepLineW = portraitSize;
        spriteBatch.Draw(pixel, new Rectangle((int)statsX, (int)cy, (int)sepLineW, 1),
            new Color(100, 100, 100, 180));
        cy += 6;

        // HP: heart icon + health bar (no text)
        float sx = statsX;
        _iconData.Add((IconType.Heart, sx + IconSize / 2f, cy + LineH / 2f, IconSize, HeartColor));
        sx += IconSize + IconGap;
        float hpBarW = unit.MaxHealth * 8f;
        float hpBarH = 6f;
        float hpBarY = cy + (LineH - hpBarH) / 2f;
        for (int i = 0; i < unit.MaxHealth; i++)
        {
            Color segColor = i < unit.Health ? new Color(40, 180, 40) : new Color(80, 30, 30);
            spriteBatch.Draw(pixel, new Rectangle((int)(sx + i * 8f), (int)hpBarY, 7, (int)hpBarH), segColor);
        }
        cy += LineH;

        // Two-column stat grid: column 1 at statsX, column 2 at statsX + col2Offset
        float col2Offset = IconSize + IconGap + font.MeasureString("00/00").X + StatGap;

        // Armor line
        AddIconAndText(spriteBatch, font, statsX, cy, IconType.Shield, ShieldColor,
            $"{unit.Armor}", Color.LightGray, _iconData);
        if (def.Damage > 0 || def.Range > 0)
        {
            AddIconAndText(spriteBatch, font, statsX + col2Offset, cy, IconType.Explosion, DmgColor,
                $"{unit.Damage}", Color.LightGray, _iconData);
        }
        cy += LineH;

        // Range + Move (or just sight)
        if (def.Damage > 0 || def.Range > 0)
        {
            AddIconAndText(spriteBatch, font, statsX, cy, IconType.Range, RangeColor,
                $"{unit.Range}", Color.LightGray, _iconData);
            if (def.Movement > 0)
            {
                AddIconAndText(spriteBatch, font, statsX + col2Offset, cy, IconType.Hex, MoveColor,
                    $"{unit.MovementPoints}/{unit.MaxMovementPoints}", Color.LightGray, _iconData);
            }
            cy += LineH;

            // Sight
            AddIconAndText(spriteBatch, font, statsX, cy, IconType.Eye, SightColor,
                $"{unit.Sight}", Color.LightGray, _iconData);
            cy += LineH;
        }
        else if (def.Movement > 0)
        {
            AddIconAndText(spriteBatch, font, statsX, cy, IconType.Hex, MoveColor,
                $"{unit.MovementPoints}/{unit.MaxMovementPoints}", Color.LightGray, _iconData);
            AddIconAndText(spriteBatch, font, statsX + col2Offset, cy, IconType.Eye, SightColor,
                $"{unit.Sight}", Color.LightGray, _iconData);
            cy += LineH;
        }
        else
        {
            AddIconAndText(spriteBatch, font, statsX, cy, IconType.Eye, SightColor,
                $"{unit.Sight}", Color.LightGray, _iconData);
            cy += LineH;
        }

        // Mine production info
        if (unit.Type == "Mine" && !unit.IsUnderConstruction && state.SelectedUnitTile != null)
        {
            var resTile = state.SelectedUnitTile;
            if (resTile.Resource != Tiles.ResourceType.None)
            {
                int prod = 2 + unit.MineLevel;
                bool isIron = resTile.Resource == Tiles.ResourceType.Iron;
                Color resColor = isIron ? IronColor : FissiumColor;
                IconType cubeType = isIron ? IconType.IronCube : IconType.FissiumCube;
                string prodLabel = $"+{prod}";
                spriteBatch.DrawString(font, prodLabel, new Vector2(statsX, cy), resColor);
                float afterText = statsX + font.MeasureString(prodLabel).X + 2;
                _iconData.Add((cubeType, afterText + IconSize / 2f, cy + LineH / 2f, IconSize, resColor));
                cy += LineH;
            }
        }

        // Mine upgrade button
        if (unit.Type == "Mine" && unit.MineLevel < 3 && !unit.IsUnderConstruction && gameplay != null)
        {
            cy += 4;
            bool canUpgrade = gameplay.CanAffordMineUpgrade(unit.Team);
            string upgradeLabel = "Upgrade ";
            float upgradeLabelW = font.MeasureString(upgradeLabel).X;
            string costPart = "3";
            float costW = font.MeasureString(costPart).X;
            float totalUpgradeW = upgradeLabelW + IconSize + 1 + costW + 8;
            _upgradeBtnRect = new Rectangle((int)statsX, (int)cy, (int)totalUpgradeW, (int)BtnHeight);
            _upgradeBtnVisible = true;
            _upgradeBtnEnabled = canUpgrade;
            Color upgColor = canUpgrade ? new Color(60, 100, 60, 200) : new Color(50, 50, 50, 200);
            Color upgTextColor = canUpgrade ? Color.White : Color.Gray;
            spriteBatch.Draw(pixel, _upgradeBtnRect, upgColor);
            float upgTx = _upgradeBtnRect.X + 4;
            float upgTy = _upgradeBtnRect.Y + (_upgradeBtnRect.Height - font.LineSpacing) / 2f;
            spriteBatch.DrawString(font, upgradeLabel, new Vector2(upgTx, upgTy), upgTextColor);
            float iconX = upgTx + upgradeLabelW;
            Color ic = canUpgrade ? IronColor : new Color(60, 60, 60);
            _iconData.Add((IconType.IronCube, iconX + IconSize / 2f, _upgradeBtnRect.Y + _upgradeBtnRect.Height / 2f, IconSize, ic));
            spriteBatch.DrawString(font, costPart, new Vector2(iconX + IconSize + 1, upgTy),
                canUpgrade ? Color.White : Color.Gray);
        }

        // --- Separator line ---
        float sepX = _x + leftSectionW;
        spriteBatch.Draw(pixel, new Rectangle((int)sepX, (int)(_y + pad), 1, (int)(_h - pad * 2)),
            new Color(100, 100, 100, 180));

        // --- Right section ---
        float rightX = sepX + pad;
        float ry = topY;

        // Under-construction display
        if (unit.IsUnderConstruction)
        {
            DrawConstructionProgress(spriteBatch, font, pixel, unit, rightX, ry);
            return;
        }

        if (!unit.CanProduce) return;

        if (unit.IsProducing)
        {
            var prodDef = UnitDefs.Get(unit.ProducingType!);
            int totalTurns = prodDef.ProductionTime;
            int elapsed = totalTurns - unit.ProductionTurnsLeft;

            float sqSize = MathF.Floor(_h * 2f / 3f);

            // --- Column 1: unit preview + cancel button below ---
            // Preview square
            spriteBatch.Draw(pixel, new Rectangle((int)rightX, (int)ry, (int)sqSize, (int)sqSize),
                new Color(40, 50, 40, 200));
            DrawHLine(spriteBatch, pixel, rightX, ry, sqSize);
            DrawHLine(spriteBatch, pixel, rightX, ry + sqSize, sqSize);
            DrawVLine(spriteBatch, pixel, rightX, ry, sqSize);
            DrawVLine(spriteBatch, pixel, rightX + sqSize, ry, sqSize);
            _previewData.Add((unit.ProducingType!, rightX + sqSize / 2f, ry + sqSize / 2f, sqSize, true));

            // Cancel button centered below preview
            float cancelW = font.MeasureString("Cancel").X + 12;
            float cancelX = rightX + (sqSize - cancelW) / 2f;
            float cancelY = ry + sqSize + 4;
            _cancelBtnRect = new Rectangle((int)cancelX, (int)cancelY, (int)cancelW, (int)BtnHeight);
            DrawBtn(spriteBatch, pixel, font, _cancelBtnRect, "Cancel", new Color(160, 40, 40, 200));

            // --- Column 2: name, separator, progress bar ---
            float infoX = rightX + sqSize + pad;
            float infoY = ry;

            // Unit name
            DrawText(spriteBatch, font, infoX, infoY, UnitDefs.DisplayName(unit.ProducingType!), Color.Yellow);
            infoY += LineH + 2;

            // Separator line (coherent with unit stats section)
            float infoSepW = sqSize;
            spriteBatch.Draw(pixel, new Rectangle((int)infoX, (int)infoY, (int)infoSepW, 1),
                new Color(100, 100, 100, 180));
            infoY += 6;

            DrawText(spriteBatch, font, infoX, infoY, "In production", Color.LightGray);
            infoY += LineH + 4;

            // Progress bar: individual turn segments with gaps
            float barW = sqSize;
            float barH = 10f;
            float segGap = 2f;
            float segW = totalTurns > 1 ? (barW - (totalTurns - 1) * segGap) / totalTurns : barW;

            for (int t = 0; t < totalTurns; t++)
            {
                float segX = infoX + t * (segW + segGap);
                Color segBg = new Color(30, 30, 30, 200);
                spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, (int)segW, (int)barH), segBg);

                // Filled segments for elapsed turns, small sliver for current turn
                Color segFill;
                if (t < elapsed)
                {
                    segFill = new Color(200, 180, 40, 220);
                    spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, (int)segW, (int)barH), segFill);
                }
                else if (t == elapsed)
                {
                    // Current turn: show a small sliver to indicate in-progress
                    segFill = new Color(200, 180, 40, 140);
                    int sliverW = Math.Max(3, (int)(segW * 0.2f));
                    spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, sliverW, (int)barH), segFill);
                }

                // Border per segment
                Color border = new Color(80, 80, 80, 180);
                spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, (int)segW, 1), border);
                spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)(infoY + barH - 1), (int)segW, 1), border);
                spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, 1, (int)barH), border);
                spriteBatch.Draw(pixel, new Rectangle((int)(segX + segW - 1), (int)infoY, 1, (int)barH), border);
            }
        }
        else
        {
            // Build grid: square tiles with unit previews (CC only produces mobile units + Mine)
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
                // CC only produces mobile units + Mine; skip other buildings
                if (tDef.IsBuilding && typeName != "Mine") continue;

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

                bool hovered = _hoveredBuildType == typeName;
                Color sqBg = affordable
                    ? (hovered ? new Color(55, 70, 55, 220) : new Color(40, 50, 40, 200))
                    : (hovered ? new Color(45, 45, 45, 220) : new Color(30, 30, 30, 200));
                spriteBatch.Draw(pixel, rect, sqBg);

                Color borderColor = hovered ? new Color(140, 140, 160, 220) : new Color(80, 80, 100, 180);
                DrawHLine(spriteBatch, pixel, sqX, sqY, sqSize, borderColor);
                DrawHLine(spriteBatch, pixel, sqX, sqY + sqSize, sqSize, borderColor);
                DrawVLine(spriteBatch, pixel, sqX, sqY, sqSize, borderColor);
                DrawVLine(spriteBatch, pixel, sqX + sqSize, sqY, sqSize, borderColor);

                _previewData.Add((typeName, sqX + sqSize / 2f, sqY + sqSize / 2f, sqSize, affordable));

                // Cost icons + numbers centered below square
                float costTotalW = MeasureIconCostWidth(font, tDef);
                float costX = sqX + (sqSize - costTotalW) / 2f;
                float costY = sqY + sqSize + 2;
                DrawIconCost(spriteBatch, font, costX, costY, tDef, affordable, _iconData);

                sqX += sqSize + sqGap;
            }
        }
    }

    private void DrawBuildTileMenu(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel,
                                    InteractionState state, GameplayManager? gameplay, float pad, float topY)
    {
        float rightX = _x + pad;

        float sqSize = MathF.Floor(_h * 2f / 3f);
        float costTextH = font.LineSpacing;
        float sqGap = 6f;
        float sqX = rightX;
        float sqY = topY;
        float maxSqX = _x + _w - pad;

        bool tileEligible = gameplay != null && state.SelectedBuildTile != null
            && gameplay.IsBuildEligible(state.SelectedBuildTile, _map!, state.CurrentTeam);

        // Show buildable buildings: CC, Bunker, AntiAir (not Mine — Mine is built via CC)
        foreach (var typeName in UnitDefs.TypeNames)
        {
            var tDef = UnitDefs.Get(typeName);
            if (!tDef.IsBuilding || typeName == "Mine") continue;

            if (sqX + sqSize > maxSqX && sqX > rightX)
            {
                sqX = rightX;
                sqY += sqSize + costTextH + sqGap;
            }

            // CC can be built anywhere; other buildings need CC proximity
            bool eligible = typeName == "CommandCenter" || tileEligible;
            bool affordable = eligible && gameplay != null && gameplay.CanAfford(typeName, state.CurrentTeam);

            var rect = new Rectangle((int)sqX, (int)sqY, (int)sqSize, (int)sqSize);
            _buildBtnRects.Add(rect);
            _buildBtnTypes.Add(typeName);
            _buildBtnAffordable.Add(affordable);

            bool hovered = _hoveredBuildType == typeName;
            Color sqBg = affordable
                ? (hovered ? new Color(55, 70, 55, 220) : new Color(40, 50, 40, 200))
                : (hovered ? new Color(45, 45, 45, 220) : new Color(30, 30, 30, 200));
            spriteBatch.Draw(pixel, rect, sqBg);

            Color borderColor = hovered ? new Color(140, 140, 160, 220) : new Color(80, 80, 100, 180);
            DrawHLine(spriteBatch, pixel, sqX, sqY, sqSize, borderColor);
            DrawHLine(spriteBatch, pixel, sqX, sqY + sqSize, sqSize, borderColor);
            DrawVLine(spriteBatch, pixel, sqX, sqY, sqSize, borderColor);
            DrawVLine(spriteBatch, pixel, sqX + sqSize, sqY, sqSize, borderColor);

            _previewData.Add((typeName, sqX + sqSize / 2f, sqY + sqSize / 2f, sqSize, affordable));

            float costTotalW = MeasureIconCostWidth(font, tDef);
            float costX = sqX + (sqSize - costTotalW) / 2f;
            float costY = sqY + sqSize + 2;
            DrawIconCost(spriteBatch, font, costX, costY, tDef, affordable, _iconData);

            sqX += sqSize + sqGap;
        }
    }

    private void DrawConstructionProgress(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel,
                                           Unit unit, float rightX, float ry)
    {
        int totalTurns = unit.ConstructionTotalTurns;
        int elapsed = totalTurns - unit.ConstructionTurnsLeft;

        float sqSize = MathF.Floor(_h * 2f / 3f);

        // Preview square
        spriteBatch.Draw(pixel, new Rectangle((int)rightX, (int)ry, (int)sqSize, (int)sqSize),
            new Color(40, 50, 40, 200));
        DrawHLine(spriteBatch, pixel, rightX, ry, sqSize);
        DrawHLine(spriteBatch, pixel, rightX, ry + sqSize, sqSize);
        DrawVLine(spriteBatch, pixel, rightX, ry, sqSize);
        DrawVLine(spriteBatch, pixel, rightX + sqSize, ry, sqSize);
        _previewData.Add((unit.Type, rightX + sqSize / 2f, ry + sqSize / 2f, sqSize, true));

        // Cancel button centered below preview
        float cancelW = font.MeasureString("Cancel").X + 12;
        float cancelX = rightX + (sqSize - cancelW) / 2f;
        float cancelY = ry + sqSize + 4;
        _cancelBtnRect = new Rectangle((int)cancelX, (int)cancelY, (int)cancelW, (int)BtnHeight);
        DrawBtn(spriteBatch, pixel, font, _cancelBtnRect, "Cancel", new Color(160, 40, 40, 200));

        // Info column
        float pad = Padding;
        float infoX = rightX + sqSize + pad;
        float infoY = ry;

        DrawText(spriteBatch, font, infoX, infoY, UnitDefs.DisplayName(unit.Type), Color.Yellow);
        infoY += LineH + 2;

        float infoSepW = sqSize;
        spriteBatch.Draw(pixel, new Rectangle((int)infoX, (int)infoY, (int)infoSepW, 1),
            new Color(100, 100, 100, 180));
        infoY += 6;

        DrawText(spriteBatch, font, infoX, infoY, "Under Construction", Color.LightGray);
        infoY += LineH + 4;

        // Progress bar
        float barW = sqSize;
        float barH = 10f;
        float segGap = 2f;
        float segW = totalTurns > 1 ? (barW - (totalTurns - 1) * segGap) / totalTurns : barW;

        for (int t = 0; t < totalTurns; t++)
        {
            float segX = infoX + t * (segW + segGap);
            Color segBg = new Color(30, 30, 30, 200);
            spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, (int)segW, (int)barH), segBg);

            Color segFill;
            if (t < elapsed)
            {
                segFill = new Color(200, 180, 40, 220);
                spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, (int)segW, (int)barH), segFill);
            }
            else if (t == elapsed)
            {
                segFill = new Color(200, 180, 40, 140);
                int sliverW = Math.Max(3, (int)(segW * 0.2f));
                spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, sliverW, (int)barH), segFill);
            }

            Color border = new Color(80, 80, 80, 180);
            spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, (int)segW, 1), border);
            spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)(infoY + barH - 1), (int)segW, 1), border);
            spriteBatch.Draw(pixel, new Rectangle((int)segX, (int)infoY, 1, (int)barH), border);
            spriteBatch.Draw(pixel, new Rectangle((int)(segX + segW - 1), (int)infoY, 1, (int)barH), border);
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

    /// Draw stat/resource icons (call in PrimitiveDrawer pass).
    public void DrawIcons(PrimitiveDrawer drawer)
    {
        foreach (var (type, cx, cy, size, color) in _iconData)
            IconRenderer.Draw(drawer, type, cx, cy, size, color);
    }

    /// Draw tooltip for hovered build option (call in separate SpriteBatch pass on top).
    public void DrawTooltip(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel)
    {
        _tooltipIconData.Clear();
        if (_hoveredBuildType == null) return;

        int idx = _buildBtnTypes.IndexOf(_hoveredBuildType);
        if (idx < 0 || idx >= _buildBtnRects.Count) return;

        var rect = _buildBtnRects[idx];
        var tDef = UnitDefs.Get(_hoveredBuildType);

        // Calculate tooltip size — each stat line has icons so we need icon width
        float iconSlot = IconSize + IconGap;
        float lineH = font.LineSpacing;
        float tipPad = 6f;

        // Pre-compute line widths
        var lineWidths = new List<float>();
        int lineCount = 1; // name line
        lineWidths.Add(font.MeasureString(UnitDefs.DisplayName(_hoveredBuildType)).X);

        // HP + Armor line
        float hpLine = iconSlot + font.MeasureString($"{tDef.Health}").X + StatGap +
                        iconSlot + font.MeasureString($"{tDef.Armor}").X;
        lineWidths.Add(hpLine);
        lineCount++;

        if (tDef.Damage > 0 || tDef.Range > 0)
        {
            float dmgLine = iconSlot + font.MeasureString($"{tDef.Damage}").X + StatGap +
                            iconSlot + font.MeasureString($"{tDef.Range}").X;
            lineWidths.Add(dmgLine);
            lineCount++;
        }

        if (tDef.Movement > 0)
        {
            float moveLine = iconSlot + font.MeasureString($"{tDef.Movement}").X + StatGap +
                             iconSlot + font.MeasureString($"{tDef.Sight}").X;
            lineWidths.Add(moveLine);
        }
        else
        {
            lineWidths.Add(iconSlot + font.MeasureString($"{tDef.Sight}").X);
        }
        lineCount++;

        float tipW = 0;
        foreach (float w in lineWidths) tipW = MathF.Max(tipW, w);
        tipW += tipPad * 2;
        float tipH = lineCount * lineH + tipPad * 2;

        float tipX = rect.X;
        float tipY = rect.Y - tipH - 4;
        if (tipX + tipW > _x + _w) tipX = _x + _w - tipW;
        if (tipY < 0) tipY = rect.Y + rect.Height + 4;

        // Background
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)tipY, (int)tipW, (int)tipH), new Color(0, 0, 0, 240));
        Color tipBorder = new Color(100, 100, 120, 200);
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)tipY, (int)tipW, 1), tipBorder);
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)(tipY + tipH), (int)tipW, 1), tipBorder);
        spriteBatch.Draw(pixel, new Rectangle((int)tipX, (int)tipY, 1, (int)tipH), tipBorder);
        spriteBatch.Draw(pixel, new Rectangle((int)(tipX + tipW), (int)tipY, 1, (int)tipH), tipBorder);

        // Draw tooltip content with icons
        float tx = tipX + tipPad;
        float ty = tipY + tipPad;

        // Name
        spriteBatch.DrawString(font, UnitDefs.DisplayName(_hoveredBuildType), new Vector2(tx, ty), Color.White);
        ty += lineH;

        // HP + Armor
        float ttsx = tx;
        ttsx = AddIconAndText(spriteBatch, font, ttsx, ty, IconType.Heart, HeartColor,
            $"{tDef.Health}", Color.LightGray, _tooltipIconData);
        ttsx += StatGap;
        AddIconAndText(spriteBatch, font, ttsx, ty, IconType.Shield, ShieldColor,
            $"{tDef.Armor}", Color.LightGray, _tooltipIconData);
        ty += lineH;

        // Dmg + Rng
        if (tDef.Damage > 0 || tDef.Range > 0)
        {
            ttsx = tx;
            ttsx = AddIconAndText(spriteBatch, font, ttsx, ty, IconType.Explosion, DmgColor,
                $"{tDef.Damage}", Color.LightGray, _tooltipIconData);
            ttsx += StatGap;
            AddIconAndText(spriteBatch, font, ttsx, ty, IconType.Range, RangeColor,
                $"{tDef.Range}", Color.LightGray, _tooltipIconData);
            ty += lineH;
        }

        // Move + Sight or Sight only
        if (tDef.Movement > 0)
        {
            ttsx = tx;
            ttsx = AddIconAndText(spriteBatch, font, ttsx, ty, IconType.Hex, MoveColor,
                $"{tDef.Movement}", Color.LightGray, _tooltipIconData);
            ttsx += StatGap;
            AddIconAndText(spriteBatch, font, ttsx, ty, IconType.Eye, SightColor,
                $"{tDef.Sight}", Color.LightGray, _tooltipIconData);
        }
        else
        {
            AddIconAndText(spriteBatch, font, tx, ty, IconType.Eye, SightColor,
                $"{tDef.Sight}", Color.LightGray, _tooltipIconData);
        }
    }

    /// Draw tooltip icons (call after tooltip SpriteBatch pass).
    public void DrawTooltipIcons(PrimitiveDrawer drawer)
    {
        foreach (var (type, cx, cy, size, color) in _tooltipIconData)
            IconRenderer.Draw(drawer, type, cx, cy, size, color);
    }

    private float AddIconAndText(SpriteBatch sb, SpriteFont font, float x, float y,
        IconType icon, Color iconColor, string value, Color textColor,
        List<(IconType Type, float Cx, float Cy, float Size, Color Color)> icons)
    {
        icons.Add((icon, x + IconSize / 2f, y + LineH / 2f, IconSize, iconColor));
        float textX = x + IconSize + IconGap;
        sb.DrawString(font, value, new Vector2(textX, y), textColor);
        return textX + font.MeasureString(value).X;
    }

    private float MeasureIconCostWidth(SpriteFont font, UnitDef def)
    {
        float w = 0;
        if (def.CostIron > 0)
            w += IconSize + 1 + font.MeasureString($"{def.CostIron}").X;
        if (def.CostIron > 0 && def.CostFissium > 0)
            w += 4;
        if (def.CostFissium > 0)
            w += IconSize + 1 + font.MeasureString($"{def.CostFissium}").X;
        return w;
    }

    private void DrawIconCost(SpriteBatch sb, SpriteFont font, float x, float y,
        UnitDef def, bool enabled,
        List<(IconType Type, float Cx, float Cy, float Size, Color Color)> icons)
    {
        float cx = x;
        if (def.CostIron > 0)
        {
            Color ic = enabled ? IronColor : new Color(60, 60, 60);
            icons.Add((IconType.IronCube, cx + IconSize / 2f, y + LineH / 2f, IconSize, ic));
            cx += IconSize + 1;
            string ironStr = $"{def.CostIron}";
            sb.DrawString(font, ironStr, new Vector2(cx, y), enabled ? Color.White : Color.Gray);
            cx += font.MeasureString(ironStr).X;
        }
        if (def.CostIron > 0 && def.CostFissium > 0)
            cx += 4;
        if (def.CostFissium > 0)
        {
            Color fc = enabled ? FissiumColor : new Color(60, 60, 60);
            icons.Add((IconType.FissiumCube, cx + IconSize / 2f, y + LineH / 2f, IconSize, fc));
            cx += IconSize + 1;
            string fissStr = $"{def.CostFissium}";
            sb.DrawString(font, fissStr, new Vector2(cx, y), enabled ? Color.White : Color.Gray);
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
