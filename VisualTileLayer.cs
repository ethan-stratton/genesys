using System;
using System.Text.Json.Serialization;

namespace Genesis;

public class VisualTileLayerData
{
    [JsonPropertyName("name")] public string Name { get; set; } = "bg";
    [JsonPropertyName("tilesetPath")] public string TilesetPath { get; set; } = "";
    [JsonPropertyName("tilesetTileSize")] public int TilesetTileSize { get; set; } = 32;
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("originX")] public int OriginX { get; set; }
    [JsonPropertyName("originY")] public int OriginY { get; set; }
    [JsonPropertyName("tiles")] public int[] Tiles { get; set; } = Array.Empty<int>();
}

public class VisualTileLayer
{
    public string Name; // "bg", "mid", "fg"
    public int Width, Height, TileSize;
    public int OriginX, OriginY;
    public int[] Tiles; // index into tileset (0 = empty/transparent)
    public string TilesetPath; // relative path to tileset PNG (from Content/)
    public int TilesetTileSize = 32;

    public int GetTile(int col, int row)
    {
        if (col < 0 || col >= Width || row < 0 || row >= Height) return 0;
        return Tiles[row * Width + col];
    }

    public void SetTile(int col, int row, int tileIndex)
    {
        if (col < 0 || col >= Width || row < 0 || row >= Height) return;
        Tiles[row * Width + col] = tileIndex;
    }

    public (int col, int row) WorldToTile(int worldX, int worldY)
    {
        return ((worldX - OriginX) / TileSize, (worldY - OriginY) / TileSize);
    }

    public VisualTileLayerData ToData()
    {
        return new VisualTileLayerData
        {
            Name = Name,
            TilesetPath = TilesetPath ?? "",
            TilesetTileSize = TilesetTileSize,
            Width = Width,
            Height = Height,
            OriginX = OriginX,
            OriginY = OriginY,
            Tiles = (int[])Tiles.Clone()
        };
    }

    public static VisualTileLayer FromData(VisualTileLayerData data)
    {
        return new VisualTileLayer
        {
            Name = data.Name ?? "bg",
            TilesetPath = data.TilesetPath ?? "",
            TilesetTileSize = data.TilesetTileSize > 0 ? data.TilesetTileSize : 32,
            Width = data.Width,
            Height = data.Height,
            TileSize = data.TilesetTileSize > 0 ? data.TilesetTileSize : 32,
            OriginX = data.OriginX,
            OriginY = data.OriginY,
            Tiles = data.Tiles != null ? (int[])data.Tiles.Clone() : new int[data.Width * data.Height]
        };
    }

    public static VisualTileLayer CreateEmpty(string name, int width, int height, int tileSize, int originX, int originY)
    {
        return new VisualTileLayer
        {
            Name = name,
            Width = width,
            Height = height,
            TileSize = tileSize,
            TilesetTileSize = tileSize,
            OriginX = originX,
            OriginY = originY,
            Tiles = new int[width * height],
            TilesetPath = ""
        };
    }

    /// <summary>Check if layer has any non-zero tiles.</summary>
    public bool HasAnyTiles()
    {
        for (int i = 0; i < Tiles.Length; i++)
            if (Tiles[i] != 0) return true;
        return false;
    }
}
