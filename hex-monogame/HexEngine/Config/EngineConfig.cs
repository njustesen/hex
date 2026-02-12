using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace HexEngine.Config;

public static class EngineConfig
{
    private const string ConfigPath = "config.json";

    public static int Width { get; set; } = 1200;
    public static int Height { get; set; } = 600;
    public static bool Fullscreen { get; set; } = false;
    public static float MoveSpeed { get; set; } = 0.5f;
    public static float ZoomSpeed { get; set; } = 4.0f;
    public static float DepthMultiplier { get; set; } = 1f / 4f;
    public static Color TileTopColor { get; set; } = new Color(40, 120, 40);
    public static float PerspectiveFactor { get; set; } = 0.1f;
    public static bool MinimapPerspective { get; set; } = false;
    public static float FogStrength { get; set; } = 0.3f;
    public static bool ShowGrid { get; set; } = false;
    public static bool ShowInnerShapes { get; set; } = true;
    public static float InnerShapeScale { get; set; } = 0.8f;

    public static void Save()
    {
        var data = new ConfigData
        {
            Width = Width,
            Height = Height,
            Fullscreen = Fullscreen,
            MoveSpeed = MoveSpeed,
            ZoomSpeed = ZoomSpeed,
            DepthMultiplier = DepthMultiplier,
            TileTopColorR = TileTopColor.R,
            TileTopColorG = TileTopColor.G,
            TileTopColorB = TileTopColor.B,
            PerspectiveFactor = PerspectiveFactor,
            MinimapPerspective = MinimapPerspective,
            FogStrength = FogStrength,
            ShowGrid = ShowGrid,
            ShowInnerShapes = ShowInnerShapes,
            InnerShapeScale = InnerShapeScale,
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(ConfigPath, json);
    }

    public static void Load()
    {
        if (!File.Exists(ConfigPath)) return;

        try
        {
            string json = File.ReadAllText(ConfigPath);
            var data = JsonSerializer.Deserialize<ConfigData>(json);
            if (data == null) return;

            Width = data.Width;
            Height = data.Height;
            Fullscreen = data.Fullscreen;
            MoveSpeed = data.MoveSpeed;
            ZoomSpeed = data.ZoomSpeed;
            DepthMultiplier = data.DepthMultiplier;
            TileTopColor = new Color(data.TileTopColorR, data.TileTopColorG, data.TileTopColorB);
            PerspectiveFactor = data.PerspectiveFactor;
            MinimapPerspective = data.MinimapPerspective;
            FogStrength = data.FogStrength;
            ShowGrid = data.ShowGrid;
            ShowInnerShapes = data.ShowInnerShapes;
            InnerShapeScale = data.InnerShapeScale;
        }
        catch (Exception)
        {
            // If config is corrupt, just use defaults
        }
    }

    private class ConfigData
    {
        public int Width { get; set; } = 1200;
        public int Height { get; set; } = 600;
        public bool Fullscreen { get; set; } = false;
        public float MoveSpeed { get; set; } = 0.5f;
        public float ZoomSpeed { get; set; } = 4.0f;
        public float DepthMultiplier { get; set; } = 0.25f;
        public int TileTopColorR { get; set; } = 40;
        public int TileTopColorG { get; set; } = 120;
        public int TileTopColorB { get; set; } = 40;
        public float PerspectiveFactor { get; set; } = 0.1f;
        public bool MinimapPerspective { get; set; } = false;
        public float FogStrength { get; set; } = 0.3f;
        public bool ShowGrid { get; set; } = false;
        public bool ShowInnerShapes { get; set; } = true;
        public float InnerShapeScale { get; set; } = 0.8f;
    }
}
