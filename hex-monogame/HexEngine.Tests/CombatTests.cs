using HexEngine.Config;
using HexEngine.Core;
using HexEngine.Maps;

namespace HexEngine.Tests;

public class CombatTests
{
    static CombatTests()
    {
        // Load unit definitions from YAML for all tests
        var yamlPath = FindUnitsYaml();
        UnitDefs.Load(yamlPath);
    }

    private static string FindUnitsYaml()
    {
        // Walk up from test assembly directory to find units.yaml
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

    /// Tick the gameplay manager until step-by-step execution completes.
    private static void TickUntilDone(GameplayManager gm, int maxSteps = 20)
    {
        float dt = EngineConfig.PlanStepDelay + 0.01f;
        for (int i = 0; i < maxSteps && gm.IsAnimating; i++)
            gm.Tick(dt);
    }

    [Fact]
    public void TakeDamage_ReducesHealth()
    {
        var unit = new Unit("Marine"); // HP:3, Armor:0
        unit.TakeDamage(2);
        Assert.Equal(1, unit.Health);
    }

    [Fact]
    public void Armor_ReducesDamage()
    {
        var unit = new Unit("Tank"); // HP:5, Armor:1
        unit.TakeDamage(4); // 4-1=3 effective
        Assert.Equal(2, unit.Health);
        Assert.Equal(1, unit.Armor); // armor doesn't deplete
    }

    [Fact]
    public void Armor_ReducesEveryAttack()
    {
        var unit = new Unit("Fighter"); // HP:4, Armor:1
        unit.TakeDamage(3); // 3-1=2 effective, HP 4→2
        Assert.Equal(2, unit.Health);
        unit.TakeDamage(3); // 3-1=2 effective, HP 2→0
        Assert.Equal(0, unit.Health);
        Assert.Equal(1, unit.Armor); // still intact
    }

    [Fact]
    public void Armor_NoDamageWhenArmorExceedsDamage()
    {
        var unit = new Unit("Battlecruiser"); // HP:7, Armor:2
        unit.TakeDamage(1); // 1-2=0 effective
        Assert.Equal(7, unit.Health);
    }

    [Fact]
    public void Unit_DiesAt0Health()
    {
        var unit = new Unit("Marine"); // HP:3, Armor:0
        unit.TakeDamage(10);
        Assert.Equal(0, unit.Health);
        Assert.False(unit.IsAlive);
    }

    [Fact]
    public void Marine_CanTargetFighter()
    {
        var marine = new Unit("Marine");
        Assert.True(marine.CanTargetAir);
    }

    [Fact]
    public void Tank_CannotTargetFighter()
    {
        var tank = new Unit("Tank");
        Assert.False(tank.CanTargetAir);
    }

    [Fact]
    public void GetTilesInRange_CorrectHopCount()
    {
        var map = CreateFlatMap();
        var center = map.Tiles[5][5];
        var tilesRange1 = Pathfinding.GetTilesInRange(map, center, 1);
        var tilesRange2 = Pathfinding.GetTilesInRange(map, center, 2);

        // Range 1 should give immediate neighbors only
        Assert.True(tilesRange1.Count <= map.EdgeCount);
        Assert.True(tilesRange1.Count > 0);
        // Range 2 should include more tiles
        Assert.True(tilesRange2.Count > tilesRange1.Count);
        // Center should not be in result
        Assert.DoesNotContain(center, tilesRange1);
        Assert.DoesNotContain(center, tilesRange2);
    }

    [Fact]
    public void AttacksRemaining_PreventsAttackWhenDepleted()
    {
        var unit = new Unit("Marine");
        Assert.True(unit.CanAttack);
        Assert.Equal(1, unit.AttacksRemaining);
        unit.AttacksRemaining = 0;
        Assert.False(unit.CanAttack);
    }

    [Fact]
    public void EndTurn_ResetsAttacksRemaining()
    {
        var map = CreateFlatMap();
        var tile = map.Tiles[5][5];
        // Unit must be Blue because EndTurn switches Red→Blue and resets Blue units
        var unit = new Unit("Marine") { Team = Team.Blue };
        unit.AttacksRemaining = 0;
        unit.MovementPoints = 0;
        tile.Unit = unit;

        var gm = new GameplayManager();
        gm.EndTurn(map);

        Assert.Equal(unit.MaxAttacks, unit.AttacksRemaining);
        Assert.Equal(unit.MaxMovementPoints, unit.MovementPoints);
    }

    [Fact]
    public void DeadUnit_RemovedFromTile_AfterAttack()
    {
        var map = CreateFlatMap();
        var attackerTile = map.Tiles[5][5];
        var targetTile = map.GetNeighbor(attackerTile, 0)!;

        var attacker = new Unit("Marine") { Team = Team.Red }; // Damage:2, Range:1
        var target = new Unit("Marine") { Team = Team.Blue };  // HP:3, Armor:0
        target.Health = 1; // set low so one hit kills

        attackerTile.Unit = attacker;
        targetTile.Unit = target;

        var gm = new GameplayManager();
        gm.OnTileClicked(attackerTile, map); // select
        gm.OnTileClicked(targetTile, map);   // preview attack
        gm.OnTileClicked(targetTile, map);   // confirm (auto-executes)
        TickUntilDone(gm);

        Assert.Null(targetTile.Unit);
        Assert.Equal(0, attacker.AttacksRemaining);
    }

    // --- Elevation armor bonus tests ---

    [Fact]
    public void ElevationBonus_TargetAboveAttacker_GainsExtraArmor()
    {
        var map = CreateFlatMap();
        var attackerTile = map.Tiles[5][5];
        var targetTile = map.GetNeighbor(attackerTile, 0)!;

        // Set target tile higher
        targetTile.Elevation = 2;
        attackerTile.Elevation = 0;
        // Add ramp so attacker is adjacent but lower
        map.AddRamp(attackerTile, 0);

        var attacker = new Unit("Marine") { Team = Team.Red }; // Damage:2
        var target = new Unit("Marine") { Team = Team.Blue };  // HP:3, Armor:0

        attackerTile.Unit = attacker;
        targetTile.Unit = target;

        var gm = new GameplayManager();
        gm.OnTileClicked(attackerTile, map); // select
        gm.OnTileClicked(targetTile, map);   // preview attack
        gm.OnTileClicked(targetTile, map);   // confirm (auto-executes)
        TickUntilDone(gm);

        // Elevation bonus: armor 0→1, damage 2-1=1 effective, HP 3→2
        Assert.Equal(2, target.Health);
        Assert.Equal(1, target.Armor);
    }

    [Fact]
    public void FlyingAttacker_IgnoresElevationBonus()
    {
        var map = CreateFlatMap();
        var attackerTile = map.Tiles[5][5];
        var targetTile = map.GetNeighbor(attackerTile, 0)!;

        targetTile.Elevation = 2;
        attackerTile.Elevation = 0;

        var attacker = new Unit("Fighter") { Team = Team.Red }; // Damage:3
        var target = new Unit("Marine") { Team = Team.Blue };   // HP:3, Armor:0

        attackerTile.Unit = attacker;
        targetTile.Unit = target;

        var gm = new GameplayManager();
        gm.OnTileClicked(attackerTile, map); // select
        gm.OnTileClicked(targetTile, map);   // preview attack
        gm.OnTileClicked(targetTile, map);   // confirm (auto-executes)
        TickUntilDone(gm);

        // Flying attacker ignores elevation bonus, so damage applies directly: 3-0=3, HP 3→0
        Assert.Equal(0, target.Health);
        Assert.Null(targetTile.Unit);
    }

    [Fact]
    public void SameElevation_NoBonus()
    {
        var map = CreateFlatMap();
        var attackerTile = map.Tiles[5][5];
        var targetTile = map.GetNeighbor(attackerTile, 0)!;

        attackerTile.Elevation = 1;
        targetTile.Elevation = 1;

        var attacker = new Unit("Marine") { Team = Team.Red }; // Damage:2
        var target = new Unit("Marine") { Team = Team.Blue };  // HP:3, Armor:0

        attackerTile.Unit = attacker;
        targetTile.Unit = target;

        var gm = new GameplayManager();
        gm.OnTileClicked(attackerTile, map); // select
        gm.OnTileClicked(targetTile, map);   // preview attack
        gm.OnTileClicked(targetTile, map);   // confirm (auto-executes)
        TickUntilDone(gm);

        // No bonus, damage applies: 2-0=2, HP 3→1
        Assert.Equal(1, target.Health);
    }

    // --- Battlecruiser tests ---

    [Fact]
    public void Battlecruiser_HasCorrectStats()
    {
        var unit = new Unit("Battlecruiser");
        Assert.Equal(7, unit.MaxHealth);
        Assert.Equal(7, unit.Health);
        Assert.Equal(2, unit.Armor);
        Assert.Equal(4, unit.Damage);
        Assert.Equal(4, unit.Range);
        Assert.Equal(3, unit.MaxMovementPoints);
        Assert.Equal(2, unit.MaxAttacks);
        Assert.False(unit.CanTargetAir);
    }

    [Fact]
    public void Battlecruiser_IsFlying()
    {
        var unit = new Unit("Battlecruiser");
        Assert.True(unit.IsFlying);
    }

    [Fact]
    public void Battlecruiser_CanAttackTwicePerTurn()
    {
        var unit = new Unit("Battlecruiser");
        Assert.Equal(2, unit.AttacksRemaining);
        Assert.True(unit.CanAttack);

        unit.AttacksRemaining--;
        Assert.Equal(1, unit.AttacksRemaining);
        Assert.True(unit.CanAttack);

        unit.AttacksRemaining--;
        Assert.Equal(0, unit.AttacksRemaining);
        Assert.False(unit.CanAttack);
    }

    [Fact]
    public void Battlecruiser_CannotTargetAir()
    {
        var battlecruiser = new Unit("Battlecruiser");
        Assert.False(battlecruiser.CanTargetAir);
    }

    [Fact]
    public void Battlecruiser_ResetTurn_RestoresAttacks()
    {
        var unit = new Unit("Battlecruiser");
        unit.AttacksRemaining = 0;
        unit.MovementPoints = 0;
        unit.ResetTurn();

        Assert.Equal(2, unit.AttacksRemaining);
        Assert.Equal(3, unit.MovementPoints);
    }

    // --- IsFlying generic tests ---

    [Fact]
    public void Fighter_IsFlying()
    {
        var unit = new Unit("Fighter");
        Assert.True(unit.IsFlying);
    }

    [Fact]
    public void Marine_IsNotFlying()
    {
        var unit = new Unit("Marine");
        Assert.False(unit.IsFlying);
    }

    [Fact]
    public void Tank_IsNotFlying()
    {
        var unit = new Unit("Tank");
        Assert.False(unit.IsFlying);
    }

    // --- Turn system tests ---

    [Fact]
    public void EndTurn_SwitchesTeam()
    {
        var map = CreateFlatMap();
        var gm = new GameplayManager();
        Assert.Equal(Team.Red, gm.CurrentTeam);
        gm.EndTurn(map);
        Assert.Equal(Team.Blue, gm.CurrentTeam);
        gm.EndTurn(map);
        Assert.Equal(Team.Red, gm.CurrentTeam);
    }

    [Fact]
    public void EndTurn_OnlyResetsNewTeam()
    {
        var map = CreateFlatMap();
        var redTile = map.Tiles[3][3];
        var blueTile = map.Tiles[5][5];

        var redUnit = new Unit("Marine") { Team = Team.Red };
        var blueUnit = new Unit("Marine") { Team = Team.Blue };
        redUnit.MovementPoints = 0;
        redUnit.AttacksRemaining = 0;
        blueUnit.MovementPoints = 0;
        blueUnit.AttacksRemaining = 0;

        redTile.Unit = redUnit;
        blueTile.Unit = blueUnit;

        var gm = new GameplayManager();
        // Currently Red's turn. EndTurn switches to Blue.
        gm.EndTurn(map);

        // Blue's units should be reset
        Assert.Equal(blueUnit.MaxMovementPoints, blueUnit.MovementPoints);
        Assert.Equal(blueUnit.MaxAttacks, blueUnit.AttacksRemaining);
        // Red's units should NOT be reset
        Assert.Equal(0, redUnit.MovementPoints);
        Assert.Equal(0, redUnit.AttacksRemaining);
    }

    [Fact]
    public void ComputeVisibleTiles_IncludesUnitTileAndNeighbors()
    {
        var map = CreateFlatMap();
        var tile = map.Tiles[5][5];
        var unit = new Unit("Marine") { Team = Team.Red }; // Sight: 3
        tile.Unit = unit;

        var visible = GameplayManager.ComputeVisibleTiles(map, Team.Red);

        // Unit's own tile should be visible
        Assert.Contains(tile, visible);
        // Adjacent tiles should be visible (within sight range)
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var neighbor = map.GetNeighbor(tile, e);
            if (neighbor != null)
                Assert.Contains(neighbor, visible);
        }
    }

