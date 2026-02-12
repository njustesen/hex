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

    // Animation
    public float AnimationTimer { get; set; }
    public Tile? AnimationSourceTile { get; set; }
    public Tile? AnimationTargetTile { get; set; }
}
