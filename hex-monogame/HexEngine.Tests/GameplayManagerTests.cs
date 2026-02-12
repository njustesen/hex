using HexEngine.Config;
using HexEngine.Core;
using HexEngine.Maps;

namespace HexEngine.Tests;

public class GameplayManagerTests
{
    static GameplayManagerTests()
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

    private static HexGridMap CreateFlatMap()
        => new HexGridMap(10, 10, 100f, 0.7f, "flat");

    private static void TickUntilDone(GameplayManager gm, int maxSteps = 20)
    {
        float dt = EngineConfig.PlanStepDelay + 0.01f;
        for (int i = 0; i < maxSteps && gm.IsAnimating; i++)
            gm.Tick(dt);
    }

    [Fact]
    public void SelectUnit_SetsSelectedUnitTile()
    {
        var map = CreateFlatMap();
        var tile = map.Tiles[5][5];
        tile.Unit = new Unit("Marine");

        var gm = new GameplayManager();
        var state = new InteractionState();

        gm.OnTileClicked(tile, map);
        gm.Update(state);

        Assert.Equal(tile, state.SelectedUnitTile);
        Assert.NotNull(state.ReachableTiles);
    }

    [Fact]
    public void DeselectUnit_ClickSameTile()
    {
        var map = CreateFlatMap();
        var tile = map.Tiles[5][5];
        tile.Unit = new Unit("Marine");

        var gm = new GameplayManager();
        var state = new InteractionState();

        gm.OnTileClicked(tile, map); // select
        gm.OnTileClicked(tile, map); // deselect
        gm.Update(state);

        Assert.Null(state.SelectedUnitTile);
        Assert.Null(state.ReachableTiles);
    }

    [Fact]
    public void EndTurn_ResetsAllUnitMP()
    {
        var map = CreateFlatMap();
        var tile = map.Tiles[5][5];
        tile.Unit = new Unit("Marine") { Team = Team.Blue };
        tile.Unit.MovementPoints = 0;

        var gm = new GameplayManager(); // CurrentTeam starts as Red
        gm.EndTurn(map); // switches to Blue, resets Blue units

        Assert.Equal(2, tile.Unit.MovementPoints);
    }

    [Fact]
    public void MoveUnit_ReducesMP()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit("Marine");

        var goal = map.GetNeighbor(start, 0)!;

        var gm = new GameplayManager();
        var state = new InteractionState();

        gm.OnTileClicked(start, map);  // select
        gm.OnTileClicked(goal, map);   // plan path
        gm.OnTileClicked(goal, map);   // execute plan (click last move)
        TickUntilDone(gm);

        Assert.Null(start.Unit);
        Assert.NotNull(goal.Unit);
        Assert.Equal("Marine", goal.Unit!.Type);
        Assert.Equal(1, goal.Unit.MovementPoints); // 2 - 1 = 1
    }
}
