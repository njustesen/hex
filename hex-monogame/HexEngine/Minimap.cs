using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using HexEngine.Maps;

namespace HexEngine;

public class Minimap : Viewport
{
    public Minimap(float screenX1, float screenY1, float screenWidth, float screenHeight,
                   float zoomLevel, GridMap map, Viewport? primaryViewport = null, Color? color = null)
        : base(screenX1, screenY1, screenWidth, screenHeight, zoomLevel, map,
               minimap: null, canZoom: false, canMove: false,
               color: color ?? Colors.BLACK, primaryViewport: primaryViewport,
               isMinimap: true, isPrimary: false)
    {
        PrimaryViewport = primaryViewport;
    }

    public void DrawMinimap(GraphicsDevice graphicsDevice, PrimitiveDrawer drawer, int? grid = null)
    {
        if (RenderTarget == null) return;

        graphicsDevice.SetRenderTarget(RenderTarget);
        graphicsDevice.Clear(BackColor);

        drawer.UpdateProjection((int)ScreenWidth, (int)ScreenHeight);

        if (grid.HasValue)
            DrawGrid(drawer, grid.Value);

        DrawMap(drawer);
        DrawCamera(drawer);

        graphicsDevice.SetRenderTarget(null);
    }

    private void DrawGrid(PrimitiveDrawer drawer, int gridSize)
    {
        float yOffset = Map.Y1 % gridSize;
        float xOffset = Map.X1 % gridSize;

        for (int yIdx = 0; yIdx < (int)(Map.Height / gridSize) + 2; yIdx++)
        {
            float yWorld = Map.Y1 + yOffset + yIdx * gridSize;
            var start = WorldToSurface(new Vector2(Map.X1, yWorld));
            var end = WorldToSurface(new Vector2(Map.X1 + Map.Width, yWorld));
            drawer.DrawLine(start, end, Colors.GREY);
        }

        for (int xIdx = 0; xIdx < (int)(Map.Width / gridSize) + 2; xIdx++)
        {
            float xWorld = Map.X1 + xOffset + xIdx * gridSize;
            var start = WorldToSurface(new Vector2(xWorld, Map.Y1));
            var end = WorldToSurface(new Vector2(xWorld, Map.Y1 + Map.Height));
            drawer.DrawLine(start, end, Colors.GREY);
        }
    }

    private void DrawCamera(PrimitiveDrawer drawer)
    {
        if (PrimaryViewport == null) return;

        float pf = EngineConfig.PerspectiveFactor;
        float cx1 = PrimaryViewport.Cam.X1;
        float cx2 = PrimaryViewport.Cam.X2;
        float cy1 = PrimaryViewport.Cam.Y1;
        float cy2 = PrimaryViewport.Cam.Y2;

        if (pf > 0)
        {
            // With perspective, the visible area is a trapezoid in world space.
            // Invert the perspective from screen corners to get the actual world bounds.
            var pv = PrimaryViewport;
            var worldTL = pv.SurfaceToWorld(new Vector2(pv.ScreenX1, pv.ScreenY1));
            var worldTR = pv.SurfaceToWorld(new Vector2(pv.ScreenX1 + pv.ScreenWidth, pv.ScreenY1));
            var worldBL = pv.SurfaceToWorld(new Vector2(pv.ScreenX1, pv.ScreenY1 + pv.ScreenHeight));
            var worldBR = pv.SurfaceToWorld(new Vector2(pv.ScreenX1 + pv.ScreenWidth, pv.ScreenY1 + pv.ScreenHeight));

            // Project these world points onto the minimap surface
            var sTL = WorldToSurface(worldTL);
            var sTR = WorldToSurface(worldTR);
            var sBL = WorldToSurface(worldBL);
            var sBR = WorldToSurface(worldBR);

            // Clamp to minimap bounds
            var points = new Vector2[] { sTL, sTR, sBR, sBL };
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(
                    Math.Clamp(points[i].X, 1, ScreenWidth - 1),
                    Math.Clamp(points[i].Y, 1, ScreenHeight - 1));
            }

            drawer.DrawPolygonOutline(points, Colors.WHITE);
        }
        else
        {
            var topLeft = WorldToSurface(new Vector2(cx1, cy1));
            var bottomRight = WorldToSurface(new Vector2(cx2, cy2));

            float sx1 = Math.Max(1, topLeft.X);
            float sy1 = Math.Max(1, topLeft.Y);
            float sx2 = Math.Min(ScreenWidth - 1, bottomRight.X);
            float sy2 = Math.Min(ScreenHeight - 1, bottomRight.Y);

            float w = sx2 - sx1;
            float h = sy2 - sy1;

            if (w > 0 && h > 0)
                drawer.DrawRectOutline(sx1, sy1, w, h, Colors.WHITE);
        }
    }

    public void HandleMinimapDrag((int X, int Y) mousePos, bool mouseDown)
    {
        if (!mouseDown) return;
        if (!IsWithin(mousePos.X, mousePos.Y)) return;

        var worldPos = SurfaceToWorld(new Vector2(mousePos.X, mousePos.Y));

        if (PrimaryViewport != null)
        {
            PrimaryViewport.Cam.Change(center: worldPos);
            PrimaryViewport.Moved = true;
        }
    }
}
