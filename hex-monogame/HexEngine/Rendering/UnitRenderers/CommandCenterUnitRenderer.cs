using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;

namespace HexEngine.Rendering.UnitRenderers;

public class CommandCenterUnitRenderer : UnitRenderer
{
    protected override void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor)
    {
        Color fogFill = MapRenderer.ApplyFog(fill, fogFactor);
        Color fogOutline = MapRenderer.ApplyFog(outline, fogFactor);

        // Flat-top hex base
        float hexR = r * 0.95f;
        var hexPts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = MathF.PI / 3f * i;
            hexPts[i] = W(wp.X + hexR * MathF.Cos(angle), wp.Y + hexR * MathF.Sin(angle));
        }
        // Fill hex as triangle fan from center
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            drawer.DrawFilledPolygon(new[] { drawCenter, hexPts[i], hexPts[next] }, fogFill);
        }
        drawer.DrawPolygonOutline(hexPts, fogOutline);

        // Inner diamond
        float dR = r * 0.55f;
        var diamondPts = new[]
        {
            W(wp.X, wp.Y - dR),
            W(wp.X + dR, wp.Y),
            W(wp.X, wp.Y + dR),
            W(wp.X - dR, wp.Y),
        };
        Color innerFill = MapRenderer.ApplyFog(new Color(
            Math.Min(fill.R + 30, 255),
            Math.Min(fill.G + 30, 255),
            Math.Min(fill.B + 30, 255)), fogFactor);
        drawer.DrawFilledPolygon(diamondPts, innerFill);
        drawer.DrawPolygonOutline(diamondPts, fogOutline);

        // Central dome circle
        float domeR = r * 0.25f;
        float domeScreenR = Math.Max(2f, Vector2.Distance(drawCenter, W(wp.X + domeR, wp.Y)));
        Color domeFill = MapRenderer.ApplyFog(new Color(
            Math.Min(fill.R + 60, 255),
            Math.Min(fill.G + 60, 255),
            Math.Min(fill.B + 60, 255)), fogFactor);
        drawer.DrawFilledCircle(drawCenter, domeScreenR, domeFill);
        drawer.DrawCircle(drawCenter, domeScreenR, fogOutline);
    }
}
