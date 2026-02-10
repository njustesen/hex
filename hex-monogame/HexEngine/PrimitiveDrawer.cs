using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HexEngine;

public class PrimitiveDrawer
{
    private readonly GraphicsDevice _graphicsDevice;
    private BasicEffect _effect;

    public PrimitiveDrawer(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
        };
    }

    public void UpdateProjection(int width, int height)
    {
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
        _effect.View = Matrix.Identity;
        _effect.World = Matrix.Identity;
    }

    public void DrawFilledPolygon(Vector2[] points, Color color)
    {
        if (points.Length < 3) return;

        // Triangle fan from first vertex
        int triangleCount = points.Length - 2;
        var vertices = new VertexPositionColor[triangleCount * 3];

        for (int i = 0; i < triangleCount; i++)
        {
            vertices[i * 3] = new VertexPositionColor(new Vector3(points[0], 0), color);
            vertices[i * 3 + 1] = new VertexPositionColor(new Vector3(points[i + 1], 0), color);
            vertices[i * 3 + 2] = new VertexPositionColor(new Vector3(points[i + 2], 0), color);
        }

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, triangleCount);
        }
    }

    public void DrawPolygonOutline(Vector2[] points, Color color)
    {
        if (points.Length < 2) return;

        // For closed polygons (where last point == first point), draw lines between consecutive points
        // For open polygons, also close the loop
        int lineCount = points.Length;
        var vertices = new VertexPositionColor[lineCount + 1];

        for (int i = 0; i < points.Length; i++)
        {
            vertices[i] = new VertexPositionColor(new Vector3(points[i], 0), color);
        }
        // Close the loop
        vertices[points.Length] = new VertexPositionColor(new Vector3(points[0], 0), color);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineStrip, vertices, 0, lineCount);
        }
    }

    public void DrawLine(Vector2 start, Vector2 end, Color color)
    {
        var vertices = new VertexPositionColor[2];
        vertices[0] = new VertexPositionColor(new Vector3(start, 0), color);
        vertices[1] = new VertexPositionColor(new Vector3(end, 0), color);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, 1);
        }
    }

    public void DrawRectOutline(float x, float y, float width, float height, Color color)
    {
        var points = new Vector2[]
        {
            new Vector2(x, y),
            new Vector2(x + width, y),
            new Vector2(x + width, y + height),
            new Vector2(x, y + height),
        };
        DrawPolygonOutline(points, color);
    }

    public void DrawFilledRect(float x, float y, float width, float height, Color color)
    {
        var vertices = new VertexPositionColor[6];
        vertices[0] = new VertexPositionColor(new Vector3(x, y, 0), color);
        vertices[1] = new VertexPositionColor(new Vector3(x + width, y, 0), color);
        vertices[2] = new VertexPositionColor(new Vector3(x + width, y + height, 0), color);
        vertices[3] = new VertexPositionColor(new Vector3(x, y, 0), color);
        vertices[4] = new VertexPositionColor(new Vector3(x + width, y + height, 0), color);
        vertices[5] = new VertexPositionColor(new Vector3(x, y + height, 0), color);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 2);
        }
    }
}
