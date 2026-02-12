using System;
using System.IO;
using HexEngine.Maps;
using HexEngine.Editor;

namespace HexEngine.Tests;

public class MapSerializerTests : IDisposable
{
    private readonly string _testDir;

    public MapSerializerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "hex_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void RoundTrip_HexMap_PreservesSize()
    {
        var map = new HexGridMap(16, 12, 100f, 0.7f, "flat");
        var path = Path.Combine(_testDir, "test_hex.json");

        MapSerializer.Save(map, path);
        var loaded = MapSerializer.Load(path);

        Assert.IsType<HexGridMap>(loaded);
        Assert.Equal(16, loaded.Cols);
        Assert.Equal(12, loaded.Rows);
    }

    [Fact]
    public void RoundTrip_HexMap_PreservesElevation()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        map.Tiles[3][4].Elevation = 2;
        map.Tiles[5][7].Elevation = 5;

        var path = Path.Combine(_testDir, "test_elev.json");
        MapSerializer.Save(map, path);
        var loaded = MapSerializer.Load(path);

        Assert.Equal(2, loaded.Tiles[3][4].Elevation);
        Assert.Equal(5, loaded.Tiles[5][7].Elevation);
        Assert.Equal(0, loaded.Tiles[0][0].Elevation);
    }

    [Fact]
    public void RoundTrip_SquareMap()
    {
        var map = new TileGridMap(8, 8, 80f, 80f);
        map.Tiles[2][3].Elevation = 1;

        var path = Path.Combine(_testDir, "test_square.json");
        MapSerializer.Save(map, path);
        var loaded = MapSerializer.Load(path);

        Assert.IsType<TileGridMap>(loaded);
        Assert.Equal(8, loaded.Cols);
        Assert.Equal(8, loaded.Rows);
        Assert.Equal(1, loaded.Tiles[2][3].Elevation);
    }

    [Fact]
    public void RoundTrip_IsometricMap()
    {
        var map = new IsometricTileGridMap(12, 10, 60f, 40f);
        map.Tiles[1][1].Elevation = 3;

        var path = Path.Combine(_testDir, "test_iso.json");
        MapSerializer.Save(map, path);
        var loaded = MapSerializer.Load(path);

        Assert.IsType<IsometricTileGridMap>(loaded);
        Assert.Equal(12, loaded.Cols);
        Assert.Equal(10, loaded.Rows);
        Assert.Equal(3, loaded.Tiles[1][1].Elevation);
    }

    [Fact]
    public void Save_OnlyStoresNonDefaultTiles()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        // All tiles at elevation 0, no ramps -> should be minimal
        var path = Path.Combine(_testDir, "test_minimal.json");
        MapSerializer.Save(map, path);

        string json = File.ReadAllText(path);
        // tiles array should be empty
        Assert.Contains("\"tiles\": []", json);
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var map = new HexGridMap(5, 5, 100f, 0.7f, "flat");
        var subDir = Path.Combine(_testDir, "subdir");
        var path = Path.Combine(subDir, "test.json");

        MapSerializer.Save(map, path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void RoundTrip_HexMapParams_Preserved()
    {
        var map = new HexGridMap(10, 10, 80f, 0.5f, "flat");
        var path = Path.Combine(_testDir, "test_params.json");
        MapSerializer.Save(map, path);
        var loaded = MapSerializer.Load(path) as HexGridMap;

        Assert.NotNull(loaded);
        Assert.Equal(80f, loaded!.HexRadius);
        Assert.Equal(0.5f, loaded.HexVerticalScale);
        Assert.Equal("flat", loaded.HexOrientation);
    }

    [Fact]
    public void RoundTrip_Ramps_Preserved()
    {
        var map = new HexGridMap(10, 10, 100f, 0.7f, "flat");
        map.Tiles[3][4].Elevation = 2;
        map.Tiles[3][5].Elevation = 0;
        map.AddRamp(map.Tiles[3][4], 0); // edge 0 from elevated tile to neighbor

        var path = Path.Combine(_testDir, "test_ramps.json");
        MapSerializer.Save(map, path);
        var loaded = MapSerializer.Load(path);

        Assert.Contains(0, loaded.Tiles[3][4].Ramps);
        var neighbor = loaded.GetNeighbor(loaded.Tiles[3][4], 0)!;
        int opposite = loaded.GetOppositeEdge(0);
        Assert.Contains(opposite, neighbor.Ramps);
    }
}
