using System;
using System.Linq;
using Microsoft.Xna.Framework;

namespace HexEngine.Tests;

public class DepthHelperTests
{
    // Helper: create flat-top hex screen points centered at (cx, cy) with radius r and verticalScale vs
    private static Vector2[] MakeFlatTopHexPoints(float cx, float cy, float r, float vs)
    {
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = i * 60f;
            float angleRad = MathHelper.ToRadians(angleDeg);
            points[i] = new Vector2(
                cx + r * MathF.Cos(angleRad),
                cy + r * vs * MathF.Sin(angleRad));
        }
        return points;
    }

    // Helper: create pointy-top hex screen points
    private static Vector2[] MakePointyTopHexPoints(float cx, float cy, float r, float vs)
    {
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 30f + i * 60f;
            float angleRad = MathHelper.ToRadians(angleDeg);
            points[i] = new Vector2(
                cx + r * MathF.Cos(angleRad),
                cy + r * vs * MathF.Sin(angleRad));
        }
        return points;
    }

    [Fact]
    public void FlatTopHex_Returns3Sides()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        Assert.Equal(3, sides.Count);
    }

    [Fact]
    public void FlatTopHex_HasRightDownLeftSides()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        var sideTypes = sides.Select(s => s.Side).ToList();
        Assert.Contains(DepthSide.Right, sideTypes);
        Assert.Contains(DepthSide.Down, sideTypes);
        Assert.Contains(DepthSide.Left, sideTypes);
    }

    [Fact]
    public void PointyTopHex_Returns2Sides()
    {
        var points = MakePointyTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        Assert.Equal(2, sides.Count);
    }

    [Fact]
    public void PointyTopHex_HasRightAndLeftSides()
    {
        var points = MakePointyTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        var sideTypes = sides.Select(s => s.Side).ToList();
        Assert.Contains(DepthSide.Right, sideTypes);
        Assert.Contains(DepthSide.Left, sideTypes);
        Assert.DoesNotContain(DepthSide.Down, sideTypes);
    }

    [Fact]
    public void EachQuadHas4Points()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        foreach (var side in sides)
            Assert.Equal(4, side.Quad.Length);
    }

    [Fact]
    public void QuadBottomPointsAreShiftedDown()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        float depth = 20f;
        var sides = DepthHelper.ComputeDepthSideQuads(points, depth);

        foreach (var side in sides)
        {
            // Quad[0] and Quad[1] are top points, Quad[2] and Quad[3] are bottom points
            // Bottom points should be exactly depth pixels below their corresponding top points
            Assert.Equal(side.Quad[1].X, side.Quad[2].X, 0.01);
            Assert.Equal(side.Quad[1].Y + depth, side.Quad[2].Y, 0.01);
            Assert.Equal(side.Quad[0].X, side.Quad[3].X, 0.01);
            Assert.Equal(side.Quad[0].Y + depth, side.Quad[3].Y, 0.01);
        }
    }

    [Fact]
    public void FlatTopHex_RightSideConnectsP0P1()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        var rightSide = sides.First(s => s.Side == DepthSide.Right);

        // Right side should connect P0 (rightmost) and P1 (bottom-right)
        Assert.Equal(points[0].X, rightSide.Quad[0].X, 0.01);
        Assert.Equal(points[0].Y, rightSide.Quad[0].Y, 0.01);
        Assert.Equal(points[1].X, rightSide.Quad[1].X, 0.01);
        Assert.Equal(points[1].Y, rightSide.Quad[1].Y, 0.01);
    }

    [Fact]
    public void FlatTopHex_DownSideConnectsP1P2()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        var downSide = sides.First(s => s.Side == DepthSide.Down);

        Assert.Equal(points[1].X, downSide.Quad[0].X, 0.01);
        Assert.Equal(points[1].Y, downSide.Quad[0].Y, 0.01);
        Assert.Equal(points[2].X, downSide.Quad[1].X, 0.01);
        Assert.Equal(points[2].Y, downSide.Quad[1].Y, 0.01);
    }

    [Fact]
    public void FlatTopHex_LeftSideConnectsP2P3()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 20f);
        var leftSide = sides.First(s => s.Side == DepthSide.Left);

        Assert.Equal(points[2].X, leftSide.Quad[0].X, 0.01);
        Assert.Equal(points[2].Y, leftSide.Quad[0].Y, 0.01);
        Assert.Equal(points[3].X, leftSide.Quad[1].X, 0.01);
        Assert.Equal(points[3].Y, leftSide.Quad[1].Y, 0.01);
    }

    [Fact]
    public void ZeroDepth_ReturnsNoSides()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, 0f);
        Assert.Empty(sides);
    }

    [Fact]
    public void NegativeDepth_ReturnsNoSides()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var sides = DepthHelper.ComputeDepthSideQuads(points, -5f);
        Assert.Empty(sides);
    }

    [Fact]
    public void ComputeDepthPixels_CorrectValue()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        float depth = DepthHelper.ComputeDepthPixels(points, 1f / 3f);

        float minX = points.Min(p => p.X);
        float maxX = points.Max(p => p.X);
        float minY = points.Min(p => p.Y);
        float maxY = points.Max(p => p.Y);
        float expectedSize = MathF.Sqrt((maxX - minX) * (maxY - minY));
        Assert.Equal(expectedSize / 3f, depth, 0.01);
    }

    [Fact]
    public void ComputeDepthPixels_ConsistentAcrossOrientations()
    {
        // Flat and pointy hexes with same radius and VS should produce same depth
        var flatPoints = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        var pointyPoints = MakePointyTopHexPoints(200, 200, 50, 0.7f);

        float flatDepth = DepthHelper.ComputeDepthPixels(flatPoints, 1f / 3f);
        float pointyDepth = DepthHelper.ComputeDepthPixels(pointyPoints, 1f / 3f);

        // Geometric mean makes them equal (width and height swap between orientations)
        Assert.Equal(flatDepth, pointyDepth, 0.01);
    }

    [Fact]
    public void ComputeDepthPixels_ZeroMultiplier()
    {
        var points = MakeFlatTopHexPoints(200, 200, 50, 0.7f);
        Assert.Equal(0f, DepthHelper.ComputeDepthPixels(points, 0f));
    }
}
