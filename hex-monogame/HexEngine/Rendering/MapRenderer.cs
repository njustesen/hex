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

        // Ground unit stat bars pass — drawn after all tiles so bars aren't covered
        foreach (var tile in sortedTiles)
        {
            bool tileVisible = state.VisibleTiles == null || state.VisibleTiles.Contains(tile);
            if (tile.Unit != null && !tile.Unit.IsFlying && tileVisible)
            {
                var screenPoints = vp.GetTileScreenPoints(tile);
                float fogFactor = UnitRenderer.ComputeFogFactor(vp, tile, fogStrength);
                bool isEnemy = state.VisibleTiles != null && tile.Unit.Team != state.CurrentTeam;
                var def = UnitDefs.Get(tile.Unit.Type);
                int? projHp = GetProjectedHealth(state, tile);
                UnitRenderer.GetRenderer(def.Shape).DrawStatBars(drawer, vp, tile, screenPoints, fogFactor, isEnemy, projectedHealth: projHp);
            }
        }

        // Flying units pass — drawn after all tiles and ground units
        foreach (var tile in sortedTiles)
        {
            bool flyVisible = state.VisibleTiles == null || state.VisibleTiles.Contains(tile);
            if (tile.Unit != null && tile.Unit.IsFlying && flyVisible)
            {
                var screenPoints = vp.GetTileScreenPoints(tile);
                float fogFactor = UnitRenderer.ComputeFogFactor(vp, tile, fogStrength);
                bool isEnemy = state.VisibleTiles != null && tile.Unit.Team != state.CurrentTeam;
                var def = UnitDefs.Get(tile.Unit.Type);
                int? projHp = GetProjectedHealth(state, tile);
                UnitRenderer.GetRenderer(def.Shape).Draw(drawer, vp, tile, screenPoints, fogFactor, isEnemy, state.IsEditor, projectedHealth: projHp);
            }
        }

        // Executing unit overlay (when passing through occupied tiles)
        if (state.ExecUnitTile != null && state.ExecUnit != null
            && state.ExecUnitTile.Unit != state.ExecUnit)
        {
            var tile = state.ExecUnitTile;
            var screenPoints = vp.GetTileScreenPoints(tile);
            float fogFactor = UnitRenderer.ComputeFogFactor(vp, tile, fogStrength);
            var def = UnitDefs.Get(state.ExecUnit.Type);
            UnitRenderer.GetRenderer(def.Shape).Draw(drawer, vp, tile, screenPoints, fogFactor,
                isEnemy: false, isEditor: state.IsEditor, unitOverride: state.ExecUnit);
        }

        // Connection arms from mines to CCs
        DrawMineConnectionArms(drawer, vp, state, fogStrength);

        // Plan visualization (drawn on top)
        DrawPlanVisualization(drawer, vp, state, fogStrength);

        // Pending attack preview
        DrawPendingAttack(drawer, vp, state);

        // Attack animation
        DrawAttackAnimation(drawer, vp, state);

        DrawEdgeHighlight(drawer, vp, state);
    }

    private void DrawPlanVisualization(PrimitiveDrawer drawer, Viewport vp, InteractionState state, float fogStrength)
    {
        Color goldColor = new Color(200, 170, 40);
        Color brightGold = new Color(255, 220, 60);

        // Draw move paths
        if (state.PlanPaths != null)
        {
            foreach (var path in state.PlanPaths)
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    var fromCenter = GetTileScreenCenter(vp, path[i]);
                    var toCenter = GetTileScreenCenter(vp, path[i + 1]);
                    drawer.DrawLine(fromCenter, toCenter, goldColor);
                }
            }
        }

        // Draw circles at each move step
        if (state.PlanSteps != null)
        {
            for (int i = 0; i < state.PlanSteps.Count; i++)
            {
                var center = GetTileScreenCenter(vp, state.PlanSteps[i]);
                float radius = 6f;
                if (i == state.PlanSteps.Count - 1)
                {
                    drawer.DrawFilledCircle(center, radius, brightGold);
                    drawer.DrawCircle(center, radius, goldColor);
                }
                else
                {
                    drawer.DrawCircle(center, radius, goldColor);
                }
            }
        }

        // Draw confirmed attack pairs in plan
        if (state.PlanAttackPairs != null)
        {
            var attackColor = new Color(255, 80, 60);

            // Deduplicate: draw one crosshair per target with attack count
            var drawnTargets = new HashSet<Tile>();

            foreach (var (source, target) in state.PlanAttackPairs)
            {
                var sourceCenter = GetTileScreenCenter(vp, source);
                var targetCenter = GetTileScreenCenter(vp, target);
                drawer.DrawLine(sourceCenter, targetCenter, attackColor);

                if (drawnTargets.Contains(target)) continue;
                drawnTargets.Add(target);

                int atkCount = 1;
                if (state.PlanDamagePreview != null && state.PlanDamagePreview.TryGetValue(target, out var dmgInfo))
                    atkCount = dmgInfo.AttackCount;

                float r = 8f;
                drawer.DrawCircle(targetCenter, r, attackColor);
                drawer.DrawLine(targetCenter - new Vector2(r, 0), targetCenter + new Vector2(r, 0), attackColor);
                drawer.DrawLine(targetCenter - new Vector2(0, r), targetCenter + new Vector2(0, r), attackColor);

                // Multiple attacks: draw concentric rings
                for (int i = 1; i < atkCount; i++)
                    drawer.DrawCircle(targetCenter, r + i * 4f, attackColor);
            }
        }

        // Draw kill indicator for units that will die
        if (state.PlanDamagePreview != null)
        {
            foreach (var (target, (_, projHp, _, _)) in state.PlanDamagePreview)
            {
                if (projHp <= 0)
                {
                    var center = GetTileScreenCenter(vp, target);
                    float xr = 10f;
                    var killColor = new Color(255, 40, 40);
                    drawer.DrawLine(center - new Vector2(xr, xr), center + new Vector2(xr, xr), killColor);
                    drawer.DrawLine(center - new Vector2(-xr, xr), center + new Vector2(-xr, xr), killColor);
                }
            }
        }
    }

    private void DrawPendingAttack(PrimitiveDrawer drawer, Viewport vp, InteractionState state)
    {
        if (state.PendingAttackTarget == null) return;

        // Draw line from attack source (last move in plan, or selected unit) to target
        Tile? sourceTile = null;
        if (state.PlanSteps != null && state.PlanSteps.Count > 0)
            sourceTile = state.PlanSteps[^1];
        sourceTile ??= state.SelectedUnitTile;
        if (sourceTile == null) return;

        var sourceCenter = GetTileScreenCenter(vp, sourceTile);
        var targetCenter = GetTileScreenCenter(vp, state.PendingAttackTarget);

        var attackColor = new Color(255, 80, 60, 200);
        drawer.DrawLine(sourceCenter, targetCenter, attackColor);

        // Crosshair on target
        float r = 8f;
        drawer.DrawCircle(targetCenter, r, attackColor);
        drawer.DrawLine(targetCenter - new Vector2(r, 0), targetCenter + new Vector2(r, 0), attackColor);
        drawer.DrawLine(targetCenter - new Vector2(0, r), targetCenter + new Vector2(0, r), attackColor);

        // Kill indicator for pending attack
        if (state.PendingAttackDamagePreview != null && state.PendingAttackDamagePreview.Value.ProjectedHealth <= 0)
        {
            float xr = 10f;
            var killColor = new Color(255, 40, 40);
            drawer.DrawLine(targetCenter - new Vector2(xr, xr), targetCenter + new Vector2(xr, xr), killColor);
            drawer.DrawLine(targetCenter - new Vector2(-xr, xr), targetCenter + new Vector2(-xr, xr), killColor);
        }
    }

    private void DrawAttackAnimation(PrimitiveDrawer drawer, Viewport vp, InteractionState state)
    {
        if (state.AnimationTimer <= 0 || state.AnimationSourceTile == null || state.AnimationTargetTile == null)
            return;

        float t = state.AnimationTimer / EngineConfig.PlanStepDelay; // normalized 1->0
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

    private void DrawMineConnectionArms(PrimitiveDrawer drawer, Viewport vp, InteractionState state, float fogStrength)
    {
        for (int y = 0; y < vp.Map.Rows; y++)
            for (int x = 0; x < vp.Map.Cols; x++)
            {
                var tile = vp.Map.Tiles[y][x];
                if (tile.Unit == null || tile.Unit.Type != "Mine") continue;
                if (tile.Resource == ResourceType.None) continue;
                bool tileVisible = state.VisibleTiles == null || state.VisibleTiles.Contains(tile);
                if (!tileVisible) continue;

                // Find adjacent friendly CC
                for (int e = 0; e < vp.Map.EdgeCount; e++)
                {
                    var neighbor = vp.Map.GetNeighbor(tile, e);
                    if (neighbor?.Unit != null && neighbor.Unit.Type == "CommandCenter" && neighbor.Unit.Team == tile.Unit.Team)
                    {
                        var mineCenter = GetTileScreenCenter(vp, tile);
                        var ccCenter = GetTileScreenCenter(vp, neighbor);
                        float fogFactor = UnitRenderer.ComputeFogFactor(vp, tile, fogStrength);
                        Color armColor = tile.Resource == ResourceType.Iron
                            ? ApplyFog(new Color(140, 90, 45, 200), fogFactor)
                            : ApplyFog(new Color(60, 200, 60, 200), fogFactor);

                        // Draw thick arm (3 parallel lines)
                        var dir = ccCenter - mineCenter;
                        if (dir.LengthSquared() > 0)
                        {
                            dir.Normalize();
                            var perp = new Vector2(-dir.Y, dir.X);
                            drawer.DrawLine(mineCenter, ccCenter, armColor);
                            drawer.DrawLine(mineCenter + perp, ccCenter + perp, armColor);
                            drawer.DrawLine(mineCenter - perp, ccCenter - perp, armColor);
                        }
                        break;
                    }
                }
            }
    }

    /// Look up projected health for a tile from plan damage preview or pending attack preview.
    private static int? GetProjectedHealth(InteractionState state, Tile tile)
    {
        if (state.PlanDamagePreview != null && state.PlanDamagePreview.TryGetValue(tile, out var dmg))
            return dmg.ProjectedHealth;
        if (state.PendingAttackTarget == tile && state.PendingAttackDamagePreview != null)
            return state.PendingAttackDamagePreview.Value.ProjectedHealth;
        return null;
    }

    private static Vector2 GetTileScreenCenter(Viewport vp, Tile tile)
    {
        return vp.WorldToSurface(tile.Pos);
    }

    private void DrawRampRects(PrimitiveDrawer drawer, Viewport vp, InteractionState state,
                               Tile tile, Color topColor, float depthMultiplier, float fogStrength)
    {
        if (state.InnerShapeScale <= 0f || state.InnerShapeScale >= 1f) return;

        // Fog-of-war: darken ramps on non-visible tiles
        bool tileVisible = state.VisibleTiles == null || state.VisibleTiles.Contains(tile);
        float visFactor = tileVisible ? 1f : 0.35f;

        foreach (int edge in tile.Ramps)
        {
            var neighbor = vp.Map.GetNeighbor(tile, edge);
            if (neighbor == null || neighbor.Elevation <= tile.Elevation) continue;

            // Use darkest visibility of the two connected tiles
            bool neighborVisible = state.VisibleTiles == null || state.VisibleTiles.Contains(neighbor);
            float rampVis = Math.Min(visFactor, neighborVisible ? 1f : 0.35f);

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
            Color rampColor = SurfaceColor(avgEdgeFrom, avgEdgeTo, rampAvgY, 0.95f, 0.05f, topColor, fogStrength, vp.ScreenHeight, rampVis);
            drawer.DrawFilledQuad(lA, lB, rA, rB, rampColor);

            int prevEdge = (edge - 1 + ec) % ec;
            int nextOppEdge = (oppEdge + 1) % ec;
            if (!tile.Ramps.Contains(prevEdge) && !neighbor.Ramps.Contains(nextOppEdge))
            {
                var p = lowerPts[edge];
                float triAvgY = (lA.Y + rB.Y + p.Y) / 3f;
                Color endColor = SurfaceColor(rB, p, triAvgY, 0f, 1f, topColor, fogStrength, vp.ScreenHeight, rampVis);
                drawer.DrawFilledPolygon(new[] { lA, rB, p }, endColor);
            }

            int nextEdge = (edge + 1) % ec;
            int prevOppEdge = (oppEdge - 1 + ec) % ec;
            if (!tile.Ramps.Contains(nextEdge) && !neighbor.Ramps.Contains(prevOppEdge))
            {
                var p = lowerPts[(edge + 1) % ec];
                float triAvgY = (lB.Y + rA.Y + p.Y) / 3f;
                Color endColor = SurfaceColor(rA, p, triAvgY, 0f, 1f, topColor, fogStrength, vp.ScreenHeight, rampVis);
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

            // Fog-of-war: use darkest visibility of the three tiles
            bool tileVis = state.VisibleTiles == null || state.VisibleTiles.Contains(tile);
            bool n1Vis = state.VisibleTiles == null || state.VisibleTiles.Contains(n1);
            bool n2Vis = state.VisibleTiles == null || state.VisibleTiles.Contains(n2);
            float cornerVisFactor = Math.Min(tileVis ? 1f : 0.35f, Math.Min(n1Vis ? 1f : 0.35f, n2Vis ? 1f : 0.35f));

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
            Color triColor = SurfaceColor(n1Pt, n2Pt, cornerAvgY, 0.9f, 0.1f, topColor, fogStrength, vp.ScreenHeight, cornerVisFactor);
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

        // Fog-of-war: darken non-visible tiles
        bool tileVisible = state.VisibleTiles == null || state.VisibleTiles.Contains(tile);
        if (!tileVisible)
            fogFactor *= 0.35f;

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

        // Gameplay highlights — only on visible tiles
        if (tileVisible && state.AttackableTiles != null && state.AttackableTiles.Contains(tile))
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
        else if (tileVisible && state.ReachableTiles != null && state.ReachableTiles.Contains(tile))
        {
            if (useInnerHighlight)
            {
                if (shouldFill) drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyBrightness(foggedTopColor, 1.35f));
                shouldFill = false;
            }
            else
            {
                fillColor = ApplyBrightness(foggedTopColor, 1.35f);
                shouldFill = true;
            }
        }
        else if (tileVisible && state.PlanAttackableTiles != null && state.PlanAttackableTiles.Contains(tile))
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
        else if (tileVisible && state.PlanReachableTiles != null && state.PlanReachableTiles.Contains(tile))
        {
            if (useInnerHighlight)
            {
                if (shouldFill) drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyBrightness(foggedTopColor, 1.35f));
                shouldFill = false;
            }
            else
            {
                fillColor = ApplyBrightness(foggedTopColor, 1.35f);
                shouldFill = true;
            }
        }

        if (tileVisible && state.PlannedPath != null && state.PlannedPath.Contains(tile))
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

        if (tileVisible && tile == state.SelectedUnitTile)
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

        if (tileVisible && tile == state.SelectedBuildTile)
        {
            if (useInnerHighlight && shouldFill)
            {
                drawer.DrawFilledPolygon(screenPoints, fillColor);
                var (center, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, ApplyFog(new Color(40, 160, 40), fogFactor));
                shouldFill = false;
            }
            else
            {
                fillColor = ApplyFog(new Color(40, 160, 40), fogFactor);
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

        if (tileVisible && tile == state.HoverTile && state.InnerShapeScale > 0f && state.InnerShapeScale < 1f)
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

        // Resource overlay diamond (scales with tile screen size)
        if (tile.Resource != ResourceType.None)
        {
            var (resCenter, _) = ComputeInnerShape(screenPoints, vertexCount, 1f);
            // Compute screen-space half-width of tile for scaling
            float sMinX2 = float.MaxValue, sMaxX2 = float.MinValue;
            for (int i = 0; i < vertexCount; i++)
            {
                if (screenPoints[i].X < sMinX2) sMinX2 = screenPoints[i].X;
                if (screenPoints[i].X > sMaxX2) sMaxX2 = screenPoints[i].X;
            }
            float resR = Math.Max(3f, (sMaxX2 - sMinX2) * 0.15f);
            Color resColor = tile.Resource == ResourceType.Iron
                ? ApplyFog(new Color(140, 90, 45), fogFactor)
                : ApplyFog(new Color(60, 200, 60), fogFactor);
            var resPts = new[]
            {
                new Vector2(resCenter.X, resCenter.Y - resR),
                new Vector2(resCenter.X + resR, resCenter.Y),
                new Vector2(resCenter.X, resCenter.Y + resR),
                new Vector2(resCenter.X - resR, resCenter.Y),
            };
            drawer.DrawFilledPolygon(resPts, resColor);
        }

        // Mine placement highlight
        if (tileVisible && state.MinePlacementTiles != null && state.MinePlacementTiles.Contains(tile))
        {
            bool useInner = state.InnerShapeScale > 0f && state.InnerShapeScale < 1f;
            Color mineHighlight = tile.Resource == ResourceType.Iron
                ? ApplyFog(new Color(220, 160, 60), fogFactor)
                : ApplyFog(new Color(60, 220, 60), fogFactor);
            if (useInner)
            {
                var (_, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
                drawer.DrawFilledPolygon(innerPts, mineHighlight);
            }
            else
            {
                drawer.DrawFilledPolygon(screenPoints, mineHighlight);
            }
        }

        // Buildable tile highlight — thin team-colored outline around inner shape
        if (tileVisible && state.BuildableTiles != null && state.BuildableTiles.Contains(tile)
            && state.InnerShapeScale > 0f && state.InnerShapeScale < 1f)
        {
            Color buildColor = state.CurrentTeam == Team.Red
                ? ApplyFog(new Color(180, 40, 40), fogFactor)
                : ApplyFog(new Color(40, 80, 180), fogFactor);
            var (_, innerPts) = ComputeInnerShape(screenPoints, vertexCount, state.InnerShapeScale);
            drawer.DrawPolygonOutline(innerPts, buildColor);
        }

        // Draw ground unit only on visible tiles (stat bars deferred to separate pass)
        if (tile.Unit != null && !tile.Unit.IsFlying && tileVisible)
        {
            bool isEnemy = state.VisibleTiles != null && tile.Unit.Team != state.CurrentTeam;
            var def = UnitDefs.Get(tile.Unit.Type);
            UnitRenderer.GetRenderer(def.Shape).Draw(drawer, vp, tile, screenPoints, fogFactor, isEnemy, state.IsEditor, deferStatBars: true);
        }

        // Starting location marker (editor only)
        if (tile.IsStartingLocation && state.IsEditor)
        {
            var (starCenter, _) = ComputeInnerShape(screenPoints, vertexCount, 1f);
            float sMinX = float.MaxValue, sMaxX = float.MinValue;
            for (int i = 0; i < vertexCount; i++)
            {
                if (screenPoints[i].X < sMinX) sMinX = screenPoints[i].X;
                if (screenPoints[i].X > sMaxX) sMaxX = screenPoints[i].X;
            }
            float starR = Math.Max(4f, (sMaxX - sMinX) * 0.25f);
            DrawStar(drawer, starCenter, starR, new Color(255, 220, 60));
        }
    }


    private static void DrawStar(PrimitiveDrawer drawer, Vector2 center, float radius, Color color)
    {
        int points = 5;
        float innerRadius = radius * 0.45f;
        var starPts = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float angle = -MathF.PI / 2f + i * MathF.PI / points;
            float r = i % 2 == 0 ? radius : innerRadius;
            starPts[i] = center + new Vector2(MathF.Cos(angle) * r, MathF.Sin(angle) * r);
        }
        drawer.DrawFilledPolygon(starPts, color);
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
                                      Color topColor, float fogStrength, float screenHeight,
                                      float visibilityFactor = 1f)
    {
        float edgeDx = edgeTo.X - edgeFrom.X;
        float edgeDy = edgeTo.Y - edgeFrom.Y;
        float len = MathF.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
        float nx = len > 0 ? edgeDy / len : 0;

        float wallBrightness = 0.35f + 0.5f * (nx + 1f) / 2f;
        float brightness = baseBrightness + wallScale * wallBrightness;

        float normScreenY = Math.Clamp(avgY / screenHeight, 0f, 1f);
        float fogFactor = ((1f - fogStrength) + fogStrength * normScreenY) * visibilityFactor;

        return ApplyFog(ApplyBrightness(topColor, brightness), fogFactor);
    }
}