    [Fact]
    public void ComputeVisibleTiles_ExcludesEnemyUnits()
    {
        var map = CreateFlatMap();
        var redTile = map.Tiles[5][5];
        var blueTile = map.Tiles[0][0]; // far away
        redTile.Unit = new Unit("Marine") { Team = Team.Red };
        blueTile.Unit = new Unit("Marine") { Team = Team.Blue };

        var redVisible = GameplayManager.ComputeVisibleTiles(map, Team.Red);

        // Red's visibility should not include blue's far-away tile
        Assert.DoesNotContain(blueTile, redVisible);
    }

    [Fact]
    public void Sight_PropertyReadFromDef()
    {
        Assert.Equal(3, new Unit("Marine").Sight);
        Assert.Equal(5, new Unit("Fighter").Sight);
        Assert.Equal(4, new Unit("LandSpeeder").Sight);
        Assert.Equal(5, new Unit("AntiAirTurret").Sight);
        Assert.Equal(4, new Unit("Battlecruiser").Sight);
    }

    [Fact]
    public void Plan_MoveAttackMove_Sequence()
    {
        var map = CreateFlatMap();
        var unitTile = map.Tiles[5][5];
        var moveTile1 = map.GetNeighbor(unitTile, 0)!; // first move
        var enemyTile = map.GetNeighbor(moveTile1, 1)!; // enemy adjacent to move target
        var moveTile2 = map.GetNeighbor(moveTile1, 2)!; // second move (different neighbor)

        var attacker = new Unit("Marine") { Team = Team.Red }; // MP:2, Attacks:1, Range:1
        var enemy = new Unit("Marine") { Team = Team.Blue };
        enemy.Health = 1;

        unitTile.Unit = attacker;
        enemyTile.Unit = enemy;

        var gm = new GameplayManager();
        var state = new InteractionState();

        // 1. Select unit
        gm.OnTileClicked(unitTile, map);
        gm.Update(state);
        Assert.NotNull(state.ReachableTiles);
        Assert.Contains(moveTile1, state.ReachableTiles!);

        // 2. Plan move to moveTile1
        gm.OnTileClicked(moveTile1, map);
        gm.Update(state);
        Assert.NotNull(state.PlanSteps);
        Assert.NotNull(state.PlanAttackableTiles);

        // 3. Click attack on enemy — single click adds to plan immediately during planning
        gm.OnTileClicked(enemyTile, map);
        gm.Update(state);
        Assert.Null(state.PendingAttackTarget); // no preview needed during plan
        // Attack is planned, not executed — enemy still alive
        Assert.NotNull(enemyTile.Unit);
        // Should show reachable tiles for further moves
        Assert.NotNull(state.PlanReachableTiles);
        Assert.True(state.PlanReachableTiles!.Count > 0);

        // 4. Plan second move
        gm.OnTileClicked(moveTile2, map);
        gm.Update(state);
        Assert.NotNull(state.PlanSteps);
        Assert.Equal(2, state.PlanSteps!.Count); // two move steps

        // 5. Execute by clicking last move destination
        gm.OnTileClicked(moveTile2, map);
        TickUntilDone(gm);

        // Verify: unit moved to moveTile2, enemy dead, attacks used
        Assert.Equal(attacker, moveTile2.Unit);
        Assert.Null(unitTile.Unit);
        Assert.Null(enemyTile.Unit); // enemy killed
        Assert.Equal(0, attacker.AttacksRemaining);
        Assert.Equal(0, attacker.MovementPoints);
    }

