using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HexEngine;

public class Toolbar : Panel
{
    public bool DebugToggled { get; private set; }
    public bool EditorToggled { get; private set; }

    private const float X = 10f;
    private const float Y = 10f;
    private const float BtnH = 22f;

    private Rectangle _dmBtnRect;
    private Rectangle _edBtnRect;
    private bool _dmHighlight;
    private bool _edHighlight;

    public float Bottom => Y + BtnH + BtnGap;

    public void SetState(bool debugVisible, bool editorVisible)
    {
        _dmHighlight = debugVisible;
        _edHighlight = editorVisible;
    }

    public void Update(InputManager input)
    {
        ConsumesClick = false;
        DebugToggled = false;
        EditorToggled = false;

        if (input.MouseReleased)
        {
            float mx = input.MousePos.X;
            float my = input.MousePos.Y;
            if (InRect(mx, my, _dmBtnRect))
            {
                DebugToggled = true;
                ConsumesClick = true;
            }
            if (InRect(mx, my, _edBtnRect))
            {
                EditorToggled = true;
                ConsumesClick = true;
            }
        }
        if (input.MouseDown)
        {
            float mx = input.MousePos.X;
            float my = input.MousePos.Y;
            if (InRect(mx, my, _dmBtnRect) || InRect(mx, my, _edBtnRect))
                ConsumesClick = true;
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel)
    {
        float x = X;
        float y = Y;

        float dmW = font.MeasureString("DM").X + 12;
        float edW = font.MeasureString("ED").X + 12;

        _dmBtnRect = new Rectangle((int)x, (int)y, (int)dmW, (int)BtnH);
        _edBtnRect = new Rectangle((int)(x + dmW + BtnGap), (int)y, (int)edW, (int)BtnH);

        var dmColor = _dmHighlight ? new Color(40, 80, 120, 200) : new Color(60, 60, 60, 200);
        DrawBtn(spriteBatch, pixel, font, _dmBtnRect, "DM", dmColor);

        var edColor = _edHighlight ? new Color(120, 80, 40, 200) : new Color(60, 60, 60, 200);
        DrawBtn(spriteBatch, pixel, font, _edBtnRect, "ED", edColor);
    }
}
