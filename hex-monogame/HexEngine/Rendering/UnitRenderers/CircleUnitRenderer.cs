using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;
using HexEngine.View;

namespace HexEngine.Rendering.UnitRenderers;

public class CircleUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        float cr = r * 0.85f;
        float screenRadius = Math.Max(3f, Vector2.Distance(drawCenter, W(wp.X + cr, wp.Y)));
        drawer.DrawFilledCircle(drawCenter, screenRadius, MapRenderer.ApplyFog(fill, fogFactor));
        drawer.DrawCircle(drawCenter, screenRadius, MapRenderer.ApplyFog(outline, fogFactor));
    }

    protected override void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        Vector2 sc = S(center.X, center.Y);
        float sr = Math.Max(2f, Vector2.Distance(sc, S(center.X + r, center.Y)));
        drawer.DrawFilledCircle(sc, sr, color);
    }
}
