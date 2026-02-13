using System;

namespace HexEngine.Core;

public enum Team { Red, Blue }

public class Unit
{
    public string Name { get; set; } = "Brian";
    public string Type { get; }
    public Team Team { get; set; } = Team.Red;
    public int MaxMovementPoints { get; }
    public int MovementPoints { get; set; }
    public bool CanMove => MovementPoints > 0;

    // Combat stats
    public int MaxHealth { get; }
    public int Health { get; set; }
    public int Armor { get; set; }
    public int Damage { get; }
    public int Range { get; }
    public bool CanTargetAir { get; }
    public bool CanTargetGround { get; }
    public int MaxAttacks { get; }
    public int AttacksRemaining { get; set; }
    public bool CanAttack => AttacksRemaining > 0 && Health > 0 && !IsUnderConstruction;
    public bool IsAlive => Health > 0;
    public bool IsFlying { get; }
    public int Sight { get; }

    // Production
    public bool CanProduce => Type == "CommandCenter" && !IsUnderConstruction;
    public string? ProducingType { get; set; }
    public int ProductionTurnsLeft { get; set; }
    public bool IsProducing => ProducingType != null;
    public (int X, int Y)? MineTargetCoords { get; set; }

    // Mine levels (1 = base, 2 = level II, 3 = level III)
    public int MineLevel { get; set; } = 1;

    // Construction state (for buildings placed on the map)
    public bool IsUnderConstruction { get; set; }
    public int ConstructionTurnsLeft { get; set; }
    public int ConstructionTotalTurns { get; set; }

    public void StartConstruction()
    {
        var def = UnitDefs.Get(Type);
        IsUnderConstruction = true;
        ConstructionTotalTurns = def.ProductionTime;
        ConstructionTurnsLeft = def.ProductionTime;
        Health = Math.Max(1, 0);
    }

    public void AdvanceConstruction()
    {
        if (!IsUnderConstruction) return;
        ConstructionTurnsLeft--;
        int elapsed = ConstructionTotalTurns - ConstructionTurnsLeft;
        Health = (int)Math.Ceiling((float)elapsed / ConstructionTotalTurns * MaxHealth);
        if (ConstructionTurnsLeft <= 0)
        {
            IsUnderConstruction = false;
            Health = MaxHealth;
        }
    }

    public void StartProduction(string type)
    {
        ProducingType = type;
        ProductionTurnsLeft = UnitDefs.Get(type).ProductionTime;
    }

    public void CancelProduction()
    {
        ProducingType = null;
        ProductionTurnsLeft = 0;
    }

    public Unit() : this("Marine") { }

    public Unit(string type)
    {
        Type = type;
        var def = UnitDefs.Get(type);

        MaxMovementPoints = def.Movement;
        MovementPoints = MaxMovementPoints;
        MaxHealth = def.Health;
        Health = MaxHealth;
        Armor = def.Armor;
        Damage = def.Damage;
        Range = def.Range;
        CanTargetAir = def.CanTargetAir;
        CanTargetGround = def.CanTargetGround;
        MaxAttacks = def.MaxAttacks;
        AttacksRemaining = MaxAttacks;
        IsFlying = def.Flying;
        Sight = def.Sight;
    }

    public void TakeDamage(int damage)
    {
        int effective = Math.Max(damage - Armor, 0);
        Health = Math.Max(Health - effective, 0);
    }

    public void ResetTurn()
    {
        MovementPoints = MaxMovementPoints;
        AttacksRemaining = MaxAttacks;
    }
}
