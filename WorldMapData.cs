using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

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
        const int W = MapW, H = MapH;
        const int seed = 42;

        // ── Step 1: Multi-octave fractal noise heightmap ──
        float[,] height = new float[W, H];
        float[,] moisture = new float[W, H];
        FractalNoise(height, W, H, seed, 6, 0.028f, 0.55f);
        FractalNoise(moisture, W, H, seed + 100, 4, 0.04f, 0.5f);

        // ── Step 2: Island shaping — fade edges to ocean ──
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Normalized distance from center (0 = center, 1 = corner)
                float nx = 2f * x / W - 1f;
                float ny = 2f * y / H - 1f;
                float d = MathF.Sqrt(nx * nx + ny * ny) / 1.2f; // 1.2 = allow land near edges
                // Smooth falloff
                float falloff = 1f - MathF.Pow(MathHelper.Clamp(d, 0, 1), 2.5f);
                height[x, y] = height[x, y] * falloff;
            }
        }

        // ── Step 3: Simple thermal erosion (smooth steep slopes) ──
        for (int pass = 0; pass < 3; pass++)
        {
            float[,] temp = (float[,])height.Clone();
            for (int y = 1; y < H - 1; y++)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    float h = height[x, y];
                    float maxDiff = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            float diff = h - height[x + dx, y + dy];
                            if (diff > maxDiff) maxDiff = diff;
                        }
                    // If slope is too steep, smooth it
                    if (maxDiff > 0.12f)
                        temp[x, y] = h - maxDiff * 0.2f;
                }
            }
            Array.Copy(temp, height, temp.Length);
        }

        // ── Step 4: Assign terrain from heightmap + moisture ──
        const float seaLevel = 0.32f;
        const float beachLevel = 0.35f;
        const float mountainLevel = 0.72f;
        const float snowLevel = 0.85f;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float h = height[x, y];
                float m = moisture[x, y];

                MapTileType tile;
                if (h < seaLevel)
                    tile = MapTileType.Ocean;
                else if (h < beachLevel)
                    tile = MapTileType.Desert; // beach/sand
                else if (h >= snowLevel)
                    tile = MapTileType.Snow;
                else if (h >= mountainLevel)
                    tile = MapTileType.Mountain;
                else
                {
                    // Biome from moisture + height
                    if (m > 0.6f)
                        tile = MapTileType.Forest;
                    else if (m > 0.45f)
                        tile = MapTileType.Plains;
                    else if (m > 0.3f)
                        tile = h > 0.55f ? MapTileType.Plains : MapTileType.Swamp;
                    else
                        tile = MapTileType.Desert;
                }
                map.SetTile(x, y, tile);
            }
        }

        // ── Step 5: Rivers — trace downhill from high points ──
        var rng = new Random(seed + 200);
        for (int r = 0; r < 8; r++)
        {
            // Start river at a random high point
            int rx = rng.Next(10, W - 10);
            int ry = rng.Next(10, H - 10);
            if (height[rx, ry] < 0.55f) continue; // need to start high

            for (int step = 0; step < 60; step++)
            {
                if (rx < 1 || rx >= W - 1 || ry < 1 || ry >= H - 1) break;
                if (height[rx, ry] < seaLevel) break; // reached ocean

                var cur = map.GetTile(rx, ry);
                if (cur != MapTileType.BiomeEntrance)
                    map.SetTile(rx, ry, MapTileType.Water);

                // Find lowest neighbor
                float lowestH = height[rx, ry];
                int bestDx = 0, bestDy = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = rx + dx, ny = ry + dy;
                        if (nx >= 0 && nx < W && ny >= 0 && ny < H && height[nx, ny] < lowestH)
                        {
                            lowestH = height[nx, ny];
                            bestDx = dx;
                            bestDy = dy;
                        }
                    }
                if (bestDx == 0 && bestDy == 0)
                {
                    // Stuck — carve slightly downhill in random direction
                    bestDx = rng.Next(-1, 2);
                    bestDy = rng.Next(-1, 2);
                    if (rx + bestDx >= 0 && rx + bestDx < W && ry + bestDy >= 0 && ry + bestDy < H)
                        height[rx + bestDx, ry + bestDy] = height[rx, ry] - 0.01f;
                }
                rx += bestDx;
                ry += bestDy;
            }
        }

        // ── Step 6: Place biome — find good inland location ──
        // Find a plains tile near center
        int biomeX = W / 2, biomeY = H / 2;
        float bestDist = float.MaxValue;
        for (int y = H / 4; y < 3 * H / 4; y++)
        {
            for (int x = W / 4; x < 3 * W / 4; x++)
            {
                if (height[x, y] > beachLevel + 0.05f && height[x, y] < mountainLevel - 0.1f)
                {
                    float dist = (x - W / 2f) * (x - W / 2f) + (y - H / 2f) * (y - H / 2f);
                    if (dist < bestDist) { bestDist = dist; biomeX = x; biomeY = y; }
                }
            }
        }

        // Clear area around biome entrance
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                var cur = map.GetTile(biomeX + dx, biomeY + dy);
                if (cur == MapTileType.Ocean || cur == MapTileType.Mountain || cur == MapTileType.Water)
                    map.SetTile(biomeX + dx, biomeY + dy, MapTileType.Plains);
            }
        map.SetTile(biomeX, biomeY, MapTileType.BiomeEntrance);

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

        map.Points.Add(new MapPoint
        {
            X = biomeX,
            Y = biomeY,
            BiomeId = "eden-reach",
            Label = "Eden Reach"
        });

        // Player starts at biome entrance
        map.PlayerX = biomeX;
        map.PlayerY = biomeY + 2;
        // Make sure spawn is walkable
        if (map.GetTile(map.PlayerX, map.PlayerY) == MapTileType.Ocean ||
            map.GetTile(map.PlayerX, map.PlayerY) == MapTileType.Mountain)
            map.SetTile(map.PlayerX, map.PlayerY, MapTileType.Plains);

        map.Reveal(map.PlayerX, map.PlayerY);

        map.Save();
        return map;
    }

    // ── Fractal Brownian Motion noise ──
    private static void FractalNoise(float[,] grid, int w, int h, int seed, int octaves, float baseFreq, float persistence)
    {
        var perm = GeneratePermutation(seed);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float val = 0, amp = 1f, freq = baseFreq, maxAmp = 0;
                for (int o = 0; o < octaves; o++)
                {
                    val += amp * Perlin2D(x * freq, y * freq, perm);
                    maxAmp += amp;
                    amp *= persistence;
                    freq *= 2f;
                }
                grid[x, y] = (val / maxAmp + 1f) * 0.5f; // normalize to 0-1
            }
        }
    }

    private static int[] GeneratePermutation(int seed)
    {
        var rng = new Random(seed);
        var p = new int[512];
        var source = new int[256];
        for (int i = 0; i < 256; i++) source[i] = i;
        // Fisher-Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (source[i], source[j]) = (source[j], source[i]);
        }
        for (int i = 0; i < 512; i++) p[i] = source[i & 255];
        return p;
    }

    private static float Perlin2D(float x, float y, int[] perm)
    {
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;
        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = perm[perm[xi] + yi];
        int ab = perm[perm[xi] + yi + 1];
        int ba = perm[perm[xi + 1] + yi];
        int bb = perm[perm[xi + 1] + yi + 1];

        float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static float Lerp(float a, float b, float t) => a + t * (b - a);
    private static float Grad(int hash, float x, float y)
    {
        return (hash & 3) switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            _ => -x - y,
        };
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
