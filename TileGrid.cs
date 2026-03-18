using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace ArenaShooter;

public enum TileType : byte
{
    Empty = 0,

    // Solid blocks (full 32x32 collision)
    Dirt = 1,
    Stone = 2,
    Grass = 3,      // dirt with grass top
    Wood = 4,
    Sand = 5,

    // Platform (pass-through from below, land on top)
    PlatformWood = 20,
    PlatformStone = 21,

    // Hazards
    Spikes = 40,

    // Slopes (45°)
    SlopeUpRight = 50,   // floor slopes up left→right
    SlopeUpLeft = 51,    // floor slopes up right→left
    SlopeCeilRight = 52, // ceiling slope mirror of UpRight
    SlopeCeilLeft = 53,  // ceiling slope mirror of UpLeft

    // Reserved ranges:
    // 54-59: Future slope variants
    // 60-69: Special (ladder, water, etc.)
    // 100+: Decorative/background tiles
    DirtBg = 101,
    StoneBg = 102,
    GrassBg = 103,
    WoodBg = 104,
    SandBg = 105,
}

public static class TileProperties
{
    public static bool IsSolid(TileType t) => t >= TileType.Dirt && t <= TileType.Sand;
    public static bool IsPlatform(TileType t) => t >= TileType.PlatformWood && t <= TileType.PlatformStone;
    public static bool IsHazard(TileType t) => t == TileType.Spikes;
    public static bool IsBackground(TileType t) => (int)t >= 100;
    public static bool IsSlope(TileType t) => t >= TileType.SlopeUpRight && t <= TileType.SlopeCeilLeft;
    public static bool IsSlopeFloor(TileType t) => t == TileType.SlopeUpRight || t == TileType.SlopeUpLeft;
    public static bool IsSlopeCeiling(TileType t) => t == TileType.SlopeCeilRight || t == TileType.SlopeCeilLeft;

    public static Color GetColor(TileType t) => t switch
    {
        TileType.Dirt => new Color(101, 67, 33),
        TileType.Stone => new Color(120, 120, 120),
        TileType.Grass => new Color(76, 153, 0),
        TileType.Wood => new Color(139, 90, 43),
        TileType.Sand => new Color(194, 178, 128),
        TileType.PlatformWood => new Color(139, 90, 43),
        TileType.PlatformStone => new Color(100, 100, 100),
        TileType.Spikes => new Color(200, 30, 30),
        TileType.SlopeUpRight => new Color(90, 60, 30),
        TileType.SlopeUpLeft => new Color(90, 60, 30),
        TileType.SlopeCeilRight => new Color(70, 50, 25),
        TileType.SlopeCeilLeft => new Color(70, 50, 25),
        TileType.DirtBg => new Color(50, 33, 16),
        TileType.StoneBg => new Color(60, 60, 60),
        TileType.GrassBg => new Color(38, 76, 0),
        TileType.WoodBg => new Color(69, 45, 21),
        TileType.SandBg => new Color(97, 89, 64),
        _ => Color.Transparent,
    };

    // Secondary color for details (e.g. grass top on Grass tile)
    public static Color GetAccentColor(TileType t) => t switch
    {
        TileType.Grass => new Color(50, 120, 20),  // darker green top
        TileType.Dirt => new Color(80, 50, 25),
        TileType.Stone => new Color(90, 90, 90),
        TileType.Wood => new Color(110, 70, 33),
        TileType.Sand => new Color(170, 155, 110),
        TileType.SlopeUpRight => new Color(70, 45, 20),
        TileType.SlopeUpLeft => new Color(70, 45, 20),
        TileType.SlopeCeilRight => new Color(55, 38, 18),
        TileType.SlopeCeilLeft => new Color(55, 38, 18),
        TileType.DirtBg => new Color(40, 25, 12),
        TileType.StoneBg => new Color(45, 45, 45),
        TileType.GrassBg => new Color(25, 60, 10),
        TileType.WoodBg => new Color(55, 35, 16),
        TileType.SandBg => new Color(85, 77, 55),
        _ => Color.Transparent,
    };

