using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;

namespace HexEngine.Rendering.UnitRenderers;

public class CircleTurretUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        // Body: filled circle (normalized so barrel tip = r)
        float bodyR = r * 0.7f;
        float bodyScreenR = Math.Max(3f, Vector2.Distance(drawCenter, W(wp.X + bodyR, wp.Y)));
        drawer.DrawFilledCircle(drawCenter, bodyScreenR, MapRenderer.ApplyFog(fill, fogFactor));
        drawer.DrawCircle(drawCenter, bodyScreenR, MapRenderer.ApplyFog(outline, fogFactor));

        Color turretFill = ColorFromDef(colorDef?.TurretFill, fill);
        Color turretOutline = ColorFromDef(colorDef?.TurretOutline, outline);

        // Barrel: slim rectangle from center going forward (up)
        float barrelW = bodyR * 0.15f;
        float barrelH = r;
        var barrelPts = new[]
        {
            W(wp.X - barrelW, wp.Y - barrelH),
            W(wp.X + barrelW, wp.Y - barrelH),
            W(wp.X + barrelW, wp.Y),
            W(wp.X - barrelW, wp.Y),
        };
        drawer.DrawFilledPolygon(barrelPts, MapRenderer.ApplyFog(turretFill, fogFactor));
        drawer.DrawPolygonOutline(barrelPts, MapRenderer.ApplyFog(turretOutline, fogFactor));

        // Turret ring: smaller circle in the center
        float turretR = bodyR * 0.45f;
        float tScreenR = Math.Max(2f, Vector2.Distance(drawCenter, W(wp.X + turretR, wp.Y)));
        drawer.DrawFilledCircle(drawCenter, tScreenR, MapRenderer.ApplyFog(turretFill, fogFactor));
        drawer.DrawCircle(drawCenter, tScreenR, MapRenderer.ApplyFog(turretOutline, fogFactor));
    }

    protected override void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        Vector2 sc = S(center.X, center.Y);
        float sr = Math.Max(2f, Vector2.Distance(sc, S(center.X + r, center.Y)));
        drawer.DrawFilledCircle(sc, sr, color);
    }
}
