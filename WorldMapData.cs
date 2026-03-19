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
        var rng = new Random(seed);

        // ═══════════════════════════════════════════════
        // STAGE 1: Multiple noise layers
        // ═══════════════════════════════════════════════
        float[,] height = new float[W, H];
        float[,] moisture = new float[W, H];
        float[,] tempNoise = new float[W, H];
        float[,] ridgeNoise = new float[W, H];
        float[,] warpX = new float[W, H];  // domain warping for organic shapes
        float[,] warpY = new float[W, H];
        float[,] detail = new float[W, H]; // high-freq detail noise

        FractalNoise(height, W, H, seed, 7, 0.022f, 0.50f);
        FractalNoise(moisture, W, H, seed + 100, 5, 0.030f, 0.45f);
        FractalNoise(tempNoise, W, H, seed + 200, 3, 0.025f, 0.5f);
        FractalNoise(ridgeNoise, W, H, seed + 300, 5, 0.045f, 0.55f);
        FractalNoise(warpX, W, H, seed + 400, 4, 0.035f, 0.5f);
        FractalNoise(warpY, W, H, seed + 500, 4, 0.035f, 0.5f);
        FractalNoise(detail, W, H, seed + 600, 6, 0.06f, 0.5f);

        float[,] temperature = new float[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                temperature[x, y] = (float)y / H * 0.75f + tempNoise[x, y] * 0.25f;

        // ═══════════════════════════════════════════════
        // STAGE 2: Continent shapes with domain warping
        // Blobs are warped by noise so edges are organic,
        // then combined additively (not max) for land bridges
        // ═══════════════════════════════════════════════
        var continents = new (float cx, float cy, float rx, float ry, float strength)[]
        {
            (0.42f, 0.28f, 0.26f, 0.20f, 1.0f),   // Main continent NW
            (0.72f, 0.38f, 0.16f, 0.18f, 0.80f),   // Eastern landmass
            (0.38f, 0.68f, 0.22f, 0.16f, 0.85f),   // Southern continent
            (0.12f, 0.50f, 0.10f, 0.22f, 0.65f),   // Western archipelago (tall/thin)
            (0.60f, 0.10f, 0.12f, 0.07f, 0.55f),   // Northern island
            (0.55f, 0.52f, 0.08f, 0.12f, 0.50f),   // Peninsula bridge
            (0.65f, 0.85f, 0.10f, 0.07f, 0.50f),   // Tropical island SE
            (0.25f, 0.88f, 0.08f, 0.06f, 0.40f),   // Small SW island
        };

        float[,] landMask = new float[W, H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float nx = (float)x / W;
                float ny = (float)y / H;
                // Domain warping — shift sample point by noise for organic shapes
                float wnx = nx + (warpX[x, y] - 0.5f) * 0.12f;
                float wny = ny + (warpY[x, y] - 0.5f) * 0.12f;

                float sum = 0f;
                foreach (var (cx, cy, rx, ry, str) in continents)
                {
                    float dx = (wnx - cx) / rx;
                    float dy = (wny - cy) / ry;
                    float dist = dx * dx + dy * dy;
                    float val = str * MathF.Exp(-dist * 2.0f);
                    sum += val; // additive — creates land bridges where blobs overlap
                }
                // Multiply by detail noise for coastline complexity
                float coastNoise = 0.6f + detail[x, y] * 0.8f;
                landMask[x, y] = sum * coastNoise;
            }
        }

        // Combine landmask with height noise
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float mask = landMask[x, y];
                float h = height[x, y];
                float combined = mask * 0.55f + h * mask * 0.45f;
                // Edge fade (3 tiles)
                float edgeDist = MathF.Min(MathF.Min(x, W - 1 - x), MathF.Min(y, H - 1 - y));
                float edgeFade = MathHelper.Clamp(edgeDist / 3f, 0f, 1f);
                height[x, y] = combined * edgeFade;
            }
        }

        // Normalize
        Normalize(height, W, H);

        // ═══════════════════════════════════════════════
        // STAGE 3: Mountain ridges — ridge noise creates
        // visible chains, stronger effect
        // ═══════════════════════════════════════════════
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float ridge = ridgeNoise[x, y];
                float ridgeFactor = 1f - MathF.Abs(ridge - 0.5f) * 2f;
                ridgeFactor = MathF.Pow(MathHelper.Clamp(ridgeFactor, 0, 1), 2.0f);
                if (height[x, y] > 0.28f)
                    height[x, y] += ridgeFactor * 0.35f;
            }
        }
        Normalize(height, W, H);

        // ═══════════════════════════════════════════════
        // STAGE 4: Basin filling (Planchon-Darboux)
        // ═══════════════════════════════════════════════
        const float seaLevel = 0.38f;
        FillBasins(height, W, H, seaLevel);

        // Noise + refill cycle
        var noiseRng = new Random(seed + 700);
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
                if (height[x, y] >= seaLevel)
                    height[x, y] += (float)(noiseRng.NextDouble() - 0.5) * 0.004f;
        FillBasins(height, W, H, seaLevel);

        // ═══════════════════════════════════════════════
        // STAGE 5: Water flux + incise flow
        // ═══════════════════════════════════════════════
        float[,] flux = ComputeFlux(height, W, H);

        // Incise flow erosion
        int[,] flowDirX = new int[W, H], flowDirY = new int[W, H];
        ComputeFlowDirs(height, W, H, flowDirX, flowDirY);
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (height[x, y] < seaLevel) continue;
                int tx = x + flowDirX[x, y], ty = y + flowDirY[x, y];
                if (tx < 0 || tx >= W || ty < 0 || ty >= H) continue;
                float slope = height[x, y] - height[tx, ty];
                if (slope <= 0) continue;
                float erosion = MathF.Min(slope * MathF.Sqrt(flux[x, y]) * 0.012f, 0.025f);
                height[x, y] -= erosion;
            }

        // ═══════════════════════════════════════════════
        // STAGE 6: Thermal erosion (3 passes)
        // ═══════════════════════════════════════════════
        for (int pass = 0; pass < 3; pass++)
        {
            var temp = (float[,])height.Clone();
            for (int y = 1; y < H - 1; y++)
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
                    if (maxDiff > 0.08f) temp[x, y] = h - maxDiff * 0.10f;
                }
            Array.Copy(temp, height, temp.Length);
        }

        // ═══════════════════════════════════════════════
        // STAGE 7: Coastline smoothing (3 passes)
        // ═══════════════════════════════════════════════
        for (int pass = 0; pass < 3; pass++)
        {
            var temp = (float[,])height.Clone();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int land = 0, water = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (height[x + dx, y + dy] >= seaLevel) land++; else water++;
                        }
                    if (height[x, y] < seaLevel && land >= 6) temp[x, y] = seaLevel + 0.02f;
                    else if (height[x, y] >= seaLevel && water >= 6) temp[x, y] = seaLevel - 0.02f;
                }
            Array.Copy(temp, height, temp.Length);
        }

        // ═══════════════════════════════════════════════
        // STAGE 8: Biome assignment
        // ═══════════════════════════════════════════════
        const float deepSea = 0.18f;
        const float shallowSea = 0.30f;
        const float beachTop = 0.41f;
        const float lowland = 0.52f;
        const float hillStart = 0.60f;
        const float mountainStart = 0.70f;
        const float highMountain = 0.80f;
        const float peakLevel = 0.90f;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float h = height[x, y];
                float m = moisture[x, y];
                float t = temperature[x, y];

                MapTileType tile;

                if (h < deepSea)
                    tile = MapTileType.DeepOcean;
                else if (h < shallowSea)
                    tile = MapTileType.Ocean;
                else if (h < seaLevel)
                    tile = (t > 0.5f && m > 0.4f) ? MapTileType.Reef : MapTileType.ShallowOcean;
                else if (h < beachTop)
                {
                    if (t < 0.2f) tile = MapTileType.RockyShore;
                    else if (m < 0.3f) tile = MapTileType.RockyShore;
                    else tile = MapTileType.Beach;
                }
                else if (h >= peakLevel)
                    tile = t < 0.4f ? MapTileType.Glacier : MapTileType.Snow;
                else if (h >= highMountain)
                    tile = t < 0.3f ? MapTileType.Snow : MapTileType.HighMountain;
                else if (h >= mountainStart)
                    tile = t < 0.25f ? MapTileType.Snow : MapTileType.Mountain;
                else if (h >= hillStart)
                {
                    if (t < 0.2f) tile = MapTileType.Tundra;
                    else if (t < 0.35f) tile = m > 0.5f ? MapTileType.Taiga : MapTileType.Foothills;
                    else tile = m > 0.5f ? MapTileType.Forest : MapTileType.Hills;
                }
                else if (h >= lowland)
                {
                    if (t < 0.2f) tile = m > 0.55f ? MapTileType.SnowForest : MapTileType.Tundra;
                    else if (t < 0.35f)
                    {
                        if (m > 0.6f) tile = MapTileType.DenseForest;
                        else if (m > 0.4f) tile = MapTileType.Taiga;
                        else tile = MapTileType.Grassland;
                    }
                    else if (t < 0.55f)
                    {
                        if (m > 0.6f) tile = MapTileType.DenseForest;
                        else if (m > 0.4f) tile = MapTileType.Forest;
                        else if (m > 0.25f) tile = MapTileType.Plains;
                        else tile = MapTileType.Wasteland;
                    }
                    else
                    {
                        if (m > 0.6f) tile = MapTileType.Jungle;
                        else if (m > 0.45f) tile = MapTileType.Forest;
                        else if (m > 0.3f) tile = MapTileType.Savanna;
                        else tile = MapTileType.Desert;
                    }
                }
                else
                {
                    if (t < 0.2f) tile = m > 0.5f ? MapTileType.SnowForest : MapTileType.Tundra;
                    else if (t < 0.35f)
                    {
                        if (m > 0.6f) tile = MapTileType.DenseForest;
                        else if (m > 0.45f) tile = MapTileType.Forest;
                        else tile = MapTileType.Grassland;
                    }
                    else if (t < 0.55f)
                    {
                        if (m > 0.65f) tile = MapTileType.Swamp;
                        else if (m > 0.5f) tile = MapTileType.Forest;
                        else if (m > 0.35f) tile = MapTileType.Plains;
                        else tile = MapTileType.Grassland;
                    }
                    else
                    {
                        if (m > 0.6f) tile = MapTileType.Marsh;
                        else if (m > 0.45f) tile = MapTileType.Savanna;
                        else if (m > 0.3f) tile = MapTileType.Plains;
                        else tile = MapTileType.Desert;
                    }
                }

                if (flux[x, y] > 20f && h >= seaLevel && h < hillStart &&
                    (tile == MapTileType.Plains || tile == MapTileType.Grassland))
                    tile = MapTileType.Floodplain;

                map.SetTile(x, y, tile);
            }
        }

        // ═══════════════════════════════════════════════
        // STAGE 9: Rivers
        // ═══════════════════════════════════════════════
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (height[x, y] < seaLevel || flux[x, y] < 60f) continue;
                var cur = map.GetTile(x, y);
                if (cur == MapTileType.BiomeEntrance || cur == MapTileType.Mountain ||
                    cur == MapTileType.HighMountain || cur == MapTileType.Snow ||
                    cur == MapTileType.Glacier) continue;
                map.SetTile(x, y, flux[x, y] > 150f ? MapTileType.Lake : MapTileType.River);
            }

        // ═══════════════════════════════════════════════
        // STAGE 10: Lakes
        // ═══════════════════════════════════════════════
        for (int y = 2; y < H - 2; y++)
            for (int x = 2; x < W - 2; x++)
            {
                if (height[x, y] < seaLevel || flux[x, y] < 100f) continue;
                bool isMin = true;
                for (int dy = -1; dy <= 1 && isMin; dy++)
                    for (int dx = -1; dx <= 1 && isMin; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (height[x + dx, y + dy] < height[x, y]) isMin = false;
                    }
                if (!isMin) continue;
                int sz = flux[x, y] > 200f ? 2 : 1;
                for (int dy = -sz; dy <= sz; dy++)
                    for (int dx = -sz; dx <= sz; dx++)
                    {
                        int lx = x + dx, ly = y + dy;
                        if (lx < 0 || lx >= W || ly < 0 || ly >= H || height[lx, ly] < seaLevel) continue;
                        map.SetTile(lx, ly, temperature[lx, ly] < 0.2f ? MapTileType.FrozenLake : MapTileType.Lake);
                    }
            }

        // ═══════════════════════════════════════════════
        // STAGE 11: Desert detail
        // ═══════════════════════════════════════════════
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (map.GetTile(x, y) != MapTileType.Desert) continue;
                if (ridgeNoise[x, y] > 0.55f) map.SetTile(x, y, MapTileType.Dunes);
                bool nearWater = false;
                for (int dy = -2; dy <= 2 && !nearWater; dy++)
                    for (int dx = -2; dx <= 2 && !nearWater; dx++)
                    {
                        int nx2 = x + dx, ny2 = y + dy;
                        if (nx2 >= 0 && nx2 < W && ny2 >= 0 && ny2 < H)
                        {
                            var a = map.GetTile(nx2, ny2);
                            if (a == MapTileType.River || a == MapTileType.Lake) nearWater = true;
                        }
                    }
                if (nearWater && rng.NextDouble() < 0.3) map.SetTile(x, y, MapTileType.Oasis);
            }

        // ═══════════════════════════════════════════════
        // STAGE 12: Caves
        // ═══════════════════════════════════════════════
        for (int y = 2; y < H - 2; y++)
            for (int x = 2; x < W - 2; x++)
            {
                var tile = map.GetTile(x, y);
                if (tile != MapTileType.Foothills && tile != MapTileType.Hills &&
                    tile != MapTileType.Tundra && tile != MapTileType.RockyShore) continue;
                bool nearMtn = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var a = map.GetTile(x + dx, y + dy);
                        if (a == MapTileType.Mountain || a == MapTileType.HighMountain) nearMtn = true;
                    }
                if (nearMtn && ridgeNoise[x, y] * moisture[x, y] > 0.3f && rng.NextDouble() < 0.10)
                    map.SetTile(x, y, MapTileType.Cave);
            }

        // ═══════════════════════════════════════════════
        // STAGE 13: Ruins
        // ═══════════════════════════════════════════════
        for (int y = 3; y < H - 3; y++)
            for (int x = 3; x < W - 3; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.DenseForest || tile == MapTileType.Wasteland ||
                     tile == MapTileType.Jungle) && rng.NextDouble() < 0.02)
                    map.SetTile(x, y, MapTileType.Ruins);
            }

        // ═══════════════════════════════════════════════
        // STAGE 14: Volcano
        // ═══════════════════════════════════════════════
        for (int y = H / 2; y < H - 5; y++)
            for (int x = 5; x < W - 5; x++)
            {
                if (map.GetTile(x, y) == MapTileType.HighMountain && temperature[x, y] > 0.45f)
                {
                    map.SetTile(x, y, MapTileType.Volcano);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var a = map.GetTile(x + dx, y + dy);
                            if (a != MapTileType.HighMountain && a != MapTileType.Mountain)
                                map.SetTile(x + dx, y + dy, MapTileType.Wasteland);
                        }
                    goto volcanoPlaced;
                }
            }
        volcanoPlaced:;

        // ═══════════════════════════════════════════════
        // STAGE 15: Fantasy biomes
        // ═══════════════════════════════════════════════

        // Crystal Forest — cold + moist
        int crystalCount = 0;
        for (int y = 3; y < H / 3 && crystalCount < 12; y++)
            for (int x = 3; x < W - 3 && crystalCount < 12; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.DenseForest || tile == MapTileType.SnowForest) &&
                    temperature[x, y] < 0.3f && moisture[x, y] > 0.6f && rng.NextDouble() < 0.08)
                {
                    map.SetTile(x, y, MapTileType.CrystalForest);
                    crystalCount++;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (rng.NextDouble() < 0.4)
                            {
                                int nx2 = x + dx, ny2 = y + dy;
                                if (nx2 > 0 && nx2 < W && ny2 > 0 && ny2 < H)
                                {
                                    var a = map.GetTile(nx2, ny2);
                                    if (a == MapTileType.DenseForest || a == MapTileType.SnowForest ||
                                        a == MapTileType.Forest || a == MapTileType.Taiga)
                                    { map.SetTile(nx2, ny2, MapTileType.CrystalForest); crystalCount++; }
                                }
                            }
                }
            }

        // Ashlands — ring around volcano
        for (int y = 5; y < H - 5; y++)
            for (int x = 5; x < W - 5; x++)
            {
                if (map.GetTile(x, y) != MapTileType.Volcano) continue;
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        if (dx * dx + dy * dy > 18) continue;
                        int nx2 = x + dx, ny2 = y + dy;
                        if (nx2 < 0 || nx2 >= W || ny2 < 0 || ny2 >= H) continue;
                        var a = map.GetTile(nx2, ny2);
                        if (a == MapTileType.Volcano || a == MapTileType.HighMountain ||
                            a == MapTileType.DeepOcean || a == MapTileType.Ocean) continue;
                        if (rng.NextDouble() < 0.7)
                            map.SetTile(nx2, ny2, MapTileType.Ashlands);
                    }
            }

        // Mushroom — deep swamp/jungle
        int mushCount = 0;
        for (int y = H / 2; y < H - 3 && mushCount < 10; y++)
            for (int x = 3; x < W - 3 && mushCount < 10; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.Swamp || tile == MapTileType.Jungle || tile == MapTileType.Marsh) &&
                    moisture[x, y] > 0.65f && rng.NextDouble() < 0.06)
                {
                    map.SetTile(x, y, MapTileType.Mushroom);
                    mushCount++;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (rng.NextDouble() < 0.35)
                            {
                                int nx2 = x + dx, ny2 = y + dy;
                                if (nx2 > 0 && nx2 < W && ny2 > 0 && ny2 < H)
                                {
                                    var a = map.GetTile(nx2, ny2);
                                    if (a == MapTileType.Swamp || a == MapTileType.Jungle ||
                                        a == MapTileType.Marsh || a == MapTileType.DenseForest)
                                    { map.SetTile(nx2, ny2, MapTileType.Mushroom); mushCount++; }
                                }
                            }
                }
            }

        // Petrified — wasteland/desert
        int petCount = 0;
        for (int y = H / 3; y < 2 * H / 3 && petCount < 8; y++)
            for (int x = 3; x < W - 3 && petCount < 8; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.Wasteland || tile == MapTileType.Desert) &&
                    ridgeNoise[x, y] > 0.45f && ridgeNoise[x, y] < 0.55f && rng.NextDouble() < 0.05)
                { map.SetTile(x, y, MapTileType.Petrified); petCount++; }
            }

        // Void Rift — single anomaly far from center
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int vx = rng.Next(5, W - 5), vy = rng.Next(5, H - 5);
            if (MathF.Sqrt((vx - W / 2f) * (vx - W / 2f) + (vy - H / 2f) * (vy - H / 2f)) < 15) continue;
            var tile = map.GetTile(vx, vy);
            if (tile == MapTileType.Mountain || tile == MapTileType.HighMountain ||
                tile == MapTileType.Wasteland || tile == MapTileType.Tundra)
            {
                map.SetTile(vx, vy, MapTileType.VoidRift);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx2 = vx + dx, ny2 = vy + dy;
                        if (nx2 > 0 && nx2 < W && ny2 > 0 && ny2 < H && !(dx == 0 && dy == 0))
                        {
                            var a = map.GetTile(nx2, ny2);
                            if (a != MapTileType.DeepOcean && a != MapTileType.Ocean && a != MapTileType.Volcano)
                                map.SetTile(nx2, ny2, MapTileType.Wasteland);
                        }
                    }
                break;
            }
        }

        // ═══════════════════════════════════════════════
        // STAGE 16: Place Eden Reach
        // ═══════════════════════════════════════════════
        int biomeX = W / 2, biomeY = (int)(H * 0.45f);
        float bestDist = float.MaxValue;
        for (int y = H / 4; y < 3 * H / 4; y++)
            for (int x = W / 4; x < 3 * W / 4; x++)
            {
                var t = map.GetTile(x, y);
                if (t == MapTileType.Plains || t == MapTileType.Grassland || t == MapTileType.Forest)
                {
                    float d = (x - W / 2f) * (x - W / 2f) + (y - H * 0.45f) * (y - H * 0.45f);
                    if (d < bestDist) { bestDist = d; biomeX = x; biomeY = y; }
                }
            }

        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int px = biomeX + dx, py = biomeY + dy;
                if (px >= 0 && px < W && py >= 0 && py < H)
                {
                    var cur = map.GetTile(px, py);
                    if (cur == MapTileType.DeepOcean || cur == MapTileType.Ocean ||
                        cur == MapTileType.ShallowOcean || cur == MapTileType.HighMountain ||
                        cur == MapTileType.River || cur == MapTileType.Lake)
                        map.SetTile(px, py, MapTileType.Plains);
                }
            }
        map.SetTile(biomeX, biomeY, MapTileType.BiomeEntrance);

        var edenReach = new BiomeData
        {
            Id = "eden-reach", Name = "Eden Reach",
            Levels = new List<BiomeLevel>
            {
                new() { Id = "garden", Name = "The Garden", LevelFile = "test-arena", Order = 0, Discovered = true, Cleared = false },
                new() { Id = "descent", Name = "The Descent", LevelFile = "the-descent", Order = 1 },
                new() { Id = "gauntlet", Name = "The Gauntlet", LevelFile = "the-gauntlet", Order = 2 },
                new() { Id = "summit", Name = "The Summit", LevelFile = "the-summit", Order = 3 },
            }
        };
        map.Biomes.Add(edenReach);
        map.Points.Add(new MapPoint { X = biomeX, Y = biomeY, BiomeId = "eden-reach", Label = "Eden Reach" });

        map.PlayerX = biomeX;
        map.PlayerY = biomeY + 2;
        if (map.GetTile(map.PlayerX, map.PlayerY) == MapTileType.Ocean ||
            map.GetTile(map.PlayerX, map.PlayerY) == MapTileType.DeepOcean)
            map.SetTile(map.PlayerX, map.PlayerY, MapTileType.Plains);

        map.Reveal(map.PlayerX, map.PlayerY);
        map.Save();
        return map;
    }



    // ── Helper: normalize height to 0-1 ──
    private static void Normalize(float[,] h, int w, int hh)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int y = 0; y < hh; y++)
            for (int x = 0; x < w; x++)
            { if (h[x, y] < min) min = h[x, y]; if (h[x, y] > max) max = h[x, y]; }
        float range = max - min;
        if (range < 0.001f) range = 1f;
        for (int y = 0; y < hh; y++)
            for (int x = 0; x < w; x++)
                h[x, y] = (h[x, y] - min) / range;
    }

    // ── Helper: Planchon-Darboux basin filling ──
    private static void FillBasins(float[,] height, int w, int h, float sea)
    {
        float[,] filled = new float[w, h];
        const float eps = 0.001f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                filled[x, y] = (x == 0 || x == w - 1 || y == 0 || y == h - 1 || height[x, y] < sea)
                    ? height[x, y] : 10f;
        bool changed = true;
        int maxIter = 50;
        while (changed && maxIter-- > 0)
        {
            changed = false;
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (filled[x, y] <= height[x, y]) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            float nVal = filled[x + dx, y + dy] + eps;
                            if (height[x, y] >= nVal) { filled[x, y] = height[x, y]; changed = true; }
                            else if (filled[x, y] > nVal) { filled[x, y] = nVal; changed = true; }
                        }
                }
        }
        Array.Copy(filled, height, filled.Length);
    }

    // ── Helper: compute flow directions ──
    private static void ComputeFlowDirs(float[,] height, int w, int h, int[,] fdx, int[,] fdy)
    {
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float lowest = height[x, y]; int bx = 0, by = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && height[nx, ny] < lowest)
                        { lowest = height[nx, ny]; bx = dx; by = dy; }
                    }
                fdx[x, y] = bx; fdy[x, y] = by;
            }
    }

    // ── Helper: compute water flux ──
    private static float[,] ComputeFlux(float[,] height, int w, int h)
    {
        int[,] fdx = new int[w, h], fdy = new int[w, h];
        ComputeFlowDirs(height, w, h, fdx, fdy);
        var cells = new List<(int x, int y, float h)>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                cells.Add((x, y, height[x, y]));
        cells.Sort((a, b) => b.h.CompareTo(a.h));
        float[,] flux = new float[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                flux[x, y] = 1f;
        foreach (var (cx, cy, _) in cells)
        {
            int tx = cx + fdx[cx, cy], ty = cy + fdy[cx, cy];
            if (tx >= 0 && tx < w && ty >= 0 && ty < h)
                flux[tx, ty] += flux[cx, cy];
        }
        return flux;
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
    DeepOcean = 0,
    Ocean = 1,
    ShallowOcean = 2,
    Reef = 3,
    Beach = 4,
    RockyShore = 5,
    Plains = 6,
    Grassland = 7,
    Savanna = 8,
    Forest = 9,
    DenseForest = 10,
    Jungle = 11,
    Hills = 12,
    Foothills = 13,
    Mountain = 14,
    HighMountain = 15,
    Snow = 16,
    Glacier = 17,
    Tundra = 18,
    Taiga = 19,
    SnowForest = 20,
    FrozenLake = 21,
    Desert = 22,
    Dunes = 23,
    Wasteland = 24,
    Swamp = 25,
    Marsh = 26,
    Floodplain = 27,
    Water = 28,  // river/lake
    Lake = 29,
    River = 30,
    Cave = 31,
    Volcano = 32,
    Ruins = 33,
    Oasis = 34,
    // Fantasy / mystery biomes
    CrystalForest = 35,
    Ashlands = 36,
    VoidRift = 37,
    Mushroom = 38,
    Petrified = 39,
    Path = 40,
    BiomeEntrance = 50,
}
