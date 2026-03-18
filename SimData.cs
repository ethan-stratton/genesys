using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArenaShooter;

public enum SimTileType
{
    Empty = 0,
    Grass = 1,
    Forest = 2,
    Rock = 3,
    Water = 4,
    Ruins = 5,
    Road = 10,
    Hub = 11,       // central beacon/temple
    Shelter = 20,
    Farm = 21,
    Workshop = 22,
    Wall = 23,
    MonsterLair = 50,
}

public class SimRegion
{
    public const int GridW = 24;
    public const int GridH = 18;
    public const int TileSize = 32;

    [JsonPropertyName("nodeId")] public string NodeId { get; set; } = "";
    [JsonPropertyName("tiles")] public int[] Tiles { get; set; }
    [JsonPropertyName("population")] public int Population { get; set; } = 0;
    [JsonPropertyName("hubPlaced")] public bool HubPlaced { get; set; } = false;

    public SimRegion()
    {
        Tiles = new int[GridW * GridH];
    }

    public SimTileType GetTile(int x, int y)
    {
        if (x < 0 || x >= GridW || y < 0 || y >= GridH) return SimTileType.Empty;
        return (SimTileType)Tiles[y * GridW + x];
    }

    public void SetTile(int x, int y, SimTileType type)
    {
        if (x < 0 || x >= GridW || y < 0 || y >= GridH) return;
        Tiles[y * GridW + x] = (int)type;
    }

    /// <summary>Generate default terrain for a region based on its node name</summary>
    public static SimRegion Generate(string nodeId)
    {
        var region = new SimRegion { NodeId = nodeId };
        var rng = new Random(nodeId.GetHashCode());

        // Fill with grass base
        for (int i = 0; i < region.Tiles.Length; i++)
            region.Tiles[i] = (int)SimTileType.Grass;

        // Scatter terrain features
        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                float r = (float)rng.NextDouble();
                if (r < 0.12f)
                    region.SetTile(x, y, SimTileType.Forest);
                else if (r < 0.16f)
                    region.SetTile(x, y, SimTileType.Rock);
                else if (r < 0.19f)
                    region.SetTile(x, y, SimTileType.Water);
            }
        }

        // Place 2-3 ruins
        int ruinCount = rng.Next(2, 4);
        for (int i = 0; i < ruinCount; i++)
        {
            int rx = rng.Next(2, GridW - 2);
            int ry = rng.Next(2, GridH - 2);
            region.SetTile(rx, ry, SimTileType.Ruins);
        }

        // Place 1-2 monster lairs at edges
        int lairCount = rng.Next(1, 3);
        for (int i = 0; i < lairCount; i++)
        {
            int side = rng.Next(4);
            int lx = side == 0 ? 0 : side == 1 ? GridW - 1 : rng.Next(GridW);
            int ly = side == 2 ? 0 : side == 3 ? GridH - 1 : rng.Next(GridH);
            region.SetTile(lx, ly, SimTileType.MonsterLair);
        }

        // Clear center area for hub placement
        int cx = GridW / 2, cy = GridH / 2;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                region.SetTile(cx + dx, cy + dy, SimTileType.Grass);

        return region;
    }

    private static string GetPath(string nodeId) => $"Content/sim/{nodeId}.json";

    public void Save()
    {
        var dir = "Content/sim";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetPath(NodeId), json);
    }

    public static SimRegion Load(string nodeId)
    {
        var path = GetPath(nodeId);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SimRegion>(json);
        }
        catch { return null; }
    }

    public static SimRegion LoadOrGenerate(string nodeId)
    {
        var existing = Load(nodeId);
        if (existing != null) return existing;
        var region = Generate(nodeId);
        region.Save();
        return region;
    }
}
