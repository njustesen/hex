using HexEngine.Core;
using HexEngine.Maps;

namespace HexEngine.Tests;

public class GameplayManagerTests
{
    private static HexGridMap CreateFlatMap()
        => new HexGridMap(10, 10, 100f, 0.7f, "flat");

    [Fact]
    public void SelectUnit_SetsSelectedUnitTile()
    {
        var map = CreateFlatMap();
        var tile = map.Tiles[5][5];
        tile.Unit = new Unit(UnitType.Marine);

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
        tile.Unit = new Unit(UnitType.Marine);

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
        tile.Unit = new Unit(UnitType.Marine);
        tile.Unit.MovementPoints = 0;

        var gm = new GameplayManager();
        gm.EndTurn(map);

        Assert.Equal(2, tile.Unit.MovementPoints);
    }

    [Fact]
    public void MoveUnit_ReducesMP()
    {
        var map = CreateFlatMap();
        var start = map.Tiles[5][5];
        start.Unit = new Unit(UnitType.Marine);

        var goal = map.GetNeighbor(start, 0)!;

        var gm = new GameplayManager();
        var state = new InteractionState();

        gm.OnTileClicked(start, map);  // select
        gm.OnTileClicked(goal, map);   // plan path
        gm.OnTileClicked(goal, map);   // confirm move

        Assert.Null(start.Unit);
        Assert.NotNull(goal.Unit);
        Assert.Equal(UnitType.Marine, goal.Unit!.Type);
        Assert.Equal(1, goal.Unit.MovementPoints); // 2 - 1 = 1
    }
}
