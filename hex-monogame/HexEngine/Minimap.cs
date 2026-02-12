using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HexEngine.Maps;

namespace HexEngine;

public class Minimap
{
    public Viewport View { get; }

    public float ScreenX1 => View.ScreenX1;
    public float ScreenY1 => View.ScreenY1;
    public float ScreenWidth => View.ScreenWidth;
    public float ScreenHeight => View.ScreenHeight;
    public RenderTarget2D? RenderTarget => View.RenderTarget;
    public bool IsMinimap => View.IsMinimap;
    public bool IsPrimary => View.IsPrimary;

    private readonly Viewport? _primaryViewport;
    private static readonly InteractionState _emptyState = new();

    public Minimap(float screenX1, float screenY1, float screenWidth, float screenHeight,
                   float zoomLevel, GridMap map, Viewport? primaryViewport = null, Color? color = null)
    {
        _primaryViewport = primaryViewport;
        View = new Viewport(screenX1, screenY1, screenWidth, screenHeight, zoomLevel, map,
               canZoom: false, canMove: false, color: color ?? Colors.BLACK,
               primaryViewport: primaryViewport, isMinimap: true, isPrimary: false);
    }

    public void CreateRenderTarget(GraphicsDevice graphicsDevice)
        => View.CreateRenderTarget(graphicsDevice);

    public void Draw(GraphicsDevice graphicsDevice, PrimitiveDrawer drawer, MapRenderer renderer)
    {
        if (View.RenderTarget == null) return;

        graphicsDevice.SetRenderTarget(View.RenderTarget);
        graphicsDevice.Clear(View.BackColor);

        drawer.UpdateProjection((int)View.ScreenWidth, (int)View.ScreenHeight);

        renderer.Draw(drawer, View, _emptyState, false);
        DrawCamera(drawer);

        graphicsDevice.SetRenderTarget(null);
    }

    private void DrawCamera(PrimitiveDrawer drawer)
    {
        if (_primaryViewport == null) return;

        float pf = EngineConfig.PerspectiveFactor;
        float cx1 = _primaryViewport.Cam.X1;
        float cx2 = _primaryViewport.Cam.X2;
        float cy1 = _primaryViewport.Cam.Y1;
        float cy2 = _primaryViewport.Cam.Y2;

        if (pf > 0)
        {
            var pv = _primaryViewport;
            var worldTL = pv.SurfaceToWorld(new Vector2(pv.ScreenX1, pv.ScreenY1));
            var worldTR = pv.SurfaceToWorld(new Vector2(pv.ScreenX1 + pv.ScreenWidth, pv.ScreenY1));
            var worldBL = pv.SurfaceToWorld(new Vector2(pv.ScreenX1, pv.ScreenY1 + pv.ScreenHeight));
            var worldBR = pv.SurfaceToWorld(new Vector2(pv.ScreenX1 + pv.ScreenWidth, pv.ScreenY1 + pv.ScreenHeight));

            var sTL = View.WorldToSurface(worldTL);
            var sTR = View.WorldToSurface(worldTR);
            var sBL = View.WorldToSurface(worldBL);
            var sBR = View.WorldToSurface(worldBR);

            var points = new Vector2[] { sTL, sTR, sBR, sBL };
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(
                    Math.Clamp(points[i].X, 1, View.ScreenWidth - 1),
                    Math.Clamp(points[i].Y, 1, View.ScreenHeight - 1));
            }

            drawer.DrawPolygonOutline(points, Colors.WHITE);
        }
        else
        {
            var topLeft = View.WorldToSurface(new Vector2(cx1, cy1));
            var bottomRight = View.WorldToSurface(new Vector2(cx2, cy2));

            float sx1 = Math.Max(1, topLeft.X);
            float sy1 = Math.Max(1, topLeft.Y);
            float sx2 = Math.Min(View.ScreenWidth - 1, bottomRight.X);
            float sy2 = Math.Min(View.ScreenHeight - 1, bottomRight.Y);

            float w = sx2 - sx1;
            float h = sy2 - sy1;

            if (w > 0 && h > 0)
                drawer.DrawRectOutline(sx1, sy1, w, h, Colors.WHITE);
        }
    }

    public void HandleDrag((int X, int Y) mousePos, bool mouseDown)
    {
        if (!mouseDown) return;
        if (!View.IsWithin(mousePos.X, mousePos.Y)) return;

        var worldPos = View.SurfaceToWorld(new Vector2(mousePos.X, mousePos.Y));

        if (_primaryViewport != null)
        {
            _primaryViewport.Cam.Change(center: worldPos);
            _primaryViewport.Moved = true;
        }
    }
}
