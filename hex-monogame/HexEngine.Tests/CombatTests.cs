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

    [Fact]
    public void TakeDamage_ReducesHealth()
    {
        var unit = new Unit("Marine"); // HP:3, Armor:0
        unit.TakeDamage(2);
        Assert.Equal(1, unit.Health);
    }

    [Fact]
    public void Armor_AbsorbsFullAttack_HealthUnchanged()
    {
        var unit = new Unit("Tank"); // HP:5, Armor:1
        int healthBefore = unit.Health;
        unit.TakeDamage(4);
        Assert.Equal(healthBefore, unit.Health);
        Assert.Equal(0, unit.Armor);
    }

    [Fact]
    public void AfterArmorConsumed_DamageApplies()
    {
        var unit = new Unit("Fighter"); // HP:4, Armor:1
        unit.TakeDamage(3); // absorbs (armor 1->0)
        Assert.Equal(4, unit.Health);
        unit.TakeDamage(3); // now hits health
        Assert.Equal(1, unit.Health);
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
        var unit = new Unit("Marine");
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
        gm.OnTileClicked(targetTile, map);   // attack

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
        gm.OnTileClicked(targetTile, map);   // attack

        // Elevation bonus: armor goes 0->1, then TakeDamage absorbs (1->0), health unchanged
        Assert.Equal(3, target.Health);
        Assert.Equal(0, target.Armor);
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
        gm.OnTileClicked(attackerTile, map);
        gm.OnTileClicked(targetTile, map);

        // Flying attacker ignores elevation bonus, so damage applies directly: 3-3=0
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
        gm.OnTileClicked(attackerTile, map);
        gm.OnTileClicked(targetTile, map);

        // No bonus, damage applies: 3-2=1
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
}