    [Fact]
    public void MoveThrough_FriendlyUnit_NotRemoved()
    {
        var map = CreateFlatMap();
        var startTile = map.Tiles[5][5];
        var middleTile = map.GetNeighbor(startTile, 0)!;
        var endTile = map.GetNeighbor(middleTile, 0)!;

        var mover = new Unit("Marine") { Team = Team.Red };
        var friendly = new Unit("Marine") { Team = Team.Red };
        friendly.MovementPoints = 0; // can't move, won't be selectable

        startTile.Unit = mover;
        middleTile.Unit = friendly;

        // Plan path that goes through the friendly unit's tile
        var gm = new GameplayManager();
        gm.OnTileClicked(startTile, map);  // select mover
        gm.OnTileClicked(endTile, map);    // plan move to endTile (path goes through middleTile)
        gm.OnTileClicked(endTile, map);    // execute
        TickUntilDone(gm);

        // Mover should be at endTile, friendly should still be on middleTile
        Assert.Equal(mover, endTile.Unit);
        Assert.Equal(friendly, middleTile.Unit);
        Assert.Null(startTile.Unit);
    }

    [Fact]
    public void QueuedAttack_DealsDamage_NoArmor()
    {
        var map = CreateFlatMap();
        var attackerTile = map.Tiles[5][5];
        var targetTile = map.GetNeighbor(attackerTile, 0)!;

        var attacker = new Unit("Marine") { Team = Team.Red }; // Damage:2
        var target = new Unit("Marine") { Team = Team.Blue };  // HP:3, Armor:0

        attackerTile.Unit = attacker;
        targetTile.Unit = target;

        var gm = new GameplayManager();
        gm.OnTileClicked(attackerTile, map); // select
        gm.OnTileClicked(targetTile, map);   // preview
        gm.OnTileClicked(targetTile, map);   // confirm (auto-executes)
        TickUntilDone(gm);

        Assert.Equal(1, target.Health); // 3 - 2 = 1
        Assert.Equal(0, target.Armor);
    }

