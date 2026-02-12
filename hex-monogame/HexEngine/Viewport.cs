using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine;

public class Viewport
{
    public float ScreenX1 { get; }
    public float ScreenY1 { get; }
    public float ScreenWidth { get; }
    public float ScreenHeight { get; }
    public float ScreenRatio { get; }
    public Color BackColor { get; }
    public GridMap Map { get; set; }
    public Minimap? Minimap { get; set; }
    public bool IsPrimary { get; }
    public bool IsMinimap { get; }
    public Viewport? PrimaryViewport { get; set; }
    public bool CanZoom { get; }
    public bool CanMove { get; }
    public Camera Cam { get; }
    public float ZoomLevel { get; set; }
    public bool Moved { get; set; }
    protected InteractionState State { get; set; } = new();

    public float Upp { get; }
    public float Ppu { get; }

    private (int X, int Y)? _prevMousePos;
    private Viewport? _prevMouseSurface;
    private RenderTarget2D? _renderTarget;

    public Viewport(float screenX1, float screenY1, float screenWidth, float screenHeight,
                    float zoomLevel, GridMap map,
                    bool canZoom = false, bool canMove = false,
                    Color? color = null, Viewport? primaryViewport = null,
                    bool isMinimap = false, bool isPrimary = false)
    {
        ScreenX1 = screenX1;
        ScreenY1 = screenY1;
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
        ScreenRatio = screenWidth / screenHeight;
        BackColor = color ?? Colors.BLACK;
        Map = map;
        IsPrimary = isPrimary;
        IsMinimap = isMinimap;
        PrimaryViewport = primaryViewport;
        CanZoom = canZoom;
        CanMove = canMove;
        ZoomLevel = zoomLevel;

        float uppWidth = map.Width / screenWidth;
        float uppHeight = map.Height / screenHeight;
        Upp = uppWidth > uppHeight ? uppHeight : uppWidth;
        Ppu = 1f / Upp;

        float mapRatio = map.Width / map.Height;

        if (isMinimap)
        {
            if (mapRatio > ScreenRatio)
                Cam = new Camera(map.Center, map.Width, map.Width * (screenHeight / screenWidth));
            else
                Cam = new Camera(map.Center, map.Height * (screenWidth / screenHeight), map.Height);
        }
        else
        {
            var bounds = new Rectangle((int)map.X1, (int)map.Y1, (int)map.Width, (int)map.Height);
            if (mapRatio > ScreenRatio)
                Cam = new Camera(map.Center, map.Height * (screenWidth / screenHeight), map.Height, bounds);
            else
                Cam = new Camera(map.Center, map.Width, map.Width * (screenHeight / screenWidth), bounds);
        }
    }

    public void CreateRenderTarget(GraphicsDevice graphicsDevice)
    {
        _renderTarget = new RenderTarget2D(graphicsDevice, (int)ScreenWidth, (int)ScreenHeight);
    }

    public RenderTarget2D? RenderTarget => _renderTarget;

    public float Scale => Ppu / ZoomLevel;

    public Vector2 WorldToSurface(Vector2 worldPosition)
    {
        var norm = Cam.Norm(worldPosition);
        float pf = (IsMinimap && !EngineConfig.MinimapPerspective) ? 0f : EngineConfig.PerspectiveFactor;

        if (pf > 0)
        {
            float normY = norm.Y;
            // Scale: smaller at top (normY=0), full size at bottom (normY=1)
            float scale = (1f - pf) + pf * normY;
            // Compress X toward center
            float centeredX = norm.X - 0.5f;
            norm = new Vector2(0.5f + centeredX * scale, norm.Y);
            // Compress Y: integrate scale for consistent vertical spacing
            float denom = 1f - pf / 2f;
            float mappedY = ((1f - pf) * normY + pf * normY * normY / 2f) / denom;
            norm = new Vector2(norm.X, mappedY);
        }

        return new Vector2(norm.X * ScreenWidth, norm.Y * ScreenHeight);
    }

