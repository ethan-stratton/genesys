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

    public const int MapW = 60;
    public const int MapH = 100;
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

        // ── Step 1: Multi-layer fractal noise ──
        float[,] height = new float[W, H];
        float[,] moisture = new float[W, H];
        float[,] temperature = new float[W, H]; // latitude-based + noise
        float[,] ridgeNoise = new float[W, H];  // for mountain ridge lines

        FractalNoise(height, W, H, seed, 7, 0.025f, 0.52f);
        FractalNoise(moisture, W, H, seed + 100, 5, 0.035f, 0.48f);
        FractalNoise(ridgeNoise, W, H, seed + 300, 4, 0.05f, 0.6f);

        // Temperature: cold at top (north), warm at bottom (south), with noise
        float[,] tempNoise = new float[W, H];
        FractalNoise(tempNoise, W, H, seed + 200, 3, 0.03f, 0.5f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                temperature[x, y] = (float)y / H * 0.7f + tempNoise[x, y] * 0.3f;

        // ── Step 2: Continental shaping ──
        // Elliptical falloff — more generous vertically for tall map
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float nx = 2f * x / W - 1f;
                float ny = 2f * y / H - 1f;
                // Ellipse: x stretched less, y stretched more for vertical continent
                float d = MathF.Sqrt(nx * nx * 1.3f + ny * ny * 0.8f) / 1.15f;
                float falloff = 1f - MathF.Pow(MathHelper.Clamp(d, 0, 1), 2.2f);
                height[x, y] = height[x, y] * falloff;
            }
        }

        // ── Step 3: Mountain ridge injection ──
        // Use ridge noise to create mountain spines (high ridgeNoise = ridge line)
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float ridge = ridgeNoise[x, y];
                // Ridge detection: values near 0.5 are ridge lines (zero crossings)
                float ridgeFactor = 1f - MathF.Abs(ridge - 0.5f) * 2f;
                ridgeFactor = MathF.Pow(MathHelper.Clamp(ridgeFactor, 0, 1), 3f);
                // Boost height along ridges if already above sea level
                if (height[x, y] > 0.3f)
                    height[x, y] += ridgeFactor * 0.25f;
            }
        }

        // ── Step 4: Thermal erosion — smooth steep terrain ──
        for (int pass = 0; pass < 5; pass++)
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
                    if (maxDiff > 0.10f)
                        temp[x, y] = h - maxDiff * 0.15f;
                }
            }
            Array.Copy(temp, height, temp.Length);
        }

        // ── Step 5: Assign biomes from height × temperature × moisture ──
        const float deepSeaLevel = 0.22f;
        const float seaLevel = 0.30f;
        const float beachLevel = 0.33f;
        const float hillLevel = 0.62f;
        const float mountainLevel = 0.72f;
        const float peakLevel = 0.82f;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float h = height[x, y];
                float m = moisture[x, y];
                float t = temperature[x, y]; // 0 = cold (north), ~0.7 = warm (south)

                MapTileType tile;
                if (h < deepSeaLevel)
                    tile = MapTileType.DeepOcean;
                else if (h < seaLevel)
                    tile = MapTileType.Ocean;
                else if (h < beachLevel)
                    tile = MapTileType.Beach;
                else if (h >= peakLevel)
                    tile = MapTileType.Snow; // snow caps
                else if (h >= mountainLevel)
                {
                    // Mountain with snow based on temperature
                    tile = t < 0.35f ? MapTileType.Snow : MapTileType.Mountain;
                }
                else if (h >= hillLevel)
                {
                    // Highland biomes
                    if (t < 0.25f)
                        tile = MapTileType.Tundra;
                    else if (t < 0.4f)
                        tile = m > 0.5f ? MapTileType.SnowForest : MapTileType.Tundra;
                    else
                        tile = m > 0.5f ? MapTileType.Forest : MapTileType.Plains;
                }
                else
                {
                    // Lowland biomes: full temperature × moisture grid
                    if (t < 0.2f)
                    {
                        // Arctic zone
                        tile = m > 0.55f ? MapTileType.SnowForest : MapTileType.Tundra;
                    }
                    else if (t < 0.35f)
                    {
                        // Cold zone
                        if (m > 0.6f) tile = MapTileType.DenseForest;
                        else if (m > 0.4f) tile = MapTileType.Forest;
                        else tile = MapTileType.Plains;
                    }
                    else if (t < 0.55f)
                    {
                        // Temperate zone
                        if (m > 0.6f) tile = MapTileType.DenseForest;
                        else if (m > 0.4f) tile = MapTileType.Forest;
                        else if (m > 0.25f) tile = MapTileType.Plains;
                        else tile = MapTileType.Desert;
                    }
                    else
                    {
                        // Hot zone (southern)
                        if (m > 0.6f) tile = MapTileType.Swamp;
                        else if (m > 0.45f) tile = MapTileType.Forest;
                        else if (m > 0.3f) tile = MapTileType.Plains;
                        else tile = MapTileType.Desert;
                    }
                }
                map.SetTile(x, y, tile);
            }
        }

        // ── Step 6: Rivers — trace from mountains to ocean ──
        var rng = new Random(seed + 200);
        var riverStarts = new List<(int x, int y)>();
        // Find good mountain/high-ground start points
        for (int y = 5; y < H - 5; y++)
            for (int x = 5; x < W - 5; x++)
                if (height[x, y] > 0.65f && height[x, y] < 0.85f)
                    riverStarts.Add((x, y));

        // Shuffle and pick best river starts (spaced apart)
        var usedRiverStarts = new List<(int x, int y)>();
        for (int i = riverStarts.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (riverStarts[i], riverStarts[j]) = (riverStarts[j], riverStarts[i]);
        }
        foreach (var start in riverStarts)
        {
            if (usedRiverStarts.Count >= 12) break;
            bool tooClose = false;
            foreach (var used in usedRiverStarts)
            {
                if (Math.Abs(start.x - used.x) + Math.Abs(start.y - used.y) < 15)
                { tooClose = true; break; }
            }
            if (tooClose) continue;
            usedRiverStarts.Add(start);
        }

        foreach (var start in usedRiverStarts)
        {
            int rx = start.x, ry = start.y;
            for (int step = 0; step < 120; step++)
            {
                if (rx < 1 || rx >= W - 1 || ry < 1 || ry >= H - 1) break;
                if (height[rx, ry] < seaLevel) break;

                var cur = map.GetTile(rx, ry);
                if (cur != MapTileType.BiomeEntrance && cur != MapTileType.Mountain && cur != MapTileType.Snow)
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
                    // Stuck — carve downhill
                    int cdx = rng.Next(-1, 2), cdy = rng.Next(-1, 2);
                    if (rx + cdx >= 0 && rx + cdx < W && ry + cdy >= 0 && ry + cdy < H)
                        height[rx + cdx, ry + cdy] = height[rx, ry] - 0.01f;
                    bestDx = cdx; bestDy = cdy;
                }
                rx += bestDx;
                ry += bestDy;
            }
        }

        // ── Step 7: Caves — place at mountain bases and cliff faces ──
        for (int y = 3; y < H - 3; y++)
        {
            for (int x = 3; x < W - 3; x++)
            {
                var tile = map.GetTile(x, y);
                if (tile != MapTileType.Plains && tile != MapTileType.Forest && tile != MapTileType.Tundra)
                    continue;
                // Check if adjacent to mountain
                bool nearMountain = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var adj = map.GetTile(x + dx, y + dy);
                        if (adj == MapTileType.Mountain || adj == MapTileType.Snow)
                            nearMountain = true;
                    }
                if (!nearMountain) continue;
                // Place caves sparingly
                float caveChance = ridgeNoise[x, y] * moisture[x, y];
                if (caveChance > 0.35f && rng.NextDouble() < 0.15)
                    map.SetTile(x, y, MapTileType.Cave);
            }
        }

        // ── Step 8: Place biomes ──
        // Eden Reach — find plains/forest in temperate zone near center-south
        int biomeX = W / 2, biomeY = (int)(H * 0.55f);
        float bestDist = float.MaxValue;
        for (int y = H / 3; y < 2 * H / 3; y++)
        {
            for (int x = W / 4; x < 3 * W / 4; x++)
            {
                var t = map.GetTile(x, y);
                if (t == MapTileType.Plains || t == MapTileType.Forest)
                {
                    float dist = (x - W / 2f) * (x - W / 2f) + (y - H * 0.55f) * (y - H * 0.55f);
                    if (dist < bestDist) { bestDist = dist; biomeX = x; biomeY = y; }
                }
            }
        }

        // Clear area around entrance
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                var cur = map.GetTile(biomeX + dx, biomeY + dy);
                if (cur == MapTileType.Ocean || cur == MapTileType.DeepOcean ||
                    cur == MapTileType.Mountain || cur == MapTileType.Water)
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

        map.Points.Add(new MapPoint { X = biomeX, Y = biomeY, BiomeId = "eden-reach", Label = "Eden Reach" });

        // Player starts at biome
        map.PlayerX = biomeX;
        map.PlayerY = biomeY + 2;
        if (map.GetTile(map.PlayerX, map.PlayerY) == MapTileType.Ocean ||
            map.GetTile(map.PlayerX, map.PlayerY) == MapTileType.DeepOcean ||
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
    DeepOcean = 8,
    Beach = 9,
    Path = 10,
    Cave = 11,
    DenseForest = 12,
    SnowForest = 13,
    Tundra = 14,
    Volcano = 15,
    BiomeEntrance = 20,
}
