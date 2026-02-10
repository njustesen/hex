using Microsoft.Xna.Framework;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine.Tests;

public class MapTests
{
    [Fact]
    public void HexGridMap_FlatOrientation_CorrectTileCount()
    {
        var map = new HexGridMap(21, 11, 100f, 0.7f, "flat");
        Assert.Equal(11, map.Tiles.Length);      // rows
        Assert.Equal(21, map.Tiles[0].Length);   // cols
    }

    [Fact]
    public void HexGridMap_PointyOrientation_CorrectTileCount()
    {
        var map = new HexGridMap(10, 8, 50f, 1f, "pointy");
        Assert.Equal(8, map.Tiles.Length);
        Assert.Equal(10, map.Tiles[0].Length);
    }

    [Fact]
    public void HexGridMap_FirstTileAtOrigin()
    {
        var map = new HexGridMap(5, 5, 100f, 1f, "flat");
        Assert.Equal(0f, map.Tiles[0][0].Pos.X, 0.01);
        Assert.Equal(0f, map.Tiles[0][0].Pos.Y, 0.01);
    }

    [Fact]
    public void HexGridMap_FlatOrientation_OddColumnsOffset()
    {
        var map = new HexGridMap(5, 5, 100f, 1f, "flat");
        // In flat orientation, odd columns are offset by half the vertical spacing
        float verticalSpacing = MathF.Sqrt(3f) * 100f; // vertical_scale=1
        Assert.Equal(0f, map.Tiles[0][0].Pos.Y, 0.01);
        Assert.Equal(verticalSpacing / 2f, map.Tiles[0][1].Pos.Y, 0.01);
    }

    [Fact]
    public void HexGridMap_PointyOrientation_OddRowsOffset()
    {
        var map = new HexGridMap(5, 5, 100f, 1f, "pointy");
        float horizontalSpacing = MathF.Sqrt(3f) * 100f; // vertical_scale=1
        Assert.Equal(0f, map.Tiles[0][0].Pos.X, 0.01);
        // Row 1 (odd) should have offset
        Assert.Equal(horizontalSpacing / 2f, map.Tiles[1][0].Pos.X, 0.01);
    }

    [Fact]
    public void HexGridMap_WidthAndHeight_Positive()
    {
        var map = new HexGridMap(21, 11, 100f, 0.7f, "flat");
        Assert.True(map.Width > 0);
        Assert.True(map.Height > 0);
    }

    [Fact]
    public void HexGridMap_GetNearestTile_ReturnsOriginTile()
    {
        var map = new HexGridMap(5, 5, 100f, 1f, "flat");
        var nearest = map.GetNearestTile(new Vector2(0f, 0f));
        Assert.Equal(0, nearest.X);
        Assert.Equal(0, nearest.Y);
    }

    [Fact]
    public void HexGridMap_GetNearestTile_ReturnsTileAtPosition()
    {
        var map = new HexGridMap(21, 11, 100f, 0.7f, "flat");
        var tile55 = map.Tiles[5][5];
        var nearest = map.GetNearestTile(tile55.Pos);
        Assert.Equal(5, nearest.X);
        Assert.Equal(5, nearest.Y);
    }

    [Fact]
    public void TileGridMap_CorrectTileCount()
    {
        var map = new TileGridMap(30, 30, 80f, 80f);
        Assert.Equal(30, map.Tiles.Length);
        Assert.Equal(30, map.Tiles[0].Length);
    }

    [Fact]
    public void TileGridMap_GetNearestTile_IndexBased()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        var nearest = map.GetNearestTile(new Vector2(160f, 160f));
        Assert.Equal(2, nearest.X);
        Assert.Equal(2, nearest.Y);
    }

    [Fact]
    public void TileGridMap_GetNearestTile_ClampsToEdge()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        // Very large position should clamp
        var nearest = map.GetNearestTile(new Vector2(10000f, 10000f));
        Assert.Equal(9, nearest.X);
        Assert.Equal(9, nearest.Y);
    }

    [Fact]
    public void TileGridMap_GetNearestTile_ClampsNegative()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        var nearest = map.GetNearestTile(new Vector2(-10000f, -10000f));
        Assert.Equal(0, nearest.X);
        Assert.Equal(0, nearest.Y);
    }

    [Fact]
    public void TileGridMap_WidthAndHeight()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        Assert.Equal(80f * 11f, map.Width, 0.01);
        Assert.Equal(80f * 11f, map.Height, 0.01);
    }

    [Fact]
    public void IsometricTileGridMap_CorrectTileCount()
    {
        var map = new IsometricTileGridMap(20, 20, 60f, 40f);
        Assert.Equal(20, map.Tiles.Length);
        Assert.Equal(20, map.Tiles[0].Length);
    }

    [Fact]
    public void IsometricTileGridMap_GetNearestTile_ReturnsCorrectTile()
    {
        var map = new IsometricTileGridMap(10, 10, 60f, 40f);
        var tile33 = map.Tiles[3][3];
        var nearest = map.GetNearestTile(tile33.Pos);
        Assert.Equal(3, nearest.X);
        Assert.Equal(3, nearest.Y);
    }

    [Fact]
    public void IsometricTileGridMap_WidthAndHeight()
    {
        var map = new IsometricTileGridMap(20, 20, 60f, 40f);
        Assert.Equal(60f * 22f, map.Width, 0.01);
        Assert.Equal(40f * 22f, map.Height, 0.01);
    }

    [Fact]
    public void GridMap_Center_IsCorrect()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        var center = map.Center;
        Assert.Equal(map.X1 + map.Width / 2f, center.X, 0.01);
        Assert.Equal(map.Y1 + map.Height / 2f, center.Y, 0.01);
    }

    [Fact]
    public void GridMap_X2Y2_AreCorrect()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        Assert.Equal(map.X1 + map.Width, map.X2, 0.01);
        Assert.Equal(map.Y1 + map.Height, map.Y2, 0.01);
    }

    [Fact]
    public void HexGridMap_AllTilesAreHexagons()
    {
        var map = new HexGridMap(5, 5, 100f, 1f, "flat");
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
                Assert.IsType<Hexagon>(map.Tiles[y][x]);
    }

    [Fact]
    public void TileGridMap_AllTilesAreSquareTiles()
    {
        var map = new TileGridMap(5, 5, 80f, 80f);
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
                Assert.IsType<SquareTile>(map.Tiles[y][x]);
    }

    [Fact]
    public void IsometricTileGridMap_AllTilesAreIsometricTiles()
    {
        var map = new IsometricTileGridMap(5, 5, 60f, 40f);
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
                Assert.IsType<IsometricTile>(map.Tiles[y][x]);
    }
}
