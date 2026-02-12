using System.Collections.Generic;
using System.Linq;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine.Core;

public class GameplayManager
{
    private Tile? _selectedUnitTile;
    private Dictionary<Tile, int>? _reachableTiles;
    private HashSet<Tile>? _attackableTiles;
    private List<Tile>? _plannedPath;
    private Tile? _pathEndpoint;

    // Multi-step plan
    private List<Tile> _planSteps = new();
    private List<List<Tile>> _planPaths = new();
    private int _planRemainingMP;
    private Dictionary<Tile, int>? _planReachable;
    private HashSet<Tile>? _planAttackable;

    // Animation
    private float _animationTimer;
    private Tile? _animSourceTile;
    private Tile? _animTargetTile;

    public bool IsAnimating => _animationTimer > 0;

    public void OnTileClicked(Tile tile, GridMap map)
    {
        // 1. Animation playing → ignore
        if (IsAnimating) return;

        // 2. Click selected unit tile → deselect
        if (_selectedUnitTile != null && tile == _selectedUnitTile)
        {
            Deselect();
            return;
        }

        // 3. Click last plan step → execute plan
        if (_planSteps.Count > 0 && tile == _planSteps[^1] && _selectedUnitTile != null)
        {
            ExecutePlan(map);
            return;
        }

        // 4. Click plan-attackable tile → execute plan + attack
        if (_planAttackable != null && _planAttackable.Contains(tile) && _selectedUnitTile?.Unit != null)
        {
            if (_planSteps.Count > 0)
                ExecutePlan(map);
            ExecuteAttack(tile, map);
            return;
        }

        // 5. Click current-position attackable tile (no plan) → attack immediately
        if (_planSteps.Count == 0 && _attackableTiles != null && _attackableTiles.Contains(tile) && _selectedUnitTile?.Unit != null)
        {
            ExecuteAttack(tile, map);
            return;
        }

        // 6. Click different selectable unit → select it
        if (tile.Unit != null && (tile.Unit.CanMove || tile.Unit.CanAttack))
        {
            SelectUnit(tile, map);
            return;
        }

        // 7. Click reachable tile (no plan yet) → start plan with one step
        if (_planSteps.Count == 0 && _reachableTiles != null && _reachableTiles.ContainsKey(tile) && _selectedUnitTile != null)
        {
            ExtendPlan(tile, map);
            return;
        }

        // 8. Click plan-reachable tile → extend plan
        if (_planSteps.Count > 0 && _planReachable != null && _planReachable.ContainsKey(tile) && _selectedUnitTile != null)
        {
            ExtendPlan(tile, map);
            return;
        }

        // 9. Otherwise → if plan exists, clear plan (keep selection). If no plan, deselect.
        if (_planSteps.Count > 0)
        {
            ClearPlan();
            if (_selectedUnitTile != null)
                RecomputeFromUnit(_selectedUnitTile, map);
        }
        else
        {
            Deselect();
        }
    }

    private void SelectUnit(Tile tile, GridMap map)
    {
        _selectedUnitTile = tile;
        ClearPlan();
        RecomputeFromUnit(tile, map);
    }

    private void RecomputeFromUnit(Tile tile, GridMap map)
    {
        var unit = tile.Unit!;
        _reachableTiles = unit.CanMove
            ? Pathfinding.GetReachableTiles(map, tile, unit.MovementPoints, unit.IsFlying)
            : new Dictionary<Tile, int>();
        _attackableTiles = ComputeAttackableTiles(tile, map);
        _plannedPath = null;
        _pathEndpoint = null;
    }

    private void ExtendPlan(Tile tile, GridMap map)
    {
        if (_selectedUnitTile?.Unit == null) return;
        var unit = _selectedUnitTile.Unit;

        Tile source = _planSteps.Count > 0 ? _planSteps[^1] : _selectedUnitTile;
        int remainingMP = _planSteps.Count > 0 ? _planRemainingMP : unit.MovementPoints;

        var path = Pathfinding.FindPath(map, source, tile, unit.IsFlying);
        if (path == null) return;

        int cost = path.Count - 1;
        if (cost > remainingMP) return;

        _planSteps.Add(tile);
        _planPaths.Add(path);
        _planRemainingMP = remainingMP - cost;

        // Recompute reachable from endpoint
        _planReachable = _planRemainingMP > 0
            ? Pathfinding.GetReachableTiles(map, tile, _planRemainingMP, unit.IsFlying)
            : new Dictionary<Tile, int>();

        // Recompute attackable from endpoint
        _planAttackable = ComputeAttackableTilesFrom(tile, unit, map);

        // Also keep a single combined planned path for visualization
        RebuildPlannedPath();
    }

