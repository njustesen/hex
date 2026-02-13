using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Core;

namespace HexEngine.UI;

public class ResourceBar
{
    private static readonly Color IronColor = new(220, 160, 80);
    private static readonly Color IronDim = new(110, 80, 40);
    private static readonly Color FissiumColor = new(80, 220, 80);
    private static readonly Color FissiumDim = new(40, 110, 40);

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, InteractionState state, int screenWidth)
    {
        string ironText = $"Iron: {state.TeamIron}";
        string ironIncome = $"+{state.TeamIronIncome}";
        string fissText = $"Fissium: {state.TeamFissium}";
        string fissIncome = $"+{state.TeamFissiumIncome}";

        float gap = 24f;
        float ironW = font.MeasureString(ironText).X;
        float ironIncW = font.MeasureString(ironIncome).X;
        float fissW = font.MeasureString(fissText).X;
        float fissIncW = font.MeasureString(fissIncome).X;
        float totalW = ironW + ironIncW + gap + fissW + fissIncW;
        float lineH = font.LineSpacing;

        float pad = 8f;
        float x = screenWidth - totalW - pad * 2 - 10f;
        float y = 12f;

        // Background
        spriteBatch.Draw(pixel, new Rectangle((int)(x - pad), (int)(y - pad / 2f), (int)(totalW + pad * 2), (int)(lineH + pad)), new Color(0, 0, 0, 160));

        // Iron text + income
        float cx = x;
        spriteBatch.DrawString(font, ironText, new Vector2(cx, y), IronColor);
        cx += ironW;
        spriteBatch.DrawString(font, ironIncome, new Vector2(cx, y), IronDim);
        cx += ironIncW + gap;

        // Fissium text + income
        spriteBatch.DrawString(font, fissText, new Vector2(cx, y), FissiumColor);
        cx += fissW;
        spriteBatch.DrawString(font, fissIncome, new Vector2(cx, y), FissiumDim);
    }
}
