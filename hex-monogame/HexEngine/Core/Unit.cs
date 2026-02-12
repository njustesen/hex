namespace HexEngine.Core;

public enum UnitType { Marine, Tank, Fighter }

public class Unit
{
    public string Name { get; set; } = "Brian";
    public UnitType Type { get; }
    public int MaxMovementPoints { get; }
    public int MovementPoints { get; set; }
    public bool CanMove => MovementPoints > 0;

    public Unit() : this(UnitType.Marine) { }

    public Unit(UnitType type)
    {
        Type = type;
        MaxMovementPoints = type switch
        {
            UnitType.Marine => 2,
            UnitType.Tank => 3,
            UnitType.Fighter => 4,
            _ => 2
        };
        MovementPoints = MaxMovementPoints;
    }

    public void ResetMovementPoints() => MovementPoints = MaxMovementPoints;
}
