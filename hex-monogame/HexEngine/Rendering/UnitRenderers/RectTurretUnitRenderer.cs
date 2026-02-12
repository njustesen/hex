using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;
using HexEngine.View;

namespace HexEngine.Rendering.UnitRenderers;

public class RectTurretUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        // Body: rectangle centered on tile
        float bw = r * 0.7f;
        float bh = r * 0.9f;
        var bodyPts = new[]
        {
            W(wp.X - bw, wp.Y - bh),
            W(wp.X + bw, wp.Y - bh),
            W(wp.X + bw, wp.Y + bh),
            W(wp.X - bw, wp.Y + bh),
        };
        drawer.DrawFilledPolygon(bodyPts, MapRenderer.ApplyFog(fill, fogFactor));
        drawer.DrawPolygonOutline(bodyPts, MapRenderer.ApplyFog(outline, fogFactor));

        Color turretFill = ColorFromDef(colorDef?.TurretFill, fill);
        Color turretOutline = ColorFromDef(colorDef?.TurretOutline, outline);

        // Barrel: long slim rectangle from center going forward (up)
        float barrelW = bw * 0.15f;
        float barrelH = bh * 1.25f;
        var barrelPts = new[]
        {
            W(wp.X - barrelW, wp.Y - barrelH),
            W(wp.X + barrelW, wp.Y - barrelH),
            W(wp.X + barrelW, wp.Y),
            W(wp.X - barrelW, wp.Y),
        };
        drawer.DrawFilledPolygon(barrelPts, MapRenderer.ApplyFog(turretFill, fogFactor));
        drawer.DrawPolygonOutline(barrelPts, MapRenderer.ApplyFog(turretOutline, fogFactor));

        // Turret ring: circle in the center
        float turretR = bw * 0.45f;
        float tScreenR = Math.Max(2f, Vector2.Distance(drawCenter, W(wp.X + turretR, wp.Y)));
        drawer.DrawFilledCircle(drawCenter, tScreenR, MapRenderer.ApplyFog(turretFill, fogFactor));
        drawer.DrawCircle(drawCenter, tScreenR, MapRenderer.ApplyFog(turretOutline, fogFactor));
    }

    protected override void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        float bw = r * 0.6f;
        float bh = r;
        var pts = new[]
        {
            S(center.X - bw, center.Y - bh),
            S(center.X + bw, center.Y - bh),
            S(center.X + bw, center.Y + bh),
            S(center.X - bw, center.Y + bh),
        };
        drawer.DrawFilledPolygon(pts, color);
    }
}
