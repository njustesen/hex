using System.Collections.Generic;
using HexEngine.Tiles;

namespace HexEngine.Core;

public class InteractionState
{
    public Tile? HoverTile { get; set; }
    public Tile? SelectedTile { get; set; }
    public bool Dragging { get; set; }
    public int? HighlightedEdgeIndex { get; set; }
    public Tile? HighlightedEdgeTile { get; set; }
    public float InnerShapeScale { get; set; }

    // Gameplay highlights
    public Tile? SelectedUnitTile { get; set; }
    public HashSet<Tile>? ReachableTiles { get; set; }
    public List<Tile>? PlannedPath { get; set; }
    public HashSet<Tile>? AttackableTiles { get; set; }

    // Multi-step plan visualization
    public List<Tile>? PlanSteps { get; set; }
    public List<List<Tile>>? PlanPaths { get; set; }
    public HashSet<Tile>? PlanReachableTiles { get; set; }
    public HashSet<Tile>? PlanAttackableTiles { get; set; }

    // Planned attacks (confirmed in plan, shown as crosshairs)
    public List<(Tile Source, Tile Target)>? PlanAttackPairs { get; set; }

    // Damage preview per target tile during plan
    public Dictionary<Tile, (int CurrentHealth, int ProjectedHealth, int MaxHealth, int AttackCount)>? PlanDamagePreview { get; set; }

    // Pending attack (shown before confirmation)
    public Tile? PendingAttackTarget { get; set; }
    public (int CurrentHealth, int ProjectedHealth, int MaxHealth)? PendingAttackDamagePreview { get; set; }

    // Executing unit overlay (drawn when unit passes through occupied tiles)
    public Tile? ExecUnitTile { get; set; }
    public Unit? ExecUnit { get; set; }

    // Animation
    public float AnimationTimer { get; set; }
    public Tile? AnimationSourceTile { get; set; }
    public Tile? AnimationTargetTile { get; set; }

    // Turn & visibility
    public Team CurrentTeam { get; set; }
    public HashSet<Tile>? VisibleTiles { get; set; }

    // Mode
    public bool IsEditor { get; set; }

    // Turn
    public int TurnNumber { get; set; }

    // Resources
    public int TeamIron { get; set; }
    public int TeamFissium { get; set; }
    public int TeamIronIncome { get; set; }
    public int TeamFissiumIncome { get; set; }
    public List<Tile>? MinePlacementTiles { get; set; }

    // Build tile selection (for placing buildings on empty tiles)
    public Tile? SelectedBuildTile { get; set; }
    public HashSet<Tile>? BuildableTiles { get; set; }

    // Derived
    public Unit? SelectedUnit => SelectedUnitTile?.Unit;
}
