using System;
using Microsoft.Xna.Framework;
using HexEngine.Tiles;

namespace HexEngine.Maps;

public class TileGridMap : GridMap
{
    public float TileWidth { get; }
    public float TileHeight { get; }

    // Square grid neighbors: N=0, E=1, S=2, W=3
    private static readonly int[,] SquareNeighborOffsets = new int[4, 2]
    {
        { 0, -1 }, // N
        { 1, 0 },  // E
        { 0, 1 },  // S
        { -1, 0 }, // W
    };

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
    public override int EdgeCount => 4;

    public override int GetOppositeEdge(int edgeIndex) => (edgeIndex + 2) % 4;

    public override Tile? GetNeighbor(Tile tile, int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= 4) return null;

        int nx = tile.X + SquareNeighborOffsets[edgeIndex, 0];
        int ny = tile.Y + SquareNeighborOffsets[edgeIndex, 1];

        if (nx < 0 || nx >= Cols || ny < 0 || ny >= Rows)
            return null;

        return Tiles[ny][nx];
    }

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
