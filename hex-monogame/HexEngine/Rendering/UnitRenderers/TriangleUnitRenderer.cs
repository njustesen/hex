using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;
using HexEngine.View;

namespace HexEngine.Rendering.UnitRenderers;

public class TriangleUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        var fTop = W(wp.X, wp.Y - r);
        var fLeft = W(wp.X - r, wp.Y + r * 0.5f);
        var fRight = W(wp.X + r, wp.Y + r * 0.5f);
        drawer.DrawFilledPolygon(new[] { fTop, fRight, fLeft }, MapRenderer.ApplyFog(fill, fogFactor));
        drawer.DrawPolygonOutline(new[] { fTop, fRight, fLeft }, MapRenderer.ApplyFog(outline, fogFactor));
    }

    protected override void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        var pts = new[]
        {
            S(center.X, center.Y - r),
            S(center.X + r, center.Y + r * 0.5f),
            S(center.X - r, center.Y + r * 0.5f),
        };
        drawer.DrawFilledPolygon(pts, color);
    }
}
