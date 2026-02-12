using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using HexEngine.Tiles;
using HexEngine.View;
using HexEngine.Core;
using HexEngine.Config;

namespace HexEngine.Rendering;

public class MapRenderer
{
    public void Draw(PrimitiveDrawer drawer, Viewport vp, InteractionState state, bool showGrid)
    {
        if (showGrid)
            DrawGrid(drawer, vp, 80);
        DrawMap(drawer, vp, state);
    }

    private void DrawMap(PrimitiveDrawer drawer, Viewport vp, InteractionState state)
    {
        float depthMultiplier = EngineConfig.DepthMultiplier;
        Color topColor = EngineConfig.TileTopColor;
        bool drawDepth = depthMultiplier > 0;
        float fogStrength = vp.IsMinimap ? 0f : EngineConfig.FogStrength;

        var sortedTiles = new List<Tile>(vp.Map.Rows * vp.Map.Cols);
        for (int y = 0; y < vp.Map.Rows; y++)
            for (int x = 0; x < vp.Map.Cols; x++)
                sortedTiles.Add(vp.Map.Tiles[y][x]);
        sortedTiles.Sort((a, b) => a.Pos.Y.CompareTo(b.Pos.Y));

        foreach (var tile in sortedTiles)
        {
            DrawTile(drawer, vp, state, tile, topColor, depthMultiplier, drawDepth, fogStrength);

            if (drawDepth && tile.Ramps.Count > 0)
                DrawRampRects(drawer, vp, state, tile, topColor, depthMultiplier, fogStrength);
        }

        foreach (var tile in sortedTiles)
        {
            if (tile.Ramps.Count >= 2)
                DrawRampCorners(drawer, vp, state, tile, topColor, fogStrength);
        }

        // Flying units pass — drawn after all tiles and ground units
        foreach (var tile in sortedTiles)
        {
            if (tile.Unit != null && tile.Unit.IsFlying)
            {
                var screenPoints = vp.GetTileScreenPoints(tile);
                float fogFactor = UnitRenderer.ComputeFogFactor(vp, tile, fogStrength);
                var def = UnitDefs.Get(tile.Unit.Type);
                UnitRenderer.GetRenderer(def.Shape).Draw(drawer, vp, tile, screenPoints, fogFactor);
            }
        }

        // Plan visualization (drawn on top)
        DrawPlanVisualization(drawer, vp, state, fogStrength);

        // Attack animation
        DrawAttackAnimation(drawer, vp, state);

        DrawEdgeHighlight(drawer, vp, state);
    }

