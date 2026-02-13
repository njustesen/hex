using System;
using Microsoft.Xna.Framework;

namespace HexEngine.Rendering;

public enum IconType { Heart, Shield, Explosion, Range, Hex, Eye, IronCube, FissiumCube }

public static class IconRenderer
{
    public static void Draw(PrimitiveDrawer drawer, IconType type, float cx, float cy, float size, Color color)
    {
        float s = size / 2f;
        switch (type)
        {
            case IconType.Heart: DrawHeart(drawer, cx, cy, s, color); break;
            case IconType.Shield: DrawShield(drawer, cx, cy, s, color); break;
            case IconType.Explosion: DrawExplosion(drawer, cx, cy, s, color); break;
            case IconType.Range: DrawRange(drawer, cx, cy, s, color); break;
            case IconType.Hex: DrawHex(drawer, cx, cy, s, color); break;
            case IconType.Eye: DrawEye(drawer, cx, cy, s, color); break;
            case IconType.IronCube:
            case IconType.FissiumCube: DrawCube(drawer, cx, cy, s, color); break;
        }
    }

    private static void DrawHeart(PrimitiveDrawer d, float cx, float cy, float s, Color c)
    {
        d.DrawFilledCircle(new Vector2(cx - s * 0.3f, cy - s * 0.15f), s * 0.38f, c);
        d.DrawFilledCircle(new Vector2(cx + s * 0.3f, cy - s * 0.15f), s * 0.38f, c);
        d.DrawFilledPolygon(new[]
        {
            new Vector2(cx - s * 0.62f, cy - s * 0.05f),
            new Vector2(cx + s * 0.62f, cy - s * 0.05f),
            new Vector2(cx, cy + s * 0.75f),
        }, c);
    }

    private static void DrawShield(PrimitiveDrawer d, float cx, float cy, float s, Color c)
    {
        d.DrawFilledPolygon(new[]
        {
            new Vector2(cx - s * 0.55f, cy - s * 0.6f),
            new Vector2(cx + s * 0.55f, cy - s * 0.6f),
            new Vector2(cx + s * 0.55f, cy + s * 0.1f),
            new Vector2(cx, cy + s * 0.7f),
            new Vector2(cx - s * 0.55f, cy + s * 0.1f),
        }, c);
    }

    private static void DrawExplosion(PrimitiveDrawer d, float cx, float cy, float s, Color c)
    {
        int points = 8;
        var verts = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float angle = i * MathF.PI / points - MathF.PI / 2f;
            float r = (i % 2 == 0) ? s * 0.7f : s * 0.3f;
            verts[i] = new Vector2(cx + MathF.Cos(angle) * r, cy + MathF.Sin(angle) * r);
        }
        d.DrawFilledPolygon(verts, c);
    }

    private static void DrawRange(PrimitiveDrawer d, float cx, float cy, float s, Color c)
    {
        float dashW = s * 0.35f;
        float dashH = s * 0.2f;
        float gap = s * 0.15f;
        float totalW = dashW * 3 + gap * 2;
        float startX = cx - totalW / 2f - s * 0.1f;
        for (int i = 0; i < 3; i++)
        {
            float dx = startX + i * (dashW + gap);
            d.DrawFilledRect(dx, cy - dashH / 2f, dashW, dashH, c);
        }
        float arrowX = startX + totalW;
        d.DrawFilledPolygon(new[]
        {
            new Vector2(arrowX, cy - s * 0.35f),
            new Vector2(arrowX + s * 0.35f, cy),
            new Vector2(arrowX, cy + s * 0.35f),
        }, c);
    }

    private static void DrawHex(PrimitiveDrawer d, float cx, float cy, float s, Color c)
    {
        float r = s * 0.6f;
        var verts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI / 3f - MathF.PI / 6f;
            verts[i] = new Vector2(cx + MathF.Cos(angle) * r, cy + MathF.Sin(angle) * r);
        }
        d.DrawFilledPolygon(verts, c);
    }

    private static void DrawEye(PrimitiveDrawer d, float cx, float cy, float s, Color c)
    {
        int arcPts = 6;
        var verts = new Vector2[arcPts * 2 + 2];
        float w = s * 0.8f;
        float h = s * 0.45f;

        verts[0] = new Vector2(cx - w, cy);
        for (int i = 0; i < arcPts; i++)
        {
            float t = (i + 1f) / (arcPts + 1f);
            verts[1 + i] = new Vector2(cx - w + t * 2 * w, cy - MathF.Sin(t * MathF.PI) * h);
        }
        verts[arcPts + 1] = new Vector2(cx + w, cy);
        for (int i = 0; i < arcPts; i++)
        {
            float t = (i + 1f) / (arcPts + 1f);
            verts[arcPts + 2 + i] = new Vector2(cx + w - t * 2 * w, cy + MathF.Sin(t * MathF.PI) * h);
        }
        d.DrawFilledPolygon(verts, c);
        d.DrawFilledCircle(new Vector2(cx, cy), s * 0.2f, new Color(20, 20, 30));
    }

    private static void DrawCube(PrimitiveDrawer d, float cx, float cy, float s, Color c)
    {
        float e = s * 0.55f; // edge length
        float w = e * 0.866f; // cos(30°) — half-width of top face
        float h = e * 0.5f;   // sin(30°) — half-height of top face
        float depth = e;      // side face height

        var top = new Vector2(cx, cy - h - depth * 0.5f);
        var left = new Vector2(cx - w, cy - depth * 0.5f);
        var right = new Vector2(cx + w, cy - depth * 0.5f);
        var center = new Vector2(cx, cy + h - depth * 0.5f);
        var bleft = new Vector2(cx - w, cy + depth * 0.5f);
        var bright = new Vector2(cx + w, cy + depth * 0.5f);
        var bottom = new Vector2(cx, cy + h + depth * 0.5f);

        Color rightC = new((int)(c.R * 0.4f), (int)(c.G * 0.4f), (int)(c.B * 0.4f));
        d.DrawFilledQuad(center, right, bright, bottom, rightC);

        Color leftC = new((int)(c.R * 0.65f), (int)(c.G * 0.65f), (int)(c.B * 0.65f));
        d.DrawFilledQuad(left, center, bottom, bleft, leftC);

        d.DrawFilledQuad(top, right, center, left, c);
    }
}
