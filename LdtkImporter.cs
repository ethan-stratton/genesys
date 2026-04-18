using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Genesis;

/// <summary>
/// Imports LDtk project files and converts levels into the game's LevelData format.
/// Supports both old JSON format and LDtk format (detected by "jsonVersion" key).
/// </summary>
public static class LdtkImporter
{
    /// <summary>
    /// Load a level from a file path. Auto-detects old JSON vs LDtk format.
    /// For LDtk files, loads the specified level by name; if null, loads the first level.
    /// </summary>
    public static LevelData LoadLevel(string path, string levelName = null)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Detect LDtk format by presence of "jsonVersion"
        if (root.TryGetProperty("jsonVersion", out _))
        {
            return LoadLdtkLevel(root, levelName);
        }

        // Fall back to old format
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var level = JsonSerializer.Deserialize<LevelData>(json, opts) ?? new LevelData();
        level.Build();
        return level;
    }

    /// <summary>Load all levels from an LDtk project file.</summary>
    public static Dictionary<string, LevelData> LoadAllLevels(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var result = new Dictionary<string, LevelData>();

        if (!root.TryGetProperty("levels", out var levels)) return result;

        foreach (var lvl in levels.EnumerateArray())
        {
            var name = lvl.GetProperty("identifier").GetString() ?? "Untitled";
            result[name] = ParseLevel(lvl, root);
        }

        return result;
    }

    private static LevelData LoadLdtkLevel(JsonElement root, string levelName)
    {
        var levels = root.GetProperty("levels");
        foreach (var lvl in levels.EnumerateArray())
        {
            var id = lvl.GetProperty("identifier").GetString();
            if (levelName == null || string.Equals(id, levelName, StringComparison.OrdinalIgnoreCase))
            {
                return ParseLevel(lvl, root);
            }
        }
        throw new Exception($"Level '{levelName}' not found in LDtk project");
    }

    private static LevelData ParseLevel(JsonElement lvl, JsonElement root)
    {
        var data = new LevelData();
        var id = lvl.GetProperty("identifier").GetString() ?? "Untitled";
        data.Name = id;

        int worldX = lvl.GetProperty("worldX").GetInt32();
        int worldY = lvl.GetProperty("worldY").GetInt32();
        int pxWid = lvl.GetProperty("pxWid").GetInt32();
        int pxHei = lvl.GetProperty("pxHei").GetInt32();

        // Parse level custom fields
        int floorY = pxHei;
        int floorHeight = 0;
        string neighborLeft = "", neighborRight = "", neighborUp = "", neighborDown = "";

        if (lvl.TryGetProperty("fieldInstances", out var fields))
        {
            foreach (var f in fields.EnumerateArray())
            {
                var fid = f.GetProperty("__identifier").GetString();
                var val = f.GetProperty("__value");
                if (val.ValueKind == JsonValueKind.Null) continue;

                switch (fid)
                {
                    case "floorY": floorY = val.GetInt32(); break;
                    case "floorHeight": floorHeight = val.GetInt32(); break;
                    case "isUnderground": data.IsUnderground = val.GetBoolean(); break;
                    case "neighborLeft": neighborLeft = val.GetString() ?? ""; break;
                    case "neighborRight": neighborRight = val.GetString() ?? ""; break;
                    case "neighborUp": neighborUp = val.GetString() ?? ""; break;
                    case "neighborDown": neighborDown = val.GetString() ?? ""; break;
                }
            }
        }

        data.Bounds = new BoundsData { Left = 0, Right = pxWid, Top = 0, Bottom = pxHei };
        data.Floor = new FloorData { Y = floorY, Height = floorHeight };

        // Build neighbors using JsonElement so NeighborData can parse them
        var neighborJson = $"{{\"left\":\"{Esc(neighborLeft)}\",\"right\":\"{Esc(neighborRight)}\",\"up\":\"{Esc(neighborUp)}\",\"down\":\"{Esc(neighborDown)}\"}}";
        data.Neighbors = JsonSerializer.Deserialize<NeighborData>(neighborJson) ?? new NeighborData();

        // Parse layer instances
        if (!lvl.TryGetProperty("layerInstances", out var layers)) { data.Build(); return data; }

        var enemies = new List<EnemySpawnData>();
        var exits = new List<ExitData>();
        var npcs = new List<NpcData>();
        var items = new List<ItemData>();
        var switches = new List<SwitchData>();
        var shelters = new List<ShelterData>();
        var objects = new List<EnvObjectData>();
        var labels = new List<LabelData>();

        foreach (var layer in layers.EnumerateArray())
        {
            var layerId = layer.GetProperty("__identifier").GetString();
            var layerType = layer.GetProperty("__type").GetString();

            if (layerType == "IntGrid" && layerId == "Collision")
            {
                int cWid = layer.GetProperty("__cWid").GetInt32();
                int cHei = layer.GetProperty("__cHei").GetInt32();
                var csv = layer.GetProperty("intGridCsv");
                var tiles = new int[cWid * cHei];
                int i = 0;
                foreach (var v in csv.EnumerateArray())
                {
                    if (i < tiles.Length) tiles[i++] = v.GetInt32();
                }

                data.TileGrid = new TileGridData
                {
                    Width = cWid,
                    Height = cHei,
                    TileSize = 32,
                    OriginX = 0,
                    OriginY = 0,
                    Tiles = tiles
                };
            }
            else if (layerType == "Entities" && layerId == "Entities")
            {
                if (!layer.TryGetProperty("entityInstances", out var entities)) continue;
                foreach (var ent in entities.EnumerateArray())
                {
                    var entId = ent.GetProperty("__identifier").GetString();
                    var px = ent.GetProperty("px");
                    int ex = px[0].GetInt32();
                    int ey = px[1].GetInt32();
                    var fi = GetFieldMap(ent);

                    switch (entId)
                    {
                        case "Enemy":
                            enemies.Add(new EnemySpawnData
                            {
                                Id = GetStr(fi, "id"),
                                Type = GetStr(fi, "type"),
                                X = ex, Y = ey,
                                Count = GetInt(fi, "count", 1),
                                Scale = GetFloat(fi, "scale", 1f),
                                ScaleX = GetFloat(fi, "scaleX", 0f),
                                ScaleY = GetFloat(fi, "scaleY", 0f),
                                Frozen = GetBool(fi, "frozen"),
                                Passive = GetBool(fi, "passive"),
                            });
                            break;
                        case "Exit":
                            exits.Add(new ExitData
                            {
                                Id = GetStr(fi, "id"),
                                X = ex, Y = ey,
                                W = GetInt(fi, "w", 64),
                                H = GetInt(fi, "h", 96),
                                TargetLevel = GetStr(fi, "targetLevel"),
                                TargetExitId = GetStr(fi, "targetExitId"),
                            });
                            break;
                        case "Npc":
                            npcs.Add(new NpcData
                            {
                                Id = GetStr(fi, "id"),
                                Name = GetStr(fi, "name", "NPC"),
                                X = ex, Y = ey,
                                W = GetInt(fi, "w", 24),
                                H = GetInt(fi, "h", 48),
                                Color = GetStr(fi, "color", "Purple"),
                                Dialogue = GetStrArray(fi, "dialogue"),
                                DialogueSpeakers = GetStrArray(fi, "dialogueSpeakers"),
                            });
                            break;
                        case "Item":
                            items.Add(new ItemData
                            {
                                Id = GetStr(fi, "id"),
                                Type = GetStr(fi, "type"),
                                X = ex, Y = ey,
                                W = GetInt(fi, "w", 20),
                                H = GetInt(fi, "h", 20),
                            });
                            break;
                        case "Switch":
                            switches.Add(new SwitchData
                            {
                                Id = GetStr(fi, "id"),
                                X = ex, Y = ey,
                                W = GetInt(fi, "w", 16),
                                H = GetInt(fi, "h", 24),
                                Action = GetStr(fi, "action"),
                                Label = GetStr(fi, "label"),
                            });
                            break;
                        case "Shelter":
                            shelters.Add(new ShelterData
                            {
                                Id = GetStr(fi, "id"),
                                X = ex, Y = ey,
                                Name = GetStr(fi, "name", "Shelter"),
                            });
                            break;
                        case "EnvObject":
                            objects.Add(new EnvObjectData
                            {
                                Id = GetStr(fi, "id"),
                                Type = GetStr(fi, "type"),
                                X = ex, Y = ey,
                                W = GetInt(fi, "w", 40),
                                H = GetInt(fi, "h", 80),
                            });
                            break;
                        case "Label":
                            labels.Add(new LabelData
                            {
                                Text = GetStr(fi, "text"),
                                X = ex, Y = ey,
                                Color = GetStr(fi, "color", "White"),
                                Size = GetStr(fi, "size", "small"),
                            });
                            break;
                        case "PlayerSpawn":
                            data.PlayerSpawn = new PointData { X = ex, Y = ey };
                            break;
                    }
                }
            }
        }

        data.Enemies = enemies.ToArray();
        data.Exits = exits.ToArray();
        data.Npcs = npcs.ToArray();
        data.Items = items.ToArray();
        data.Switches = switches.ToArray();
        data.Shelters = shelters.ToArray();
        data.Objects = objects.ToArray();
        data.Labels = labels.ToArray();

        data.Build();
        return data;
    }

    private static string Esc(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    private static Dictionary<string, JsonElement> GetFieldMap(JsonElement ent)
    {
        var map = new Dictionary<string, JsonElement>();
        if (ent.TryGetProperty("fieldInstances", out var fi))
        {
            foreach (var f in fi.EnumerateArray())
            {
                var key = f.GetProperty("__identifier").GetString();
                if (key != null && f.TryGetProperty("__value", out var val))
                    map[key] = val;
            }
        }
        return map;
    }

    private static string GetStr(Dictionary<string, JsonElement> fi, string key, string def = "")
    {
        if (fi.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? def;
        return def;
    }

    private static int GetInt(Dictionary<string, JsonElement> fi, string key, int def = 0)
    {
        if (fi.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetInt32();
        return def;
    }

    private static float GetFloat(Dictionary<string, JsonElement> fi, string key, float def = 0f)
    {
        if (fi.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number)
            return v.GetSingle();
        return def;
    }

    private static bool GetBool(Dictionary<string, JsonElement> fi, string key, bool def = false)
    {
        if (fi.TryGetValue(key, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        return def;
    }

    private static string[] GetStrArray(Dictionary<string, JsonElement> fi, string key)
    {
        if (fi.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in v.EnumerateArray())
                list.Add(item.GetString() ?? "");
            return list.ToArray();
        }
        return Array.Empty<string>();
    }
}
