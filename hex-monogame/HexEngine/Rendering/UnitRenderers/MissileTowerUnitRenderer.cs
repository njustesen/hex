using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;

namespace HexEngine.Rendering.UnitRenderers;

public class MissileTowerUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        Color turretFill = ColorFromDef(colorDef?.TurretFill, fill);
        Color turretOutline = ColorFromDef(colorDef?.TurretOutline, outline);

        // Tower base: shorter tapered rectangle
        float baseW = r * 0.45f;
        float topW = r * 0.3f;
        float towerH = r * 0.55f;
        float towerTop = wp.Y - towerH;
        var towerPts = new[]
        {
            W(wp.X - topW, towerTop),
            W(wp.X + topW, towerTop),
            W(wp.X + baseW, wp.Y + towerH),
            W(wp.X - baseW, wp.Y + towerH),
        };
        drawer.DrawFilledPolygon(towerPts, MapRenderer.ApplyFog(fill, fogFactor));
        drawer.DrawPolygonOutline(towerPts, MapRenderer.ApplyFog(outline, fogFactor));

        // Round dome on top of tower
        float domeR = r * 0.3f;
        float domeCenterY = towerTop - domeR * 0.15f;
        Vector2 domeCenter = W(wp.X, domeCenterY);
        float domeScreenR = Math.Max(3f, Vector2.Distance(domeCenter, W(wp.X + domeR, domeCenterY)));
        drawer.DrawFilledCircle(domeCenter, domeScreenR, MapRenderer.ApplyFog(turretFill, fogFactor));
        drawer.DrawCircle(domeCenter, domeScreenR, MapRenderer.ApplyFog(turretOutline, fogFactor));

        // Turret barrel pointing up from dome
        float barrelW = domeR * 0.18f;
        float barrelH = r * 0.5f;
        float barrelTop = domeCenterY - barrelH;
        var barrelPts = new[]
        {
            W(wp.X - barrelW, barrelTop),
            W(wp.X + barrelW, barrelTop),
            W(wp.X + barrelW, domeCenterY),
            W(wp.X - barrelW, domeCenterY),
        };
        drawer.DrawFilledPolygon(barrelPts, MapRenderer.ApplyFog(turretFill, fogFactor));
        drawer.DrawPolygonOutline(barrelPts, MapRenderer.ApplyFog(turretOutline, fogFactor));

        // Small turret ring in dome center
        float ringR = domeR * 0.4f;
        float ringScreenR = Math.Max(2f, Vector2.Distance(domeCenter, W(wp.X + ringR, domeCenterY)));
        drawer.DrawFilledCircle(domeCenter, ringScreenR, MapRenderer.ApplyFog(turretOutline, fogFactor));
    }

    protected override void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        float w = r * 0.4f;
        float h = r * 0.6f;
        var pts = new[]
        {
            S(center.X - w, center.Y - h),
            S(center.X + w, center.Y - h),
            S(center.X + w, center.Y + h),
            S(center.X - w, center.Y + h),
        };
        drawer.DrawFilledPolygon(pts, color);
    }
}
