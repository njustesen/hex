using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HexEngine.Core;

public class UnitColorDef
{
    public List<int> Fill { get; set; } = new() { 128, 128, 128 };
    public List<int> Outline { get; set; } = new() { 80, 80, 80 };
    public List<int>? TurretFill { get; set; }
    public List<int>? TurretOutline { get; set; }
}

public class UnitDef
{
    public int Movement { get; set; } = 2;
    public int Health { get; set; } = 3;
    public int Armor { get; set; }
    public int Damage { get; set; } = 2;
    public int Range { get; set; } = 1;
    public bool CanTargetAir { get; set; } = true;
    public bool CanTargetGround { get; set; } = true;
    public int MaxAttacks { get; set; } = 1;
    public bool Flying { get; set; }
    public int Sight { get; set; } = 3;
    public float Hover { get; set; }
    public string Shape { get; set; } = "circle";
    public float Scale { get; set; } = 0.25f;
    public float Size { get; set; } = 0.5f;
    public Dictionary<string, UnitColorDef> Colors { get; set; } = new();
}

public static class UnitDefs
{
    private static Dictionary<string, UnitDef> _defs = new();
    private static bool _loaded;
    private static readonly List<string> _typeNames = new();

    public static IReadOnlyList<string> TypeNames => _typeNames;

    public static void Load(string path = "units.yaml")
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        string yaml = File.ReadAllText(path);
        var root = deserializer.Deserialize<Dictionary<string, Dictionary<string, UnitDef>>>(yaml);

        _defs.Clear();
        _typeNames.Clear();

        if (root != null && root.TryGetValue("units", out var units))
        {
            foreach (var kvp in units)
            {
                _defs[kvp.Key] = kvp.Value;
                _typeNames.Add(kvp.Key);
            }
        }

        _loaded = true;
    }

    public static void EnsureLoaded()
    {
        if (!_loaded) Load();
    }

    public static UnitDef Get(string type)
    {
        EnsureLoaded();
        if (_defs.TryGetValue(type, out var def))
            return def;
        throw new ArgumentException($"Unknown unit type: {type}");
    }

    public static bool Exists(string type)
    {
        EnsureLoaded();
        return _defs.ContainsKey(type);
    }
}