    /// <summary>All placeable tile types for the editor palette.</summary>
    public static readonly TileType[] PaletteTiles = new[]
    {
        TileType.Dirt,
        TileType.Stone,
        TileType.Grass,
        TileType.Wood,
        TileType.Sand,
        TileType.PlatformWood,
        TileType.PlatformStone,
        TileType.Spikes,
        TileType.SlopeUpRight,
        TileType.SlopeUpLeft,
        TileType.SlopeCeilRight,
        TileType.SlopeCeilLeft,
        TileType.DirtBg,
        TileType.StoneBg,
        TileType.GrassBg,
        TileType.WoodBg,
        TileType.SandBg,
    };
}

public class TileGridData
{
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("tileSize")] public int TileSize { get; set; } = 32;
    [JsonPropertyName("originX")] public int OriginX { get; set; }
    [JsonPropertyName("originY")] public int OriginY { get; set; }
    [JsonPropertyName("tiles")] public int[] Tiles { get; set; } = Array.Empty<int>();
}

public class TileGrid
{
    public int Width;
    public int Height;
    public int TileSize;
    public int OriginX;
    public int OriginY;
    public TileType[,] Tiles;

    // Cached collision rects (rebuilt on demand)
    private Rectangle[] _solidRects;
    private Rectangle[] _platformRects;
    private Rectangle[] _hazardRects;
    private bool _dirty = true;

    public TileGrid(int width, int height, int tileSize, int originX, int originY)
    {
        Width = width;
        Height = height;
        TileSize = tileSize;
        OriginX = originX;
        OriginY = originY;
        Tiles = new TileType[width, height];
    }

    public void MarkDirty() => _dirty = true;

    /// <summary>Get tile at world coordinates. Returns Empty if out of bounds.</summary>
    public TileType GetTile(int worldX, int worldY)
    {
        int tx = (worldX - OriginX) / TileSize;
        int ty = (worldY - OriginY) / TileSize;
        // Handle negative division correctly
        if (worldX - OriginX < 0) tx--;
        if (worldY - OriginY < 0) ty--;
        if (tx < 0 || tx >= Width || ty < 0 || ty >= Height)
            return TileType.Empty;
        return Tiles[tx, ty];
    }

    /// <summary>Set tile at world coordinates. No-op if out of bounds.</summary>
    public void SetTile(int worldX, int worldY, TileType type)
    {
        int tx = (worldX - OriginX) / TileSize;
        int ty = (worldY - OriginY) / TileSize;
        if (worldX - OriginX < 0) tx--;
        if (worldY - OriginY < 0) ty--;
        if (tx < 0 || tx >= Width || ty < 0 || ty >= Height)
            return;
        Tiles[tx, ty] = type;
        _dirty = true;
    }

    /// <summary>Convert grid coords to tile indices. Returns (-1,-1) if out of bounds.</summary>
    public (int tx, int ty) WorldToTile(int worldX, int worldY)
    {
        int tx, ty;
        if (worldX >= OriginX)
            tx = (worldX - OriginX) / TileSize;
        else
            tx = (worldX - OriginX) / TileSize - 1;
        if (worldY >= OriginY)
            ty = (worldY - OriginY) / TileSize;
        else
            ty = (worldY - OriginY) / TileSize - 1;

        if (tx < 0 || tx >= Width || ty < 0 || ty >= Height)
            return (-1, -1);
        return (tx, ty);
    }

    /// <summary>Set tile by grid index directly.</summary>
    public void SetTileAt(int tx, int ty, TileType type)
    {
        if (tx < 0 || tx >= Width || ty < 0 || ty >= Height) return;
        Tiles[tx, ty] = type;
        _dirty = true;
    }

    public TileType GetTileAt(int tx, int ty)
    {
        if (tx < 0 || tx >= Width || ty < 0 || ty >= Height) return TileType.Empty;
        return Tiles[tx, ty];
    }

    private void RebuildIfDirty()
    {
        if (!_dirty) return;
        _dirty = false;
        _solidRects = MergeRects(TileProperties.IsSolid);
        _platformRects = MergeRects(TileProperties.IsPlatform);
        _hazardRects = MergeRects(TileProperties.IsHazard);
    }

    public Rectangle[] GetSolidRects() { RebuildIfDirty(); return _solidRects; }
    public Rectangle[] GetPlatformRects() { RebuildIfDirty(); return _platformRects; }
    public Rectangle[] GetHazardRects() { RebuildIfDirty(); return _hazardRects; }

