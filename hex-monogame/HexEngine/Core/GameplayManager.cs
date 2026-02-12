using System.Collections.Generic;
using System.Linq;
using HexEngine.Config;
using HexEngine.Maps;
using HexEngine.Tiles;

namespace HexEngine.Core;

public enum GameMode { HotSeat, Multiplayer, Editor }

public class GameplayManager
{
    private Tile? _selectedUnitTile;
    private Dictionary<Tile, int>? _reachableTiles;
    private HashSet<Tile>? _attackableTiles;

    // Unified plan: sequence of move and attack actions
    private readonly List<(bool IsAttack, Tile Target, List<Tile>? Path)> _planActions = new();
    private int _planRemainingMP;
    private int _planRemainingAttacks;
    private Dictionary<Tile, int>? _planReachable;
    private HashSet<Tile>? _planAttackable;

    // Pending attack confirmation
    private Tile? _pendingAttackTarget;

    // Animation
    private float _animationTimer;
    private Tile? _animSourceTile;
    private Tile? _animTargetTile;

    // Step-by-step execution
    private List<(bool IsAttack, Tile Target, List<Tile>? Path)>? _executingPlan;
    private int _execIndex;
    private float _execTimer;
    private Tile? _execCurrentTile;
    private Unit? _execUnit;

    // Turn system
    private GridMap? _map;
    private HashSet<Tile>? _visibleTiles;
    public Team CurrentTeam { get; private set; } = Team.Red;
    public GameMode Mode { get; set; }

    public bool IsAnimating => _animationTimer > 0 || _executingPlan != null;

    /// The tile the unit would be at after all current plan actions.
    private Tile? PlanCurrentTile
    {
        get
        {
            for (int i = _planActions.Count - 1; i >= 0; i--)
                if (!_planActions[i].IsAttack) return _planActions[i].Target;
            return _selectedUnitTile;
        }
    }

    public void StartGame(GridMap map, GameMode mode)
    {
        _map = map;
        Mode = mode;
        CurrentTeam = Team.Red;
        Deselect();
        RefreshVisibility();
    }

