using Microsoft.Xna.Framework;

namespace HexEngine.Tiles;

public class IsometricTile : Tile
{
    public IsometricTile(Vector2 pos, int x, int y, float width, float height)
        : base(pos, x, y, width, height)
    {
        Points = new Vector2[]
        {
            new Vector2(pos.X, pos.Y - height / 2f),         // Top
            new Vector2(pos.X + width / 2f, pos.Y),          // Right
            new Vector2(pos.X, pos.Y + height / 2f),         // Bottom
            new Vector2(pos.X - width / 2f, pos.Y),          // Left
            new Vector2(pos.X, pos.Y - height / 2f)           // Close
        };
    }
}
