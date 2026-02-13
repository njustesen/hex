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
        ["circle_turret"] = new UnitRenderers.CircleTurretUnitRenderer(),
        ["missile_tower"] = new UnitRenderers.MissileTowerUnitRenderer(),
        ["command_center"] = new UnitRenderers.CommandCenterUnitRenderer(),
        ["mine"] = new UnitRenderers.MineUnitRenderer(),
    };
    private static readonly UnitRenderer _fallback = new UnitRenderers.DiamondUnitRenderer();

    public static UnitRenderer GetRenderer(string shape)
        => _renderers.TryGetValue(shape, out var r) ? r : _fallback;

    // Shared team colors — all units on the same team use the same fill/outline
    private static readonly Dictionary<Team, (Color Fill, Color Outline, Color TurretFill, Color TurretOutline)> TeamColors = new()
    {
        [Team.Red] = (new Color(180, 40, 40), new Color(120, 20, 20), new Color(210, 70, 70), new Color(140, 40, 40)),
        [Team.Blue] = (new Color(40, 80, 180), new Color(20, 50, 120), new Color(70, 110, 210), new Color(40, 70, 140)),
    };

    public void Draw(PrimitiveDrawer drawer, Viewport vp, Tile tile,
                     Vector2[] screenPoints, float fogFactor, bool isEnemy = false, bool isEditor = false,
                     Unit? unitOverride = null, bool deferStatBars = false)
    {
        var unit = unitOverride ?? tile.Unit!;
        var def = UnitDefs.Get(unit.Type);
        var teamColor = TeamColors.TryGetValue(unit.Team, out var tc) ? tc : TeamColors[Team.Red];

        // Minimap: just a filled circle with team color, no shape/stats
        if (vp.IsMinimap)
        {
            int vertCount = vp.Map.EdgeCount;
            float cx = 0, cy = 0;
            for (int i = 0; i < vertCount; i++) { cx += screenPoints[i].X; cy += screenPoints[i].Y; }
            var center = new Vector2(cx / vertCount, cy / vertCount);
            float hw = 0, hh = 0;
            for (int i = 0; i < vertCount; i++)
            {
                float dx = Math.Abs(screenPoints[i].X - center.X);
                float dy = Math.Abs(screenPoints[i].Y - center.Y);
                if (dx > hw) hw = dx;
                if (dy > hh) hh = dy;
            }
            float mr = Math.Max(2f, Math.Min(hw, hh) * 0.6f);
            drawer.DrawFilledCircle(center, mr, teamColor.Fill);
            drawer.DrawCircle(center, mr, teamColor.Outline);
            return;
        }

        var colorDef = new UnitColorDef
        {
            Fill = new List<int> { teamColor.Fill.R, teamColor.Fill.G, teamColor.Fill.B },
            Outline = new List<int> { teamColor.Outline.R, teamColor.Outline.G, teamColor.Outline.B },
            TurretFill = new List<int> { teamColor.TurretFill.R, teamColor.TurretFill.G, teamColor.TurretFill.B },
            TurretOutline = new List<int> { teamColor.TurretOutline.R, teamColor.TurretOutline.G, teamColor.TurretOutline.B },
        };

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
        if (isEditor)
            drawer.DrawCircle(drawCenter, baseScreenR, MapRenderer.ApplyFog(new Color(90, 90, 90, 140), fogFactor));

        Color fill = ColorFromDef(colorDef.Fill, new Color(128, 128, 128));
        Color outline = ColorFromDef(colorDef.Outline, new Color(80, 80, 80));

        // Shapes fill the base circle — baseWorldR is the reference radius
        float r = baseWorldR;

        DrawShape(drawer, W, drawCenter, wp, r, fill, outline, colorDef, fogFactor);

        // Stat bars below unit (only if not deferred)
        if (!deferStatBars)
            DrawStatBarsInternal(drawer, drawCenter, baseScreenR, unit, fogFactor, isEnemy);
    }

    /// Draw only the stat bars for a unit (health, ammo, movement).
    /// Called in a separate pass so bars render on top of all tiles.
    public void DrawStatBars(PrimitiveDrawer drawer, Viewport vp, Tile tile,
                             Vector2[] screenPoints, float fogFactor, bool isEnemy = false,
                             Unit? unitOverride = null)
    {
        var unit = unitOverride ?? tile.Unit!;
        if (vp.IsMinimap) return;

        var def = UnitDefs.Get(unit.Type);
        int vertexCount = vp.Map.EdgeCount;

        float scx = 0, scy = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            scx += screenPoints[i].X;
            scy += screenPoints[i].Y;
        }
        Vector2 screenCenter = new Vector2(scx / vertexCount, scy / vertexCount);

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
        float scaleY = halfExtentY > 0 ? screenHalfH / halfExtentY : 1f;
        float scaleX = halfExtentX > 0 ? screenHalfW / halfExtentX : 1f;

        float flyOffset = unit.IsFlying ? halfExtentY * (0.15f + def.Size * 0.35f) : 0f;
        float liftY = unit.IsFlying ? flyOffset : def.Hover * halfExtentY;
        Vector2 drawCenter = new Vector2(screenCenter.X, screenCenter.Y - liftY * scaleY);
        float baseScreenR = Math.Max(3f, def.Size * halfExtentX * scaleX);

        DrawStatBarsInternal(drawer, drawCenter, baseScreenR, unit, fogFactor, isEnemy);
    }

    private static void DrawStatBarsInternal(PrimitiveDrawer drawer, Vector2 drawCenter,
                                              float baseScreenR, Unit unit, float fogFactor, bool isEnemy)
    {
        const float rowHeight = 8f;
        const float statGap = 3f;
        float rowStartY = drawCenter.Y + baseScreenR + statGap;

        if (isEnemy)
        {
            Vector2 healthAnchor = new Vector2(drawCenter.X, rowStartY);
            DrawHealthBar(drawer, healthAnchor, unit, fogFactor);
        }
        else
        {
            Vector2 ammoAnchor = new Vector2(drawCenter.X, rowStartY);
            Vector2 mpAnchor = new Vector2(drawCenter.X, rowStartY + rowHeight);
            Vector2 healthAnchor = new Vector2(drawCenter.X, rowStartY + 2 * rowHeight);
            DrawAmmoPips(drawer, ammoAnchor, unit, fogFactor);
            DrawMpBar(drawer, mpAnchor, unit, fogFactor);
            DrawHealthBar(drawer, healthAnchor, unit, fogFactor);
        }
    }

    /// Draw a unit shape preview centered at (cx, cy) fitting within the given size.
    public static void DrawPreview(PrimitiveDrawer drawer, string unitType, Team team, float cx, float cy, float size, float brightness = 1f)
    {
        var def = UnitDefs.Get(unitType);
        var teamColor = TeamColors.TryGetValue(team, out var tc) ? tc : TeamColors[Team.Red];

        List<int> Dim(Color c) => new() { (int)(c.R * brightness), (int)(c.G * brightness), (int)(c.B * brightness) };

        var colorDef = new UnitColorDef
        {
            Fill = Dim(teamColor.Fill),
            Outline = Dim(teamColor.Outline),
            TurretFill = Dim(teamColor.TurretFill),
            TurretOutline = Dim(teamColor.TurretOutline),
        };

        Color fill = ColorFromDef(colorDef.Fill, new Color(128, 128, 128));
        Color outline = ColorFromDef(colorDef.Outline, new Color(80, 80, 80));

        Vector2 drawCenter = new Vector2(cx, cy);
        Vector2 wp = Vector2.Zero;
        float r = def.Size * size * 0.6f;

        Vector2 W(float x, float y) => new Vector2(cx + x, cy + y);

        GetRenderer(def.Shape).DrawShape(drawer, W, drawCenter, wp, r, fill, outline, colorDef, 1f);
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
        float py = anchor.Y - pipW / 2f;

        for (int i = 0; i < unit.MaxAttacks; i++)
        {
            float px = startX + i * (pipW + gap);
            Color pipColor = i < unit.AttacksRemaining
                ? MapRenderer.ApplyFog(new Color(255, 220, 40), fogFactor)
                : MapRenderer.ApplyFog(new Color(60, 50, 20), fogFactor);
            drawer.DrawFilledRect(px, py, pipW, pipW, pipColor);
        }
    }

    private static void DrawMpBar(PrimitiveDrawer drawer, Vector2 anchor,
                                  Unit unit, float fogFactor)
    {
        int max = unit.MaxMovementPoints;
        if (max <= 0) return;

        const float circleR = 2.5f;
        const float gap = 3f;
        float totalW = max * (circleR * 2f) + (max - 1) * gap;
        float startX = anchor.X - totalW / 2f;

        for (int i = 0; i < max; i++)
        {
            float cx = startX + circleR + i * (circleR * 2f + gap);
            Color segColor = i < unit.MovementPoints
                ? MapRenderer.ApplyFog(new Color(100, 160, 220), fogFactor)
                : MapRenderer.ApplyFog(new Color(15, 20, 40), fogFactor);
            drawer.DrawFilledCircle(new Vector2(cx, anchor.Y), circleR, segColor);
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
        float top = anchor.Y - (segH + pad * 2) / 2f;

        drawer.DrawFilledRect(startX - pad, top, totalW + pad * 2, segH + pad * 2,
            MapRenderer.ApplyFog(new Color(20, 20, 20), fogFactor));

        for (int i = 0; i < max; i++)
        {
            float sx = startX + i * (segW + segGap);
            Color segColor = i < unit.Health
                ? MapRenderer.ApplyFog(new Color(40, 180, 40), fogFactor)
                : MapRenderer.ApplyFog(new Color(120, 30, 30), fogFactor);
            drawer.DrawFilledRect(sx, top + pad, segW, segH, segColor);
        }
    }
}
