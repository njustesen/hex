using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Core;
using HexEngine.Rendering;

namespace HexEngine.UI;

public class ResourceBar
{
    private static readonly Color IronColor = new(220, 160, 80);
    private static readonly Color IronDim = new(110, 80, 40);
    private static readonly Color FissiumColor = new(80, 220, 80);
    private static readonly Color FissiumDim = new(40, 110, 40);

    private const float IconSize = 16f;
    private const float IconGap = 3f;

    private readonly List<(IconType Type, float Cx, float Cy, float Size, Color Color)> _iconData = new();

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, InteractionState state, int screenWidth)
    {
        _iconData.Clear();

        string ironAmt = $"{state.TeamIron}";
        string ironIncome = $"+{state.TeamIronIncome}";
        string fissAmt = $"{state.TeamFissium}";
        string fissIncome = $"+{state.TeamFissiumIncome}";

        float gap = 24f;
        float ironAmtW = font.MeasureString(ironAmt).X;
        float ironIncW = font.MeasureString(ironIncome).X;
        float fissAmtW = font.MeasureString(fissAmt).X;
        float fissIncW = font.MeasureString(fissIncome).X;
        float iconSlot = IconSize + IconGap;
        float totalW = iconSlot + ironAmtW + ironIncW + gap + iconSlot + fissAmtW + fissIncW;
        float lineH = font.LineSpacing;

        float pad = 8f;
        float x = screenWidth - totalW - pad * 2 - 10f;
        float y = 12f;

        // Background
        spriteBatch.Draw(pixel, new Rectangle((int)(x - pad), (int)(y - pad / 2f), (int)(totalW + pad * 2), (int)(lineH + pad)), new Color(0, 0, 0, 160));

        // Iron cube icon + amount + income
        float cx = x;
        _iconData.Add((IconType.IronCube, cx + IconSize / 2f, y + lineH / 2f, IconSize, IronColor));
        cx += iconSlot;
        spriteBatch.DrawString(font, ironAmt, new Vector2(cx, y), IronColor);
        cx += ironAmtW;
        spriteBatch.DrawString(font, ironIncome, new Vector2(cx, y), IronDim);
        cx += ironIncW + gap;

        // Fissium cube icon + amount + income
        _iconData.Add((IconType.FissiumCube, cx + IconSize / 2f, y + lineH / 2f, IconSize, FissiumColor));
        cx += iconSlot;
        spriteBatch.DrawString(font, fissAmt, new Vector2(cx, y), FissiumColor);
        cx += fissAmtW;
        spriteBatch.DrawString(font, fissIncome, new Vector2(cx, y), FissiumDim);
    }

    /// Draw resource cube icons (call in PrimitiveDrawer pass).
    public void DrawIcons(PrimitiveDrawer drawer)
    {
        foreach (var (type, cx, cy, size, color) in _iconData)
            IconRenderer.Draw(drawer, type, cx, cy, size, color);
    }
}
