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
    public GridMap Map { get; }
    public Viewport? Minimap { get; set; }
    public bool IsPrimary { get; }
    public bool IsMinimap { get; }
    public Viewport? PrimaryViewport { get; set; }
    public bool CanZoom { get; }
    public bool CanMove { get; }
    public Camera Cam { get; }
    public float ZoomLevel { get; set; }
    public bool Moved { get; set; }
    public Tile? HoverTile { get; set; }
    public Tile? SelectedTile { get; set; }
    public bool Dragging { get; set; }

    public float Upp { get; }
    public float Ppu { get; }

    private (int X, int Y)? _prevMousePos;
    private Viewport? _prevMouseSurface;
    private RenderTarget2D? _renderTarget;

    public Viewport(float screenX1, float screenY1, float screenWidth, float screenHeight,
                    float zoomLevel, GridMap map, Viewport? minimap = null,
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
        Minimap = minimap;
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
        return new Vector2(norm.X * ScreenWidth, norm.Y * ScreenHeight);
    }

    public Vector2 SurfaceToWorld(Vector2 surfacePosition)
    {
        float normX = (surfacePosition.X - ScreenX1) / ScreenWidth;
        float normY = (surfacePosition.Y - ScreenY1) / ScreenHeight;
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

    public void Draw(GraphicsDevice graphicsDevice, PrimitiveDrawer drawer, int? grid = null)
    {
        if (_renderTarget == null) return;

        graphicsDevice.SetRenderTarget(_renderTarget);
        graphicsDevice.Clear(BackColor);

        drawer.UpdateProjection((int)ScreenWidth, (int)ScreenHeight);

        if (grid.HasValue)
            DrawGrid(drawer, grid.Value);

        DrawMap(drawer);

        graphicsDevice.SetRenderTarget(null);
    }

    protected virtual void DrawMap(PrimitiveDrawer drawer)
    {
        var outlineColor = new Color(0, 80, 0);

        for (int y = 0; y < Map.Rows; y++)
        {
            for (int x = 0; x < Map.Cols; x++)
            {
                var tile = Map.Tiles[y][x];
                var screenPoints = new Vector2[tile.Points.Length];
                for (int i = 0; i < tile.Points.Length; i++)
                    screenPoints[i] = WorldToSurface(tile.Points[i]);

                // Fill colors for hover/select
                if (tile == SelectedTile && tile == HoverTile)
                    drawer.DrawFilledPolygon(screenPoints, new Color(80, 160, 80));
                else if (tile == SelectedTile)
                    drawer.DrawFilledPolygon(screenPoints, new Color(40, 120, 40));
                else if (tile == HoverTile)
                    drawer.DrawFilledPolygon(screenPoints, new Color(0, 80, 0));

                // Outline
                drawer.DrawPolygonOutline(screenPoints, outlineColor);

                // Unit
                if (tile.Unit != null)
                {
                    float unitSize = 50f;
                    var unitPoints = new Vector2[]
                    {
                        WorldToSurface(new Vector2(tile.Pos.X - unitSize / 2, tile.Pos.Y - unitSize / 2)),
                        WorldToSurface(new Vector2(tile.Pos.X + unitSize / 2, tile.Pos.Y - unitSize / 2)),
                        WorldToSurface(new Vector2(tile.Pos.X + unitSize / 2, tile.Pos.Y + unitSize / 2)),
                        WorldToSurface(new Vector2(tile.Pos.X - unitSize / 2, tile.Pos.Y + unitSize / 2)),
                    };
                    drawer.DrawFilledPolygon(unitPoints, new Color(200, 0, 0));
                }
            }
        }
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

    public void Update(float seconds, InputManager? inputManager = null)
    {
        Cam.Update(seconds);

        if (inputManager == null) return;

        // Keyboard movement
        if (inputManager.DirectionX != 0 || inputManager.DirectionY != 0)
            HandleKeyboardMovement(inputManager.DirectionX, inputManager.DirectionY, seconds, EngineConfig.MoveSpeed);

        // Zoom
        if (inputManager.ZoomDirection != 0)
            HandleZoom(inputManager.ZoomDirection, seconds, EngineConfig.ZoomSpeed);

        // Mouse interactions
        HandleMouseInteractions(inputManager.MousePos, inputManager.MouseDown, inputManager.MouseReleased);

        // Update hover tile
        if (IsWithin(inputManager.MousePos.X, inputManager.MousePos.Y))
            UpdateHoverTile(inputManager.MousePos);

        // Update minimap
        if (Minimap != null)
            Minimap.Update(seconds);
    }

    public void HandleKeyboardMovement(int dirX, int dirY, float seconds, float moveSpeed)
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
        var worldPos = SurfaceToWorld(new Vector2(mousePos.X, mousePos.Y));
        HoverTile = Map.GetNearestTile(worldPos);
    }

    public bool HandleMouseDrag((int X, int Y) mousePos, (int X, int Y)? prevMousePos,
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

    public bool HandleMouseClick((int X, int Y) mousePos, bool mouseReleased, bool wasDragging)
    {
        if (mouseReleased && !wasDragging)
        {
            var worldPos = SurfaceToWorld(new Vector2(mousePos.X, mousePos.Y));
            var tile = Map.GetNearestTile(worldPos);
            if (tile != null)
            {
                SelectedTile = tile;
                return true;
            }
        }
        return false;
    }

    private void HandleMouseInteractions((int X, int Y) mousePos, bool mouseDown, bool mouseReleased)
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
                        Dragging = HandleMouseDrag(mousePos, _prevMousePos, mouseDown, Dragging);
                    }
                    else if (surface.IsMinimap && Minimap != null)
                    {
                        (Minimap as Minimap)?.HandleMinimapDrag(mousePos, mouseDown);
                    }
                }
                else if (_prevMouseSurface == null && surface.IsMinimap && Minimap != null)
                {
                    (Minimap as Minimap)?.HandleMinimapDrag(mousePos, mouseDown);
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
                    HandleMouseClick(mousePos, mouseReleased, Dragging);
                Dragging = false;
            }
            _prevMousePos = null;
            _prevMouseSurface = null;
        }
    }

    private Viewport? GetSurfaceAt(float x, float y)
    {
        if (Minimap != null && Minimap.IsWithin(x, y))
            return Minimap;
        if (IsWithin(x, y))
            return this;
        return null;
    }
}
