using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Core;
using HexEngine.Input;

namespace HexEngine.UI;

public class InfoPanel : Panel
{
    public string? ProductionStartType { get; private set; }
    public bool ProductionCancelled { get; private set; }

    private float _x, _y, _w, _h;
    private readonly List<Rectangle> _buildBtnRects = new();
    private readonly List<string> _buildBtnTypes = new();
    private Rectangle _cancelBtnRect;

    public void Update(InputManager input, InteractionState state, float minimapX, int screenWidth, int screenHeight)
    {
        ConsumesClick = false;
        ProductionStartType = null;
        ProductionCancelled = false;

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

        if (!input.MouseReleased) return;

        float cmx = input.MousePos.X;
        float cmy = input.MousePos.Y;

        // Check build buttons
        for (int i = 0; i < _buildBtnRects.Count; i++)
        {
            if (InRect(cmx, cmy, _buildBtnRects[i]))
            {
                ProductionStartType = _buildBtnTypes[i];
                ConsumesClick = true;
                return;
            }
        }

        // Check cancel button
        if (unit.IsProducing && InRect(cmx, cmy, _cancelBtnRect))
        {
            ProductionCancelled = true;
            ConsumesClick = true;
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, InteractionState state)
    {
        var unit = state.SelectedUnit;
        if (unit == null) return;
        if (_w <= 0) return;

        _buildBtnRects.Clear();
        _buildBtnTypes.Clear();

        // Background
        DrawBg(spriteBatch, pixel, _x, _y, _w, _h);

        float pad = Padding;
        float lineH = 16f;
        float leftX = _x + pad;
        float topY = _y + pad;

        // Left side: unit stats
        var def = UnitDefs.Get(unit.Type);
        float cy = topY;

        DrawText(spriteBatch, font, leftX, cy, unit.Type, Color.White);
        cy += lineH;

        DrawText(spriteBatch, font, leftX, cy, $"HP {unit.Health}/{unit.MaxHealth}  Armor {unit.Armor}", Color.LightGray);
        cy += lineH;

        if (def.Damage > 0 || def.Range > 0)
        {
            DrawText(spriteBatch, font, leftX, cy, $"Dmg {unit.Damage}  Rng {unit.Range}", Color.LightGray);
            cy += lineH;
        }

        if (def.Movement > 0)
        {
            DrawText(spriteBatch, font, leftX, cy, $"Move {unit.MovementPoints}/{unit.MaxMovementPoints}", Color.LightGray);
            cy += lineH;
        }

        DrawText(spriteBatch, font, leftX, cy, $"Sight {unit.Sight}", Color.LightGray);

        // Right side: production controls (CommandCenter only)
        if (!unit.CanProduce) return;

        float rightX = _x + _w * 0.45f;
        float ry = topY;

        if (unit.IsProducing)
        {
            // Show current production
            var prodDef = UnitDefs.Get(unit.ProducingType!);
            int totalTurns = prodDef.ProductionTime;
            int elapsed = totalTurns - unit.ProductionTurnsLeft;

            DrawText(spriteBatch, font, rightX, ry, $"Building: {unit.ProducingType}", Color.Yellow);
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
            // Show build options
            DrawText(spriteBatch, font, rightX, ry, "Build:", Color.White);
            ry += lineH + 2;

            float btnX = rightX;
            float btnGap = 4f;

            foreach (var typeName in UnitDefs.TypeNames)
            {
                if (typeName == "CommandCenter") continue;
                var tDef = UnitDefs.Get(typeName);
                string label = $"{typeName}({tDef.ProductionTime})";
                float btnW = font.MeasureString(label).X + 12;

                // Wrap to next row if needed
                if (btnX + btnW > _x + _w - pad)
                {
                    btnX = rightX;
                    ry += BtnHeight + btnGap;
                }

                var rect = new Rectangle((int)btnX, (int)ry, (int)btnW, (int)BtnHeight);
                _buildBtnRects.Add(rect);
                _buildBtnTypes.Add(typeName);
                DrawBtn(spriteBatch, pixel, font, rect, label, new Color(60, 80, 60, 200));

                btnX += btnW + btnGap;
            }
        }
    }

    private static void DrawText(SpriteBatch spriteBatch, SpriteFont font, float x, float y, string text, Color color)
    {
        spriteBatch.DrawString(font, text, new Vector2(x, y), color);
    }
}
