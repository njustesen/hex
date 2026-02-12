using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Core;
using HexEngine.Tiles;
using HexEngine.View;

namespace HexEngine.Rendering;

public abstract class UnitRenderer
{
    private static readonly Dictionary<string, UnitRenderer> _renderers = new()
    {
        ["circle"] = new UnitRenderers.CircleUnitRenderer(),
        ["rect_turret"] = new UnitRenderers.RectTurretUnitRenderer(),
        ["triangle"] = new UnitRenderers.TriangleUnitRenderer(),
        ["arrow"] = new UnitRenderers.ArrowUnitRenderer(),
        ["hexagon"] = new UnitRenderers.HexagonUnitRenderer(),
    };
    private static readonly UnitRenderer _fallback = new UnitRenderers.DiamondUnitRenderer();

    public static UnitRenderer GetRenderer(string shape)
        => _renderers.TryGetValue(shape, out var r) ? r : _fallback;

    public void Draw(PrimitiveDrawer drawer, Viewport vp, Tile tile,
                     Vector2[] screenPoints, float fogFactor)
    {
        var unit = tile.Unit!;
        var def = UnitDefs.Get(unit.Type);
        string teamKey = unit.Team == Team.Red ? "red" : "blue";
        var colorDef = def.Colors.ContainsKey(teamKey) ? def.Colors[teamKey] : null;

        // Screen-space polygon centroid (accounts for perspective + elevation)
        int vertexCount = vp.Map.EdgeCount;
        float scx = 0, scy = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            scx += screenPoints[i].X;
            scy += screenPoints[i].Y;
        }
        Vector2 screenCenter = new Vector2(scx / vertexCount, scy / vertexCount);

        // Screen-space extents of tile polygon
        float sMinX = float.MaxValue, sMaxX = float.MinValue;
        float sMinY = float.MaxValue, sMaxY = float.MinValue;
        for (int i = 0; i < vertexCount; i++)
        {
            if (screenPoints[i].X < sMinX) sMinX = screenPoints[i].X;
            if (screenPoints[i].X > sMaxX) sMaxX = screenPoints[i].X;
            if (screenPoints[i].Y < sMinY) sMinY = screenPoints[i].Y;
            if (screenPoints[i].Y > sMaxY) sMaxY = screenPoints[i].Y;
        }
        float screenHalfW = (sMaxX - sMinX) / 2f;
        float screenHalfH = (sMaxY - sMinY) / 2f;

