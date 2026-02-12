using Microsoft.Xna.Framework;
using HexEngine.View;

namespace HexEngine.Tests;

public class CameraTests
{
    [Fact]
    public void Camera_CenterIsSet()
    {
        var cam = new Camera(new Vector2(100, 200), 400, 300);
        Assert.Equal(100f, cam.Center.X, 0.01);
        Assert.Equal(200f, cam.Center.Y, 0.01);
    }

    [Fact]
    public void Camera_BoundsProperties()
    {
        var cam = new Camera(new Vector2(200, 150), 400, 300);
        Assert.Equal(0f, cam.X1, 0.01);
        Assert.Equal(400f, cam.X2, 0.01);
        Assert.Equal(0f, cam.Y1, 0.01);
        Assert.Equal(300f, cam.Y2, 0.01);
    }

    [Fact]
    public void Camera_NormReturnsZeroToOne()
    {
        var cam = new Camera(new Vector2(200, 150), 400, 300);
        // Top-left corner
        var n1 = cam.Norm(new Vector2(0, 0));
        Assert.Equal(0f, n1.X, 0.01);
        Assert.Equal(0f, n1.Y, 0.01);

        // Bottom-right corner
        var n2 = cam.Norm(new Vector2(400, 300));
        Assert.Equal(1f, n2.X, 0.01);
        Assert.Equal(1f, n2.Y, 0.01);

        // Center
        var n3 = cam.Norm(new Vector2(200, 150));
        Assert.Equal(0.5f, n3.X, 0.01);
        Assert.Equal(0.5f, n3.Y, 0.01);
    }

    [Fact]
    public void Camera_ChangeUpdatesCenter()
    {
        var cam = new Camera(new Vector2(100, 100), 400, 300);
        cam.Change(new Vector2(200, 200));
        Assert.Equal(200f, cam.Center.X, 0.01);
        Assert.Equal(200f, cam.Center.Y, 0.01);
    }

    [Fact]
    public void Camera_ChangeUpdatesDimensions()
    {
        var cam = new Camera(new Vector2(100, 100), 400, 300);
        cam.Change(width: 800f, height: 600f);
        Assert.Equal(800f, cam.CameraWidth, 0.01);
        Assert.Equal(600f, cam.CameraHeight, 0.01);
    }

    [Fact]
    public void Camera_BoundsClamping_LeftEdge()
    {
        var bounds = new Rectangle(0, 0, 1000, 1000);
        var cam = new Camera(new Vector2(100, 500), 400, 300, bounds);
        // Camera left edge at -100, should clamp to 0 -> center.x = 200
        cam.Change(new Vector2(100, 500));
        Assert.True(cam.X1 >= 0f);
    }

    [Fact]
    public void Camera_BoundsClamping_RightEdge()
    {
        var bounds = new Rectangle(0, 0, 1000, 1000);
        var cam = new Camera(new Vector2(900, 500), 400, 300, bounds);
        // Camera right edge at 1100, should clamp to 1000
        cam.Change(new Vector2(900, 500));
        Assert.True(cam.X2 <= 1000f);
    }

    [Fact]
    public void Camera_BoundsClamping_TopEdge()
    {
        var bounds = new Rectangle(0, 0, 1000, 1000);
        var cam = new Camera(new Vector2(500, 100), 400, 300, bounds);
        cam.Change(new Vector2(500, 100));
        Assert.True(cam.Y1 >= 0f);
    }

    [Fact]
    public void Camera_BoundsClamping_BottomEdge()
    {
        var bounds = new Rectangle(0, 0, 1000, 1000);
        var cam = new Camera(new Vector2(500, 900), 400, 300, bounds);
        cam.Change(new Vector2(500, 900));
        Assert.True(cam.Y2 <= 1000f);
    }

    [Fact]
    public void Camera_BoundsClamping_WiderThanBounds()
    {
        var bounds = new Rectangle(0, 0, 200, 200);
        var cam = new Camera(new Vector2(100, 100), 400, 300, bounds);
        // Trigger clamping via Change()
        cam.Change(new Vector2(100, 100));
        // Camera width should be clamped to bounds width, height adjusted by ratio
        Assert.True(cam.CameraWidth <= 200f + 1f);
        Assert.True(cam.CameraHeight <= 200f + 1f);
    }

    [Fact]
    public void Camera_IsWithin_PointInside()
    {
        var cam = new Camera(new Vector2(200, 150), 400, 300);
        Assert.True(cam.IsWithin(new Vector2(200, 150)));
        Assert.True(cam.IsWithin(new Vector2(0, 0)));
    }

    [Fact]
    public void Camera_IsWithin_PointOutside()
    {
        var cam = new Camera(new Vector2(200, 150), 400, 300);
        Assert.False(cam.IsWithin(new Vector2(-100, -100)));
        Assert.False(cam.IsWithin(new Vector2(500, 400)));
    }

    [Fact]
    public void Camera_SetCenter_UpdatesPosition()
    {
        var cam = new Camera(new Vector2(0, 0), 400, 300);
        cam.SetCenter(new Vector2(500, 500));
        Assert.Equal(500f, cam.Center.X, 0.01);
        Assert.Equal(500f, cam.Center.Y, 0.01);
    }

    [Fact]
    public void Camera_Ratios()
    {
        var cam = new Camera(new Vector2(0, 0), 400, 200);
        Assert.Equal(2f, cam.WToHRatio, 0.01);
        Assert.Equal(0.5f, cam.HToWRatio, 0.01);
    }

    [Fact]
    public void Camera_Update_DecaysShakeTime()
    {
        var cam = new Camera(new Vector2(0, 0), 400, 300);
        cam.ScreenShake(1.0f);
        Assert.Equal(1.0f, cam.ShakingTime, 0.01);

        // After several updates, shake time should decrease
        for (int i = 0; i < 100; i++)
            cam.Update(0.016f);

        Assert.True(cam.ShakingTime < 0.01f);
    }
}
