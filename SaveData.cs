using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genesis;

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
    [JsonPropertyName("weaponInventory")] public List<string> WeaponInventory { get; set; } = new();
    [JsonPropertyName("leftHand")] public string LeftHand { get; set; } = "None";
    [JsonPropertyName("rightHand")] public string RightHand { get; set; } = "None";
    [JsonPropertyName("rightHand1")] public string RightHand1 { get; set; } = "None";
    [JsonPropertyName("rightHand2")] public string RightHand2 { get; set; } = "None";
    [JsonPropertyName("leftHand1")] public string LeftHand1 { get; set; } = "None";
    [JsonPropertyName("leftHand2")] public string LeftHand2 { get; set; } = "None";
    [JsonPropertyName("rightActiveSlot1")] public bool RightActiveSlot1 { get; set; } = true;
    [JsonPropertyName("leftActiveSlot1")] public bool LeftActiveSlot1 { get; set; } = true;
    [JsonPropertyName("collectedItems")] public HashSet<string> CollectedItems { get; set; } = new(); // item IDs picked up
    [JsonPropertyName("windowSizeIndex")] public int WindowSizeIndex { get; set; } = 0;
    [JsonPropertyName("crtEnabled")] public bool CrtEnabled { get; set; } = false;
    [JsonPropertyName("moveTier")] public int MoveTier { get; set; } = 0; // 0=Tech, 1=Bio, 2=Cipher
    [JsonPropertyName("upgrades")] public List<string> Upgrades { get; set; } = new(); // future: equipped upgrade IDs
    [JsonPropertyName("shelterLevel")] public string ShelterLevel { get; set; } = ""; // last rested level
    [JsonPropertyName("shelterX")] public float ShelterX { get; set; }
    [JsonPropertyName("shelterY")] public float ShelterY { get; set; }
    [JsonPropertyName("deathCount")] public int DeathCount { get; set; } = 0;
    [JsonPropertyName("hp")] public int Hp { get; set; } = 10;
    [JsonPropertyName("suitIntegrity")] public float SuitIntegrity { get; set; } = 31f;
    [JsonPropertyName("battery")] public float Battery { get; set; } = 80f;
    [JsonPropertyName("bestiary")] public Bestiary Bestiary { get; set; } = new();
    [JsonPropertyName("worldTime")] public float WorldTime { get; set; } = 8f;
    [JsonPropertyName("evolutionFlags")] public HashSet<string> EvolutionFlags { get; set; } = new();
    [JsonPropertyName("cipherScanUnlocked")] public bool CipherScanUnlocked { get; set; } = false;
    [JsonPropertyName("hasLantern")] public bool HasLantern { get; set; } = false;
    [JsonPropertyName("hasCipherHelmet")] public bool HasCipherHelmet { get; set; } = false;
    [JsonPropertyName("cipherHelmetEquipped")] public bool CipherHelmetEquipped { get; set; } = false;
    [JsonPropertyName("torchFuel")] public float TorchFuel { get; set; } = 100f;
    [JsonPropertyName("durR1")] public int DurR1 { get; set; }
    [JsonPropertyName("durR2")] public int DurR2 { get; set; }
    [JsonPropertyName("durL1")] public int DurL1 { get; set; }
    [JsonPropertyName("durL2")] public int DurL2 { get; set; }

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

    /// <summary>Migrate old melee/ranged split into unified weapon inventory if needed.</summary>
    public void MigrateWeapons()
    {
        if (WeaponInventory.Count == 0)
        {
            var all = new HashSet<string>();
            if (MeleeInventory != null) foreach (var w in MeleeInventory) all.Add(w);
            if (RangedInventory != null) foreach (var w in RangedInventory) all.Add(w);
            WeaponInventory = new List<string>(all);
            if (MeleeInventory?.Count > 0 && MeleeIndex >= 0 && MeleeIndex < MeleeInventory.Count)
                RightHand = MeleeInventory[MeleeIndex];
            if (RangedInventory?.Count > 0 && RangedIndex >= 0 && RangedIndex < RangedInventory.Count)
                LeftHand = RangedInventory[RangedIndex];
        }
        // Migrate old single LeftHand/RightHand into dual-slot system
        if (RightHand1 == "None" && RightHand != "None")
            RightHand1 = RightHand;
        if (LeftHand1 == "None" && LeftHand != "None")
            LeftHand1 = LeftHand;
    }

    public static void Delete()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}
