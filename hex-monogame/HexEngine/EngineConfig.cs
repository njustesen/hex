using Microsoft.Xna.Framework;

namespace HexEngine;

public static class EngineConfig
{
    public static int Width { get; set; } = 1200;
    public static int Height { get; set; } = 600;
    public static bool Fullscreen { get; set; } = false;
    public static float MoveSpeed { get; set; } = 0.5f;
    public static float ZoomSpeed { get; set; } = 4.0f;
    public static float DepthMultiplier { get; set; } = 1f / 4f;
    public static Color TileTopColor { get; set; } = new Color(40, 120, 40);
}
