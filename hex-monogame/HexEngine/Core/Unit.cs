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
    public int MaxAttacks { get; }
    public int AttacksRemaining { get; set; }
    public bool CanAttack => AttacksRemaining > 0 && Health > 0;
    public bool IsAlive => Health > 0;
    public bool IsFlying { get; }

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
        MaxAttacks = def.MaxAttacks;
        AttacksRemaining = MaxAttacks;
        IsFlying = def.Flying;
    }

    public void TakeDamage(int damage)
    {
        if (Armor > 0)
        {
            Armor--;
            return;
        }
        Health = Math.Max(Health - damage, 0);
    }

    public void ResetTurn()
    {
        MovementPoints = MaxMovementPoints;
        AttacksRemaining = MaxAttacks;
    }
}