        // World-space half-extents from tile polygon
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var pt in tile.Points)
        {
            if (pt.X < minX) minX = pt.X;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.Y > maxY) maxY = pt.Y;
        }
        float halfExtentX = (maxX - minX) / 2f;
        float halfExtentY = (maxY - minY) / 2f;

        // Scale factors: world units → screen pixels
        float scaleX = halfExtentX > 0 ? screenHalfW / halfExtentX : 1f;
        float scaleY = halfExtentY > 0 ? screenHalfH / halfExtentY : 1f;

        // Flying units: shift up and shadow down by equal amount, scaled by unit size
        float flyOffset = unit.IsFlying ? halfExtentY * (0.15f + def.Size * 0.35f) : 0f;
        float liftY = unit.IsFlying ? flyOffset : def.Hover * halfExtentY;
        Vector2 wp = new Vector2(tile.Pos.X, tile.Pos.Y - liftY);

        // Draw center: polygon centroid with flying lift in screen space
        Vector2 drawCenter = new Vector2(screenCenter.X, screenCenter.Y - liftY * scaleY);

        // Draw shadow at ground level (polygon centroid + shadow offset)
        if (unit.IsFlying || def.Hover > 0f)
        {
            float shadowR = def.Size * halfExtentX * (unit.IsFlying ? 0.8f : 0.6f);
            int shadowAlpha = unit.IsFlying ? 70 : 50;
            float shadowOffsetY = unit.IsFlying ? flyOffset : def.Hover * halfExtentY * 0.5f;
            Vector2 shadowScreenCenter = new Vector2(screenCenter.X, screenCenter.Y + shadowOffsetY * scaleY);
            // Shadow W: affine transform anchored at shadow screen center
            Vector2 shadowWorldCenter = new Vector2(tile.Pos.X, tile.Pos.Y + shadowOffsetY);
            Vector2 Sw(float x, float y) => new Vector2(
                shadowScreenCenter.X + (x - shadowWorldCenter.X) * scaleX,
                shadowScreenCenter.Y + (y - shadowWorldCenter.Y) * scaleY);
            DrawShadow(drawer, Sw, shadowWorldCenter, shadowR, new Color(0, 0, 0, shadowAlpha));
        }

        // Affine world-to-screen: anchored at drawCenter, scaled by tile extents
        Vector2 W(float x, float y) => new Vector2(
            drawCenter.X + (x - wp.X) * scaleX,
            drawCenter.Y + (y - wp.Y) * scaleY);

        // Base circle — size is radius relative to tile half-extent
        float baseWorldR = def.Size * halfExtentX;
        float baseScreenR = Math.Max(3f, baseWorldR * scaleX);
        drawer.DrawCircle(drawCenter, baseScreenR, MapRenderer.ApplyFog(new Color(90, 90, 90, 140), fogFactor));

        Color fill = ColorFromDef(colorDef?.Fill, new Color(128, 128, 128));
        Color outline = ColorFromDef(colorDef?.Outline, new Color(80, 80, 80));

        // Shapes fill the base circle — baseWorldR is the reference radius
        float r = baseWorldR;

        DrawShape(drawer, W, drawCenter, wp, r, fill, outline, colorDef, fogFactor);

        // Anchor points at top and bottom of the base circle
        Vector2 bottomAnchor = new Vector2(drawCenter.X, drawCenter.Y + baseScreenR);
        Vector2 topAnchor = new Vector2(drawCenter.X, drawCenter.Y - baseScreenR);

        DrawHealthBar(drawer, bottomAnchor, unit, fogFactor);
        DrawMpBar(drawer, bottomAnchor, unit, fogFactor);
        DrawAmmoPips(drawer, topAnchor, unit, fogFactor);
    }

    protected abstract void DrawShape(PrimitiveDrawer drawer,
        Func<float, float, Vector2> W, Vector2 drawCenter,
        Vector2 wp, float r, Color fill, Color outline,
        UnitColorDef? colorDef, float fogFactor);

    protected virtual void DrawShadow(PrimitiveDrawer drawer, Func<float, float, Vector2> S,
        Vector2 center, float r, Color color)
    {
        // Default: diamond shadow
        var pts = new[]
        {
            S(center.X, center.Y - r),
            S(center.X + r, center.Y),
            S(center.X, center.Y + r),
            S(center.X - r, center.Y),
        };
        drawer.DrawFilledPolygon(pts, color);
    }

    internal static Color ColorFromDef(List<int>? rgb, Color fallback)
    {
        if (rgb == null || rgb.Count < 3) return fallback;
        return new Color(rgb[0], rgb[1], rgb[2]);
    }

    internal static float ComputeFogFactor(Viewport vp, Tile tile, float fogStrength)
    {
        var screenPoints = vp.GetTileScreenPoints(tile);
        int vertexCount = vp.Map.EdgeCount;
        float screenCenterY = 0;
        for (int i = 0; i < vertexCount; i++)
            screenCenterY += screenPoints[i].Y;
        screenCenterY /= vertexCount;
        float normScreenY = Math.Clamp(screenCenterY / vp.ScreenHeight, 0f, 1f);
        return (1f - fogStrength) + fogStrength * normScreenY;
    }

    private static void DrawAmmoPips(PrimitiveDrawer drawer, Vector2 anchor,
                                     Unit unit, float fogFactor)
    {
        if (unit.MaxAttacks <= 0) return;

        const float pipW = 4f;
        const float gap = 3f;
        float totalW = unit.MaxAttacks * pipW + (unit.MaxAttacks - 1) * gap;
        float startX = anchor.X - totalW / 2f;

        for (int i = 0; i < unit.MaxAttacks; i++)
        {
            float px = startX + i * (pipW + gap);
            Color pipColor = i < unit.AttacksRemaining
                ? MapRenderer.ApplyFog(new Color(255, 220, 40), fogFactor)
                : MapRenderer.ApplyFog(new Color(60, 50, 20), fogFactor);
            drawer.DrawFilledRect(px, anchor.Y - pipW / 2f, pipW, pipW, pipColor);
        }
    }

    private static void DrawMpBar(PrimitiveDrawer drawer, Vector2 anchor,
                                  Unit unit, float fogFactor)
    {
        int max = unit.MaxMovementPoints;
        if (max <= 0) return;

        const float circleR = 2.5f;
        const float gap = 3f;
        const float mpOffset = 7f;
        float totalW = max * (circleR * 2f) + (max - 1) * gap;
        float startX = anchor.X - totalW / 2f;
        float cy = anchor.Y - mpOffset;

        for (int i = 0; i < max; i++)
        {
            float cx = startX + circleR + i * (circleR * 2f + gap);
            Color segColor = i < unit.MovementPoints
                ? MapRenderer.ApplyFog(new Color(100, 160, 220), fogFactor)
                : MapRenderer.ApplyFog(new Color(15, 20, 40), fogFactor);
            drawer.DrawFilledCircle(new Vector2(cx, cy), circleR, segColor);
        }
    }

    private static void DrawHealthBar(PrimitiveDrawer drawer, Vector2 anchor,
                                      Unit unit, float fogFactor)
    {
        int max = unit.MaxHealth;
        if (max <= 0) return;

        const float segW = 6f;
        const float segH = 3f;
        const float segGap = 1f;
        const float pad = 1f;
        float totalW = max * segW + (max - 1) * segGap;
        float startX = anchor.X - totalW / 2f;

        drawer.DrawFilledRect(startX - pad, anchor.Y - pad, totalW + pad * 2, segH + pad * 2,
            MapRenderer.ApplyFog(new Color(20, 20, 20), fogFactor));

        for (int i = 0; i < max; i++)
        {
            float sx = startX + i * (segW + segGap);
            Color segColor = i < unit.Health
                ? MapRenderer.ApplyFog(new Color(40, 180, 40), fogFactor)
                : MapRenderer.ApplyFog(new Color(120, 30, 30), fogFactor);
            drawer.DrawFilledRect(sx, anchor.Y, segW, segH, segColor);
        }
    }
}
