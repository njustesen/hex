using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;
using HexEngine.View;

namespace HexEngine.Rendering.UnitRenderers;

public class HexagonUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        // Star Destroyer wedge: pointed nose, wide stern
        // Normalize so nose (max extent) = r
        float s = r / 1.1f;
        Vector2 nose      = W(wp.X, wp.Y - s * 1.1f);
        Vector2 rNeck     = W(wp.X + s * 0.25f, wp.Y - s * 0.5f);
        Vector2 rWing     = W(wp.X + s * 0.9f, wp.Y + s * 0.6f);
        Vector2 rStern    = W(wp.X + s * 0.7f, wp.Y + s * 0.9f);
        Vector2 lStern    = W(wp.X - s * 0.7f, wp.Y + s * 0.9f);
        Vector2 lWing     = W(wp.X - s * 0.9f, wp.Y + s * 0.6f);
        Vector2 lNeck     = W(wp.X - s * 0.25f, wp.Y - s * 0.5f);

        // Hull is concave at the neck→wing transition, so split into convex sub-polygons
        Color fogFill = MapRenderer.ApplyFog(fill, fogFactor);
        drawer.DrawFilledPolygon(new[] { nose, rNeck, lNeck }, fogFill);
        drawer.DrawFilledPolygon(new[] { rNeck, rWing, lWing, lNeck }, fogFill);
        drawer.DrawFilledPolygon(new[] { rWing, rStern, lStern, lWing }, fogFill);

        var hullPts = new[] { nose, rNeck, rWing, rStern, lStern, lWing, lNeck };
        drawer.DrawPolygonOutline(hullPts, MapRenderer.ApplyFog(outline, fogFactor));

        // Two turrets — both pointing forward (up)
        Color tFill = ColorFromDef(colorDef?.TurretFill, fill);
        Color tOutline = ColorFromDef(colorDef?.TurretOutline, outline);
        float tBarrelW = s * 0.07f;
        float tBarrelH = s * 0.5f;
        float tRingR = s * 0.18f;

        // Forward turret (near the nose)
        float fwdY = wp.Y - s * 0.25f;
        DrawTurretForward(drawer, W, wp.X, fwdY, tBarrelW, tBarrelH, tRingR, tFill, tOutline, fogFactor);

        // Aft turret (mid-body) — also pointing forward
        float aftY = wp.Y + s * 0.55f;
        DrawTurretForward(drawer, W, wp.X, aftY, tBarrelW, tBarrelH, tRingR, tFill, tOutline, fogFactor);
    }

    private static void DrawTurretForward(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, float cx, float cy,
        float barrelW, float barrelH, float ringR,
        Color tFill, Color tOutline, float fogFactor)
    {
        // Barrel extends upward (forward)
        var barrel = new[]
        {
            W(cx - barrelW, cy - barrelH),
            W(cx + barrelW, cy - barrelH),
            W(cx + barrelW, cy),
            W(cx - barrelW, cy),
        };
        drawer.DrawFilledPolygon(barrel, MapRenderer.ApplyFog(tFill, fogFactor));
        drawer.DrawPolygonOutline(barrel, MapRenderer.ApplyFog(tOutline, fogFactor));

        // Turret ring
        Vector2 center = W(cx, cy);
        float screenR = Math.Max(2f, Vector2.Distance(center, W(cx + ringR, cy)));
        drawer.DrawFilledCircle(center, screenR, MapRenderer.ApplyFog(tFill, fogFactor));
        drawer.DrawCircle(center, screenR, MapRenderer.ApplyFog(tOutline, fogFactor));
    }

    protected override void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        // Wedge shadow matching the hull shape — split into convex sub-polygons
        float s = r / 1.1f;
        Vector2 nose   = S(center.X, center.Y - s * 1.1f);
        Vector2 rNeck  = S(center.X + s * 0.25f, center.Y - s * 0.5f);
        Vector2 rWing  = S(center.X + s * 0.9f, center.Y + s * 0.6f);
        Vector2 rStern = S(center.X + s * 0.7f, center.Y + s * 0.9f);
        Vector2 lStern = S(center.X - s * 0.7f, center.Y + s * 0.9f);
        Vector2 lWing  = S(center.X - s * 0.9f, center.Y + s * 0.6f);
        Vector2 lNeck  = S(center.X - s * 0.25f, center.Y - s * 0.5f);

        drawer.DrawFilledPolygon(new[] { nose, rNeck, lNeck }, color);
        drawer.DrawFilledPolygon(new[] { rNeck, rWing, lWing, lNeck }, color);
        drawer.DrawFilledPolygon(new[] { rWing, rStern, lStern, lWing }, color);
    }
}
