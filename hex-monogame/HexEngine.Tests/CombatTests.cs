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
        Assert.Equal(2, UnitDefs.Get("Tank").ProductionTime);
        Assert.Equal(2, UnitDefs.Get("Fighter").ProductionTime);
        Assert.Equal(1, UnitDefs.Get("LandSpeeder").ProductionTime);
        Assert.Equal(2, UnitDefs.Get("Bunker").ProductionTime);
        Assert.Equal(2, UnitDefs.Get("AntiAirTurret").ProductionTime);
        Assert.Equal(3, UnitDefs.Get("Battlecruiser").ProductionTime);
        Assert.Equal(0, UnitDefs.Get("CommandCenter").ProductionTime);
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
    public void MultiTurnProduction_Tank()
    {
        var map = CreateFlatMap();
        var ccTile = map.Tiles[5][5];
        var cc = new Unit("CommandCenter") { Team = Team.Red };
        cc.StartProduction("Tank"); // 2 turns
        ccTile.Unit = cc;

        var gm = new GameplayManager();

        // Turn 1: Red → Blue → Red (advances production by 1)
        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red, production 2→1

        Assert.True(cc.IsProducing);
        Assert.Equal(1, cc.ProductionTurnsLeft);

        // Turn 2: Red → Blue → Red (production done, spawns)
        gm.EndTurn(map); // → Blue
        gm.EndTurn(map); // → Red, production 1→0, spawn

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
}
