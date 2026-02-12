using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;
using HexEngine.View;

namespace HexEngine.Rendering.UnitRenderers;

public class ArrowUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        float ar = r * 0.9f;
        var arrowPts = new[]
        {
            W(wp.X, wp.Y - ar),              // tip
            W(wp.X + ar, wp.Y + ar * 0.3f),  // right wing
            W(wp.X + ar * 0.35f, wp.Y),      // right notch
            W(wp.X, wp.Y + ar * 0.6f),       // tail center
            W(wp.X - ar * 0.35f, wp.Y),      // left notch
            W(wp.X - ar, wp.Y + ar * 0.3f),  // left wing
        };
        drawer.DrawFilledPolygon(arrowPts, MapRenderer.ApplyFog(fill, fogFactor));
        drawer.DrawPolygonOutline(arrowPts, MapRenderer.ApplyFog(outline, fogFactor));
    }

    protected override void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        var pts = new[]
        {
            S(center.X, center.Y - r),
            S(center.X + r, center.Y + r * 0.3f),
            S(center.X + r * 0.35f, center.Y),
            S(center.X, center.Y + r * 0.6f),
            S(center.X - r * 0.35f, center.Y),
            S(center.X - r, center.Y + r * 0.3f),
        };
        drawer.DrawFilledPolygon(pts, color);
    }
}
