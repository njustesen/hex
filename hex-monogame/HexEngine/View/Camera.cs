using System;
using Microsoft.Xna.Framework;

namespace HexEngine.View;

public class Camera
{
    private Vector2 _center;
    public Rectangle? Bounds { get; set; }
    public float CameraWidth { get; set; }
    public float CameraHeight { get; set; }
    public float WToHRatio { get; }
    public float HToWRatio { get; }
    public float ShakeOffsetX { get; private set; }
    public float ShakeOffsetY { get; private set; }
    public float ShakingTime { get; private set; }

    private static readonly Random _random = new Random();

    public Camera(Vector2 center, float width, float height, Rectangle? bounds = null)
    {
        _center = center;
        Bounds = bounds;
        CameraWidth = width;
        CameraHeight = height;
        WToHRatio = width / height;
        HToWRatio = height / width;
        ShakeOffsetX = 0;
        ShakeOffsetY = 0;
        ShakingTime = 0;
    }

    public Vector2 Center => new Vector2(_center.X + ShakeOffsetX, _center.Y + ShakeOffsetY);

    public float X1 => Center.X - CameraWidth / 2f;
    public float X2 => Center.X + CameraWidth / 2f;
    public float Y1 => Center.Y - CameraHeight / 2f;
    public float Y2 => Center.Y + CameraHeight / 2f;

    // Unshaken bounds (for clamping)
    private float _X1 => _center.X - CameraWidth / 2f;
    private float _X2 => _center.X + CameraWidth / 2f;
    private float _Y1 => _center.Y - CameraHeight / 2f;
    private float _Y2 => _center.Y + CameraHeight / 2f;

    public Rectangle Rect => new Rectangle((int)X1, (int)Y1, (int)CameraWidth, (int)CameraHeight);

    public void SetCenter(Vector2 position)
    {
        _center = position;
        if (Bounds.HasValue)
            Adjust();
    }

    public Vector2 Norm(Vector2 pos)
    {
        return new Vector2(
            (pos.X - X1) / CameraWidth,
            (pos.Y - Y1) / CameraHeight
        );
    }

    public bool IsWithin(Vector2 pos, float size = 0)
    {
        return IsWithinXY(pos.X, pos.Y, size);
    }

    public bool IsWithinXY(float x, float y, float size = 0)
    {
        return X1 <= x + size && x - size <= X2 && Y1 <= y + size && y - size <= Y2;
    }

    public void Change(Vector2? center = null, float? width = null, float? height = null)
    {
        if (center.HasValue)
            _center = center.Value;
        if (width.HasValue)
            CameraWidth = width.Value;
        if (height.HasValue)
            CameraHeight = height.Value;
        if (Bounds.HasValue)
            Adjust();
    }

    private void Adjust()
    {
        var bounds = Bounds!.Value;

        if (CameraWidth > bounds.Width)
        {
            _center = new Vector2(bounds.Center.X, _center.Y);
            CameraWidth = bounds.Width;
            CameraHeight = CameraWidth * HToWRatio;
        }
        if (CameraHeight > bounds.Height)
        {
            _center = new Vector2(_center.X, bounds.Center.Y);
            CameraHeight = bounds.Height;
            CameraWidth = CameraHeight * WToHRatio;
        }
        if (_X1 < bounds.X)
        {
            _center = new Vector2(bounds.X + CameraWidth / 2f, _center.Y);
        }
        if (_X2 > bounds.X + bounds.Width)
        {
            _center = new Vector2(bounds.X + bounds.Width - CameraWidth / 2f, _center.Y);
        }
        if (_Y1 < bounds.Y)
        {
            _center = new Vector2(_center.X, bounds.Y + CameraHeight / 2f);
        }
        if (_Y2 > bounds.Y + bounds.Height)
        {
            _center = new Vector2(_center.X, bounds.Y + bounds.Height - CameraHeight / 2f);
        }
    }

    public void ScreenShake(float seconds)
    {
        ShakingTime = seconds;
    }

    public void Shake(float amount)
    {
        int x = _random.Next(-1, 2); // -1, 0, or 1
        int y = _random.Next(-1, 2);
        ShakeOffsetX = x * amount * CameraWidth;
        ShakeOffsetY = y * amount * CameraHeight;
    }

    public void Update(float seconds, float shake = 0.05f)
    {
        if (ShakingTime > 0)
        {
            Shake(ShakingTime * shake);
        }
        ShakingTime *= 0.95f;
        if (ShakingTime < 0)
        {
            ShakingTime = 0;
        }
    }
}
