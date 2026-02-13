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
    public int TurnNumber { get; private set; } = 1;
    public GameMode Mode { get; set; }

    // Resources
    private Dictionary<Team, (int Iron, int Fissium)> _resources = new()
    {
        [Team.Red] = (0, 0),
        [Team.Blue] = (0, 0),
    };

    // Mine placement mode
    private bool _pendingMineMode;
    private List<Tile>? _minePlacementTiles;

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
        TurnNumber = 1;
        _resources[Team.Red] = (3, 3);
        _resources[Team.Blue] = (3, 3);
        Deselect();
        RefreshVisibility();
    }

    public (int Iron, int Fissium) GetResources(Team team)
    {
        return _resources.TryGetValue(team, out var r) ? r : (0, 0);
    }

    public bool CanAfford(string type, Team team)
    {
        var def = UnitDefs.Get(type);
        var r = GetResources(team);
        return r.Iron >= def.CostIron && r.Fissium >= def.CostFissium;
    }

    public void SpendResources(string type, Team team)
    {
        var def = UnitDefs.Get(type);
        var r = GetResources(team);
        _resources[team] = (r.Iron - def.CostIron, r.Fissium - def.CostFissium);
    }

    public void RefundResources(string type, Team team)
    {
        var def = UnitDefs.Get(type);
        var r = GetResources(team);
        _resources[team] = (r.Iron + def.CostIron, r.Fissium + def.CostFissium);
    }

    public bool CanAffordMineUpgrade(Team team)
    {
        var r = GetResources(team);
        return r.Iron >= 3;
    }

    public void SpendMineUpgrade(Team team)
    {
        var r = GetResources(team);
        _resources[team] = (r.Iron - 3, r.Fissium);
    }

    public (int Iron, int Fissium) ComputeIncome(GridMap map, Team team)
    {
        int ironIncome = 3;
        int fissiumIncome = 3;

        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
            {
                var tile = map.Tiles[y][x];
                if (tile.Unit == null || tile.Unit.Type != "Mine" || tile.Unit.Team != team) continue;
                if (tile.Resource == ResourceType.None) continue;

                // Check if adjacent to own CC
                bool adjacentToCC = false;
                for (int e = 0; e < map.EdgeCount; e++)
                {
                    var neighbor = map.GetNeighbor(tile, e);
                    if (neighbor?.Unit != null && neighbor.Unit.Type == "CommandCenter" && neighbor.Unit.Team == team)
                    {
                        adjacentToCC = true;
                        break;
                    }
                }
                if (!adjacentToCC) continue;

                int production = 2 + tile.Unit.MineLevel; // level 1 = 3, level 2 = 4, level 3 = 5
                if (tile.Resource == ResourceType.Iron)
                    ironIncome += production;
                else if (tile.Resource == ResourceType.Fissium)
                    fissiumIncome += production;
            }

        return (ironIncome, fissiumIncome);
    }

    public void AddIncome(GridMap map, Team team)
    {
        var r = GetResources(team);
        var income = ComputeIncome(map, team);
        _resources[team] = (r.Iron + income.Iron, r.Fissium + income.Fissium);
    }

    public void EnterMineMode()
    {
        if (_selectedUnitTile?.Unit == null || !_selectedUnitTile.Unit.CanProduce) return;
        if (_selectedUnitTile.Unit.IsProducing) return;
        if (!CanAfford("Mine", _selectedUnitTile.Unit.Team)) return;

        _pendingMineMode = true;
        _minePlacementTiles = ComputeValidMineTiles(_selectedUnitTile, _map!);
        if (_minePlacementTiles.Count == 0)
        {
            _pendingMineMode = false;
            _minePlacementTiles = null;
        }
    }

    public void ExitMineMode()
    {
        _pendingMineMode = false;
        _minePlacementTiles = null;
    }

    public bool IsMineMode => _pendingMineMode;

    private List<Tile> ComputeValidMineTiles(Tile ccTile, GridMap map)
    {
        var valid = new List<Tile>();
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var neighbor = map.GetNeighbor(ccTile, e);
            if (neighbor != null && neighbor.Resource != ResourceType.None && neighbor.Unit == null)
                valid.Add(neighbor);
        }
        return valid;
    }

    public bool HasAdjacentResourceTiles(Tile ccTile, GridMap map)
    {
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var neighbor = map.GetNeighbor(ccTile, e);
            if (neighbor != null && neighbor.Resource != ResourceType.None && neighbor.Unit == null)
                return true;
        }
        return false;
    }

    public void OnMineTileClicked(Tile tile)
    {
        if (!_pendingMineMode || _minePlacementTiles == null) return;
        if (!_minePlacementTiles.Contains(tile)) { ExitMineMode(); return; }
        if (_selectedUnitTile?.Unit == null) return;

        var cc = _selectedUnitTile.Unit;
        SpendResources("Mine", cc.Team);
        cc.StartProduction("Mine");
        cc.MineTargetCoords = (tile.X, tile.Y);
        ExitMineMode();
    }

    public void UpgradeMine()
    {
        if (_selectedUnitTile?.Unit == null) return;
        var unit = _selectedUnitTile.Unit;
        if (unit.Type != "Mine" || unit.MineLevel >= 3) return;
        if (!CanAffordMineUpgrade(unit.Team)) return;

        SpendMineUpgrade(unit.Team);
        unit.MineLevel++;
    }

    public void OnTileClicked(Tile tile, GridMap map)
    {
        _map = map;

        // Mine placement mode intercept
        if (_pendingMineMode)
        {
            OnMineTileClicked(tile);
            return;
        }

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
        if (tile.Unit != null && (tile.Unit.CanMove || tile.Unit.CanAttack || tile.Unit.CanProduce || tile.Unit.Type == "Mine") && CanControlUnit(tile.Unit))
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
        ExitMineMode();
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

        if (unit.CanMove || unit.CanAttack || unit.CanProduce)
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
        ExitMineMode();
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

        state.SelectedUnitTile = executing || _pendingMineMode ? null : _selectedUnitTile;

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
        state.TurnNumber = TurnNumber;
        state.VisibleTiles = _visibleTiles;
        state.IsEditor = Mode == GameMode.Editor;

        // Resources
        var res = GetResources(CurrentTeam);
        state.TeamIron = res.Iron;
        state.TeamFissium = res.Fissium;

        if (_map != null)
        {
            var income = ComputeIncome(_map, CurrentTeam);
            state.TeamIronIncome = income.Iron;
            state.TeamFissiumIncome = income.Fissium;
        }

        // Mine placement
        state.MinePlacementTiles = _pendingMineMode ? _minePlacementTiles : null;
    }

    public void StartProduction(string type)
    {
        if (_selectedUnitTile?.Unit == null || !_selectedUnitTile.Unit.CanProduce) return;
        if (_selectedUnitTile.Unit.IsProducing) return;
        var team = _selectedUnitTile.Unit.Team;
        if (!CanAfford(type, team)) return;
        SpendResources(type, team);
        _selectedUnitTile.Unit.StartProduction(type);
    }

    public void CancelProduction()
    {
        if (_selectedUnitTile?.Unit == null || !_selectedUnitTile.Unit.CanProduce) return;
        if (!_selectedUnitTile.Unit.IsProducing) return;
        var type = _selectedUnitTile.Unit.ProducingType!;
        var team = _selectedUnitTile.Unit.Team;
        _selectedUnitTile.Unit.CancelProduction();
        _selectedUnitTile.Unit.MineTargetCoords = null;
        RefundResources(type, team);
    }

    public void EndTurn(GridMap map)
    {
        _map = map;
        Deselect();
        CurrentTeam = CurrentTeam == Team.Red ? Team.Blue : Team.Red;
        if (CurrentTeam == Team.Red) TurnNumber++;
        AddIncome(map, CurrentTeam);
        AdvanceProduction(map);
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
            {
                var unit = map.Tiles[y][x].Unit;
                if (unit != null && unit.Team == CurrentTeam)
                    unit.ResetTurn();
            }
        RefreshVisibility();
    }

    private void AdvanceProduction(GridMap map)
    {
        for (int y = 0; y < map.Rows; y++)
            for (int x = 0; x < map.Cols; x++)
            {
                var tile = map.Tiles[y][x];
                var unit = tile.Unit;
                if (unit == null || unit.Team != CurrentTeam || !unit.IsProducing) continue;

                if (unit.ProductionTurnsLeft > 0)
                {
                    unit.ProductionTurnsLeft--;
                }

                if (unit.ProductionTurnsLeft <= 0)
                {
                    // Mine production: spawn at target coords
                    if (unit.ProducingType == "Mine" && unit.MineTargetCoords.HasValue)
                    {
                        var (mx, my) = unit.MineTargetCoords.Value;
                        if (my >= 0 && my < map.Rows && mx >= 0 && mx < map.Cols)
                        {
                            var targetTile = map.Tiles[my][mx];
                            if (targetTile.Unit == null)
                            {
                                var newUnit = new Unit("Mine") { Team = unit.Team };
                                newUnit.MovementPoints = 0;
                                newUnit.AttacksRemaining = 0;
                                targetTile.Unit = newUnit;
                                unit.MineTargetCoords = null;
                                unit.CancelProduction();
                            }
                            // If target occupied, waits
                        }
                    }
                    else
                    {
                        var freeTile = FindFreeAdjacentTile(map, tile);
                        if (freeTile != null)
                        {
                            var newUnit = new Unit(unit.ProducingType!) { Team = unit.Team };
                            newUnit.MovementPoints = 0;
                            newUnit.AttacksRemaining = 0;
                            freeTile.Unit = newUnit;
                            unit.CancelProduction();
                        }
                        // If no free tile, stays at 0 and waits
                    }
                }
            }
    }

    public static Tile? FindFreeAdjacentTile(GridMap map, Tile tile)
    {
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var neighbor = map.GetNeighbor(tile, e);
            if (neighbor != null && neighbor.Unit == null && neighbor.Resource == ResourceType.None)
                return neighbor;
        }
        return null;
    }
}
