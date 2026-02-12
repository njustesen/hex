using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HexEngine;

public abstract class Panel
{
    public bool Visible { get; set; }
    public bool ConsumesClick { get; protected set; }
    public float PanelBottom { get; protected set; }

    public const float Padding = 8f;
    public const float BtnHeight = 20f;
    public const float BtnGap = 4f;
    public const float RowHeight = 24f;

    protected static void DrawBtn(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font,
                                   Rectangle rect, string text, Color bgColor)
    {
        spriteBatch.Draw(pixel, rect, bgColor);
        var textSize = font.MeasureString(text);
        float tx = rect.X + (rect.Width - textSize.X) / 2f;
        float ty = rect.Y + (rect.Height - textSize.Y) / 2f;
        spriteBatch.DrawString(font, text, new Vector2(tx, ty), Color.White);
    }

    public static bool InRect(float x, float y, Rectangle r)
        => x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height;

    protected bool ConsumeIfHit(float mx, float my, float px, float py, float pw, float ph)
    {
        if (mx >= px && mx <= px + pw && my >= py && my <= py + ph)
        {
            ConsumesClick = true;
            return true;
        }
        return false;
    }

    protected static void DrawBg(SpriteBatch spriteBatch, Texture2D pixel,
                                  float x, float y, float w, float h)
    {
        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)w, (int)h), new Color(0, 0, 0, 200));
    }
}
