using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Core;
using HexEngine.Input;

namespace HexEngine.UI;

public class Toolbar : Panel
{
    public bool DebugToggled { get; private set; }
    public bool EditorToggled { get; private set; }
    public bool EndTurnPressed { get; private set; }

    private const float X = 10f;
    private const float Y = 10f;
    private const float BtnH = 22f;

    private Rectangle _dmBtnRect;
    private Rectangle _edBtnRect;
    private Rectangle _etBtnRect;
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
        EndTurnPressed = false;

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
            if (InRect(mx, my, _etBtnRect))
            {
                EndTurnPressed = true;
                ConsumesClick = true;
            }
        }
        if (input.MouseDown)
        {
            float mx = input.MousePos.X;
            float my = input.MousePos.Y;
            if (InRect(mx, my, _dmBtnRect) || InRect(mx, my, _edBtnRect) || InRect(mx, my, _etBtnRect))
                ConsumesClick = true;
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, Team currentTeam, int turnNumber = 1)
    {
        float x = X;
        float y = Y;

        float dmW = font.MeasureString("DM").X + 12;
        float edW = font.MeasureString("ED").X + 12;

        string turnLabel = currentTeam == Team.Red ? "RED" : "BLUE";
        float turnW = font.MeasureString(turnLabel).X + 12;

        _dmBtnRect = new Rectangle((int)x, (int)y, (int)dmW, (int)BtnH);
        _edBtnRect = new Rectangle((int)(x + dmW + BtnGap), (int)y, (int)edW, (int)BtnH);
        _etBtnRect = new Rectangle((int)(x + dmW + BtnGap + edW + BtnGap), (int)y, (int)turnW, (int)BtnH);

        var dmColor = _dmHighlight ? new Color(40, 80, 120, 200) : new Color(60, 60, 60, 200);
        DrawBtn(spriteBatch, pixel, font, _dmBtnRect, "DM", dmColor);

        var edColor = _edHighlight ? new Color(120, 80, 40, 200) : new Color(60, 60, 60, 200);
        DrawBtn(spriteBatch, pixel, font, _edBtnRect, "ED", edColor);

        var turnColor = currentTeam == Team.Red
            ? new Color(160, 40, 40, 200)
            : new Color(40, 40, 160, 200);
        DrawBtn(spriteBatch, pixel, font, _etBtnRect, turnLabel, turnColor);

        // Turn counter
        string turnNum = $"Turn {turnNumber}";
        float turnNumX = _etBtnRect.X + _etBtnRect.Width + BtnGap + 4;
        float turnNumY = y + (BtnH - font.LineSpacing) / 2f;
        spriteBatch.DrawString(font, turnNum, new Vector2(turnNumX, turnNumY), Color.LightGray);
    }
}
