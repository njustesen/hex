using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using HexEngine.Maps;
using HexEngine.Tiles;
using HexEngine.Config;

namespace HexEngine.Editor;

public static class MapSerializer
{
    private const string MapsDirectory = "maps";

    public static void Save(GridMap map, string filePath)
    {
        var data = new MapData();

        switch (map)
        {
            case HexGridMap hex:
                data.MapType = "hex";
                data.Cols = hex.Cols;
                data.Rows = hex.Rows;
                data.HexRadius = hex.HexRadius;
                data.HexVerticalScale = hex.HexVerticalScale;
                data.HexOrientation = hex.HexOrientation;
                break;
            case IsometricTileGridMap iso:
                data.MapType = "isometric";
                data.Cols = iso.Cols;
                data.Rows = iso.Rows;
                data.TileWidth = iso.TileWidth;
                data.TileHeight = iso.TileHeight;
                break;
            case TileGridMap square:
                data.MapType = "square";
                data.Cols = square.Cols;
                data.Rows = square.Rows;
                data.TileWidth = square.TileWidth;
                data.TileHeight = square.TileHeight;
                break;
        }

        data.Tiles = new List<TileData>();
        for (int y = 0; y < map.Rows; y++)
        {
            for (int x = 0; x < map.Cols; x++)
            {
                var tile = map.Tiles[y][x];
                if (tile.Elevation > 0 || tile.Ramps.Count > 0)
                {
                    var td = new TileData { X = x, Y = y, Elevation = tile.Elevation };
                    if (tile.Ramps.Count > 0)
                        td.Ramps = tile.Ramps.OrderBy(r => r).ToList();
                    data.Tiles.Add(td);
                }
            }
        }

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(filePath, json);
    }

    public static GridMap Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<MapData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize map file.");

        GridMap map = data.MapType switch
        {
            "hex" => new HexGridMap(data.Cols, data.Rows, data.HexRadius, data.HexVerticalScale, data.HexOrientation ?? "flat"),
            "square" => new TileGridMap(data.Cols, data.Rows, data.TileWidth, data.TileHeight),
            "isometric" => new IsometricTileGridMap(data.Cols, data.Rows, data.TileWidth, data.TileHeight),
            _ => throw new InvalidOperationException($"Unknown map type: {data.MapType}")
        };

        if (data.Tiles != null)
        {
            // First pass: set elevations
            foreach (var td in data.Tiles)
            {
                if (td.Y >= 0 && td.Y < map.Rows && td.X >= 0 && td.X < map.Cols)
                    map.Tiles[td.Y][td.X].Elevation = td.Elevation;
            }

            // Second pass: add ramps (elevations must be set first)
            foreach (var td in data.Tiles)
            {
                if (td.Ramps == null || td.Y < 0 || td.Y >= map.Rows || td.X < 0 || td.X >= map.Cols)
                    continue;
                var tile = map.Tiles[td.Y][td.X];
                foreach (int edge in td.Ramps)
                    map.AddRamp(tile, edge);
            }
        }

        return map;
    }

    public static string GetMapsDirectory()
    {
        if (!Directory.Exists(MapsDirectory))
            Directory.CreateDirectory(MapsDirectory);
        return MapsDirectory;
    }

    public static string[] GetMapFiles()
    {
        var dir = GetMapsDirectory();
        if (!Directory.Exists(dir))
            return Array.Empty<string>();
        return Directory.GetFiles(dir, "*.json")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .OrderBy(f => f)
            .ToArray();
    }

    private class MapData
    {
        [JsonPropertyName("mapType")]
        public string MapType { get; set; } = "hex";

        [JsonPropertyName("cols")]
        public int Cols { get; set; }

        [JsonPropertyName("rows")]
        public int Rows { get; set; }

        [JsonPropertyName("hexRadius")]
        public float HexRadius { get; set; } = 100f;

        [JsonPropertyName("hexVerticalScale")]
        public float HexVerticalScale { get; set; } = 0.7f;

        [JsonPropertyName("hexOrientation")]
        public string? HexOrientation { get; set; }

        [JsonPropertyName("tileWidth")]
        public float TileWidth { get; set; } = 100f;

        [JsonPropertyName("tileHeight")]
        public float TileHeight { get; set; } = 100f;

        [JsonPropertyName("tiles")]
        public List<TileData>? Tiles { get; set; }
    }

    private class TileData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("elevation")]
        public int Elevation { get; set; }

        [JsonPropertyName("ramps")]
        public List<int>? Ramps { get; set; }
    }
}
