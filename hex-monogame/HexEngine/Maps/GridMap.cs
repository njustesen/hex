using Microsoft.Xna.Framework;
using HexEngine.Tiles;

namespace HexEngine.Maps;

public abstract class GridMap
{
    public int Cols { get; }
    public int Rows { get; }
    public Tile[][] Tiles { get; protected set; }

    protected GridMap(int cols, int rows)
    {
        Cols = cols;
        Rows = rows;
        Tiles = System.Array.Empty<Tile[]>();
    }

    public abstract Tile GetNearestTile(Vector2 pos);
    public abstract float Width { get; }
    public abstract float Height { get; }
    public abstract float X1 { get; }
    public abstract float Y1 { get; }
    public abstract int EdgeCount { get; }
    public abstract Tile? GetNeighbor(Tile tile, int edgeIndex);
    public abstract int GetOppositeEdge(int edgeIndex);

    public float X2 => X1 + Width;
    public float Y2 => Y1 + Height;
    public Vector2 Center => new Vector2(X1 + Width / 2f, Y1 + Height / 2f);
    public Rectangle Rect => new Rectangle((int)X1, (int)Y1, (int)Width, (int)Height);

    public bool AddRamp(Tile tile, int edgeIndex)
    {
        var neighbor = GetNeighbor(tile, edgeIndex);
        if (neighbor == null || tile.Elevation == neighbor.Elevation)
            return false;

        tile.Ramps.Add(edgeIndex);
        neighbor.Ramps.Add(GetOppositeEdge(edgeIndex));
        return true;
    }

    public bool RemoveRamp(Tile tile, int edgeIndex)
    {
        var neighbor = GetNeighbor(tile, edgeIndex);
        if (neighbor == null)
            return false;

        bool removed = tile.Ramps.Remove(edgeIndex);
        if (removed)
            neighbor.Ramps.Remove(GetOppositeEdge(edgeIndex));
        return removed;
    }
}
