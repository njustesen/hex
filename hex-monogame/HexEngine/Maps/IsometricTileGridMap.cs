using System;
using Microsoft.Xna.Framework;
using HexEngine.Tiles;

namespace HexEngine.Maps;

public class IsometricTileGridMap : GridMap
{
    public float TileWidth { get; }
    public float TileHeight { get; }

    // Isometric neighbors: NE=0, SE=1, SW=2, NW=3
    // In iso grid coordinates, neighbors are at cardinal offsets
    private static readonly int[,] IsoNeighborOffsets = new int[4, 2]
    {
        { 0, -1 }, // NE
        { 1, 0 },  // SE
        { 0, 1 },  // SW
        { -1, 0 }, // NW
    };

    public IsometricTileGridMap(int cols, int rows, float tileWidth, float tileHeight)
        : base(cols, rows)
    {
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        Generate();
    }

    public override float Width => (Cols + Rows + 4) * TileWidth / 2f;
    public override float Height => (Cols + Rows + 4) * TileHeight / 2f;
    public override float X1 => (Cols - Rows - 2) * TileWidth / 2f;
    public override float Y1 => -TileHeight;
    public override int EdgeCount => 4;

    public override int GetOppositeEdge(int edgeIndex) => (edgeIndex + 2) % 4;

    public override Tile? GetNeighbor(Tile tile, int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= 4) return null;

        int nx = tile.X + IsoNeighborOffsets[edgeIndex, 0];
        int ny = tile.Y + IsoNeighborOffsets[edgeIndex, 1];

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
