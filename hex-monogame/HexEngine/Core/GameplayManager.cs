using System.Collections.Generic;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine.Core;

public class GameplayManager
{
    private Tile? _selectedUnitTile;
    private Dictionary<Tile, int>? _reachableTiles;
    private List<Tile>? _plannedPath;
    private Tile? _pathEndpoint;

    public void OnTileClicked(Tile tile, GridMap map)
    {
        // Case 2: Click selected unit's tile → deselect
        if (_selectedUnitTile != null && tile == _selectedUnitTile)
        {
            Deselect();
            return;
        }

        // Case 5: Click same endpoint again → execute move
        if (_pathEndpoint != null && tile == _pathEndpoint && _plannedPath != null && _selectedUnitTile != null)
        {
            ExecuteMove(map);
            return;
        }

        // Case 3: Click different tile with movable unit → select that one
        if (tile.Unit != null && tile.Unit.CanMove)
        {
            SelectUnit(tile, map);
            return;
        }

        // Case 4: Click reachable tile → compute path
        if (_reachableTiles != null && _reachableTiles.ContainsKey(tile) && _selectedUnitTile != null)
        {
            var path = Pathfinding.FindPath(map, _selectedUnitTile, tile, _selectedUnitTile.Unit!.Type);
            if (path != null)
            {
                _plannedPath = path;
                _pathEndpoint = tile;
                return;
            }
        }

        // Case 6: Click unreachable tile → deselect
        Deselect();
    }

    private void SelectUnit(Tile tile, GridMap map)
    {
        _selectedUnitTile = tile;
        _reachableTiles = Pathfinding.GetReachableTiles(map, tile, tile.Unit!.MovementPoints, tile.Unit.Type);
        _plannedPath = null;
        _pathEndpoint = null;
    }

    private void ExecuteMove(GridMap map)
    {
        if (_selectedUnitTile?.Unit == null || _plannedPath == null || _pathEndpoint == null)
            return;

        var unit = _selectedUnitTile.Unit;
        int cost = _plannedPath.Count - 1; // path includes start
        unit.MovementPoints -= cost;

        // Move unit
        _selectedUnitTile.Unit = null;
        _pathEndpoint.Unit = unit;

        // Re-select if MP remains, else deselect
        if (unit.CanMove)
        {
            SelectUnit(_pathEndpoint, map);
        }
        else
        {
            Deselect();
        }
    }

    public void Deselect()
    {
        _selectedUnitTile = null;
        _reachableTiles = null;
        _plannedPath = null;
        _pathEndpoint = null;
    }

    public void Update(InteractionState state)
    {
        state.SelectedUnitTile = _selectedUnitTile;
        state.ReachableTiles = _reachableTiles != null ? new System.Collections.Generic.HashSet<Tile>(_reachableTiles.Keys) : null;
        state.PlannedPath = _plannedPath;
    }

    public void EndTurn(GridMap map)
    {
        Deselect();
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
                map.Tiles[y][x].Unit?.ResetMovementPoints();
    }
}