    public Vector2 SurfaceToWorld(Vector2 surfacePosition)
    {
        float flatNormX = (surfacePosition.X - ScreenX1) / ScreenWidth;
        float flatNormY = (surfacePosition.Y - ScreenY1) / ScreenHeight;
        float pf = EngineConfig.PerspectiveFactor;

        float normX, normY;
        if (pf > 0)
        {
            // Invert Y compression: solve (1-pf)*t + pf*t²/2 = flatNormY * (1 - pf/2)
            float target = flatNormY * (1f - pf / 2f);
            float a = pf / 2f;
            float b = 1f - pf;
            float c = -target;
            float disc = b * b - 4f * a * c;
            normY = (-b + MathF.Sqrt(MathF.Max(0, disc))) / (2f * a);
            normY = Math.Clamp(normY, 0f, 1f);

            // Invert X: undo scale at this normY
            float scale = (1f - pf) + pf * normY;
            float centeredX = (flatNormX - 0.5f) / scale;
            normX = 0.5f + centeredX;
        }
        else
        {
            normX = flatNormX;
            normY = flatNormY;
        }

        float xWorld = Cam.X1 + Cam.CameraWidth * normX;
        float yWorld = Cam.Y1 + Cam.CameraHeight * normY;
        return new Vector2(xWorld, yWorld);
    }

    public bool IsWithin(float x, float y)
    {
        return x >= ScreenX1 && x <= ScreenX1 + ScreenWidth &&
               y >= ScreenY1 && y <= ScreenY1 + ScreenHeight;
    }

    public (int X, int Y)? MouseOnSurface()
    {
        var state = Mouse.GetState();
        if (IsWithin(state.X, state.Y))
            return (state.X, state.Y);
        return null;
    }

    public void Draw(GraphicsDevice graphicsDevice, PrimitiveDrawer drawer, MapRenderer renderer, InteractionState state, bool showGrid)
    {
        if (_renderTarget == null) return;

        graphicsDevice.SetRenderTarget(_renderTarget);
        graphicsDevice.Clear(BackColor);
        drawer.UpdateProjection((int)ScreenWidth, (int)ScreenHeight);
        renderer.Draw(drawer, this, state, showGrid);
        graphicsDevice.SetRenderTarget(null);
    }

    public void Update(float seconds, InputManager? inputManager = null, InteractionState? state = null)
    {
        if (state != null) State = state;
        Cam.Update(seconds);

        if (inputManager == null) return;

        // Keyboard movement
        if (inputManager.DirectionX != 0 || inputManager.DirectionY != 0)
            MoveByKeys(inputManager.DirectionX, inputManager.DirectionY, seconds, EngineConfig.MoveSpeed);

        // Zoom
        if (inputManager.ZoomDirection != 0)
            HandleZoom(inputManager.ZoomDirection, seconds, EngineConfig.ZoomSpeed);

        // Mouse interactions
        HandleMouse(inputManager.MousePos, inputManager.MouseDown, inputManager.MouseReleased);

        // Update hover tile
        if (IsWithin(inputManager.MousePos.X, inputManager.MousePos.Y))
            UpdateHoverTile(inputManager.MousePos);

        // Update minimap
        if (Minimap != null)
            Minimap.View.Update(seconds);
    }

    public void MoveByKeys(int dirX, int dirY, float seconds, float moveSpeed)
    {
        float speed = seconds * Cam.CameraWidth * moveSpeed;
        MoveCam(dirX * speed, dirY * speed);
    }

    public void HandleZoom(int zoomDirection, float seconds, float zoomSpeed)
    {
        ZoomCam(zoomDirection * zoomSpeed * seconds);
    }

    public void ZoomCam(float zoomDirection)
    {
        var mouseOnSurf = MouseOnSurface();
        if (ZoomLevel + zoomDirection < 0.1f)
            zoomDirection = 0.1f - ZoomLevel;
        ZoomLevel += zoomDirection;
        ZoomLevel = Math.Clamp(ZoomLevel, 0.1f, 1f);

        if (mouseOnSurf.HasValue && zoomDirection != 0)
        {
            var mouseWorld = SurfaceToWorld(new Vector2(mouseOnSurf.Value.X, mouseOnSurf.Value.Y));
            Cam.Change(Cam.Center,
                       Cam.CameraWidth + Cam.CameraWidth * zoomDirection,
                       Cam.CameraHeight + Cam.CameraHeight * zoomDirection);
            var mouseWorldAfter = SurfaceToWorld(new Vector2(mouseOnSurf.Value.X, mouseOnSurf.Value.Y));
            float dx = mouseWorld.X - mouseWorldAfter.X;
            float dy = mouseWorld.Y - mouseWorldAfter.Y;
            Cam.Change(new Vector2(Cam.Center.X + dx, Cam.Center.Y + dy));
        }
    }

    public void MoveCam(float dx, float dy)
    {
        Cam.Change(new Vector2(Cam.Center.X + dx, Cam.Center.Y + dy));
    }

    public void PanDrag((int X, int Y) mouseMovement)
    {
        var mouseOnSurf = MouseOnSurface();
        if (mouseOnSurf.HasValue)
        {
            float normX = mouseMovement.X / ScreenWidth;
            float normY = mouseMovement.Y / ScreenHeight;
            float xWorld = Cam.CameraWidth * normX;
            float yWorld = Cam.CameraHeight * normY;
            MoveCam(-xWorld, -yWorld);
            Moved = true;
        }
    }

