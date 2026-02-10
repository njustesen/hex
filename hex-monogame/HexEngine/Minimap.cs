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

        float x1 = PrimaryViewport.Cam.X1;
        float x2 = PrimaryViewport.Cam.X2;
        float y1 = PrimaryViewport.Cam.Y1;
        float y2 = PrimaryViewport.Cam.Y2;

        var topLeft = WorldToSurface(new Vector2(x1, y1));
        var bottomRight = WorldToSurface(new Vector2(x2, y2));

        // Clamp to minimap surface (inset by 1 to keep outline inside surface,
        // matching pygame.draw.rect which draws outline inside the rect bounds)
        float sx1 = Math.Max(1, topLeft.X);
        float sy1 = Math.Max(1, topLeft.Y);
        float sx2 = Math.Min(ScreenWidth - 1, bottomRight.X);
        float sy2 = Math.Min(ScreenHeight - 1, bottomRight.Y);

        float w = sx2 - sx1;
        float h = sy2 - sy1;

        if (w > 0 && h > 0)
            drawer.DrawRectOutline(sx1, sy1, w, h, Colors.WHITE);
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
