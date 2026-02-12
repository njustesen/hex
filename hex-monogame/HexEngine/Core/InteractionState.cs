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
}
