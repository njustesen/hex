using Microsoft.Xna.Framework;
using HexEngine.Maps;

namespace HexEngine.Tests;

public class ViewportTests
{
    public ViewportTests()
    {
        EngineConfig.PerspectiveFactor = 0f;
    }

    private GridMap CreateTestMap()
    {
        return new TileGridMap(10, 10, 80f, 80f);
    }

    [Fact]
    public void Viewport_WorldToSurface_CenterMapsToCenter()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(0, 0, 800, 600, 1f, map, isPrimary: true);

        var center = map.Center;
        var surfacePos = viewport.WorldToSurface(center);

        // Center of map should map approximately to center of screen
        Assert.Equal(400f, surfacePos.X, 1.0);
        Assert.Equal(300f, surfacePos.Y, 1.0);
    }

    [Fact]
    public void Viewport_SurfaceToWorld_RoundTrip()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(0, 0, 800, 600, 1f, map, isPrimary: true);

        var originalWorld = map.Center;
        var surface = viewport.WorldToSurface(originalWorld);
        // For surface_to_world, position includes screen offset
        var backToWorld = viewport.SurfaceToWorld(surface);

        Assert.Equal(originalWorld.X, backToWorld.X, 1.0);
        Assert.Equal(originalWorld.Y, backToWorld.Y, 1.0);
    }

    [Fact]
    public void Viewport_SurfaceToWorld_TopLeft()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(0, 0, 800, 600, 1f, map, isPrimary: true);

        // Surface position (0,0) should map to camera's top-left
        var worldPos = viewport.SurfaceToWorld(new Vector2(0, 0));
        Assert.Equal(viewport.Cam.X1, worldPos.X, 1.0);
        Assert.Equal(viewport.Cam.Y1, worldPos.Y, 1.0);
    }

    [Fact]
    public void Viewport_SurfaceToWorld_BottomRight()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(0, 0, 800, 600, 1f, map, isPrimary: true);

        // Surface position (screenWidth, screenHeight) should map to camera's bottom-right
        var worldPos = viewport.SurfaceToWorld(new Vector2(800, 600));
        Assert.Equal(viewport.Cam.X2, worldPos.X, 1.0);
        Assert.Equal(viewport.Cam.Y2, worldPos.Y, 1.0);
    }

    [Fact]
    public void Viewport_IsWithin_PointInside()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(100, 50, 800, 600, 1f, map, isPrimary: true);

        Assert.True(viewport.IsWithin(400, 300));
        Assert.True(viewport.IsWithin(100, 50));
        Assert.True(viewport.IsWithin(900, 650));
    }

    [Fact]
    public void Viewport_IsWithin_PointOutside()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(100, 50, 800, 600, 1f, map, isPrimary: true);

        Assert.False(viewport.IsWithin(50, 300));
        Assert.False(viewport.IsWithin(400, 700));
    }

    [Fact]
    public void Viewport_MoveCam_ChangesCenter()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(0, 0, 800, 600, 1f, map, isPrimary: true);

        var originalCenter = viewport.Cam.Center;
        viewport.MoveCam(50f, 30f);

        // Center should have moved (may be clamped by bounds)
        // For a large enough map, the move should succeed
        Assert.NotEqual(originalCenter, viewport.Cam.Center);
    }

    [Fact]
    public void InteractionState_HoverTile_IsNullByDefault()
    {
        var state = new InteractionState();
        Assert.Null(state.HoverTile);
    }

    [Fact]
    public void InteractionState_SelectedTile_IsNullByDefault()
    {
        var state = new InteractionState();
        Assert.Null(state.SelectedTile);
    }

    [Fact]
    public void Viewport_WithHexMap_WorldToSurfaceRoundTrip()
    {
        var map = new HexGridMap(21, 11, 100f, 0.7f);
        var viewport = new Viewport(0, 0, 1200, 600, 1f, map, isPrimary: true);

        var tile = map.Tiles[5][10];
        var surface = viewport.WorldToSurface(tile.Pos);
        var backToWorld = viewport.SurfaceToWorld(surface);

        Assert.Equal(tile.Pos.X, backToWorld.X, 1.0);
        Assert.Equal(tile.Pos.Y, backToWorld.Y, 1.0);
    }

    [Fact]
    public void Viewport_Scale_IsPositive()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(0, 0, 800, 600, 1f, map, isPrimary: true);
        Assert.True(viewport.Scale > 0);
    }

    [Fact]
    public void Viewport_MinimapFlag()
    {
        var map = CreateTestMap();
        var viewport = new Viewport(0, 0, 800, 600, 1f, map, isPrimary: true);
        var minimap = new Minimap(600, 400, 200, 150, 1f, map, primaryViewport: viewport);

        Assert.True(viewport.IsPrimary);
        Assert.False(viewport.IsMinimap);
        Assert.True(minimap.IsMinimap);
        Assert.False(minimap.IsPrimary);
    }
}
