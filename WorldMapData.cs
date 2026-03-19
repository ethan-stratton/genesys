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

        // Temperature: latitude (cold north → warm south) + noise
        float[,] temperature = new float[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                temperature[x, y] = (float)y / H * 0.75f + tempNoise[x, y] * 0.25f;

        // ═══════════════════════════════════════════════════
        // STAGE 2: Island shaping — Wilbur Gaussian envelope
        // curval * exp(-sqr(r/k)) - sqr(r), then cubic
        // ═══════════════════════════════════════════════════
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float nx = 2f * x / W - 1f;
                float ny = 2f * y / H - 1f;
                // Elliptical for vertical map
                float r = MathF.Sqrt(nx * nx * 1.4f + ny * ny * 0.7f);
                float curval = height[x, y];
                float shaped = curval * MathF.Exp(-(r / 0.55f) * (r / 0.55f)) - r * r * 0.4f;
                // Cubic modifier for flatter coasts
                height[x, y] = shaped > 0 ? MathF.Pow(shaped, 0.6f) : shaped;
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
        // STAGE 3: Mountain ridge injection
        // ═══════════════════════════════════════════════════
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float ridge = ridgeNoise[x, y];
                float ridgeFactor = 1f - MathF.Abs(ridge - 0.5f) * 2f;
                ridgeFactor = MathF.Pow(MathHelper.Clamp(ridgeFactor, 0, 1), 4f);
                if (height[x, y] > 0.35f)
                    height[x, y] += ridgeFactor * 0.2f;
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 4: Basin filling (simplified Planchon-Darboux)
        // Ensures all drainage flows to ocean
        // ═══════════════════════════════════════════════════
        const float seaLevel = 0.35f;
        float[,] filled = new float[W, H];
        const float eps = 0.001f;

        // Initialize: edges get original height, interior gets infinity
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x == 0 || x == W - 1 || y == 0 || y == H - 1 || height[x, y] < seaLevel)
                    filled[x, y] = height[x, y];
                else
                    filled[x, y] = 10f; // "infinity"
            }

        // Iterate until stable
        bool changed = true;
        int maxIter = 50;
        while (changed && maxIter-- > 0)
        {
            changed = false;
            for (int y = 1; y < H - 1; y++)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    if (filled[x, y] <= height[x, y]) continue;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            float nVal = filled[x + dx, y + dy] + eps;
                            if (height[x, y] >= nVal)
                            {
                                filled[x, y] = height[x, y];
                                changed = true;
                            }
                            else if (filled[x, y] > nVal)
                            {
                                filled[x, y] = nVal;
                                changed = true;
                            }
                        }
                }
            }
        }
        // Use filled heightmap
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                height[x, y] = filled[x, y];

        // Add small noise to break up flat filled areas
        var noiseRng = new Random(seed + 500);
        for (int y = 1; y < H - 1; y++)
            for (int x = 1; x < W - 1; x++)
                if (height[x, y] >= seaLevel)
                    height[x, y] += (float)(noiseRng.NextDouble() - 0.5) * 0.005f;

        // Re-fill after noise
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (x == 0 || x == W - 1 || y == 0 || y == H - 1 || height[x, y] < seaLevel)
                    filled[x, y] = height[x, y];
                else
                    filled[x, y] = 10f;
            }
        changed = true;
        maxIter = 30;
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
        // STAGE 5: Water flux accumulation + incise flow erosion
        // ═══════════════════════════════════════════════════
        // Compute downhill direction for each cell
        int[,] flowDirX = new int[W, H];
        int[,] flowDirY = new int[W, H];
        for (int y = 0; y < H; y++)
        {
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
        }

        // Sort cells by height descending, accumulate flux
        var cells = new List<(int x, int y, float h)>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                cells.Add((x, y, height[x, y]));
        cells.Sort((a, b) => b.h.CompareTo(a.h));

        float[,] flux = new float[W, H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                flux[x, y] = 1f; // base rainfall

        foreach (var (cx, cy, _) in cells)
        {
            int tx = cx + flowDirX[cx, cy];
            int ty = cy + flowDirY[cx, cy];
            if (tx >= 0 && tx < W && ty >= 0 && ty < H)
                flux[tx, ty] += flux[cx, cy];
        }

        // Incise flow: erode proportional to slope × sqrt(flux)
        float[,] eroded = (float[,])height.Clone();
        for (int y = 1; y < H - 1; y++)
        {
            for (int x = 1; x < W - 1; x++)
            {
                if (height[x, y] < seaLevel) continue;
                int tx = x + flowDirX[x, y];
                int ty = y + flowDirY[x, y];
                if (tx < 0 || tx >= W || ty < 0 || ty >= H) continue;
                float slope = height[x, y] - height[tx, ty];
                if (slope <= 0) continue;
                float erosion = slope * MathF.Sqrt(flux[x, y]) * 0.015f;
                erosion = MathF.Min(erosion, 0.03f); // cap
                eroded[x, y] -= erosion;
            }
        }
        Array.Copy(eroded, height, eroded.Length);

        // ═══════════════════════════════════════════════════
        // STAGE 6: Thermal erosion — smooth steep terrain
        // ═══════════════════════════════════════════════════
        for (int pass = 0; pass < 4; pass++)
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
                        temp[x, y] = h - maxDiff * 0.12f;
                }
            Array.Copy(temp, height, temp.Length);
        }

        // ═══════════════════════════════════════════════════
        // STAGE 7: Coastline smoothing (majority-neighbor filter)
        // Clean up jagged shoreline and 1-tile islands
        // ═══════════════════════════════════════════════════
        for (int pass = 0; pass < 3; pass++)
        {
            var temp = (float[,])height.Clone();
            for (int y = 1; y < H - 1; y++)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    int landNeighbors = 0, waterNeighbors = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (height[x + dx, y + dy] >= seaLevel) landNeighbors++;
                            else waterNeighbors++;
                        }
                    // Pull isolated water tiles up and isolated land tiles down
                    if (height[x, y] < seaLevel && landNeighbors >= 6)
                        temp[x, y] = seaLevel + 0.02f;
                    else if (height[x, y] >= seaLevel && waterNeighbors >= 6)
                        temp[x, y] = seaLevel - 0.02f;
                }
            }
            Array.Copy(temp, height, temp.Length);
        }

        // ═══════════════════════════════════════════════════
        // STAGE 8: Biome assignment
        // height × temperature × moisture → tile type
        // ═══════════════════════════════════════════════════
        const float deepSea = 0.20f;
        const float shallowSea = 0.30f;
        // seaLevel = 0.35f (already defined)
        const float beachTop = 0.38f;
        const float lowland = 0.50f;
        const float hillStart = 0.58f;
        const float mountainStart = 0.68f;
        const float highMountain = 0.78f;
        const float peakLevel = 0.88f;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float h = height[x, y];
                float m = moisture[x, y];
                float t = temperature[x, y];
                float f = flux[x, y];

                MapTileType tile;

                // ── WATER ──
                if (h < deepSea)
                    tile = MapTileType.DeepOcean;
                else if (h < shallowSea)
                    tile = MapTileType.Ocean;
                else if (h < seaLevel)
                {
                    // Shallow water — reef in warm areas
                    tile = (t > 0.5f && m > 0.4f) ? MapTileType.Reef : MapTileType.ShallowOcean;
                }
                // ── COAST ──
                else if (h < beachTop)
                {
                    if (t < 0.2f) tile = MapTileType.RockyShore;
                    else if (m < 0.3f) tile = MapTileType.RockyShore;
                    else tile = MapTileType.Beach;
                }
                // ── PEAKS ──
                else if (h >= peakLevel)
                {
                    tile = t < 0.4f ? MapTileType.Glacier : MapTileType.Snow;
                }
                else if (h >= highMountain)
                {
                    tile = t < 0.3f ? MapTileType.Snow : MapTileType.HighMountain;
                }
                else if (h >= mountainStart)
                {
                    if (t < 0.25f) tile = MapTileType.Snow;
                    else if (t < 0.4f) tile = MapTileType.Mountain;
                    else tile = MapTileType.Mountain;
                }
                // ── HILLS ──
                else if (h >= hillStart)
                {
                    if (t < 0.2f) tile = MapTileType.Tundra;
                    else if (t < 0.35f) tile = m > 0.5f ? MapTileType.Taiga : MapTileType.Foothills;
                    else if (t < 0.55f) tile = m > 0.5f ? MapTileType.Forest : MapTileType.Hills;
                    else tile = m > 0.5f ? MapTileType.Forest : MapTileType.Hills;
                }
                // ── LOWLAND ──
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
                // ── FLAT LOWLAND (just above beach) ──
                else
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

                // High-flux lowland areas → floodplain
                if (f > 15f && h >= seaLevel && h < hillStart &&
                    tile != MapTileType.Beach && tile != MapTileType.RockyShore)
                {
                    if (tile == MapTileType.Plains || tile == MapTileType.Grassland)
                        tile = MapTileType.Floodplain;
                }

                map.SetTile(x, y, tile);
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 9: Rivers — trace flux network
        // Only render tiles with high flux as River tiles
        // ═══════════════════════════════════════════════════
        float riverThreshold = 25f;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (height[x, y] < seaLevel) continue;
                if (flux[x, y] >= riverThreshold)
                {
                    var cur = map.GetTile(x, y);
                    if (cur != MapTileType.BiomeEntrance && cur != MapTileType.Mountain &&
                        cur != MapTileType.HighMountain && cur != MapTileType.Snow &&
                        cur != MapTileType.Glacier)
                    {
                        // Wider rivers for higher flux
                        map.SetTile(x, y, flux[x, y] > 80f ? MapTileType.Lake : MapTileType.River);
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 10: Lakes — find local minima with high flux
        // ═══════════════════════════════════════════════════
        for (int y = 2; y < H - 2; y++)
        {
            for (int x = 2; x < W - 2; x++)
            {
                if (height[x, y] < seaLevel) continue;
                if (flux[x, y] < 60f) continue;
                // Is this a local minimum?
                bool isMin = true;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (height[x + dx, y + dy] < height[x, y]) isMin = false;
                    }
                if (!isMin) continue;
                // Paint small lake
                int lakeSize = flux[x, y] > 120f ? 2 : 1;
                for (int dy = -lakeSize; dy <= lakeSize; dy++)
                    for (int dx = -lakeSize; dx <= lakeSize; dx++)
                    {
                        int lx = x + dx, ly = y + dy;
                        if (lx < 0 || lx >= W || ly < 0 || ly >= H) continue;
                        if (height[lx, ly] < seaLevel) continue;
                        var lt = temperature[lx, ly];
                        map.SetTile(lx, ly, lt < 0.2f ? MapTileType.FrozenLake : MapTileType.Lake);
                    }
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 11: Deserts get dunes + oases near water
        // ═══════════════════════════════════════════════════
        for (int y = 1; y < H - 1; y++)
        {
            for (int x = 1; x < W - 1; x++)
            {
                var tile = map.GetTile(x, y);
                if (tile == MapTileType.Desert)
                {
                    // Dunes from ridge noise
                    if (ridgeNoise[x, y] > 0.55f)
                        map.SetTile(x, y, MapTileType.Dunes);
                    // Oasis near rivers
                    bool nearWater = false;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < W && ny >= 0 && ny < H)
                            {
                                var adj = map.GetTile(nx, ny);
                                if (adj == MapTileType.River || adj == MapTileType.Lake || adj == MapTileType.Water)
                                    nearWater = true;
                            }
                        }
                    if (nearWater && rng.NextDouble() < 0.3)
                        map.SetTile(x, y, MapTileType.Oasis);
                }
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 12: Caves at mountain bases
        // ═══════════════════════════════════════════════════
        for (int y = 2; y < H - 2; y++)
        {
            for (int x = 2; x < W - 2; x++)
            {
                var tile = map.GetTile(x, y);
                if (tile != MapTileType.Foothills && tile != MapTileType.Hills &&
                    tile != MapTileType.Tundra && tile != MapTileType.RockyShore) continue;
                bool nearMountain = false;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var adj = map.GetTile(x + dx, y + dy);
                        if (adj == MapTileType.Mountain || adj == MapTileType.HighMountain)
                            nearMountain = true;
                    }
                if (nearMountain && ridgeNoise[x, y] * moisture[x, y] > 0.3f && rng.NextDouble() < 0.12)
                    map.SetTile(x, y, MapTileType.Cave);
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 13: Ruins scattered in forests and wasteland
        // ═══════════════════════════════════════════════════
        for (int y = 3; y < H - 3; y++)
        {
            for (int x = 3; x < W - 3; x++)
            {
                var tile = map.GetTile(x, y);
                if ((tile == MapTileType.DenseForest || tile == MapTileType.Wasteland ||
                     tile == MapTileType.Jungle) && rng.NextDouble() < 0.02)
                    map.SetTile(x, y, MapTileType.Ruins);
            }
        }

        // ═══════════════════════════════════════════════════
        // STAGE 14: Volcano — one per map, in high mountains near hot zone
        // ═══════════════════════════════════════════════════
        for (int y = H / 2; y < H - 5; y++)
        {
            for (int x = 5; x < W - 5; x++)
            {
                if (map.GetTile(x, y) == MapTileType.HighMountain && temperature[x, y] > 0.45f)
                {
                    map.SetTile(x, y, MapTileType.Volcano);
                    // Wasteland ring
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            var adj = map.GetTile(x + dx, y + dy);
                            if (adj != MapTileType.HighMountain && adj != MapTileType.Mountain)
                                map.SetTile(x + dx, y + dy, MapTileType.Wasteland);
                        }
                    goto volcanoPlaced;
                }
            }
        }
        volcanoPlaced:;

        // ═══════════════════════════════════════════════════
        // STAGE 15: Place biome entrance — Eden Reach
        // ═══════════════════════════════════════════════════
        int biomeX = W / 2, biomeY = (int)(H * 0.5f);
        float bestDist = float.MaxValue;
        for (int y = H / 3; y < 2 * H / 3; y++)
        {
            for (int x = W / 4; x < 3 * W / 4; x++)
            {
                var t = map.GetTile(x, y);
                if (t == MapTileType.Plains || t == MapTileType.Grassland || t == MapTileType.Forest)
                {
                    float dist = (x - W / 2f) * (x - W / 2f) + (y - H * 0.5f) * (y - H * 0.5f);
                    if (dist < bestDist) { bestDist = dist; biomeX = x; biomeY = y; }
                }
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
    Path = 40,
    BiomeEntrance = 50,
}
