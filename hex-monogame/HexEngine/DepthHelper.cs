using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HexEngine;

public enum DepthSide { Left, Down, Right }

public struct DepthSideQuad
{
    public Vector2[] Quad;
    public DepthSide Side;
}

public static class DepthHelper
{
    /// <summary>
    /// Given screen-space hex top points and a depth offset in pixels,
    /// returns the side quads for south-facing edges.
    /// Flat-top hex (6 points): 3 quads (Right, Down, Left)
    /// Pointy-top hex (6 points): 2 quads (Right, Left)
    /// </summary>
    public static List<DepthSideQuad> ComputeDepthSideQuads(Vector2[] topPoints, float depthPixels)
    {
        var result = new List<DepthSideQuad>();
        if (topPoints.Length < 3 || depthPixels <= 0) return result;

        // Compute polygon center
        float centerX = 0, centerY = 0;
        for (int i = 0; i < topPoints.Length; i++)
        {
            centerX += topPoints[i].X;
            centerY += topPoints[i].Y;
        }
        centerX /= topPoints.Length;
        centerY /= topPoints.Length;

        int n = topPoints.Length;
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float midY = (topPoints[i].Y + topPoints[next].Y) / 2f;

            // Strict > gives 3 sides for flat-top, 2 for pointy-top
            if (midY > centerY)
            {
                float midX = (topPoints[i].X + topPoints[next].X) / 2f;

                DepthSide side;
                if (midX < centerX - 1f)
                    side = DepthSide.Left;
                else if (midX > centerX + 1f)
                    side = DepthSide.Right;
                else
                    side = DepthSide.Down;

                // Quad: top-edge start, top-edge end, bottom-edge end, bottom-edge start
                var quad = new Vector2[]
                {
                    topPoints[i],
                    topPoints[next],
                    new Vector2(topPoints[next].X, topPoints[next].Y + depthPixels),
                    new Vector2(topPoints[i].X, topPoints[i].Y + depthPixels)
                };

                result.Add(new DepthSideQuad { Quad = quad, Side = side });
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the screen-space depth offset in pixels for a hex.
    /// </summary>
    public static float ComputeDepthPixels(Vector2[] screenPoints, float depthMultiplier)
    {
        if (screenPoints.Length < 2 || depthMultiplier <= 0) return 0;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < screenPoints.Length; i++)
        {
            if (screenPoints[i].X < minX) minX = screenPoints[i].X;
            if (screenPoints[i].X > maxX) maxX = screenPoints[i].X;
            if (screenPoints[i].Y < minY) minY = screenPoints[i].Y;
            if (screenPoints[i].Y > maxY) maxY = screenPoints[i].Y;
        }
        float screenWidth = maxX - minX;
        float screenHeight = maxY - minY;
        float size = MathF.Sqrt(screenWidth * screenHeight);
        return size * depthMultiplier;
    }
}