    private void DrawPlanVisualization(PrimitiveDrawer drawer, Viewport vp, InteractionState state, float fogStrength)
    {
        if (state.PlanPaths == null || state.PlanSteps == null) return;

        Color goldColor = new Color(200, 170, 40);
        Color brightGold = new Color(255, 220, 60);

        // Draw lines connecting consecutive tile centers for each path
        foreach (var path in state.PlanPaths)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                var fromCenter = GetTileScreenCenter(vp, path[i]);
                var toCenter = GetTileScreenCenter(vp, path[i + 1]);
                drawer.DrawLine(fromCenter, toCenter, goldColor);
            }
        }

        // Draw circles at each plan step
        for (int i = 0; i < state.PlanSteps.Count; i++)
        {
            var center = GetTileScreenCenter(vp, state.PlanSteps[i]);
            float radius = 6f;
            if (i == state.PlanSteps.Count - 1)
            {
                // Final step: filled circle (brighter gold)
                drawer.DrawFilledCircle(center, radius, brightGold);
                drawer.DrawCircle(center, radius, goldColor);
            }
            else
            {
                // Intermediate step: outline only
                drawer.DrawCircle(center, radius, goldColor);
            }
        }
    }

    private void DrawAttackAnimation(PrimitiveDrawer drawer, Viewport vp, InteractionState state)
    {
        if (state.AnimationTimer <= 0 || state.AnimationSourceTile == null || state.AnimationTargetTile == null)
            return;

        float t = state.AnimationTimer / 0.3f; // normalized 1->0
        var sourceCenter = GetTileScreenCenter(vp, state.AnimationSourceTile);
        var targetCenter = GetTileScreenCenter(vp, state.AnimationTargetTile);

        // Projectile line
        int alpha = (int)(255 * t);
        var projColor = new Color(255, 255, 60, alpha);
        drawer.DrawLine(sourceCenter, targetCenter, projColor);

        // Hit flash: expanding circle on target
        float hitRadius = 8f + (1f - t) * 20f;
        int hitAlpha = (int)(200 * t);
        var hitColor = new Color(255, 100, 60, hitAlpha);
        drawer.DrawCircle(targetCenter, hitRadius, hitColor);
        drawer.DrawFilledCircle(targetCenter, hitRadius * 0.5f, new Color(255, 255, 255, (int)(150 * t)));
    }

    private static Vector2 GetTileScreenCenter(Viewport vp, Tile tile)
    {
        return vp.WorldToSurface(tile.Pos);
    }

    private void DrawRampRects(PrimitiveDrawer drawer, Viewport vp, InteractionState state,
                               Tile tile, Color topColor, float depthMultiplier, float fogStrength)
    {
        if (state.InnerShapeScale <= 0f || state.InnerShapeScale >= 1f) return;

        foreach (int edge in tile.Ramps)
        {
            var neighbor = vp.Map.GetNeighbor(tile, edge);
            if (neighbor == null || neighbor.Elevation <= tile.Elevation) continue;

            var lowerPts = vp.GetTileScreenPoints(tile);
            var raisedPts = vp.GetTileScreenPoints(neighbor);
            int oppEdge = vp.Map.GetOppositeEdge(edge);
            int ec = vp.Map.EdgeCount;

            Vector2 lowerCenter = Vector2.Zero;
            for (int i = 0; i < ec; i++) lowerCenter += lowerPts[i];
            lowerCenter /= ec;

            Vector2 lA = Vector2.Lerp(lowerCenter, lowerPts[edge], state.InnerShapeScale);
            Vector2 lB = Vector2.Lerp(lowerCenter, lowerPts[(edge + 1) % ec], state.InnerShapeScale);

            Vector2 rA = raisedPts[oppEdge];
            Vector2 rB = raisedPts[(oppEdge + 1) % ec];

            Vector2 avgEdgeFrom = (lA + rB) / 2f;
            Vector2 avgEdgeTo = (lB + rA) / 2f;
            float rampAvgY = (lA.Y + lB.Y + rA.Y + rB.Y) / 4f;
            Color rampColor = SurfaceColor(avgEdgeFrom, avgEdgeTo, rampAvgY, 0.95f, 0.05f, topColor, fogStrength, vp.ScreenHeight);
            drawer.DrawFilledQuad(lA, lB, rA, rB, rampColor);

            int prevEdge = (edge - 1 + ec) % ec;
            int nextOppEdge = (oppEdge + 1) % ec;
            if (!tile.Ramps.Contains(prevEdge) && !neighbor.Ramps.Contains(nextOppEdge))
            {
                var p = lowerPts[edge];
                float triAvgY = (lA.Y + rB.Y + p.Y) / 3f;
                Color endColor = SurfaceColor(rB, p, triAvgY, 0f, 1f, topColor, fogStrength, vp.ScreenHeight);
                drawer.DrawFilledPolygon(new[] { lA, rB, p }, endColor);
            }

            int nextEdge = (edge + 1) % ec;
            int prevOppEdge = (oppEdge - 1 + ec) % ec;
            if (!tile.Ramps.Contains(nextEdge) && !neighbor.Ramps.Contains(prevOppEdge))
            {
                var p = lowerPts[(edge + 1) % ec];
                float triAvgY = (lB.Y + rA.Y + p.Y) / 3f;
                Color endColor = SurfaceColor(rA, p, triAvgY, 0f, 1f, topColor, fogStrength, vp.ScreenHeight);
                drawer.DrawFilledPolygon(new[] { lB, rA, p }, endColor);
            }
        }
    }

    private void DrawRampCorners(PrimitiveDrawer drawer, Viewport vp, InteractionState state,
                                         Tile tile, Color topColor, float fogStrength)
    {
        if (state.InnerShapeScale <= 0f || state.InnerShapeScale >= 1f) return;

        int ec = vp.Map.EdgeCount;

        foreach (int e1 in tile.Ramps)
        {
            int e2 = (e1 + 1) % ec;
            if (!tile.Ramps.Contains(e2)) continue;

            var n1 = vp.Map.GetNeighbor(tile, e1);
            var n2 = vp.Map.GetNeighbor(tile, e2);
            if (n1 == null || n2 == null) continue;
            bool bothLower = n1.Elevation < tile.Elevation && n2.Elevation < tile.Elevation;
            bool bothHigher = n1.Elevation > tile.Elevation && n2.Elevation > tile.Elevation;
            if (!bothLower && !bothHigher) continue;

            var tilePts = vp.GetTileScreenPoints(tile);
            Vector2 sharedPt;
            if (bothLower)
            {
                sharedPt = tilePts[e2];
            }
            else
            {
                Vector2 tileCenter = Vector2.Zero;
                for (int i = 0; i < ec; i++) tileCenter += tilePts[i];
                tileCenter /= ec;
                sharedPt = Vector2.Lerp(tileCenter, tilePts[e2], state.InnerShapeScale);
            }

            int oppE1 = vp.Map.GetOppositeEdge(e1);
            var n1Pts = vp.GetTileScreenPoints(n1);
            Vector2 n1Pt;
            if (bothLower)
            {
                Vector2 n1Center = Vector2.Zero;
                for (int i = 0; i < ec; i++) n1Center += n1Pts[i];
                n1Center /= ec;
                n1Pt = Vector2.Lerp(n1Center, n1Pts[oppE1], state.InnerShapeScale);
            }
            else
            {
                n1Pt = n1Pts[oppE1];
            }

            int oppE2 = vp.Map.GetOppositeEdge(e2);
            var n2Pts = vp.GetTileScreenPoints(n2);
            Vector2 n2Pt;
            if (bothLower)
            {
                Vector2 n2Center = Vector2.Zero;
                for (int i = 0; i < ec; i++) n2Center += n2Pts[i];
                n2Center /= ec;
                n2Pt = Vector2.Lerp(n2Center, n2Pts[(oppE2 + 1) % ec], state.InnerShapeScale);
            }
            else
            {
                n2Pt = n2Pts[(oppE2 + 1) % ec];
            }

            float cornerAvgY = (sharedPt.Y + n1Pt.Y + n2Pt.Y) / 3f;
            Color triColor = SurfaceColor(n1Pt, n2Pt, cornerAvgY, 0.9f, 0.1f, topColor, fogStrength, vp.ScreenHeight);
            drawer.DrawFilledPolygon(new[] { sharedPt, n1Pt, n2Pt }, triColor);
        }
    }

    private void DrawEdgeHighlight(PrimitiveDrawer drawer, Viewport vp, InteractionState state)
    {
        if (state.HighlightedEdgeIndex == null || state.HighlightedEdgeTile == null) return;

        var screenPoints = vp.GetTileScreenPoints(state.HighlightedEdgeTile);
        int edge = state.HighlightedEdgeIndex.Value;
        int nextIdx = (edge + 1) % screenPoints.Length;
        drawer.DrawLine(screenPoints[edge], screenPoints[nextIdx], Colors.YELLOW);
    }

    private static (Vector2 center, Vector2[] innerPts) ComputeInnerShape(Vector2[] screenPoints, int vertexCount, float innerShapeScale)
    {
        Vector2 center = Vector2.Zero;
        for (int i = 0; i < vertexCount; i++)
            center += screenPoints[i];
        center /= vertexCount;

        var innerPts = new Vector2[vertexCount];
        for (int i = 0; i < vertexCount; i++)
            innerPts[i] = Vector2.Lerp(center, screenPoints[i], innerShapeScale);

        return (center, innerPts);
    }

    private void DrawTile(PrimitiveDrawer drawer, Viewport vp, InteractionState state,
                          Tile tile, Color topColor, float depthMultiplier, bool drawDepth, float fogStrength)
    {
        var screenPoints = new Vector2[tile.Points.Length];
        for (int i = 0; i < tile.Points.Length; i++)
            screenPoints[i] = vp.WorldToSurface(tile.Points[i]);

        int vertexCount = vp.Map.EdgeCount;
        float screenCenterY = 0;
        for (int i = 0; i < vertexCount; i++)
            screenCenterY += screenPoints[i].Y;
        screenCenterY /= vertexCount;
        float normScreenY = Math.Clamp(screenCenterY / vp.ScreenHeight, 0f, 1f);
        float fogFactor = (1f - fogStrength) + fogStrength * normScreenY;

        float elevBrightness = 1f + tile.Elevation * 0.08f;
        Color foggedTopColor = ApplyFog(ApplyBrightness(topColor, elevBrightness), fogFactor);
        var outlineColor = ApplyFog(new Color(0, 80, 0), fogFactor);

        if (drawDepth)
        {
            float depthPixels = DepthHelper.ComputeDepthPixels(screenPoints, depthMultiplier);
            float liftPixels = depthPixels * tile.Elevation;
            float totalDepth = depthPixels * (tile.Elevation + 1);

            for (int i = 0; i < screenPoints.Length; i++)
                screenPoints[i] = new Vector2(screenPoints[i].X, screenPoints[i].Y - liftPixels);

            var sideQuads = DepthHelper.ComputeDepthSideQuads(screenPoints, totalDepth);

            foreach (var sq in sideQuads)
            {
                float edgeDx = sq.Quad[1].X - sq.Quad[0].X;
                float edgeDy = sq.Quad[1].Y - sq.Quad[0].Y;
                float len = MathF.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
                float nx = len > 0 ? edgeDy / len : 0;

                float brightness = 0.35f + 0.5f * (nx + 1f) / 2f;
                Color sideColor = ApplyFog(ApplyBrightness(topColor, brightness), fogFactor);

                drawer.DrawFilledQuad(sq.Quad[0], sq.Quad[1], sq.Quad[2], sq.Quad[3], sideColor);

                drawer.DrawLine(sq.Quad[0], sq.Quad[3], outlineColor);
                drawer.DrawLine(sq.Quad[1], sq.Quad[2], outlineColor);
                drawer.DrawLine(sq.Quad[2], sq.Quad[3], outlineColor);
            }
        }

        // Determine fill color with gameplay highlights
        Color fillColor = foggedTopColor;
        bool shouldFill = drawDepth;
        bool useInnerHighlight = state.InnerShapeScale > 0f && state.InnerShapeScale < 1f;

        // Gameplay highlights — use inner shape only
        if (state.AttackableTiles != null && state.AttackableTiles.Contains(tile))
        {
            if (useInnerHighlight)
            {
                if (shouldFill) drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyFog(new Color(160, 40, 40), fogFactor));
                shouldFill = false; // already drawn
            }
            else
            {
                fillColor = ApplyFog(new Color(160, 40, 40), fogFactor);
                shouldFill = true;
            }
        }
        else if (state.ReachableTiles != null && state.ReachableTiles.Contains(tile))
        {
            if (useInnerHighlight)
            {
                if (shouldFill) drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyFog(new Color(60, 60, 160), fogFactor));
                shouldFill = false;
            }
            else
            {
                fillColor = ApplyFog(new Color(60, 60, 160), fogFactor);
                shouldFill = true;
            }
        }
        else if (state.PlanAttackableTiles != null && state.PlanAttackableTiles.Contains(tile))
        {
            if (useInnerHighlight)
            {
                if (shouldFill) drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyFog(new Color(180, 60, 60), fogFactor));
                shouldFill = false;
            }
            else
            {
                fillColor = ApplyFog(new Color(180, 60, 60), fogFactor);
                shouldFill = true;
            }
        }
        else if (state.PlanReachableTiles != null && state.PlanReachableTiles.Contains(tile))
        {
            if (useInnerHighlight)
            {
                if (shouldFill) drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyFog(new Color(80, 80, 180), fogFactor));
                shouldFill = false;
            }
            else
            {
                fillColor = ApplyFog(new Color(80, 80, 180), fogFactor);
                shouldFill = true;
            }
        }

        if (state.PlannedPath != null && state.PlannedPath.Contains(tile))
        {
            if (useInnerHighlight && shouldFill)
            {
                drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyFog(new Color(180, 140, 40), fogFactor));
                shouldFill = false;
            }
            else if (!useInnerHighlight)
            {
                fillColor = ApplyFog(new Color(180, 140, 40), fogFactor);
                shouldFill = true;
            }
        }

        if (tile == state.SelectedUnitTile)
        {
            if (useInnerHighlight && shouldFill)
            {
                drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyFog(new Color(160, 160, 40), fogFactor));
                shouldFill = false;
            }
            else
            {
                fillColor = ApplyFog(new Color(160, 160, 40), fogFactor);
                shouldFill = true;
            }
        }

        if (tile == state.SelectedTile)
        {
            fillColor = ApplyFog(new Color(40, 120, 40), fogFactor);
            shouldFill = true;
        }

        if (shouldFill)
            drawer.DrawFilledPolygon(screenPoints, fillColor);

        if (tile == state.HoverTile && state.InnerShapeScale > 0f && state.InnerShapeScale < 1f)
        {
            var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);

            Color hoverColor = tile == state.SelectedTile
                ? ApplyFog(new Color(80, 160, 80), fogFactor)
                : ApplyFog(new Color(0, 80, 0), fogFactor);
            drawer.DrawFilledPolygon(innerPts, hoverColor);
        }

        drawer.DrawPolygonOutline(screenPoints, outlineColor);

        if (EngineConfig.ShowInnerShapes && state.InnerShapeScale > 0f && state.InnerShapeScale < 1f)
        {
            var (center, innerPoints) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
            drawer.DrawPolygonOutline(innerPoints, outlineColor);
        }

        if (tile.Unit != null && !tile.Unit.IsFlying)
        {
            var def = UnitDefs.Get(tile.Unit.Type);
            UnitRenderer.GetRenderer(def.Shape).Draw(drawer, vp, tile, screenPoints, fogFactor);
        }
    }


    private void DrawGrid(PrimitiveDrawer drawer, Viewport vp, int gridSize)
    {
        float yOffset = vp.Map.Y1 % gridSize;
        float xOffset = vp.Map.X1 % gridSize;

        for (int yIdx = 0; yIdx < (int)(vp.Map.Height / gridSize) + 2; yIdx++)
        {
            float yWorld = vp.Map.Y1 + yOffset + yIdx * gridSize;
            var start = vp.WorldToSurface(new Vector2(vp.Map.X1, yWorld));
            var end = vp.WorldToSurface(new Vector2(vp.Map.X1 + vp.Map.Width, yWorld));
            drawer.DrawLine(start, end, Colors.GREY);
        }

        for (int xIdx = 0; xIdx < (int)(vp.Map.Width / gridSize) + 2; xIdx++)
        {
            float xWorld = vp.Map.X1 + xOffset + xIdx * gridSize;
            var start = vp.WorldToSurface(new Vector2(xWorld, vp.Map.Y1));
            var end = vp.WorldToSurface(new Vector2(xWorld, vp.Map.Y1 + vp.Map.Height));
            drawer.DrawLine(start, end, Colors.GREY);
        }
    }

    internal static Color ApplyFog(Color color, float fogFactor)
    {
        return new Color(
            (int)(color.R * fogFactor),
            (int)(color.G * fogFactor),
            (int)(color.B * fogFactor));
    }

    internal static Color ApplyBrightness(Color color, float brightness)
    {
        return new Color(
            (int)(color.R * brightness),
            (int)(color.G * brightness),
            (int)(color.B * brightness));
    }

    private static Color SurfaceColor(Vector2 edgeFrom, Vector2 edgeTo, float avgY,
                                      float baseBrightness, float wallScale,
                                      Color topColor, float fogStrength, float screenHeight)
    {
        float edgeDx = edgeTo.X - edgeFrom.X;
        float edgeDy = edgeTo.Y - edgeFrom.Y;
        float len = MathF.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
        float nx = len > 0 ? edgeDy / len : 0;

        float wallBrightness = 0.35f + 0.5f * (nx + 1f) / 2f;
        float brightness = baseBrightness + wallScale * wallBrightness;

        float normScreenY = Math.Clamp(avgY / screenHeight, 0f, 1f);
        float fogFactor = (1f - fogStrength) + fogStrength * normScreenY;

        return ApplyFog(ApplyBrightness(topColor, brightness), fogFactor);
    }
}
