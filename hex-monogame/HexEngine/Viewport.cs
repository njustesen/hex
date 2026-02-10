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
        float depthMultiplier = EngineConfig.DepthMultiplier;
        Color topColor = EngineConfig.TileTopColor;
        bool drawDepth = depthMultiplier > 0;
        float fogStrength = IsMinimap ? 0f : EngineConfig.FogStrength;

        // Collect all tiles and sort by center Y for correct depth overlap
        var sortedTiles = new System.Collections.Generic.List<Tiles.Tile>(Map.Rows * Map.Cols);
        for (int y = 0; y < Map.Rows; y++)
            for (int x = 0; x < Map.Cols; x++)
                sortedTiles.Add(Map.Tiles[y][x]);
        sortedTiles.Sort((a, b) => a.Pos.Y.CompareTo(b.Pos.Y));

        foreach (var tile in sortedTiles)
            DrawTile(drawer, tile, topColor, depthMultiplier, drawDepth, fogStrength);
    }

    private static Color ApplyFog(Color color, float fogFactor)
    {
        return new Color(
            (int)(color.R * fogFactor),
            (int)(color.G * fogFactor),
            (int)(color.B * fogFactor));
    }

    private static Color ApplyBrightness(Color color, float brightness)
    {
        return new Color(
            (int)(color.R * brightness),
            (int)(color.G * brightness),
            (int)(color.B * brightness));
    }

    private void DrawTile(PrimitiveDrawer drawer, Tile tile, Color topColor,
                          float depthMultiplier, bool drawDepth, float fogStrength)
    {
        var screenPoints = new Vector2[tile.Points.Length];
        for (int i = 0; i < tile.Points.Length; i++)
            screenPoints[i] = WorldToSurface(tile.Points[i]);

        // Compute fog based on screen Y position (top = far = darker)
        float screenCenterY = 0;
        for (int i = 0; i < screenPoints.Length; i++)
            screenCenterY += screenPoints[i].Y;
        screenCenterY /= screenPoints.Length;
        float normScreenY = Math.Clamp(screenCenterY / ScreenHeight, 0f, 1f);
        // fogFactor: 1 at bottom (close, full brightness), (1-fogStrength) at top (far, dimmed)
        float fogFactor = (1f - fogStrength) + fogStrength * normScreenY;

        Color foggedTopColor = ApplyFog(topColor, fogFactor);
        var outlineColor = ApplyFog(new Color(0, 80, 0), fogFactor);

        if (drawDepth)
        {
            float depthPixels = DepthHelper.ComputeDepthPixels(screenPoints, depthMultiplier);
            float liftPixels = depthPixels * tile.Elevation;
            float totalDepth = depthPixels * (tile.Elevation + 1);

            // Raise top surface by elevation
            for (int i = 0; i < screenPoints.Length; i++)
                screenPoints[i] = new Vector2(screenPoints[i].X, screenPoints[i].Y - liftPixels);

            var sideQuads = DepthHelper.ComputeDepthSideQuads(screenPoints, totalDepth);

            foreach (var sq in sideQuads)
            {
                // Smooth lighting: compute edge outward normal and use its X component
                // to interpolate brightness (light from upper-right)
                float edgeDx = sq.Quad[1].X - sq.Quad[0].X;
                float edgeDy = sq.Quad[1].Y - sq.Quad[0].Y;
                float len = MathF.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
                float nx = len > 0 ? edgeDy / len : 0;  // outward normal X (clockwise winding)

                // Map normal X from [-1, 1] to brightness [0.35, 0.85]
                float brightness = 0.35f + 0.5f * (nx + 1f) / 2f;
                Color sideColor = ApplyFog(ApplyBrightness(topColor, brightness), fogFactor);

                drawer.DrawFilledQuad(sq.Quad[0], sq.Quad[1], sq.Quad[2], sq.Quad[3], sideColor);

                drawer.DrawLine(sq.Quad[0], sq.Quad[3], outlineColor);
                drawer.DrawLine(sq.Quad[1], sq.Quad[2], outlineColor);
                drawer.DrawLine(sq.Quad[2], sq.Quad[3], outlineColor);
            }
        }

        // Fill top hex
        Color fillColor = foggedTopColor;
        if (tile == SelectedTile && tile == HoverTile)
            fillColor = ApplyFog(new Color(80, 160, 80), fogFactor);
        else if (tile == SelectedTile)
            fillColor = ApplyFog(new Color(40, 120, 40), fogFactor);
        else if (tile == HoverTile)
            fillColor = ApplyFog(new Color(0, 80, 0), fogFactor);

        if (drawDepth || tile == SelectedTile || tile == HoverTile)
            drawer.DrawFilledPolygon(screenPoints, fillColor);

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
            drawer.DrawFilledPolygon(unitPoints, ApplyFog(new Color(200, 0, 0), fogFactor));
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
        HoverTile = GetTileAtScreen(mousePos);
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
            var tile = GetTileAtScreen(mousePos);
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
