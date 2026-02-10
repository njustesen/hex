using System;
using Microsoft.Xna.Framework;

namespace HexEngine.Tiles;

public class Hexagon : Tile
{
    public float Radius { get; }
    public float VerticalScale { get; }
    public string Orientation { get; }

    public Hexagon(Vector2 pos, int x, int y, float radius, float verticalScale, float width, float height, string orientation = "pointy")
        : base(pos, x, y, width, height)
    {
        Radius = radius;
        VerticalScale = verticalScale;
        Orientation = orientation;
        Points = ComputePoints();
    }

    private Vector2[] ComputePoints()
    {
        float cx = Pos.X;
        float cy = Pos.Y;
        var points = new Vector2[6];

        for (int i = 0; i < 6; i++)
        {
            float angleDeg;
            if (Orientation == "flat")
                angleDeg = i * 60f;
            else
                angleDeg = 30f + i * 60f;

            float angleRad = MathHelper.ToRadians(angleDeg);
            points[i] = new Vector2(
                cx + Radius * MathF.Cos(angleRad),
                cy + Radius * VerticalScale * MathF.Sin(angleRad)
            );
        }

        return points;
    }
}
