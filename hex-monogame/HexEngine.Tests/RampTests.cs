using Microsoft.Xna.Framework;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine.Tests;

public class RampTests
{
    [Fact]
    public void AddRamp_DifferentElevations_AddsBothSides()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[2][2];
        tile.Elevation = 1;
        var neighbor = map.GetNeighbor(tile, 0)!;

        bool result = map.AddRamp(tile, 0);

        Assert.True(result);
        Assert.Contains(0, tile.Ramps);
        int opposite = map.GetOppositeEdge(0);
        Assert.Contains(opposite, neighbor.Ramps);
    }

    [Fact]
    public void AddRamp_SameElevation_Rejected()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[2][2];

        bool result = map.AddRamp(tile, 0);

        Assert.False(result);
        Assert.Empty(tile.Ramps);
    }

    [Fact]
    public void AddRamp_NoNeighbor_Rejected()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var corner = map.Tiles[0][0];
        corner.Elevation = 1;

        bool result = map.AddRamp(corner, 4);

        Assert.False(result);
        Assert.Empty(corner.Ramps);
    }

    [Fact]
    public void RemoveRamp_RemovesBothSides()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[2][2];
        tile.Elevation = 1;
        var neighbor = map.GetNeighbor(tile, 0)!;

        map.AddRamp(tile, 0);
        bool result = map.RemoveRamp(tile, 0);

        Assert.True(result);
        Assert.Empty(tile.Ramps);
        Assert.Empty(neighbor.Ramps);
    }

    [Fact]
    public void RemoveRamp_NotPresent_ReturnsFalse()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[2][2];

        bool result = map.RemoveRamp(tile, 0);

        Assert.False(result);
    }

    [Fact]
    public void AddRamp_SquareGrid_Works()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        var tile = map.Tiles[3][3];
        tile.Elevation = 2;
        var neighbor = map.GetNeighbor(tile, 1)!;

        bool result = map.AddRamp(tile, 1);

        Assert.True(result);
        Assert.Contains(1, tile.Ramps);
        int opposite = map.GetOppositeEdge(1);
        Assert.Contains(opposite, neighbor.Ramps);
    }
}

public class NeighborTests
{
    [Fact]
    public void HexGridMap_GetNeighbor_EvenCol_Edge0_SE()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[5][4]; // even col
        var neighbor = map.GetNeighbor(tile, 0);
        Assert.NotNull(neighbor);
        Assert.Equal(5, neighbor!.X);
        Assert.Equal(5, neighbor.Y);
    }

    [Fact]
    public void HexGridMap_GetNeighbor_OddCol_Edge0_SE()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[5][3]; // odd col
        var neighbor = map.GetNeighbor(tile, 0);
        Assert.NotNull(neighbor);
        Assert.Equal(4, neighbor!.X);
        Assert.Equal(6, neighbor.Y);
    }

    [Fact]
    public void HexGridMap_GetNeighbor_Edge1_South()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[5][4];
        var neighbor = map.GetNeighbor(tile, 1);
        Assert.NotNull(neighbor);
        Assert.Equal(4, neighbor!.X);
        Assert.Equal(6, neighbor.Y);
    }

    [Fact]
    public void HexGridMap_GetNeighbor_Edge4_North()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[5][4];
        var neighbor = map.GetNeighbor(tile, 4);
        Assert.NotNull(neighbor);
        Assert.Equal(4, neighbor!.X);
        Assert.Equal(4, neighbor.Y);
    }

    [Fact]
    public void HexGridMap_GetNeighbor_OutOfBounds_ReturnsNull()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[0][0]; // corner tile
        var neighbor = map.GetNeighbor(tile, 3); // NW - should be out of bounds
        Assert.Null(neighbor);
    }

    [Fact]
    public void HexGridMap_GetOppositeEdge_Roundtrip()
    {
        var map = new HexGridMap(5, 5, 100f, 0.7f, "flat");
        for (int e = 0; e < 6; e++)
        {
            int opposite = map.GetOppositeEdge(e);
            int backAgain = map.GetOppositeEdge(opposite);
            Assert.Equal(e, backAgain);
        }
    }

    [Fact]
    public void HexGridMap_GetNeighbor_Symmetric()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        var tile = map.Tiles[5][4];
        for (int e = 0; e < 6; e++)
        {
            var neighbor = map.GetNeighbor(tile, e);
            if (neighbor != null)
            {
                int opposite = map.GetOppositeEdge(e);
                var backTile = map.GetNeighbor(neighbor, opposite);
                Assert.NotNull(backTile);
                Assert.Same(tile, backTile);
            }
        }
    }

    [Fact]
    public void TileGridMap_GetNeighbor_AllDirections()
    {
        var map = new TileGridMap(10, 10, 80f, 80f);
        var tile = map.Tiles[5][5];
        // N=0, E=1, S=2, W=3
        var n = map.GetNeighbor(tile, 0);
        var e = map.GetNeighbor(tile, 1);
        var s = map.GetNeighbor(tile, 2);
        var w = map.GetNeighbor(tile, 3);

        Assert.Equal(5, n!.X); Assert.Equal(4, n.Y);
        Assert.Equal(6, e!.X); Assert.Equal(5, e.Y);
        Assert.Equal(5, s!.X); Assert.Equal(6, s.Y);
        Assert.Equal(4, w!.X); Assert.Equal(5, w.Y);
    }

    [Fact]
    public void TileGridMap_EdgeCount()
    {
        var map = new TileGridMap(5, 5, 80f, 80f);
        Assert.Equal(4, map.EdgeCount);
    }

    [Fact]
    public void HexGridMap_EdgeCount()
    {
        var map = new HexGridMap(5, 5, 100f, 0.7f, "flat");
        Assert.Equal(6, map.EdgeCount);
    }
}
