using System;
using Microsoft.Xna.Framework;
using HexEngine.Tiles;

namespace HexEngine.Maps;

public class HexGridMap : GridMap
{
    public float HexRadius { get; }
    public float HexVerticalScale { get; }
    public string HexOrientation { get; }
    public float HexWidth { get; }
    public float HexHeight { get; }
    private float _horizontalSpacing;
    private float _verticalSpacing;

    // Flat-top hex neighbor offsets: [edgeIndex][even/odd col] = (dx, dy)
    private static readonly int[,,] FlatNeighborOffsets = new int[6, 2, 2]
    {
        // Edge 0 (SE): even col (x+1, y+0), odd col (x+1, y+1)
        { { 1, 0 }, { 1, 1 } },
        // Edge 1 (S):  both (x+0, y+1)
        { { 0, 1 }, { 0, 1 } },
        // Edge 2 (SW): even col (x-1, y+0), odd col (x-1, y+1)
        { { -1, 0 }, { -1, 1 } },
        // Edge 3 (NW): even col (x-1, y-1), odd col (x-1, y+0)
        { { -1, -1 }, { -1, 0 } },
        // Edge 4 (N):  both (x+0, y-1)
        { { 0, -1 }, { 0, -1 } },
        // Edge 5 (NE): even col (x+1, y-1), odd col (x+1, y+0)
        { { 1, -1 }, { 1, 0 } },
    };

    public HexGridMap(int cols, int rows, float hexRadius, float hexVerticalScale, string hexOrientation = "flat")
        : base(cols, rows)
    {
        HexRadius = hexRadius;
        HexVerticalScale = hexVerticalScale;
        HexOrientation = hexOrientation;

        if (hexOrientation == "flat")
        {
            HexWidth = 2f * hexRadius;
            HexHeight = hexVerticalScale * MathF.Sqrt(3f) * hexRadius;
            _horizontalSpacing = 3f / 4f * HexWidth;
            _verticalSpacing = HexHeight;
        }
        else // pointy
        {
            HexWidth = MathF.Sqrt(3f) * hexRadius;
            HexHeight = 2f * hexRadius * hexVerticalScale;
            _horizontalSpacing = HexWidth;
            _verticalSpacing = 3f / 4f * HexHeight;
        }

        Generate();
    }

    public override float Width => HexOrientation == "flat"
        ? _horizontalSpacing * (Cols + 1)
        : _horizontalSpacing * (Cols + 1.5f);
    public override float Height => HexOrientation == "flat"
        ? _verticalSpacing * (Rows + 1.5f)
        : _verticalSpacing * (Rows + 1.5f);
    public override float X1 => -_horizontalSpacing;
    public override float Y1 => -_verticalSpacing;
    public override int EdgeCount => 6;

    public override int GetOppositeEdge(int edgeIndex) => (edgeIndex + 3) % 6;

    public override Tile? GetNeighbor(Tile tile, int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= 6) return null;

        int parity = tile.X % 2; // 0 = even, 1 = odd
        int nx = tile.X + FlatNeighborOffsets[edgeIndex, parity, 0];
        int ny = tile.Y + FlatNeighborOffsets[edgeIndex, parity, 1];

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
                float xOffset = x * _horizontalSpacing;
                float yOffset = y * _verticalSpacing;

                if (HexOrientation == "flat" && x % 2 == 1)
                    yOffset += _verticalSpacing / 2f;
                else if (HexOrientation == "pointy" && y % 2 == 1)
                    xOffset += _horizontalSpacing / 2f;

                var pos = new Vector2(xOffset, yOffset);
                Tiles[y][x] = new Hexagon(pos, x, y, HexRadius, HexVerticalScale, HexHeight, HexWidth, HexOrientation);
            }
        }
    }

    public override Tile GetNearestTile(Vector2 pos)
    {
        float upscale = 1f / HexVerticalScale;
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