    public void UpdateHoverTile((int X, int Y) mousePos)
    {
        State.HoverTile = GetTileAtScreen(mousePos);
    }

    private Tile? GetTileAtScreen((int X, int Y) mousePos)
    {
        var mouse = new Vector2(mousePos.X, mousePos.Y);

        // Build tiles in render order (back-to-front by Pos.Y)
        var sortedTiles = new System.Collections.Generic.List<Tile>(Map.Rows * Map.Cols);
        for (int y = 0; y < Map.Rows; y++)
            for (int x = 0; x < Map.Cols; x++)
                sortedTiles.Add(Map.Tiles[y][x]);
        sortedTiles.Sort((a, b) => a.Pos.Y.CompareTo(b.Pos.Y));

        // Check in reverse render order (front-to-back) so visually-on-top tiles win
        for (int i = sortedTiles.Count - 1; i >= 0; i--)
        {
            var screenPoints = GetTileScreenPoints(sortedTiles[i]);
            if (PointInPolygon(mouse, screenPoints))
                return sortedTiles[i];
        }

        return null;
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        int n = polygon.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) /
                           (polygon[j].Y - polygon[i].Y) + polygon[i].X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Get screen-space points of a tile's top surface (accounting for elevation lift).
    /// </summary>
    public Vector2[] GetTileScreenPoints(Tile tile)
    {
        float depthMultiplier = EngineConfig.DepthMultiplier;
        var screenPoints = new Vector2[tile.Points.Length];
        for (int i = 0; i < tile.Points.Length; i++)
            screenPoints[i] = WorldToSurface(tile.Points[i]);

        if (depthMultiplier > 0 && tile.Elevation > 0)
        {
            float depthPixels = DepthHelper.ComputeDepthPixels(screenPoints, depthMultiplier);
            float liftPixels = depthPixels * tile.Elevation;
            for (int i = 0; i < screenPoints.Length; i++)
                screenPoints[i] = new Vector2(screenPoints[i].X, screenPoints[i].Y - liftPixels);
        }

        return screenPoints;
    }

    public bool DragCam((int X, int Y) mousePos, (int X, int Y)? prevMousePos,
                                 bool mouseDown, bool isDragging, int draggingThreshold = 2)
    {
        if (!mouseDown || prevMousePos == null || !IsPrimary) return false;

        int dx = mousePos.X - prevMousePos.Value.X;
        int dy = mousePos.Y - prevMousePos.Value.Y;

        if (isDragging || Math.Abs(dx) > draggingThreshold || Math.Abs(dy) > draggingThreshold)
        {
            PanDrag((dx, dy));
            return true;
        }

        return false;
    }

    public bool ClickTile((int X, int Y) mousePos, bool mouseReleased, bool wasDragging)
    {
        if (mouseReleased && !wasDragging)
        {
            var tile = GetTileAtScreen(mousePos);
            if (tile != null)
            {
                State.SelectedTile = tile;
                return true;
            }
        }
        return false;
    }

    private void HandleMouse((int X, int Y) mousePos, bool mouseDown, bool mouseReleased)
    {
        var surface = GetSurfaceAt(mousePos.X, mousePos.Y);

        if (mouseDown)
        {
            if (surface != null)
            {
                if (_prevMousePos.HasValue && _prevMouseSurface == surface)
                {
                    if (surface.IsPrimary)
                    {
                        State.Dragging = DragCam(mousePos, _prevMousePos, mouseDown, State.Dragging);
                    }
                    else if (surface.IsMinimap && Minimap != null)
                    {
                        Minimap.HandleDrag(mousePos, mouseDown);
                    }
                }
                else if (_prevMouseSurface == null && surface.IsMinimap && Minimap != null)
                {
                    Minimap.HandleDrag(mousePos, mouseDown);
                }

                _prevMousePos = mousePos;
                _prevMouseSurface = surface;
            }
        }
        else
        {
            if (mouseReleased)
            {
                if (surface == this)
                    ClickTile(mousePos, mouseReleased, State.Dragging);
                State.Dragging = false;
            }
            _prevMousePos = null;
            _prevMouseSurface = null;
        }
    }

    private Viewport? GetSurfaceAt(float x, float y)
    {
        if (Minimap != null && Minimap.View.IsWithin(x, y))
            return Minimap.View;
        if (IsWithin(x, y))
            return this;
        return null;
    }
}
