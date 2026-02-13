using System;
using Microsoft.Xna.Framework;
using HexEngine.Core;

namespace HexEngine.Rendering.UnitRenderers;

public class MineUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        Color fogFill = MapRenderer.ApplyFog(fill, fogFactor);
        Color fogOutline = MapRenderer.ApplyFog(outline, fogFactor);

        // Vertical shaft rectangle
        float shaftW = r * 0.4f;
        float shaftH = r * 0.6f;
        var shaftPts = new[]
        {
            W(wp.X - shaftW, wp.Y - shaftH * 0.2f),
            W(wp.X + shaftW, wp.Y - shaftH * 0.2f),
            W(wp.X + shaftW, wp.Y + shaftH),
            W(wp.X - shaftW, wp.Y + shaftH),
        };
        drawer.DrawFilledPolygon(shaftPts, fogFill);
        drawer.DrawPolygonOutline(shaftPts, fogOutline);

        // Triangle roof
        var roofPts = new[]
        {
            W(wp.X, wp.Y - r * 0.8f),
            W(wp.X + r * 0.65f, wp.Y - shaftH * 0.2f),
            W(wp.X - r * 0.65f, wp.Y - shaftH * 0.2f),
        };
        Color roofFill = MapRenderer.ApplyFog(new Color(
            Math.Min(fill.R + 40, 255),
            Math.Min(fill.G + 40, 255),
            Math.Min(fill.B + 40, 255)), fogFactor);
        drawer.DrawFilledPolygon(roofPts, roofFill);
        drawer.DrawPolygonOutline(roofPts, fogOutline);
    }
}
