using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;
using HexEngine.View;

namespace HexEngine.Rendering.UnitRenderers;

public class DiamondUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        var pts = new[] { W(wp.X, wp.Y - r), W(wp.X + r, wp.Y), W(wp.X, wp.Y + r), W(wp.X - r, wp.Y) };
        drawer.DrawFilledPolygon(pts, MapRenderer.ApplyFog(fill, fogFactor));
        drawer.DrawPolygonOutline(pts, MapRenderer.ApplyFog(outline, fogFactor));
    }
}
