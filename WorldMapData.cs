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

        // ═══════════════════════════════════════════════════
        // STAGE 1: Base heightmap — multi-octave fractal noise
        // ═══════════════════════════════════════════════════
        float[,] height = new float[W, H];
        float[,] moisture = new float[W, H];
        float[,] tempNoise = new float[W, H];
        float[,] ridgeNoise = new float[W, H];

        FractalNoise(height, W, H, seed, 7, 0.022f, 0.50f);
        FractalNoise(moisture, W, H, seed + 100, 5, 0.030f, 0.45f);
        FractalNoise(tempNoise, W, H, seed + 200, 3, 0.025f, 0.5f);
        FractalNoise(ridgeNoise, W, H, seed + 300, 5, 0.045f, 0.55f);

        // Temperature: latitude gradient (cold north → warm south) + noise
        float[,] temperature = new float[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                temperature[x, y] = (float)y / H * 0.75f + tempNoise[x, y] * 0.25f;

        // ═══════════════════════════════════════════════════
        // STAGE 2: Multi-continent shaping
        // Instead of one ellipse, scatter multiple landmass blobs
        // with irregular shapes and channels between them
        // ═══════════════════════════════════════════════════
        
        // Define continent centers (normalized 0-1 coords)
        var continents = new (float cx, float cy, float rx, float ry, float strength)[]
        {
            // Main continent (large, center-north)
            (0.45f, 0.30f, 0.28f, 0.22f, 1.0f),
            // Eastern landmass
            (0.75f, 0.45f, 0.18f, 0.20f, 0.85f),
            // Southern continent (larger)
            (0.40f, 0.72f, 0.25f, 0.18f, 0.90f),
            // Western islands chain
            (0.15f, 0.55f, 0.12f, 0.15f, 0.70f),
            // Small northern island
            (0.65f, 0.12f, 0.10f, 0.08f, 0.60f),
            // Peninsula connecting main + south
            (0.50f, 0.52f, 0.10f, 0.14f, 0.65f),
            // Far south tropical island
            (0.60f, 0.88f, 0.11f, 0.08f, 0.55f),
        };

        float[,] landMask = new float[W, H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float nx = (float)x / W;
                float ny = (float)y / H;
                float best = 0f;
                foreach (var (cx, cy, rx, ry, str) in continents)
                {
                    float dx = (nx - cx) / rx;
                    float dy = (ny - cy) / ry;
                    float dist = dx * dx + dy * dy;
                    // Gaussian blob with noise perturbation for irregular coastlines
                    float noiseWarp = height[x, y] * 0.4f; // use base noise to warp shape
                    float val = str * MathF.Exp(-(dist * (1.8f - noiseWarp)));
                    if (val > best) best = val;
                }
                landMask[x, y] = best;
            }
        }

        // Combine: height = noise * landmask, with land mask dominating shape
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float mask = landMask[x, y];
                float h = height[x, y];
                // Blend: landmask controls whether it's land, noise adds detail
                float combined = mask * 0.65f + h * mask * 0.35f;
                // Force ocean at map edges (2-tile border)
                float edgeDist = MathF.Min(MathF.Min(x, W - 1 - x), MathF.Min(y, H - 1 - y));
                float edgeFade = MathHelper.Clamp(edgeDist / 4f, 0f, 1f);
                height[x, y] = combined * edgeFade;
            }
        }

        // Normalize to 0-1
        float hMin = float.MaxValue, hMax = float.MinValue;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (height[x, y] < hMin) hMin = height[x, y];
                if (height[x, y] > hMax) hMax = height[x, y];
            }
        float hRange = hMax - hMin;
        if (hRange < 0.001f) hRange = 1f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                height[x, y] = (height[x, y] - hMin) / hRange;

        // ═══════════════════════════════════════════════════
        // STAGE 3: Mountain ridges — use ridge noise to create
        // visible mountain chains, not just random peaks
        // ═══════════════════════════════════════════════════
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Ridge = narrow band where ridgeNoise crosses 0.5
                float ridge = ridgeNoise[x, y];
                float ridgeFactor = 1f - MathF.Abs(ridge - 0.5f) * 2f;
                ridgeFactor = MathF.Pow(MathHelper.Clamp(ridgeFactor, 0, 1), 2.5f); // sharper ridges
                // Only inject ridges on existing land (above 30% height)
                if (height[x, y] > 0.30f)
                    height[x, y] += ridgeFactor * 0.30f; // stronger ridges
            }
        }
        
        // Re-normalize after ridge injection
        hMin = float.MaxValue; hMax = float.MinValue;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (height[x, y] < hMin) hMin = height[x, y];
                if (height[x, y] > hMax) hMax = height[x, y];
            }
        hRange = hMax - hMin;
        if (hRange < 0.001f) hRange = 1f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                height[x, y] = (height[x, y] - hMin) / hRange;

        // ═══════════════════════════════════════════════════
        // STAGE 4: Basin filling (simplified Planchon-Darboux)
        // ═══════════════════════════════════════════════════
        const float seaLevel = 0.38f;
        float[,] filled = new float[W, H];
        const float eps = 0.001f;

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x == 0 || x == W - 1 || y == 0 || y == H - 1 || height[x, y] < seaLevel)
                    filled[x, y] = height[x, y];
                else
                    filled[x, y] = 10f;
            }

        bool changed = true;
        int maxIter = 50;
        while (changed && maxIter-- > 0)
        {
            changed = false;
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    if (filled[x, y] <= height[x, y]) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            float nVal = filled[x + dx, y + dy] + eps;
                            if (height[x, y] >= nVal)
                            { filled[x, y] = height[x, y]; changed = true; }
                            else if (filled[x, y] > nVal)
                            { filled[x, y] = nVal; changed = true; }
                        }
                }
        }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                height[x, y] = filled[x, y];

        // Noise + refill cycle
        var noiseRng = new Random(seed + 500);
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
                if (height[x, y] >= seaLevel)
                    height[x, y] += (float)(noiseRng.NextDouble() - 0.5) * 0.004f;

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                filled[x, y] = (x == 0 || x == W - 1 || y == 0 || y == H - 1 || height[x, y] < seaLevel)
                    ? height[x, y] : 10f;
        changed = true; maxIter = 30;
        while (changed && maxIter-- > 0)
        {
            changed = false;
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
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
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                height[x, y] = filled[x, y];

        // ═══════════════════════════════════════════════════
        // STAGE 5: Water flux + incise flow erosion
        // ═══════════════════════════════════════════════════
        int[,] flowDirX = new int[W, H];
        int[,] flowDirY = new int[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float lowestH = height[x, y];
                int bx = 0, by = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < W && ny >= 0 && ny < H && height[nx, ny] < lowestH)
                        { lowestH = height[nx, ny]; bx = dx; by = dy; }
                    }
                flowDirX[x, y] = bx;
                flowDirY[x, y] = by;
            }

        var cells = new List<(int x, int y, float h)>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                cells.Add((x, y, height[x, y]));
        cells.Sort((a, b) => b.h.CompareTo(a.h));

        float[,] flux = new float[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                flux[x, y] = 1f;

        foreach (var (cx, cy, _) in cells)
        {
            int tx = cx + flowDirX[cx, cy];
            int ty = cy + flowDirY[cx, cy];
            if (tx >= 0 && tx < W && ty >= 0 && ty < H)
                flux[tx, ty] += flux[cx, cy];
        }

        // Incise flow
        float[,] eroded = (float[,])height.Clone();
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (height[x, y] < seaLevel) continue;
                int tx = x + flowDirX[x, y];
                int ty = y + flowDirY[x, y];
                if (tx < 0 || tx >= W || ty < 0 || ty >= H) continue;
                float slope = height[x, y] - height[tx, ty];
                if (slope <= 0) continue;
                float erosion = slope * MathF.Sqrt(flux[x, y]) * 0.012f;
                erosion = MathF.Min(erosion, 0.025f);
                eroded[x, y] -= erosion;
            }
        Array.Copy(eroded, height, eroded.Length);

        // ═══════════════════════════════════════════════════
        // STAGE 6: Thermal erosion
        // ═══════════════════════════════════════════════════
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
                    if (maxDiff > 0.08f)
                        temp[x, y] = h - maxDiff * 0.10f;
                }
            Array.Copy(temp, height, temp.Length);
        }

        // ═══════════════════════════════════════════════════
        // STAGE 7: Coastline smoothing
        // ═══════════════════════════════════════════════════
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
                    if (height[x, y] < seaLevel && land >= 6)
                        temp[x, y] = seaLevel + 0.02f;
                    else if (height[x, y] >= seaLevel && water >= 6)
                        temp[x, y] = seaLevel - 0.02f;
                }
            Array.Copy(temp, height, temp.Length);
        }

        // ═══════════════════════════════════════════════════
        // STAGE 8: Biome assignment
        // ═══════════════════════════════════════════════════
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
                    if (t < 0.2f)
                        tile = m > 0.55f ? MapTileType.SnowForest : MapTileType.Tundra;
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
                else // flat lowland just above beach
                {
                    if (t < 0.2f)
                        tile = m > 0.5f ? MapTileType.SnowForest : MapTileType.Tundra;
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

                // High flux lowland → floodplain
                if (flux[x, y] > 20f && h >= seaLevel && h < hillStart &&
                    (tile == MapTileType.Plains || tile == MapTileType.Grassland))
                    tile = MapTileType.Floodplain;

                map.SetTile(x, y, tile);
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 9: Rivers — MUCH higher threshold (fewer rivers)
        // ═══════════════════════════════════════════════════
        float riverThreshold = 60f; // was 25, way too many
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (height[x, y] < seaLevel) continue;
                if (flux[x, y] < riverThreshold) continue;
                var cur = map.GetTile(x, y);
                if (cur == MapTileType.BiomeEntrance || cur == MapTileType.Mountain ||
                    cur == MapTileType.HighMountain || cur == MapTileType.Snow ||
                    cur == MapTileType.Glacier) continue;
                map.SetTile(x, y, flux[x, y] > 150f ? MapTileType.Lake : MapTileType.River);
            }

        // ═══════════════════════════════════════════════════
        // STAGE 10: Lakes at local minima
        // ═══════════════════════════════════════════════════
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
                        if (lx < 0 || lx >= W || ly < 0 || ly >= H) continue;
                        if (height[lx, ly] < seaLevel) continue;
                        map.SetTile(lx, ly, temperature[lx, ly] < 0.2f ? MapTileType.FrozenLake : MapTileType.Lake);
                    }
            }

        // ═══════════════════════════════════════════════════
        // STAGE 11: Desert detail — dunes + oases
        // ═══════════════════════════════════════════════════
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (map.GetTile(x, y) != MapTileType.Desert) continue;
                if (ridgeNoise[x, y] > 0.55f)
                    map.SetTile(x, y, MapTileType.Dunes);
                bool nearWater = false;
                for (int dy = -2; dy <= 2 && !nearWater; dy++)
                    for (int dx = -2; dx <= 2 && !nearWater; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < W && ny >= 0 && ny < H)
                        {
                            var a = map.GetTile(nx, ny);
                            if (a == MapTileType.River || a == MapTileType.Lake || a == MapTileType.Water)
                                nearWater = true;
                        }
                    }
                if (nearWater && rng.NextDouble() < 0.3)
                    map.SetTile(x, y, MapTileType.Oasis);
            }

        // ═══════════════════════════════════════════════════
        // STAGE 12: Caves at mountain bases
        // ═══════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════
        // STAGE 13: Ruins
        // ═══════════════════════════════════════════════════
        for (int y = 3; y < H - 3; y++)
            for (int x = 3; x < W - 3; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.DenseForest || tile == MapTileType.Wasteland ||
                     tile == MapTileType.Jungle) && rng.NextDouble() < 0.02)
                    map.SetTile(x, y, MapTileType.Ruins);
            }

        // ═══════════════════════════════════════════════════
        // STAGE 14: Volcano
        // ═══════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════
        // STAGE 15: Fantasy / mystery biomes
        // Placed as rare clusters in specific conditions
        // ═══════════════════════════════════════════════════

        // Crystal Forest — cold + very high moisture + forest/dense forest
        // Eerie crystallized trees, appears in northern regions
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
                    // Spread to 1-2 neighbors
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (rng.NextDouble() < 0.4)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx > 0 && nx < W && ny > 0 && ny < H)
                                {
                                    var a = map.GetTile(nx, ny);
                                    if (a == MapTileType.DenseForest || a == MapTileType.SnowForest ||
                                        a == MapTileType.Forest || a == MapTileType.Taiga)
                                    { map.SetTile(nx, ny, MapTileType.CrystalForest); crystalCount++; }
                                }
                            }
                        }
                }
            }

        // Ashlands — near volcano, spreads as a dead zone
        // Find the volcano and paint a wider Ashlands region
        for (int y = 5; y < H - 5; y++)
            for (int x = 5; x < W - 5; x++)
            {
                if (map.GetTile(x, y) != MapTileType.Volcano) continue;
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        if (dx * dx + dy * dy > 18) continue; // circular
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                        var a = map.GetTile(nx, ny);
                        if (a == MapTileType.Volcano || a == MapTileType.HighMountain ||
                            a == MapTileType.DeepOcean || a == MapTileType.Ocean) continue;
                        if (rng.NextDouble() < 0.7)
                            map.SetTile(nx, ny, MapTileType.Ashlands);
                    }
            }

        // Mushroom — deep swamp/jungle clusters
        int mushroomCount = 0;
        for (int y = H / 2; y < H - 3 && mushroomCount < 10; y++)
            for (int x = 3; x < W - 3 && mushroomCount < 10; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.Swamp || tile == MapTileType.Jungle || tile == MapTileType.Marsh) &&
                    moisture[x, y] > 0.65f && rng.NextDouble() < 0.06)
                {
                    map.SetTile(x, y, MapTileType.Mushroom);
                    mushroomCount++;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (rng.NextDouble() < 0.35)
                            {
                                int nx2 = x + dx, ny2 = y + dy;
                                if (nx2 > 0 && nx2 < W && ny2 > 0 && ny2 < H)
                                {
                                    var a = map.GetTile(nx2, ny2);
                                    if (a == MapTileType.Swamp || a == MapTileType.Jungle ||
                                        a == MapTileType.Marsh || a == MapTileType.DenseForest)
                                    { map.SetTile(nx2, ny2, MapTileType.Mushroom); mushroomCount++; }
                                }
                            }
                        }
                }
            }

        // Petrified — wasteland/desert areas with ancient stone trees
        int petCount = 0;
        for (int y = H / 3; y < 2 * H / 3 && petCount < 8; y++)
            for (int x = 3; x < W - 3 && petCount < 8; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.Wasteland || tile == MapTileType.Desert) &&
                    ridgeNoise[x, y] > 0.45f && ridgeNoise[x, y] < 0.55f && rng.NextDouble() < 0.05)
                {
                    map.SetTile(x, y, MapTileType.Petrified);
                    petCount++;
                }
            }

        // Void Rift — one single anomaly, far from player start, on an island or mountain
        bool voidPlaced = false;
        for (int attempt = 0; attempt < 50 && !voidPlaced; attempt++)
        {
            int vx = rng.Next(5, W - 5);
            int vy = rng.Next(5, H - 5);
            float dist = MathF.Sqrt((vx - W / 2f) * (vx - W / 2f) + (vy - H / 2f) * (vy - H / 2f));
            if (dist < 15) continue; // must be far from center
            var tile = map.GetTile(vx, vy);
            if (tile == MapTileType.Mountain || tile == MapTileType.HighMountain ||
                tile == MapTileType.Wasteland || tile == MapTileType.Tundra)
            {
                map.SetTile(vx, vy, MapTileType.VoidRift);
                // Small ring of wasteland around it
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = vx + dx, ny = vy + dy;
                        if (nx > 0 && nx < W && ny > 0 && ny < H && !(dx == 0 && dy == 0))
                        {
                            var a = map.GetTile(nx, ny);
                            if (a != MapTileType.DeepOcean && a != MapTileType.Ocean &&
                                a != MapTileType.Volcano)
                                map.SetTile(nx, ny, MapTileType.Wasteland);
                        }
                    }
                voidPlaced = true;
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 16: Place biome entrance — Eden Reach
        // ═══════════════════════════════════════════════════
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

        // Clear area around biome entrance
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

        map.PlayerX = biomeX;
        map.PlayerY = biomeY + 2;
        var spawnTile = map.GetTile(map.PlayerX, map.PlayerY);
        if (spawnTile == MapTileType.Ocean || spawnTile == MapTileType.DeepOcean ||
            spawnTile == MapTileType.HighMountain || spawnTile == MapTileType.Lake)
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