    [Fact]
    public void QueuedAttack_ArmorReducesDamage()
    {
        var map = CreateFlatMap();
        var attackerTile = map.Tiles[5][5];
        var targetTile = map.GetNeighbor(attackerTile, 0)!;

        var attacker = new Unit("Tank") { Team = Team.Red }; // Damage:4
        var target = new Unit("Tank") { Team = Team.Blue };  // HP:5, Armor:1

        attackerTile.Unit = attacker;
        targetTile.Unit = target;

        var gm = new GameplayManager();
        gm.OnTileClicked(attackerTile, map); // select
        gm.OnTileClicked(targetTile, map);   // preview
        gm.OnTileClicked(targetTile, map);   // confirm (auto-executes)
        TickUntilDone(gm);

        // Armor reduces: 4-1=3 effective, HP 5→2, armor stays at 1
        Assert.Equal(2, target.Health);
        Assert.Equal(1, target.Armor);
    }

    [Fact]
    public void QueuedAttack_MoveAttack_DealsDamage()
    {
        var map = CreateFlatMap();
        var unitTile = map.Tiles[5][5];
        var moveTile = map.GetNeighbor(unitTile, 0)!;
        var enemyTile = map.GetNeighbor(moveTile, 1)!;

        var attacker = new Unit("Marine") { Team = Team.Red }; // MP:2, Damage:2
        var enemy = new Unit("Marine") { Team = Team.Blue };   // HP:3, Armor:0

        unitTile.Unit = attacker;
        enemyTile.Unit = enemy;

        var gm = new GameplayManager();
        gm.OnTileClicked(unitTile, map);   // select
        gm.OnTileClicked(moveTile, map);   // plan move
        gm.OnTileClicked(enemyTile, map);  // add attack (immediate during plan)
        gm.OnTileClicked(enemyTile, map);  // execute (last target is attack)
        TickUntilDone(gm);

        Assert.Equal(1, enemy.Health); // 3 - 2 = 1
        Assert.Equal(attacker, moveTile.Unit);
    }

    [Fact]
    public void QueuedAttack_AttackMove_DealsDamage()
    {
        var map = CreateFlatMap();
        var unitTile = map.Tiles[5][5];
        var enemyTile = map.GetNeighbor(unitTile, 0)!;
        var moveTile = map.GetNeighbor(unitTile, 2)!;

        var attacker = new Unit("Marine") { Team = Team.Red }; // MP:2, Damage:2
        var enemy = new Unit("Marine") { Team = Team.Blue };   // HP:3, Armor:0

        unitTile.Unit = attacker;
        enemyTile.Unit = enemy;

        var gm = new GameplayManager();
        gm.OnTileClicked(unitTile, map);   // select
        gm.OnTileClicked(enemyTile, map);  // preview attack
        gm.OnTileClicked(moveTile, map);   // auto-confirm attack + plan move
        gm.OnTileClicked(moveTile, map);   // execute
        TickUntilDone(gm);

        Assert.Equal(1, enemy.Health); // 3 - 2 = 1
        Assert.Equal(attacker, moveTile.Unit);
    }

    // --- CommandCenter & Production tests ---

    [Fact]
    public void CommandCenter_HasCorrectStats()
    {
        var unit = new Unit("CommandCenter");
        Assert.Equal(15, unit.MaxHealth);
        Assert.Equal(1, unit.Armor);
        Assert.Equal(0, unit.Damage);
        Assert.Equal(0, unit.Range);
        Assert.Equal(0, unit.MaxMovementPoints);
        Assert.Equal(0, unit.MaxAttacks);
        Assert.False(unit.CanTargetAir);
        Assert.False(unit.CanTargetGround);
        Assert.Equal(3, unit.Sight);
    }

    [Fact]
    public void CommandCenter_CanProduce()
    {
        var cc = new Unit("CommandCenter");
        Assert.True(cc.CanProduce);
    }

    [Fact]
    public void CommandCenter_CannotAttackOrMove()
    {
        var cc = new Unit("CommandCenter");
        Assert.False(cc.CanMove);
        Assert.False(cc.CanAttack);
    }

    [Fact]
    public void ProductionTime_LoadedFromDefs()
    {
        Assert.Equal(1, UnitDefs.Get("Marine").ProductionTime);
        Assert.Equal(1, UnitDefs.Get("Tank").ProductionTime);
        Assert.Equal(1, UnitDefs.Get("Fighter").ProductionTime);
        Assert.Equal(1, UnitDefs.Get("LandSpeeder").ProductionTime);
        Assert.Equal(1, UnitDefs.Get("Bunker").ProductionTime);
        Assert.Equal(1, UnitDefs.Get("AntiAirTurret").ProductionTime);
        Assert.Equal(1, UnitDefs.Get("Battlecruiser").ProductionTime);
        Assert.Equal(3, UnitDefs.Get("CommandCenter").ProductionTime);
    }

