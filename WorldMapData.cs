using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArenaShooter;

public class BiomeData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("levels")] public List<BiomeLevel> Levels { get; set; } = new();
    [JsonPropertyName("cleared")] public bool Cleared { get; set; } = false;

    /// <summary>A biome is cleared when all its levels are cleared</summary>
    public bool CheckCleared() => Levels.Count > 0 && Levels.All(l => l.Cleared);

    public BiomeLevel FindLevel(string levelId) => Levels.FirstOrDefault(l => l.Id == levelId);
}

public class BiomeLevel
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("level")] public string LevelFile { get; set; } = ""; // e.g. "test-arena"
    [JsonPropertyName("cleared")] public bool Cleared { get; set; } = false;
    [JsonPropertyName("discovered")] public bool Discovered { get; set; } = false;
    [JsonPropertyName("order")] public int Order { get; set; } = 0; // sequence within biome
}

public class WorldMapData
{
    private const string SavePath = "Content/worldmap.json";

    public const int MapW = 80;
    public const int MapH = 60;
    public const int TileSize = 32;

    [JsonPropertyName("tiles")] public int[] Tiles { get; set; }
    [JsonPropertyName("fog")] public bool[] Revealed { get; set; }
    [JsonPropertyName("biomes")] public List<BiomeData> Biomes { get; set; } = new();
    [JsonPropertyName("playerX")] public int PlayerX { get; set; } = MapW / 2;
    [JsonPropertyName("playerY")] public int PlayerY { get; set; } = MapH / 2;
    [JsonPropertyName("points")] public List<MapPoint> Points { get; set; } = new();

    public WorldMapData()
    {
        Tiles = new int[MapW * MapH];
        Revealed = new bool[MapW * MapH];
    }

    public MapTileType GetTile(int x, int y)
    {
        if (x < 0 || x >= MapW || y < 0 || y >= MapH) return MapTileType.Ocean;
        return (MapTileType)Tiles[y * MapW + x];
    }

    public void SetTile(int x, int y, MapTileType type)
    {
        if (x < 0 || x >= MapW || y < 0 || y >= MapH) return;
        Tiles[y * MapW + x] = (int)type;
    }

    public bool IsRevealed(int x, int y)
    {
        if (x < 0 || x >= MapW || y < 0 || y >= MapH) return false;
        return Revealed[y * MapW + x];
    }

