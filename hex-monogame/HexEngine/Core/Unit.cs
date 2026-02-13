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
    public bool CanAttack => AttacksRemaining > 0 && Health > 0;
    public bool IsAlive => Health > 0;
    public bool IsFlying { get; }
    public int Sight { get; }

    // Production
    public bool CanProduce => Type == "CommandCenter";
    public string? ProducingType { get; set; }
    public int ProductionTurnsLeft { get; set; }
    public bool IsProducing => ProducingType != null;

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
