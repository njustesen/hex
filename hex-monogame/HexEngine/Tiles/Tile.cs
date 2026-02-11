using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HexEngine.Tiles;

public class Tile
{
    public Vector2 Pos { get; }
    public int X { get; }
    public int Y { get; }
    public float Width { get; }
    public float Height { get; }
    public Vector2[] Points { get; protected set; }
    public Unit? Unit { get; set; }
    public int Elevation { get; set; }
    public HashSet<int> Ramps { get; } = new();

    public Tile(Vector2 pos, int x, int y, float width, float height)
    {
        Pos = pos;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Points = System.Array.Empty<Vector2>();
    }
}
