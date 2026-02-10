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
