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
}
