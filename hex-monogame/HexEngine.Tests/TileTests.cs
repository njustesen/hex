using Microsoft.Xna.Framework;
using HexEngine.Tiles;

namespace HexEngine.Tests;

public class TileTests
{
    [Fact]
    public void Hexagon_PointyOrientation_Has6Points()
    {
        var hex = new Hexagon(new Vector2(100, 100), 0, 0, 50f, 1f, 100f, 100f, "pointy");
        Assert.Equal(6, hex.Points.Length);
    }

    [Fact]
    public void Hexagon_FlatOrientation_Has6Points()
    {
        var hex = new Hexagon(new Vector2(100, 100), 0, 0, 50f, 1f, 100f, 100f, "flat");
        Assert.Equal(6, hex.Points.Length);
    }

    [Fact]
    public void Hexagon_FlatOrientation_FirstPointIsAtRight()
    {
        var hex = new Hexagon(new Vector2(0, 0), 0, 0, 100f, 1f, 200f, 200f, "flat");
        // Flat orientation: first angle = 0 degrees, so first point at (radius, 0) = (100, 0)
        Assert.Equal(100f, hex.Points[0].X, 0.01);
        Assert.Equal(0f, hex.Points[0].Y, 0.01);
    }

    [Fact]
    public void Hexagon_PointyOrientation_FirstPointIsAt30Degrees()
    {
        var hex = new Hexagon(new Vector2(0, 0), 0, 0, 100f, 1f, 200f, 200f, "pointy");
        // Pointy orientation: first angle = 30 degrees
        float expectedX = 100f * MathF.Cos(MathHelper.ToRadians(30f));
        float expectedY = 100f * MathF.Sin(MathHelper.ToRadians(30f));
        Assert.Equal(expectedX, hex.Points[0].X, 0.01);
        Assert.Equal(expectedY, hex.Points[0].Y, 0.01);
    }

    [Fact]
    public void Hexagon_VerticalScale_AffectsYCoordinates()
    {
        var hex1 = new Hexagon(new Vector2(0, 0), 0, 0, 100f, 1f, 200f, 200f, "flat");
        var hex07 = new Hexagon(new Vector2(0, 0), 0, 0, 100f, 0.7f, 200f, 200f, "flat");

        // At 60 degrees, Y should be scaled by vertical_scale
        // hex1 point[1] = (cos(60)*100, sin(60)*100)
        // hex07 point[1] = (cos(60)*100, 0.7*sin(60)*100)
        Assert.Equal(hex1.Points[1].X, hex07.Points[1].X, 0.01);
        Assert.Equal(hex1.Points[1].Y * 0.7f, hex07.Points[1].Y, 0.01);
    }

    [Fact]
    public void SquareTile_Has5Points()
    {
        var tile = new SquareTile(new Vector2(50, 50), 0, 0, 100f, 80f);
        Assert.Equal(5, tile.Points.Length);
    }

    [Fact]
    public void SquareTile_PointsFormClosedRectangle()
    {
        var tile = new SquareTile(new Vector2(50, 50), 0, 0, 100f, 80f);
        // First and last point should be the same (closed polygon)
        Assert.Equal(tile.Points[0].X, tile.Points[4].X, 0.01);
        Assert.Equal(tile.Points[0].Y, tile.Points[4].Y, 0.01);

        // Top-left
        Assert.Equal(0f, tile.Points[0].X, 0.01);
        Assert.Equal(10f, tile.Points[0].Y, 0.01);
        // Top-right
        Assert.Equal(100f, tile.Points[1].X, 0.01);
        Assert.Equal(10f, tile.Points[1].Y, 0.01);
        // Bottom-right
        Assert.Equal(100f, tile.Points[2].X, 0.01);
        Assert.Equal(90f, tile.Points[2].Y, 0.01);
        // Bottom-left
        Assert.Equal(0f, tile.Points[3].X, 0.01);
        Assert.Equal(90f, tile.Points[3].Y, 0.01);
    }

    [Fact]
    public void IsometricTile_Has5Points()
    {
        var tile = new IsometricTile(new Vector2(100, 100), 0, 0, 60f, 40f);
        Assert.Equal(5, tile.Points.Length);
    }

    [Fact]
    public void IsometricTile_PointsFormDiamond()
    {
        var tile = new IsometricTile(new Vector2(100, 100), 0, 0, 60f, 40f);
        // First and last point should be the same (closed polygon)
        Assert.Equal(tile.Points[0].X, tile.Points[4].X, 0.01);
        Assert.Equal(tile.Points[0].Y, tile.Points[4].Y, 0.01);

        // Top: (100, 80)
        Assert.Equal(100f, tile.Points[0].X, 0.01);
        Assert.Equal(80f, tile.Points[0].Y, 0.01);
        // Right: (130, 100)
        Assert.Equal(130f, tile.Points[1].X, 0.01);
        Assert.Equal(100f, tile.Points[1].Y, 0.01);
        // Bottom: (100, 120)
        Assert.Equal(100f, tile.Points[2].X, 0.01);
        Assert.Equal(120f, tile.Points[2].Y, 0.01);
        // Left: (70, 100)
        Assert.Equal(70f, tile.Points[3].X, 0.01);
        Assert.Equal(100f, tile.Points[3].Y, 0.01);
    }

    [Fact]
    public void Tile_UnitIsNullByDefault()
    {
        var tile = new Tile(new Vector2(0, 0), 0, 0, 100f, 100f);
        Assert.Null(tile.Unit);
    }

    [Fact]
    public void Tile_CanAssignUnit()
    {
        var tile = new Tile(new Vector2(0, 0), 0, 0, 100f, 100f);
        var unit = new Unit();
        tile.Unit = unit;
        Assert.NotNull(tile.Unit);
        Assert.Equal("Brian", tile.Unit.Name);
    }
}