    public Rectangle[] GetSlopeRects()
    {
        var rects = new List<Rectangle>();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (TileProperties.IsSlope(Tiles[x, y]))
                    rects.Add(new Rectangle(OriginX + x * TileSize, OriginY + y * TileSize, TileSize, TileSize));
        return rects.ToArray();
    }

    public float GetSlopeFloorY(float worldX, float worldY, int playerWidth)
    {
        float bestY = float.MaxValue;
        float centerX = worldX + playerWidth / 2f;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var t = Tiles[x, y];
                if (!TileProperties.IsSlopeFloor(t)) continue;
                int wx = OriginX + x * TileSize;
                int wy = OriginY + y * TileSize;
                if (centerX < wx || centerX > wx + TileSize) continue;
                float localX = MathHelper.Clamp(centerX - wx, 0, TileSize);
                float slopeY = t == TileType.SlopeUpRight
                    ? wy + TileSize - localX
                    : wy + localX;
                if (slopeY < bestY) bestY = slopeY;
            }
        }
        return bestY;
    }

    public float GetSlopeCeilY(float worldX, float worldY, int playerWidth)
    {
        float bestY = float.MinValue;
        float centerX = worldX + playerWidth / 2f;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var t = Tiles[x, y];
                if (!TileProperties.IsSlopeCeiling(t)) continue;
                int wx = OriginX + x * TileSize;
                int wy = OriginY + y * TileSize;
                if (centerX < wx || centerX > wx + TileSize) continue;
                float localX = MathHelper.Clamp(centerX - wx, 0, TileSize);
                // Ceiling mirrors: SlopeCeilRight surface goes from top-left to bottom-right
                float slopeY = t == TileType.SlopeCeilRight
                    ? wy + localX
                    : wy + TileSize - localX;
                if (slopeY > bestY) bestY = slopeY;
            }
        }
        return bestY;
    }

    /// <summary>Greedy rectangle merging for tiles matching a predicate.</summary>
    private Rectangle[] MergeRects(Func<TileType, bool> predicate)
    {
        var visited = new bool[Width, Height];
        var rects = new List<Rectangle>();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (visited[x, y] || !predicate(Tiles[x, y]))
                    continue;

                // Extend right
                int maxX = x;
                while (maxX + 1 < Width && !visited[maxX + 1, y] && predicate(Tiles[maxX + 1, y]))
                    maxX++;

                // Extend down
                int maxY = y;
                bool canExtend = true;
                while (canExtend && maxY + 1 < Height)
                {
                    for (int cx = x; cx <= maxX; cx++)
                    {
                        if (visited[cx, maxY + 1] || !predicate(Tiles[cx, maxY + 1]))
                        { canExtend = false; break; }
                    }
                    if (canExtend) maxY++;
                }

                // Mark visited
                for (int cy = y; cy <= maxY; cy++)
                    for (int cx = x; cx <= maxX; cx++)
                        visited[cx, cy] = true;

                rects.Add(new Rectangle(
                    OriginX + x * TileSize,
                    OriginY + y * TileSize,
                    (maxX - x + 1) * TileSize,
                    (maxY - y + 1) * TileSize));
            }
        }

        return rects.ToArray();
    }

    public TileGridData ToData()
    {
        var data = new TileGridData
        {
            Width = Width,
            Height = Height,
            TileSize = TileSize,
            OriginX = OriginX,
            OriginY = OriginY,
            Tiles = new int[Width * Height]
        };
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                data.Tiles[y * Width + x] = (int)Tiles[x, y];
        return data;
    }

    public static TileGrid FromData(TileGridData data)
    {
        if (data == null) return null;
        var grid = new TileGrid(data.Width, data.Height, data.TileSize > 0 ? data.TileSize : 32, data.OriginX, data.OriginY);
        if (data.Tiles != null)
        {
            for (int y = 0; y < data.Height; y++)
                for (int x = 0; x < data.Width; x++)
                {
                    int idx = y * data.Width + x;
                    if (idx < data.Tiles.Length)
                        grid.Tiles[x, y] = (TileType)data.Tiles[idx];
                }
        }
        grid._dirty = true;
        return grid;
    }
}