    [Fact]
    public void StartProduction_SetsFields()
    {
        var cc = new Unit("CommandCenter");
        cc.StartProduction("Marine");

        Assert.True(cc.IsProducing);
        Assert.Equal("Marine", cc.ProducingType);
        Assert.Equal(1, cc.ProductionTurnsLeft);
    }

    [Fact]
    public void CancelProduction_ClearsFields()
    {
        var cc = new Unit("CommandCenter");
        cc.StartProduction("Tank");
        cc.CancelProduction();

        Assert.False(cc.IsProducing);
        Assert.Null(cc.ProducingType);
        Assert.Equal(0, cc.ProductionTurnsLeft);
    }

    [Fact]
    public void EndTurn_AdvancesProduction_SpawnsUnit()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        cc.StartProduction("Marine"); // 1 turn
        ccTile.Unit = cc;

        var gm = new GameplayManager();
        // Red's turn → EndTurn switches to Blue (resets Blue, no production for Red)
        // Then EndTurn again switches to Red and advances Red's production
        gm.EndTurn(map); // → Blue's turn
        gm.EndTurn(map); // → Red's turn, advances Red production

        // Marine should be spawned on a free adjacent tile
        Assert.False(cc.IsProducing);
        bool found = false;
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var n = map.GetNeighbor(ccTile, e);
            if (n?.Unit != null && n.Unit.Type == "Marine" && n.Unit.Team == Team.Red)
            {
                found = true;
                // Spawned unit gets reset along with all team units
                Assert.Equal(n.Unit.MaxMovementPoints, n.Unit.MovementPoints);
                Assert.Equal(n.Unit.MaxAttacks, n.Unit.AttacksRemaining);
                break;
            }
        }
        Assert.True(found, "Produced Marine not found on adjacent tile");
    }

    [Fact]
    public void Production_BlockedWhenNoFreeAdjacentTile()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        cc.StartProduction("Marine"); // 1 turn
        ccTile.Unit = cc;

        // Fill all adjacent tiles
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var n = map.GetNeighbor(ccTile, e);
            if (n != null) n.Unit = new Unit("Marine") { Team = Team.Red };
        }

        var gm = new GameplayManager();
        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red, tries to spawn but can't

        // Still producing, turns left at 0 (waiting for free tile)
        Assert.True(cc.IsProducing);
        Assert.Equal(0, cc.ProductionTurnsLeft);
    }

    [Fact]
    public void Production_Tank_CompletesInOneTurn()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        cc.StartProduction("Tank"); // 1 turn
        ccTile.Unit = cc;

        var gm = new GameplayManager();

        // Red → Blue → Red (production 1→0, spawn)
        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red, production done

        Assert.False(cc.IsProducing);
        bool found = false;
        for (int e = 0; e < map.EdgeCount; e++)
        {
            var n = map.GetNeighbor(ccTile, e);
            if (n?.Unit != null && n.Unit.Type == "Tank" && n.Unit.Team == Team.Red)
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Produced Tank not found on adjacent tile");
    }

    [Fact]
    public void CommandCenter_IsSelectable()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        ccTile.Unit = cc;

        var gm = new GameplayManager();
        var state = new InteractionState();
        gm.OnTileClicked(ccTile, map);
        gm.Update(state);

        Assert.Equal(ccTile, state.SelectedUnitTile);
        Assert.Equal(cc, state.SelectedUnit);
    }

    // --- Resource system tests ---

    [Fact]
    public void BaseIncome_Adds3EachPerTurn()
    {
        var map = CreateFlatMap();
        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        Assert.Equal((3, 3), gm.GetResources(Team.Red));

        // End Red turn → Blue gets income (3 base + 3 start)
        gm.EndTurn(map);
        Assert.Equal((6, 6), gm.GetResources(Team.Blue));
        Assert.Equal((3, 3), gm.GetResources(Team.Red));

        // End Blue turn → Red gets income (3 base + 3 start)
        gm.EndTurn(map);
        Assert.Equal((6, 6), gm.GetResources(Team.Red));
    }

    [Fact]
    public void MineIncome_Adds3OfResourceType()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var mineTile = map.GetNeighbor(ccTile, 0)!;

        var cc = new Unit("CommandCenter") { Team = Team.Red };
        ccTile.Unit = cc;

        mineTile.Resource = Tiles.ResourceType.Iron;
        mineTile.Unit = new Unit("Mine") { Team = Team.Red };

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // End Red → Blue (Blue gets base income only)
        gm.EndTurn(map);
        // End Blue → Red (Red gets base 3 + mine 3 iron; starting 3I+3F)
        gm.EndTurn(map);

        var res = gm.GetResources(Team.Red);
        Assert.Equal(9, res.Iron);   // 3 start + 3 base + 3 mine
        Assert.Equal(6, res.Fissium); // 3 start + 3 base
    }

    [Fact]
    public void MineIncome_FissiumMine()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var mineTile = map.GetNeighbor(ccTile, 0)!;

        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Blue };
        mineTile.Resource = Tiles.ResourceType.Fissium;
        mineTile.Unit = new Unit("Mine") { Team = Team.Blue };

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // End Red → Blue (Blue gets base 3 + mine 3 fissium; starting 3I+3F)
        gm.EndTurn(map);

        var res = gm.GetResources(Team.Blue);
        Assert.Equal(6, res.Iron);   // 3 start + 3 base
        Assert.Equal(9, res.Fissium); // 3 start + 3 base + 3 mine
    }

    [Fact]
    public void MineIncome_NotAdjacentToCC_NoBonus()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var farTile = map.Tiles[0][0]; // not adjacent to CC

        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };
        farTile.Resource = Tiles.ResourceType.Iron;
        farTile.Unit = new Unit("Mine") { Team = Team.Red };

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red, income

        var res = gm.GetResources(Team.Red);
        Assert.Equal(6, res.Iron); // 3 start + 3 base, no mine bonus
    }

    [Fact]
    public void Costs_DeductedOnProductionStart()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        ccTile.Unit = cc;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Select CC and start production (starts with 3I, 3F)
        gm.OnTileClicked(ccTile, map);
        gm.StartProduction("Marine"); // costs 1I

        var res = gm.GetResources(Team.Red);
        Assert.Equal(2, res.Iron);   // 3 - 1
        Assert.Equal(3, res.Fissium); // unchanged
        Assert.True(cc.IsProducing);
    }

    [Fact]
    public void CannotBuild_WhenCantAfford()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        ccTile.Unit = cc;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Starts with 3I, 3F — can afford Tank (3I+2F) but not Battlecruiser (5I+5F)
        Assert.True(gm.CanAfford("Tank", Team.Red));
        Assert.False(gm.CanAfford("Battlecruiser", Team.Red));

        gm.OnTileClicked(ccTile, map);
        gm.StartProduction("Battlecruiser"); // should fail silently

        Assert.False(cc.IsProducing); // didn't start
    }

    [Fact]
    public void CancelProduction_RefundsResources()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        ccTile.Unit = cc;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Starts with 3I, 3F
        gm.OnTileClicked(ccTile, map);
        gm.StartProduction("LandSpeeder"); // costs 2I+1F

        var res = gm.GetResources(Team.Red);
        Assert.Equal(1, res.Iron);   // 3 - 2
        Assert.Equal(2, res.Fissium); // 3 - 1

        gm.CancelProduction();

        res = gm.GetResources(Team.Red);
        Assert.Equal(3, res.Iron);   // refunded
        Assert.Equal(3, res.Fissium); // refunded
        Assert.False(cc.IsProducing);
    }

    [Fact]
    public void MinePlacement_ValidTilesAdjacentToCC()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var resourceTile = map.GetNeighbor(ccTile, 0)!;
        var emptyTile = map.GetNeighbor(ccTile, 1)!; // no resource

        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };
        resourceTile.Resource = Tiles.ResourceType.Iron;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Starts with 3I, 3F — need 5I for mine, so earn more
        gm.EndTurn(map); gm.EndTurn(map); // Red gets +3I +3F → 6I, 6F

        gm.OnTileClicked(ccTile, map);
        gm.EnterMineMode();

        Assert.True(gm.IsMineMode);

        var state = new InteractionState();
        gm.Update(state);

        Assert.NotNull(state.MinePlacementTiles);
        Assert.Contains(resourceTile, state.MinePlacementTiles!);
        Assert.DoesNotContain(emptyTile, state.MinePlacementTiles!);
    }

    [Fact]
    public void MineSpawns_ImmediatelyUnderConstruction()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var resourceTile = map.GetNeighbor(ccTile, 0)!;

        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };
        resourceTile.Resource = Tiles.ResourceType.Fissium;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Starts with 3I, need 5I for mine — earn more
        gm.EndTurn(map); gm.EndTurn(map); // Red gets +3I → 6I

        // Select CC, enter mine mode, click resource tile
        gm.OnTileClicked(ccTile, map);
        gm.EnterMineMode();
        gm.OnTileClicked(resourceTile, map);

        // Mine spawns immediately on tile under construction (CC freed)
        Assert.False(ccTile.Unit!.IsProducing);
        Assert.NotNull(resourceTile.Unit);
        Assert.Equal("Mine", resourceTile.Unit!.Type);
        Assert.True(resourceTile.Unit.IsUnderConstruction);
        Assert.Equal(1, resourceTile.Unit.Health); // starts at 1 HP

        // Advance: Red → Blue → Red (mine construction completes after 1 turn)
        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red, construction advances

        Assert.False(resourceTile.Unit.IsUnderConstruction);
        Assert.Equal(resourceTile.Unit.MaxHealth, resourceTile.Unit.Health);
        Assert.Equal(Team.Red, resourceTile.Unit.Team);
    }

    [Fact]
    public void MineUpgrade_IncreasesLevel_Costs3Iron()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var mineTile = map.GetNeighbor(ccTile, 0)!;

        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };
        mineTile.Resource = Tiles.ResourceType.Iron;
        var mine = new Unit("Mine") { Team = Team.Red };
        mineTile.Unit = mine;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Starts with 3I — earn more (base 3 + mine 3 iron)
        gm.EndTurn(map); gm.EndTurn(map); // Red: 3 + 3 + 3 = 9I, 3 + 3 = 6F

        Assert.Equal(1, mine.MineLevel);

        // Select mine, upgrade (costs 3I)
        gm.OnTileClicked(mineTile, map);
        gm.UpgradeMine();

        Assert.Equal(2, mine.MineLevel);
        var res = gm.GetResources(Team.Red);
        Assert.Equal(6, res.Iron); // 9 - 3
    }

    [Fact]
    public void MineUpgrade_Level3Max()
    {
        var map = CreateFlatMap();
        var mineTile = map.Tiles[5][5];
        var mine = new Unit("Mine") { Team = Team.Red };
        mine.MineLevel = 3;
        mineTile.Unit = mine;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Give resources
        gm.EndTurn(map); gm.EndTurn(map);

        gm.OnTileClicked(mineTile, map);
        gm.UpgradeMine(); // should do nothing at max level

        Assert.Equal(3, mine.MineLevel); // stays at 3
    }

    [Fact]
    public void MineUpgrade_IncreasesIncome()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var mineTile = map.GetNeighbor(ccTile, 0)!;

        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };
        mineTile.Resource = Tiles.ResourceType.Iron;
        var mine = new Unit("Mine") { Team = Team.Red };
        mine.MineLevel = 2; // level 2 = 2+2 = 4 production
        mineTile.Unit = mine;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red: 3 start + base 3 + mine 4 = 10 iron, 3+3 = 6 fissium

        var res = gm.GetResources(Team.Red);
        Assert.Equal(10, res.Iron);
        Assert.Equal(6, res.Fissium);
    }

    [Fact]
    public void UnitCosts_LoadedFromDefs()
    {
        Assert.Equal(1, UnitDefs.Get("Marine").CostIron);
        Assert.Equal(0, UnitDefs.Get("Marine").CostFissium);
        Assert.Equal(2, UnitDefs.Get("LandSpeeder").CostIron);
        Assert.Equal(1, UnitDefs.Get("LandSpeeder").CostFissium);
        Assert.Equal(3, UnitDefs.Get("Tank").CostIron);
        Assert.Equal(2, UnitDefs.Get("Tank").CostFissium);
        Assert.Equal(2, UnitDefs.Get("Fighter").CostIron);
        Assert.Equal(3, UnitDefs.Get("Fighter").CostFissium);
        Assert.Equal(5, UnitDefs.Get("Battlecruiser").CostIron);
        Assert.Equal(5, UnitDefs.Get("Battlecruiser").CostFissium);
        Assert.Equal(3, UnitDefs.Get("Bunker").CostIron);
        Assert.Equal(0, UnitDefs.Get("Bunker").CostFissium);
        Assert.Equal(2, UnitDefs.Get("AntiAirTurret").CostIron);
        Assert.Equal(0, UnitDefs.Get("AntiAirTurret").CostFissium);
        Assert.Equal(10, UnitDefs.Get("CommandCenter").CostIron);
        Assert.Equal(0, UnitDefs.Get("CommandCenter").CostFissium);
        Assert.Equal(5, UnitDefs.Get("Mine").CostIron);
        Assert.Equal(0, UnitDefs.Get("Mine").CostFissium);
    }

    [Fact]
    public void Mine_HasCorrectStats()
    {
        var unit = new Unit("Mine");
        Assert.Equal(5, unit.MaxHealth);
        Assert.Equal(1, unit.Armor);
        Assert.Equal(0, unit.Damage);
        Assert.Equal(0, unit.Range);
        Assert.Equal(0, unit.MaxMovementPoints);
        Assert.Equal(0, unit.MaxAttacks);
        Assert.Equal(1, unit.Sight);
        Assert.Equal(1, unit.MineLevel);
    }

    [Fact]
    public void CanAfford_ChecksBothResources()
    {
        var map = CreateFlatMap();
        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Red starts with 3I, 3F
        Assert.True(gm.CanAfford("Marine", Team.Red));     // needs 1I
        Assert.True(gm.CanAfford("LandSpeeder", Team.Red)); // needs 2I+1F
        Assert.True(gm.CanAfford("Tank", Team.Red));        // needs 3I+2F
        Assert.False(gm.CanAfford("Battlecruiser", Team.Red)); // needs 5I+5F
    }

    // --- Construction system tests ---

    [Fact]
    public void IsBuilding_LoadedFromDefs()
    {
        Assert.True(UnitDefs.Get("CommandCenter").IsBuilding);
        Assert.True(UnitDefs.Get("Mine").IsBuilding);
        Assert.True(UnitDefs.Get("Bunker").IsBuilding);
        Assert.True(UnitDefs.Get("AntiAirTurret").IsBuilding);
        Assert.False(UnitDefs.Get("Marine").IsBuilding);
        Assert.False(UnitDefs.Get("Tank").IsBuilding);
        Assert.False(UnitDefs.Get("Fighter").IsBuilding);
    }

    [Fact]
    public void StartConstruction_SetsState()
    {
        var unit = new Unit("Bunker");
        unit.StartConstruction();

        Assert.True(unit.IsUnderConstruction);
        Assert.Equal(1, unit.ConstructionTotalTurns);
        Assert.Equal(1, unit.ConstructionTurnsLeft);
        Assert.Equal(1, unit.Health); // starts at 1 HP
    }

    [Fact]
    public void AdvanceConstruction_CompletesAndRestoresHealth()
    {
        var unit = new Unit("Bunker"); // HP:5, ProductionTime:1
        unit.StartConstruction();

        Assert.True(unit.IsUnderConstruction);
        unit.AdvanceConstruction();

        Assert.False(unit.IsUnderConstruction);
        Assert.Equal(unit.MaxHealth, unit.Health);
    }

    [Fact]
    public void Construction_MultiTurn_ProportionalHealth()
    {
        var unit = new Unit("CommandCenter"); // HP:15, ProductionTime:3
        unit.StartConstruction();

        Assert.Equal(3, unit.ConstructionTotalTurns);
        Assert.Equal(1, unit.Health);

        unit.AdvanceConstruction(); // 1 of 3 done
        Assert.True(unit.IsUnderConstruction);
        Assert.Equal(5, unit.Health); // ceil(1/3 * 15) = 5

        unit.AdvanceConstruction(); // 2 of 3 done
        Assert.True(unit.IsUnderConstruction);
        Assert.Equal(10, unit.Health); // ceil(2/3 * 15) = 10

        unit.AdvanceConstruction(); // 3 of 3 done
        Assert.False(unit.IsUnderConstruction);
        Assert.Equal(15, unit.Health); // full
    }

    [Fact]
    public void UnderConstruction_CannotAttack()
    {
        var unit = new Unit("Bunker");
        Assert.True(unit.CanAttack);
        unit.StartConstruction();
        Assert.False(unit.CanAttack);
    }

    [Fact]
    public void UnderConstruction_CannotProduce()
    {
        var unit = new Unit("CommandCenter");
        Assert.True(unit.CanProduce);
        unit.StartConstruction();
        Assert.False(unit.CanProduce);
    }

    [Fact]
    public void BuildEligible_Within2OfCC()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Adjacent tile (range 1) — eligible
        var adjacentTile = map.GetNeighbor(ccTile, 0)!;
        Assert.True(gm.IsBuildEligible(adjacentTile, map, Team.Red));

        // Range 2 tile — eligible
        var range2Tile = map.GetNeighbor(adjacentTile, 0)!;
        Assert.True(gm.IsBuildEligible(range2Tile, map, Team.Red));

        // Far tile — not eligible
        var farTile = map.Tiles[0][0];
        Assert.False(gm.IsBuildEligible(farTile, map, Team.Red));
    }

    [Fact]
    public void BuildEligible_AdjacentToFriendlyUnit()
    {
        var map = CreateFlatMap();
        var unitTile = map.Tiles[5][5];
        unitTile.Unit = new Unit("Marine") { Team = Team.Red };

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Adjacent to marine — eligible
        var adjacentTile = map.GetNeighbor(unitTile, 0)!;
        Assert.True(gm.IsBuildEligible(adjacentTile, map, Team.Red));

        // 2 tiles from marine (no CC) — not eligible
        var range2Tile = map.GetNeighbor(adjacentTile, 0)!;
        Assert.False(gm.IsBuildEligible(range2Tile, map, Team.Red));
    }

    [Fact]
    public void BuildEligible_NotAdjacentToUnderConstructionUnit()
    {
        var map = CreateFlatMap();
        var unitTile = map.Tiles[5][5];
        var bunker = new Unit("Bunker") { Team = Team.Red };
        bunker.StartConstruction();
        unitTile.Unit = bunker;

        var gm = new GameplayManager();

        var adjacentTile = map.GetNeighbor(unitTile, 0)!;
        Assert.False(gm.IsBuildEligible(adjacentTile, map, Team.Red));
    }

    [Fact]
    public void BuildEligible_ContestedByAdjacentEnemy()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };

        var buildTile = map.GetNeighbor(ccTile, 0)!;
        var enemyTile = map.GetNeighbor(buildTile, 1)!;
        enemyTile.Unit = new Unit("Marine") { Team = Team.Blue };

        var gm = new GameplayManager();

        // Adjacent to CC but also adjacent to enemy — contested, cannot build
        Assert.False(gm.IsBuildEligible(buildTile, map, Team.Red));
    }

    [Fact]
    public void BuildEligible_BlockedByResource()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };

        var resourceTile = map.GetNeighbor(ccTile, 0)!;
        resourceTile.Resource = Tiles.ResourceType.Iron;

        var gm = new GameplayManager();
        Assert.False(gm.IsBuildEligible(resourceTile, map, Team.Red));
    }

    [Fact]
    public void BuildEligible_BlockedByUnit()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        ccTile.Unit = new Unit("CommandCenter") { Team = Team.Red };

        var occupiedTile = map.GetNeighbor(ccTile, 0)!;
        occupiedTile.Unit = new Unit("Marine") { Team = Team.Red };

        var gm = new GameplayManager();
        Assert.False(gm.IsBuildEligible(occupiedTile, map, Team.Red));
    }

    [Fact]
    public void BuildEligible_UnderConstructionCC_NotCounted()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        cc.StartConstruction();
        ccTile.Unit = cc;

        var gm = new GameplayManager();

        var adjacentTile = map.GetNeighbor(ccTile, 0)!;
        Assert.False(gm.IsBuildEligible(adjacentTile, map, Team.Red));
    }

    [Fact]
    public void CancelBuildingConstruction_RefundsResources()
    {
        var map = CreateFlatMap();
        var friendlyTile = map.Tiles[5][5];
        friendlyTile.Unit = new Unit("CommandCenter") { Team = Team.Red };

        var buildTile = map.GetNeighbor(friendlyTile, 0)!;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Earn resources for Bunker (3I)
        var startRes = gm.GetResources(Team.Red); // 3I, 3F

        // Click the build tile to select it
        gm.OnTileClicked(buildTile, map);
        var state = new InteractionState();
        gm.Update(state);
        Assert.NotNull(state.SelectedBuildTile);

        // Place a bunker
        gm.PlaceBuilding("Bunker");
        Assert.NotNull(buildTile.Unit);
        Assert.True(buildTile.Unit!.IsUnderConstruction);

        var resAfterBuild = gm.GetResources(Team.Red);
        Assert.Equal(startRes.Iron - 3, resAfterBuild.Iron);

        // Select the under-construction building and cancel
        gm.OnTileClicked(buildTile, map);
        gm.CancelBuildingConstruction();

        Assert.Null(buildTile.Unit);
        var resAfterCancel = gm.GetResources(Team.Red);
        Assert.Equal(startRes.Iron, resAfterCancel.Iron);
    }

    [Fact]
    public void Construction_CompletesViaEndTurn()
    {
        var map = CreateFlatMap();
        var friendlyTile = map.Tiles[5][5];
        friendlyTile.Unit = new Unit("CommandCenter") { Team = Team.Red };

        var buildTile = map.GetNeighbor(friendlyTile, 0)!;

        var gm = new GameplayManager();
        gm.StartGame(map, GameMode.HotSeat);

        // Select build tile and place bunker (1 turn construction)
        gm.OnTileClicked(buildTile, map);
        gm.PlaceBuilding("Bunker");

        Assert.True(buildTile.Unit!.IsUnderConstruction);

        // End turns: Red→Blue→Red (advances Red construction)
        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red, construction completes

        Assert.False(buildTile.Unit.IsUnderConstruction);
        Assert.Equal(buildTile.Unit.MaxHealth, buildTile.Unit.Health);
    }
}
