using HexEngine.Core;
using HexEngine.Maps;

namespace HexEngine.Tests;

public class PathfindingTests
{
    static PathfindingTests()
    {
        var yamlPath = FindUnitsYaml();
        UnitDefs.Load(yamlPath);
    }

    private static string FindUnitsYaml()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "units.yaml");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new FileNotFoundException("Could not find units.yaml");
    }

    private static HexGridMap CreateFlatMap(int cols = 10, int rows = 10)
        => new HexGridMap(cols, rows, 100f, 0.7f, "flat");

    [Fact]
    public void FlatMap_ReachableTiles_Marine()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Marine"); // 2 MP

        var reachable = Pathfinding.GetReachableTiles(map, start, 2, false);

        Assert.NotEmpty(reachable);
        // All reachable tiles should have cost <= 2
        foreach (var kvp in reachable)
            Assert.True(kvp.Value <= 2);
        // Start tile should not be in result
        Assert.DoesNotContain(start, reachable.Keys);
    }

    [Fact]
    public void FlatMap_ReachableTiles_Tank_MoreRange()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Tank"); // 3 MP

        var reachableTank = Pathfinding.GetReachableTiles(map, start, 3, false);
        var reachableMarine = Pathfinding.GetReachableTiles(map, start, 2, false);

        Assert.True(reachableTank.Count > reachableMarine.Count);
    }

    [Fact]
    public void ElevationBlocks_GroundUnit()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Marine");

        // Raise all neighbors to elevation 2 (no ramps)
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var neighbor = map.GetNeighbor(start, e);
            if (neighbor != null) neighbor.Elevation = 2;
        }

        var reachable = Pathfinding.GetReachableTiles(map, start, 2, false);
        Assert.Empty(reachable);
    }

    [Fact]
    public void RampAllows_GroundUnit()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Marine");

        // Raise neighbor at edge 0 and add a ramp
        var neighbor = map.GetNeighbor(start, 0)!;
        neighbor.Elevation = 1;
        map.AddRamp(start, 0);

        var reachable = Pathfinding.GetReachableTiles(map, start, 2, false);
        Assert.Contains(neighbor, reachable.Keys);
    }

    [Fact]
    public void FighterIgnores_Elevation()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Fighter");

        // Raise all neighbors (no ramps)
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var n = map.GetNeighbor(start, e);
            if (n != null) n.Elevation = 3;
        }

        var reachable = Pathfinding.GetReachableTiles(map, start, 4, true);
        Assert.NotEmpty(reachable);
    }

    [Fact]
    public void OccupiedTile_ExcludedFromReachable()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Marine");

        // Place another unit on a neighbor
        var neighbor = map.GetNeighbor(start, 0)!;
        neighbor.Unit = new Unit("Tank");

        var reachable = Pathfinding.GetReachableTiles(map, start, 2, false);
        Assert.DoesNotContain(neighbor, reachable.Keys);
    }

    [Fact]
    public void OccupiedTile_Passable_ForTraversal()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Tank"); // 3 MP

        // Place a friendly unit on neighbor at edge 0
        var neighbor = map.GetNeighbor(start, 0)!;
        neighbor.Unit = new Unit("Marine");

        // There should be tiles reachable beyond the occupied neighbor
        var reachable = Pathfinding.GetReachableTiles(map, start, 3, false);
        // The occupied tile itself shouldn't be reachable
        Assert.DoesNotContain(neighbor, reachable.Keys);
        // But tiles beyond it should be (at least some with cost 2+)
        var beyond = map.GetNeighbor(neighbor, 0);
        if (beyond != null && beyond.Unit == null)
            Assert.Contains(beyond, reachable.Keys);
    }

    [Fact]
    public void FindPath_FlatMap_ReturnsPath()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        var goal = map.GetNeighbor(start, 0)!;

        var path = Pathfinding.FindPath(map, start, goal, false);
        Assert.NotNull(path);
        Assert.Equal(start, path![0]);
        Assert.Equal(goal, path[^1]);
    }

    [Fact]
    public void FindPath_NoPath_ReturnsNull()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];

        // Surround start with elevated tiles (no ramps)
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var n = map.GetNeighbor(start, e);
            if (n != null) n.Elevation = 2;
        }

        var goal = map.Tiles[0][0]; // far away, unreachable
        var path = Pathfinding.FindPath(map, start, goal, false);
        Assert.Null(path);
    }

    [Fact]
    public void FindPath_OccupiedGoal_ReturnsNull()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        var goal = map.GetNeighbor(start, 0)!;
        goal.Unit = new Unit("Marine");

        var path = Pathfinding.FindPath(map, start, goal, false);
        Assert.Null(path);
    }

    [Fact]
    public void CanTraverse_SameElevation_True()
    {
        var map = CreateFlatMap();
        var a = map.Tiles[5][5];
        var b = map.GetNeighbor(a, 0)!;
        Assert.True(Pathfinding.CanTraverse(a, b, 0, map, false));
    }

    [Fact]
    public void CanTraverse_DifferentElevation_NoRamp_False()
    {
        var map = CreateFlatMap();
        var a = map.Tiles[5][5];
        var b = map.GetNeighbor(a, 0)!;
        b.Elevation = 2;
        Assert.False(Pathfinding.CanTraverse(a, b, 0, map, false));
    }

    [Fact]
    public void CanTraverse_DifferentElevation_WithRamp_True()
    {
        var map = CreateFlatMap();
        var a = map.Tiles[5][5];
        var b = map.GetNeighbor(a, 0)!;
        b.Elevation = 1;
        map.AddRamp(a, 0);
        Assert.True(Pathfinding.CanTraverse(a, b, 0, map, false));
    }

    [Fact]
    public void CanTraverse_Fighter_AlwaysTrue()
    {
        var map = CreateFlatMap();
        var a = map.Tiles[5][5];
        var b = map.GetNeighbor(a, 0)!;
        b.Elevation = 5;
        Assert.True(Pathfinding.CanTraverse(a, b, 0, map, true));
    }
}
