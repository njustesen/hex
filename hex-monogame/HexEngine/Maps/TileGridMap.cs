using System;
using Microsoft.Xna.Framework;
using HexEngine.Tiles;

namespace HexEngine.Maps;

public class TileGridMap : GridMap
{
    public float TileWidth { get; }
    public float TileHeight { get; }

    public TileGridMap(int cols, int rows, float tileWidth, float tileHeight)
        : base(cols, rows)
    {
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Generate();
    }

    public override float Width => TileWidth * (Cols + 1);
    public override float Height => TileHeight * (Rows + 1);
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
                float xOffset = x * TileWidth;
                float yOffset = y * TileHeight;
                var pos = new Vector2(xOffset, yOffset);
                Tiles[y][x] = new SquareTile(pos, x, y, TileWidth, TileHeight);
            }
        }
    }

    public override Tile GetNearestTile(Vector2 pos)
    {
        int xIdx = (int)((pos.X + TileWidth / 2f) / TileWidth);
        int yIdx = (int)((pos.Y + TileHeight / 2f) / TileHeight);
        xIdx = Math.Clamp(xIdx, 0, Cols - 1);
        yIdx = Math.Clamp(yIdx, 0, Rows - 1);
        return Tiles[yIdx][xIdx];
    }
}
