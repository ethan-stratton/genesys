using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArenaShooter;

public class SaveData
{
    private const string SavePath = "savedata.json";

    [JsonPropertyName("currentLevel")] public string CurrentLevel { get; set; } = "test-arena";
    [JsonPropertyName("spawnX")] public float SpawnX { get; set; } = 400;
    [JsonPropertyName("spawnY")] public float SpawnY { get; set; } = 502;
    [JsonPropertyName("unlockedAbilities")] public HashSet<string> UnlockedAbilities { get; set; } = new();
    [JsonPropertyName("flags")] public Dictionary<string, bool> Flags { get; set; } = new();
    [JsonPropertyName("playTime")] public float PlayTime { get; set; } = 0f;
    [JsonPropertyName("meleeInventory")] public List<string> MeleeInventory { get; set; } = new();
    [JsonPropertyName("rangedInventory")] public List<string> RangedInventory { get; set; } = new();
    [JsonPropertyName("meleeIndex")] public int MeleeIndex { get; set; } = 0;
    [JsonPropertyName("rangedIndex")] public int RangedIndex { get; set; } = 0;
    [JsonPropertyName("collectedItems")] public HashSet<string> CollectedItems { get; set; } = new(); // item IDs picked up
    [JsonPropertyName("windowSizeIndex")] public int WindowSizeIndex { get; set; } = 0;
    [JsonPropertyName("crtEnabled")] public bool CrtEnabled { get; set; } = false;

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SavePath, json);
    }

    public static SaveData Load()
    {
        if (!File.Exists(SavePath)) return null;
        try
        {
            var json = File.ReadAllText(SavePath);
            return JsonSerializer.Deserialize<SaveData>(json);
        }
        catch { return null; }
    }

    public static bool Exists() => File.Exists(SavePath);

    public static void Delete()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}
