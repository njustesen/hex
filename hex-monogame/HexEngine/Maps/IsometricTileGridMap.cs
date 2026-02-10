using System;
using Microsoft.Xna.Framework;
using HexEngine.Tiles;

namespace HexEngine.Maps;

public class IsometricTileGridMap : GridMap
{
    public float TileWidth { get; }
    public float TileHeight { get; }

    public IsometricTileGridMap(int cols, int rows, float tileWidth, float tileHeight)
        : base(cols, rows)
    {
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Generate();
    }

    public override float Width => TileWidth * (Cols + 2);
    public override float Height => TileHeight * (Rows + 2);
    public override float X1 => -TileWidth;
    public override float Y1 => -TileHeight;

    private void Generate()
    {
        Tiles = new Tile[Rows][];
        for (int y = 0; y < Rows; y++)
        {
            Tiles[y] = new Tile[Cols];
            for (int x = 0; x < Cols; x++)
            {
                float xOffset = (Cols * TileWidth / 2f) + (x - y) * (TileWidth / 2f);
                float yOffset = (TileHeight / 2f) + (x + y) * (TileHeight / 2f);
                var pos = new Vector2(xOffset, yOffset);
                Tiles[y][x] = new IsometricTile(pos, x, y, TileWidth, TileHeight);
            }
        }
    }

    public override Tile GetNearestTile(Vector2 pos)
    {
        float upscale = TileWidth / TileHeight;
        float scaledPosY = pos.Y * upscale;
        float minDistance = float.MaxValue;
        Tile? nearest = null;

        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Cols; x++)
            {
                var tile = Tiles[y][x];
                float scaledTileY = tile.Pos.Y * upscale;
                float dx = tile.Pos.X - pos.X;
                float dy = scaledTileY - scaledPosY;
                float distance = MathF.Sqrt(dx * dx + dy * dy);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = tile;
                }
            }
        }

        return nearest!;
    }
}