    public void OnTileClicked(Tile tile, GridMap map)
    {
        _map = map;
        var tilePos = $"({tile.X},{tile.Y})";
        var planDesc = $"plan={_planActions.Count} mp={_planRemainingMP} atk={_planRemainingAttacks}";

        // 1. Animation playing → ignore
        if (IsAnimating) { System.Console.WriteLine($"[Click {tilePos}] IGNORED (animating) {planDesc}"); return; }

        // 2. Click selected unit tile → clear plan or deselect
        if (_selectedUnitTile != null && tile == _selectedUnitTile)
        {
            if (_planActions.Count > 0)
            {
                System.Console.WriteLine($"[Click {tilePos}] Step2: CLEAR plan {planDesc}");
                ClearPlan();
                RecomputeFromUnit(_selectedUnitTile, map);
            }
            else
            {
                System.Console.WriteLine($"[Click {tilePos}] Step2: DESELECT {planDesc}");
                Deselect();
            }
            return;
        }

        // 3. Click last action target → execute plan
        var lastTarget = _planActions.Count > 0 ? _planActions[^1].Target : null;
        if (lastTarget != null && tile == lastTarget && tile != _selectedUnitTile)
        {
            System.Console.WriteLine($"[Click {tilePos}] Step3: EXECUTE (last target) {planDesc}");
            ExecutePlan(map);
            return;
        }

        // 4. Click attackable tile (from plan endpoint or current position)
        bool isPlanAttackable = _planAttackable != null && _planAttackable.Contains(tile);
        bool isDirectAttackable = _planActions.Count == 0 && _attackableTiles != null && _attackableTiles.Contains(tile);
        if ((isPlanAttackable || isDirectAttackable) && _selectedUnitTile?.Unit != null)
        {
            if (isPlanAttackable)
            {
                // During plan: single click adds attack immediately
                System.Console.WriteLine($"[Click {tilePos}] Step4: ADD ATTACK to plan (immediate) {planDesc}");
                _pendingAttackTarget = null;
                AddAttackToPlan(tile, map);
            }
            else if (tile == _pendingAttackTarget)
            {
                System.Console.WriteLine($"[Click {tilePos}] Step4: EXECUTE ATTACK (confirmed) {planDesc}");
                // Second click on direct attack → add and execute immediately
                AddAttackToPlan(tile, map);
                ExecutePlan(map);
            }
            else
            {
                System.Console.WriteLine($"[Click {tilePos}] Step4: PREVIEW attack {planDesc}");
                // First click on direct attack → preview
                _pendingAttackTarget = tile;
            }
            return;
        }

        // 5. Click different selectable unit → select it (team-filtered)
        if (tile.Unit != null && (tile.Unit.CanMove || tile.Unit.CanAttack) && CanControlUnit(tile.Unit))
        {
            System.Console.WriteLine($"[Click {tilePos}] Step5: SELECT unit {tile.Unit.Type} {planDesc}");
            SelectUnit(tile, map);
            return;
        }

        // 6. Click reachable tile (no plan yet) → start plan with move
        //    If there's a pending attack preview, auto-confirm it first (attack → move)
        if (_planActions.Count == 0 && _reachableTiles != null && _reachableTiles.ContainsKey(tile) && _selectedUnitTile != null)
        {
            if (_pendingAttackTarget != null)
            {
                System.Console.WriteLine($"[Click {tilePos}] Step6: AUTO-CONFIRM attack + START plan (move) {planDesc}");
                AddAttackToPlan(_pendingAttackTarget, map);
            }
            else
            {
                System.Console.WriteLine($"[Click {tilePos}] Step6: START plan (move) {planDesc}");
            }
            AddMoveToPlan(tile, map);
            return;
        }

        // 7. Click plan-reachable tile → extend plan with move
        if (_planActions.Count > 0 && _planReachable != null && _planReachable.ContainsKey(tile) && _selectedUnitTile != null)
        {
            System.Console.WriteLine($"[Click {tilePos}] Step7: EXTEND plan (move) {planDesc}");
            AddMoveToPlan(tile, map);
            return;
        }

        // 8. Otherwise → clear plan or deselect
        System.Console.WriteLine($"[Click {tilePos}] Step8: CLEAR/DESELECT unit={tile.Unit?.Type} planReachable={_planReachable?.Count} {planDesc}");
        if (_planActions.Count > 0)
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

    private bool CanControlUnit(Unit unit)
    {
        if (Mode == GameMode.Editor) return true;
        if (Mode == GameMode.Multiplayer) return unit.Team == Team.Red && CurrentTeam == Team.Red;
        return unit.Team == CurrentTeam;
    }

    private void SelectUnit(Tile tile, GridMap map)
    {
        _selectedUnitTile = tile;
        _pendingAttackTarget = null;
        ClearPlan();
        RecomputeFromUnit(tile, map);
    }

    private void RecomputeFromUnit(Tile tile, GridMap map)
    {
        var unit = tile.Unit!;
        _reachableTiles = unit.CanMove
            ? Pathfinding.GetReachableTiles(map, tile, unit.MovementPoints, unit.IsFlying)
            : new Dictionary<Tile, int>();
        _attackableTiles = ComputeAttackableTilesFrom(tile, unit, map);
    }

    private void AddMoveToPlan(Tile tile, GridMap map)
    {
        if (_selectedUnitTile?.Unit == null) return;
        var unit = _selectedUnitTile.Unit;

        Tile source = PlanCurrentTile!;
        int remainingMP = _planActions.Count > 0 ? _planRemainingMP : unit.MovementPoints;

        var path = Pathfinding.FindPath(map, source, tile, unit.IsFlying);
        if (path == null) return;

        int cost = path.Count - 1;
        if (cost > remainingMP) return;

        _planActions.Add((false, tile, path));
        _planRemainingMP = remainingMP - cost;
        if (_planActions.Count == 1)
            _planRemainingAttacks = unit.AttacksRemaining;
        _pendingAttackTarget = null;

        RecomputePlanHighlights(tile, unit, map);
    }

    private void AddAttackToPlan(Tile target, GridMap map)
    {
        if (_selectedUnitTile?.Unit == null) return;
        var unit = _selectedUnitTile.Unit;

        _planActions.Add((true, target, null));
        _pendingAttackTarget = null;

        if (_planActions.Count == 1)
        {
            _planRemainingMP = unit.MovementPoints;
            _planRemainingAttacks = unit.AttacksRemaining - 1;
        }
        else
        {
            _planRemainingAttacks--;
        }

        RecomputePlanHighlights(PlanCurrentTile!, unit, map);
    }

    private void RecomputePlanHighlights(Tile fromTile, Unit unit, GridMap map)
    {
        _planReachable = _planRemainingMP > 0
            ? Pathfinding.GetReachableTiles(map, fromTile, _planRemainingMP, unit.IsFlying)
            : new Dictionary<Tile, int>();

        _planAttackable = _planRemainingAttacks > 0
            ? ComputeAttackableTilesFrom(fromTile, unit, map)
            : null;
    }

    private void ExecutePlan(GridMap map)
    {
        if (_selectedUnitTile?.Unit == null || _planActions.Count == 0) return;

        _execUnit = _selectedUnitTile.Unit;
        _execCurrentTile = _selectedUnitTile;

        // Expand move paths into individual tile-by-tile steps
        _executingPlan = new List<(bool IsAttack, Tile Target, List<Tile>? Path)>();
        foreach (var action in _planActions)
        {
            if (action.IsAttack)
            {
                _executingPlan.Add(action);
            }
            else
            {
                // Path includes source tile at index 0; each subsequent tile is one step
                for (int i = 1; i < action.Path!.Count; i++)
                    _executingPlan.Add((false, action.Path[i], null));
            }
        }

        _execIndex = -1; // -1 means first step hasn't fired yet
        _execTimer = EngineConfig.PlanStepDelay;

        ClearPlan();
    }

    private void ExecuteCurrentStep(GridMap map)
    {
        if (_executingPlan == null || _execUnit == null || _execCurrentTile == null) return;

        var action = _executingPlan[_execIndex];

        if (action.IsAttack)
        {
            var target = action.Target.Unit;
            if (target != null && target.Team != _execUnit.Team)
            {
                bool elevBonus = action.Target.Elevation > _execCurrentTile.Elevation
                                 && !_execUnit.IsFlying;
                if (elevBonus) target.Armor++;

                int hpBefore = target.Health;
                int armorBefore = target.Armor;
                target.TakeDamage(_execUnit.Damage);
                _execUnit.AttacksRemaining--;

                System.Console.WriteLine($"[ATTACK] {_execUnit.Type} ({_execCurrentTile.X},{_execCurrentTile.Y}) → {target.Type} ({action.Target.X},{action.Target.Y}) dmg={_execUnit.Damage} elevBonus={elevBonus} armor={armorBefore}→{target.Armor} hp={hpBefore}→{target.Health} alive={target.IsAlive}");

                _animationTimer = EngineConfig.PlanStepDelay;
                _animSourceTile = _execCurrentTile;
                _animTargetTile = action.Target;

                if (!target.IsAlive)
                    action.Target.Unit = null;
            }
            else
            {
                System.Console.WriteLine($"[ATTACK] {_execUnit.Type} → MISSED (target={target?.Type ?? "null"} team={target?.Team.ToString() ?? "n/a"})");
            }
        }
        else
        {
            // Single tile move — only clear/place if not occupied by another unit
            _execUnit.MovementPoints--;
            if (_execCurrentTile.Unit == _execUnit)
                _execCurrentTile.Unit = null;
            if (action.Target.Unit == null)
                action.Target.Unit = _execUnit;
            _execCurrentTile = action.Target;
        }

        _execTimer = EngineConfig.PlanStepDelay;
    }

    private void FinishExecution()
    {
        if (_executingPlan == null || _execUnit == null || _execCurrentTile == null || _map == null) return;

        var unit = _execUnit;
        var finalTile = _execCurrentTile;

        _executingPlan = null;
        _execUnit = null;
        _execCurrentTile = null;
        _execIndex = 0;
        _execTimer = 0;

        RefreshVisibility();

        if (unit.CanMove || unit.CanAttack)
            SelectUnit(finalTile, _map);
        else
            Deselect();
    }

    private HashSet<Tile>? ComputeAttackableTilesFrom(Tile fromTile, Unit unit, GridMap map)
    {
        int attacks = _planActions.Count > 0 ? _planRemainingAttacks : unit.AttacksRemaining;
        if (attacks <= 0) return null;

        var tilesInRange = Pathfinding.GetTilesInRange(map, fromTile, unit.Range);
        var attackable = new HashSet<Tile>();

        foreach (var tile in tilesInRange)
        {
            if (tile.Unit == null) continue;
            if (tile.Unit.Team == unit.Team) continue;
            if (tile.Unit.IsFlying && !unit.CanTargetAir) continue;
            if (!tile.Unit.IsFlying && !unit.CanTargetGround) continue;
            attackable.Add(tile);
        }

        return attackable.Count > 0 ? attackable : null;
    }

    private void ClearPlan()
    {
        _planActions.Clear();
        _planRemainingMP = 0;
        _planRemainingAttacks = 0;
        _planReachable = null;
        _planAttackable = null;
        _pendingAttackTarget = null;
    }

    public static HashSet<Tile> ComputeVisibleTiles(GridMap map, Team team)
    {
        var visible = new HashSet<Tile>();
        for (int y = 0; y < map.Rows; y++)
        {
            for (int x = 0; x < map.Cols; x++)
            {
                var tile = map.Tiles[y][x];
                if (tile.Unit != null && tile.Unit.Team == team)
                {
                    visible.Add(tile);
                    foreach (var t in Pathfinding.GetTilesInRange(map, tile, tile.Unit.Sight))
                        visible.Add(t);
                }
            }
        }
        return visible;
    }

    private void RefreshVisibility()
    {
        if (_map == null || Mode == GameMode.Editor)
        {
            _visibleTiles = null;
            return;
        }
        Team perspectiveTeam = Mode == GameMode.Multiplayer ? Team.Red : CurrentTeam;
        _visibleTiles = ComputeVisibleTiles(_map, perspectiveTeam);
    }

    public void Deselect()
    {
        _selectedUnitTile = null;
        _reachableTiles = null;
        _attackableTiles = null;
        _pendingAttackTarget = null;
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

        if (_executingPlan != null)
        {
            _execTimer -= seconds;
            if (_execTimer <= 0)
            {
                _execIndex++;
                if (_execIndex >= _executingPlan.Count)
                {
                    FinishExecution();
                }
                else
                {
                    ExecuteCurrentStep(_map!);
                    RefreshVisibility();
                }
            }
        }
    }

    public void Update(InteractionState state)
    {
        bool executing = _executingPlan != null;

        state.SelectedUnitTile = executing ? null : _selectedUnitTile;

        bool hasPlan = _planActions.Count > 0;

        // Hide all highlights during execution
        state.ReachableTiles = !executing && !hasPlan && _reachableTiles != null ? new HashSet<Tile>(_reachableTiles.Keys) : null;
        state.AttackableTiles = !executing && !hasPlan ? _attackableTiles : null;

        // Build plan visualization from actions
        if (hasPlan)
        {
            var moveSteps = new List<Tile>();
            var movePaths = new List<List<Tile>>();
            var attackPairs = new List<(Tile Source, Tile Target)>();
            var combinedPath = new List<Tile>();
            Tile attackSource = _selectedUnitTile!;

            foreach (var action in _planActions)
            {
                if (action.IsAttack)
                {
                    attackPairs.Add((attackSource, action.Target));
                }
                else
                {
                    moveSteps.Add(action.Target);
                    movePaths.Add(new List<Tile>(action.Path!));
                    attackSource = action.Target;

                    // Build combined path
                    int start = combinedPath.Count == 0 ? 0 : 1;
                    for (int j = start; j < action.Path!.Count; j++)
                        combinedPath.Add(action.Path[j]);
                }
            }

            state.PlanSteps = moveSteps.Count > 0 ? moveSteps : null;
            state.PlanPaths = movePaths.Count > 0 ? movePaths : null;
            state.PlannedPath = combinedPath.Count > 0 ? combinedPath : null;
            state.PlanAttackPairs = attackPairs.Count > 0 ? attackPairs : null;
        }
        else
        {
            state.PlanSteps = null;
            state.PlanPaths = null;
            state.PlannedPath = null;
            state.PlanAttackPairs = null;
        }

        state.PlanReachableTiles = executing ? null : (_planReachable != null ? new HashSet<Tile>(_planReachable.Keys) : null);
        state.PlanAttackableTiles = executing ? null : _planAttackable;

        // Pending attack
        state.PendingAttackTarget = executing ? null : _pendingAttackTarget;

        // Executing unit overlay (when passing through occupied tiles)
        state.ExecUnitTile = executing ? _execCurrentTile : null;
        state.ExecUnit = executing ? _execUnit : null;

        // Animation state
        state.AnimationTimer = _animationTimer;
        state.AnimationSourceTile = _animSourceTile;
        state.AnimationTargetTile = _animTargetTile;

        // Turn & visibility
        state.CurrentTeam = CurrentTeam;
        state.VisibleTiles = _visibleTiles;
        state.IsEditor = Mode == GameMode.Editor;
    }

    public void EndTurn(GridMap map)
    {
        _map = map;
        Deselect();
        CurrentTeam = CurrentTeam == Team.Red ? Team.Blue : Team.Red;
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
            {
                var unit = map.Tiles[y][x].Unit;
                if (unit != null && unit.Team == CurrentTeam)
                    unit.ResetTurn();
            }
        RefreshVisibility();
    }
}