    private void RebuildPlannedPath()
    {
        if (_planPaths.Count == 0)
        {
            _plannedPath = null;
            _pathEndpoint = null;
            return;
        }

        var combined = new List<Tile>();
        for (int i = 0; i < _planPaths.Count; i++)
        {
            var path = _planPaths[i];
            int start = (i == 0) ? 0 : 1; // skip duplicate start for subsequent paths
            for (int j = start; j < path.Count; j++)
                combined.Add(path[j]);
        }
        _plannedPath = combined;
        _pathEndpoint = _planSteps[^1];
    }

    private void ExecutePlan(GridMap map)
    {
        if (_selectedUnitTile?.Unit == null || _planSteps.Count == 0) return;

        var unit = _selectedUnitTile.Unit;
        var finalTile = _planSteps[^1];

        // Compute total cost
        int totalCost = 0;
        foreach (var path in _planPaths)
            totalCost += path.Count - 1;

        unit.MovementPoints -= totalCost;

        // Move unit
        _selectedUnitTile.Unit = null;
        finalTile.Unit = unit;

        // Clear plan
        ClearPlan();

        // Re-select at new position
        if (unit.CanMove || unit.CanAttack)
            SelectUnit(finalTile, map);
        else
            Deselect();
    }

    private HashSet<Tile>? ComputeAttackableTiles(Tile unitTile, GridMap map)
    {
        var unit = unitTile.Unit;
        if (unit == null || !unit.CanAttack) return null;
        return ComputeAttackableTilesFrom(unitTile, unit, map);
    }

    private HashSet<Tile>? ComputeAttackableTilesFrom(Tile fromTile, Unit unit, GridMap map)
    {
        if (!unit.CanAttack) return null;

        var tilesInRange = Pathfinding.GetTilesInRange(map, fromTile, unit.Range);
        var attackable = new HashSet<Tile>();

        foreach (var tile in tilesInRange)
        {
            if (tile.Unit == null) continue;
            if (tile.Unit.Team == unit.Team) continue;
            if (tile.Unit.IsFlying && !unit.CanTargetAir) continue;
            attackable.Add(tile);
        }

        return attackable.Count > 0 ? attackable : null;
    }

    private void ExecuteAttack(Tile targetTile, GridMap map)
    {
        if (_selectedUnitTile?.Unit == null || targetTile.Unit == null) return;

        var attacker = _selectedUnitTile.Unit;
        var target = targetTile.Unit;

        // Elevation armor bonus
        bool elevBonus = targetTile.Elevation > _selectedUnitTile.Elevation
                         && !attacker.IsFlying;
        if (elevBonus) target.Armor++;

        target.TakeDamage(attacker.Damage);
        attacker.AttacksRemaining--;

        if (!target.IsAlive)
            targetTile.Unit = null;

        // Trigger animation
        _animationTimer = 0.3f;
        _animSourceTile = _selectedUnitTile;
        _animTargetTile = targetTile;

        // Re-select attacker to refresh highlights
        if (attacker.CanMove || attacker.CanAttack)
            SelectUnit(_selectedUnitTile, map);
        else
            Deselect();
    }

    private void ClearPlan()
    {
        _planSteps.Clear();
        _planPaths.Clear();
        _planRemainingMP = 0;
        _planReachable = null;
        _planAttackable = null;
        _plannedPath = null;
        _pathEndpoint = null;
    }

    public void Deselect()
    {
        _selectedUnitTile = null;
        _reachableTiles = null;
        _attackableTiles = null;
        ClearPlan();
    }

    public void Tick(float seconds)
    {
        if (_animationTimer > 0)
        {
            _animationTimer -= seconds;
            if (_animationTimer <= 0)
            {
                _animationTimer = 0;
                _animSourceTile = null;
                _animTargetTile = null;
            }
        }
    }

    public void Update(InteractionState state)
    {
        state.SelectedUnitTile = _selectedUnitTile;
        state.ReachableTiles = _reachableTiles != null ? new HashSet<Tile>(_reachableTiles.Keys) : null;
        state.PlannedPath = _plannedPath;
        state.AttackableTiles = _attackableTiles;

        // Plan state
        state.PlanSteps = _planSteps.Count > 0 ? new List<Tile>(_planSteps) : null;
        state.PlanPaths = _planPaths.Count > 0 ? _planPaths.Select(p => new List<Tile>(p)).ToList() : null;
        state.PlanReachableTiles = _planReachable != null ? new HashSet<Tile>(_planReachable.Keys) : null;
        state.PlanAttackableTiles = _planAttackable;

        // Animation state
        state.AnimationTimer = _animationTimer;
        state.AnimationSourceTile = _animSourceTile;
        state.AnimationTargetTile = _animTargetTile;
    }

    public void EndTurn(GridMap map)
    {
        Deselect();
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
                map.Tiles[y][x].Unit?.ResetTurn();
    }
}