    public void Reveal(int cx, int cy)
    {
        // 3x3 reveal around position
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int rx = cx + dx, ry = cy + dy;
                if (rx >= 0 && rx < MapW && ry >= 0 && ry < MapH)
                    Revealed[ry * MapW + rx] = true;
            }
    }

    public BiomeData FindBiome(string id) => Biomes.FirstOrDefault(b => b.Id == id);

    /// <summary>Find which biome a level belongs to</summary>
    public BiomeData FindBiomeForLevel(string levelId)
    {
        return Biomes.FirstOrDefault(b => b.Levels.Any(l => l.LevelFile == levelId));
    }

    /// <summary>Mark a level as cleared and check biome completion</summary>
    public void MarkLevelCleared(string levelFile)
    {
        foreach (var biome in Biomes)
        {
            var level = biome.Levels.FirstOrDefault(l => l.LevelFile == levelFile);
            if (level != null)
            {
                level.Cleared = true;
                // Discover next level in sequence
                var next = biome.Levels.FirstOrDefault(l => l.Order == level.Order + 1);
                if (next != null) next.Discovered = true;
                // Check biome completion
                biome.Cleared = biome.CheckCleared();
                break;
            }
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SavePath, json);
    }

    public static WorldMapData Load()
    {
        if (!File.Exists(SavePath)) return null;
        try
        {
            var json = File.ReadAllText(SavePath);
            return JsonSerializer.Deserialize<WorldMapData>(json);
        }
        catch { return null; }
    }

    public static WorldMapData LoadOrCreate()
    {
        var existing = Load();
        if (existing != null) return existing;
        return GenerateDefault();
    }

    public static WorldMapData GenerateDefault()
    {
        var map = new WorldMapData();
        var rng = new Random(42); // deterministic seed

        // Fill with grass
        for (int i = 0; i < map.Tiles.Length; i++)
            map.Tiles[i] = (int)MapTileType.Plains;

        // Generate terrain regions
        // Ocean borders
        for (int x = 0; x < MapW; x++)
        {
            for (int d = 0; d < 3; d++)
            {
                map.SetTile(x, d, MapTileType.Ocean);
                map.SetTile(x, MapH - 1 - d, MapTileType.Ocean);
            }
        }
        for (int y = 0; y < MapH; y++)
        {
            for (int d = 0; d < 3; d++)
            {
                map.SetTile(d, y, MapTileType.Ocean);
                map.SetTile(MapW - 1 - d, y, MapTileType.Ocean);
            }
        }

        // Scatter forests
        for (int i = 0; i < 300; i++)
        {
            int fx = rng.Next(5, MapW - 5);
            int fy = rng.Next(5, MapH - 5);
            int size = rng.Next(1, 4);
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    if (map.GetTile(fx + dx, fy + dy) == MapTileType.Plains)
                        map.SetTile(fx + dx, fy + dy, MapTileType.Forest);
        }

        // Mountain ranges
        for (int i = 0; i < 80; i++)
        {
            int mx = rng.Next(5, MapW - 5);
            int my = rng.Next(5, MapH - 5);
            map.SetTile(mx, my, MapTileType.Mountain);
            if (rng.NextDouble() < 0.5) map.SetTile(mx + 1, my, MapTileType.Mountain);
            if (rng.NextDouble() < 0.5) map.SetTile(mx, my + 1, MapTileType.Mountain);
        }

        // Rivers/lakes
        for (int i = 0; i < 40; i++)
        {
            int rx = rng.Next(5, MapW - 5);
            int ry = rng.Next(5, MapH - 5);
            map.SetTile(rx, ry, MapTileType.Water);
            // Extend river a bit
            for (int j = 0; j < rng.Next(2, 6); j++)
            {
                rx += rng.Next(-1, 2);
                ry += rng.Next(-1, 2);
                if (rx > 3 && rx < MapW - 3 && ry > 3 && ry < MapH - 3)
                    map.SetTile(rx, ry, MapTileType.Water);
            }
        }

        // Desert patches
        for (int i = 0; i < 30; i++)
        {
            int dx = rng.Next(MapW / 2, MapW - 8);
            int dy = rng.Next(5, MapH - 5);
            int size = rng.Next(2, 5);
            for (int yy = 0; yy < size; yy++)
                for (int xx = 0; xx < size; xx++)
                    if (map.GetTile(dx + xx, dy + yy) == MapTileType.Plains)
                        map.SetTile(dx + xx, dy + yy, MapTileType.Desert);
        }

        // ── First Biome: Eden Reach ──
        var edenReach = new BiomeData
        {
            Id = "eden-reach",
            Name = "Eden Reach",
            Levels = new List<BiomeLevel>
            {
                new() { Id = "garden", Name = "The Garden", LevelFile = "test-arena", Order = 0, Discovered = true, Cleared = false },
                new() { Id = "descent", Name = "The Descent", LevelFile = "the-descent", Order = 1, Discovered = false, Cleared = false },
                new() { Id = "gauntlet", Name = "The Gauntlet", LevelFile = "the-gauntlet", Order = 2, Discovered = false, Cleared = false },
                new() { Id = "summit", Name = "The Summit", LevelFile = "the-summit", Order = 3, Discovered = false, Cleared = false },
            }
        };
        map.Biomes.Add(edenReach);

        // Place biome entrance on map
        int biomeX = MapW / 2, biomeY = MapH / 2;
        // Clear area around biome entrance
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                map.SetTile(biomeX + dx, biomeY + dy, MapTileType.Plains);
        map.SetTile(biomeX, biomeY, MapTileType.BiomeEntrance);

        map.Points.Add(new MapPoint
        {
            X = biomeX,
            Y = biomeY,
            BiomeId = "eden-reach",
            Label = "Eden Reach"
        });

        // Player starts near biome entrance
        map.PlayerX = biomeX;
        map.PlayerY = biomeY + 2;

        // Reveal starting area
        map.Reveal(map.PlayerX, map.PlayerY);

        map.Save();
        return map;
    }
}

public class MapPoint
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("biomeId")] public string BiomeId { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
}

public enum MapTileType
{
    Ocean = 0,
    Plains = 1,
    Forest = 2,
    Mountain = 3,
    Water = 4,
    Desert = 5,
    Swamp = 6,
    Snow = 7,
    Path = 10,
    BiomeEntrance = 20,
}
