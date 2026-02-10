using Microsoft.Xna.Framework;

namespace HexEngine.Tiles;

public class SquareTile : Tile
{
    public SquareTile(Vector2 pos, int x, int y, float width, float height)
        : base(pos, x, y, width, height)
    {
        Points = new Vector2[]
        {
            new Vector2(pos.X - width / 2f, pos.Y - height / 2f),
            new Vector2(pos.X + width / 2f, pos.Y - height / 2f),
            new Vector2(pos.X + width / 2f, pos.Y + height / 2f),
            new Vector2(pos.X - width / 2f, pos.Y + height / 2f),
            new Vector2(pos.X - width / 2f, pos.Y - height / 2f)
        };
    }
}
