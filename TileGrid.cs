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
    SpikesDown = 41,
    SpikesLeft = 42,
    SpikesRight = 43,
    HalfSpikesUp = 44,
    HalfSpikesDown = 45,
    HalfSpikesLeft = 46,
    HalfSpikesRight = 47,

    // Retractable variants (cycle in/out)
    RetractSpikesUp = 48,
    RetractSpikesDown = 49,
    RetractSpikesLeft = 84,
    RetractSpikesRight = 85,
    RetractHalfSpikesUp = 86,
    RetractHalfSpikesDown = 87,
    RetractHalfSpikesLeft = 88,
    RetractHalfSpikesRight = 89,

    // Slopes (45°)
    SlopeUpRight = 50,   // floor slopes up left→right (45°)
    SlopeUpLeft = 51,    // floor slopes up right→left (45°)
    SlopeCeilRight = 52, // ceiling slope mirror of UpRight
    SlopeCeilLeft = 53,  // ceiling slope mirror of UpLeft
    GentleUpRight = 54,  // gentle floor slope up right (half height: 16px rise over 32px)
    GentleUpLeft = 55,   // gentle floor slope up left (half height)
    ShavedRight = 56,    // full block with gentle slope shaved off top-right
    ShavedLeft = 57,     // full block with gentle slope shaved off top-left
    GentleCeilRight = 58, // gentle ceiling slope, mirrors GentleUpRight
    GentleCeilLeft = 59,  // gentle ceiling slope, mirrors GentleUpLeft
    ShavedCeilRight = 60, // full block with gentle ceiling slope shaved off bottom-right
    ShavedCeilLeft = 61,  // full block with gentle ceiling slope shaved off bottom-left

    // 1:4 gentle slopes (1 tile rise over 4 tile run = 8px per tile)
    // Each tile handles one quarter of the total rise
    // UpRight: surface goes from bottom-left to top-right across 4 tiles
    Gentle4UpRightA = 62,  // lowest quarter (surface 32→24)
    Gentle4UpRightB = 63,  // second quarter (surface 24→16)
    Gentle4UpRightC = 64,  // third quarter (surface 16→8)
    Gentle4UpRightD = 65,  // top quarter (surface 8→0)
    Gentle4UpLeftA = 66,   // lowest quarter going left
    Gentle4UpLeftB = 67,
    Gentle4UpLeftC = 68,
    Gentle4UpLeftD = 69,

    // Interactive / Effect tiles
    Breakable = 70,       // solid until attacked, drops health item
    DamageTile = 71,      // slow continuous damage on contact
    KnockbackTile = 72,   // knocks player back at high velocity
    SpeedBoostTile = 73,  // briefly increases movement speed
    FloatTile = 74,       // slowly floats player upward on timer

    // 1:4 ceiling slopes
    Gentle4CeilRightA = 75, // shallowest (surface wy→wy+8)
    Gentle4CeilRightB = 76,
    Gentle4CeilRightC = 77,
    Gentle4CeilRightD = 78, // deepest (surface wy+24→wy+32)
    Gentle4CeilLeftA = 79,
    Gentle4CeilLeftB = 80,
    Gentle4CeilLeftC = 81,
    Gentle4CeilLeftD = 82,
    DamageNoKBTile = 83,  // continuous damage, no knockback (dark red)
    DamageFloorTile = 84, // solid + continuous damage, no knockback (dark red, standable)

    // Liquid tiles
    Water = 90,
    Lava = 91,
    Acid = 92,

    // Platforms
    PlatformTop = 22,     // half platform spanning top half of tile
    PlatformBottom = 23,  // half platform spanning bottom half of tile

    // Reserved ranges:
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
    public static bool IsSolid(TileType t) => (t >= TileType.Dirt && t <= TileType.Sand) || t == TileType.Breakable || t == TileType.DamageFloorTile;
    public static bool IsPlatform(TileType t) => t >= TileType.PlatformWood && t <= TileType.PlatformBottom;
    /// <summary>Standard thin platforms only (for merged rects). Half platforms use custom rects.</summary>
    public static bool IsStandardPlatform(TileType t) => t == TileType.PlatformWood || t == TileType.PlatformStone;
    public static bool IsHazard(TileType t) => (t >= TileType.Spikes && t <= TileType.RetractSpikesDown) || (t >= TileType.RetractSpikesLeft && t <= TileType.RetractHalfSpikesRight);
    public static bool IsRetractable(TileType t) => t == TileType.RetractSpikesUp || t == TileType.RetractSpikesDown
        || t == TileType.RetractSpikesLeft || t == TileType.RetractSpikesRight
        || t == TileType.RetractHalfSpikesUp || t == TileType.RetractHalfSpikesDown
        || t == TileType.RetractHalfSpikesLeft || t == TileType.RetractHalfSpikesRight;
    /// <summary>Full-size hazards only (for merged rect collision). Half spikes use per-tile hitboxes.</summary>
    public static bool IsFullHazard(TileType t) => t >= TileType.Spikes && t <= TileType.SpikesRight;
    public static bool IsBackground(TileType t) => (int)t >= 100;
    public static bool IsSlope(TileType t) => (t >= TileType.SlopeUpRight && t <= TileType.ShavedCeilLeft)
        || (t >= TileType.Gentle4UpRightA && t <= TileType.Gentle4UpLeftD)
        || (t >= TileType.Gentle4CeilRightA && t <= TileType.Gentle4CeilLeftD);
    public static bool IsSlopeFloor(TileType t) => t == TileType.SlopeUpRight || t == TileType.SlopeUpLeft
        || t == TileType.GentleUpRight || t == TileType.GentleUpLeft
        || t == TileType.ShavedRight || t == TileType.ShavedLeft
        || (t >= TileType.Gentle4UpRightA && t <= TileType.Gentle4UpLeftD);
    public static bool IsSlopeCeiling(TileType t) => t == TileType.SlopeCeilRight || t == TileType.SlopeCeilLeft
        || t == TileType.GentleCeilRight || t == TileType.GentleCeilLeft
        || t == TileType.ShavedCeilRight || t == TileType.ShavedCeilLeft
        || (t >= TileType.Gentle4CeilRightA && t <= TileType.Gentle4CeilLeftD);
    public static bool IsEffectTile(TileType t) => (t >= TileType.DamageTile && t <= TileType.FloatTile) || t == TileType.DamageNoKBTile || t == TileType.DamageFloorTile;
    public static bool IsLiquid(TileType t) => t == TileType.Water || t == TileType.Lava || t == TileType.Acid;

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
        TileType.SpikesDown => new Color(200, 30, 30),
        TileType.SpikesLeft => new Color(200, 30, 30),
        TileType.SpikesRight => new Color(200, 30, 30),
        TileType.HalfSpikesUp => new Color(180, 25, 25),
        TileType.HalfSpikesDown => new Color(180, 25, 25),
        TileType.HalfSpikesLeft => new Color(180, 25, 25),
        TileType.HalfSpikesRight => new Color(180, 25, 25),
        TileType.RetractSpikesUp => new Color(200, 120, 30),
        TileType.RetractSpikesDown => new Color(200, 120, 30),
        TileType.RetractSpikesLeft => new Color(200, 120, 30),
        TileType.RetractSpikesRight => new Color(200, 120, 30),
        TileType.RetractHalfSpikesUp => new Color(180, 110, 25),
        TileType.RetractHalfSpikesDown => new Color(180, 110, 25),
        TileType.RetractHalfSpikesLeft => new Color(180, 110, 25),
        TileType.RetractHalfSpikesRight => new Color(180, 110, 25),
        TileType.PlatformTop => new Color(100, 100, 100),
        TileType.PlatformBottom => new Color(100, 100, 100),
        TileType.SlopeUpRight => new Color(90, 60, 30),
        TileType.SlopeUpLeft => new Color(90, 60, 30),
        TileType.SlopeCeilRight => new Color(70, 50, 25),
        TileType.SlopeCeilLeft => new Color(70, 50, 25),
        TileType.GentleUpRight => new Color(95, 65, 35),
        TileType.GentleUpLeft => new Color(95, 65, 35),
        TileType.ShavedRight => new Color(85, 55, 28),
        TileType.ShavedLeft => new Color(85, 55, 28),
        TileType.GentleCeilRight => new Color(65, 45, 22),
        TileType.GentleCeilLeft => new Color(65, 45, 22),
        TileType.ShavedCeilRight => new Color(65, 45, 22),
        TileType.ShavedCeilLeft => new Color(65, 45, 22),
        TileType.Gentle4UpRightA => new Color(95, 65, 35),
        TileType.Gentle4UpRightB => new Color(95, 65, 35),
        TileType.Gentle4UpRightC => new Color(95, 65, 35),
        TileType.Gentle4UpRightD => new Color(95, 65, 35),
        TileType.Gentle4UpLeftA => new Color(95, 65, 35),
        TileType.Gentle4UpLeftB => new Color(95, 65, 35),
        TileType.Gentle4UpLeftC => new Color(95, 65, 35),
        TileType.Gentle4UpLeftD => new Color(95, 65, 35),
        TileType.Gentle4CeilRightA => new Color(65, 45, 22),
        TileType.Gentle4CeilRightB => new Color(65, 45, 22),
        TileType.Gentle4CeilRightC => new Color(65, 45, 22),
        TileType.Gentle4CeilRightD => new Color(65, 45, 22),
        TileType.Gentle4CeilLeftA => new Color(65, 45, 22),
        TileType.Gentle4CeilLeftB => new Color(65, 45, 22),
        TileType.Gentle4CeilLeftC => new Color(65, 45, 22),
        TileType.Gentle4CeilLeftD => new Color(65, 45, 22),
        TileType.Breakable => new Color(160, 140, 80),
        TileType.DamageTile => new Color(150, 40, 150),
        TileType.KnockbackTile => new Color(40, 120, 200),
        TileType.SpeedBoostTile => new Color(40, 200, 80),
        TileType.FloatTile => new Color(180, 180, 255),
        TileType.DamageNoKBTile => new Color(120, 20, 20),
        TileType.DamageFloorTile => new Color(100, 15, 15),
        TileType.DirtBg => new Color(50, 33, 16),
        TileType.StoneBg => new Color(60, 60, 60),
        TileType.GrassBg => new Color(38, 76, 0),
        TileType.WoodBg => new Color(69, 45, 21),
        TileType.SandBg => new Color(97, 89, 64),
        TileType.Water => new Color(30, 90, 180),
        TileType.Lava => new Color(200, 60, 20),
        TileType.Acid => new Color(60, 200, 40),
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
        TileType.GentleUpRight => new Color(75, 50, 25),
        TileType.GentleUpLeft => new Color(75, 50, 25),
        TileType.ShavedRight => new Color(68, 42, 20),
        TileType.ShavedLeft => new Color(68, 42, 20),
        TileType.GentleCeilRight => new Color(50, 35, 16),
        TileType.GentleCeilLeft => new Color(50, 35, 16),
        TileType.ShavedCeilRight => new Color(50, 35, 16),
        TileType.ShavedCeilLeft => new Color(50, 35, 16),
        TileType.Spikes => new Color(160, 20, 20),
        TileType.SpikesDown => new Color(160, 20, 20),
        TileType.SpikesLeft => new Color(160, 20, 20),
        TileType.SpikesRight => new Color(160, 20, 20),
        TileType.HalfSpikesUp => new Color(140, 18, 18),
        TileType.HalfSpikesDown => new Color(140, 18, 18),
        TileType.HalfSpikesLeft => new Color(140, 18, 18),
        TileType.HalfSpikesRight => new Color(140, 18, 18),
        TileType.RetractSpikesUp => new Color(160, 90, 20),
        TileType.RetractSpikesDown => new Color(160, 90, 20),
        TileType.RetractSpikesLeft => new Color(160, 90, 20),
        TileType.RetractSpikesRight => new Color(160, 90, 20),
        TileType.RetractHalfSpikesUp => new Color(140, 80, 18),
        TileType.RetractHalfSpikesDown => new Color(140, 80, 18),
        TileType.RetractHalfSpikesLeft => new Color(140, 80, 18),
        TileType.RetractHalfSpikesRight => new Color(140, 80, 18),
        TileType.PlatformTop => new Color(80, 80, 80),
        TileType.PlatformBottom => new Color(80, 80, 80),
        TileType.Gentle4UpRightA => new Color(75, 50, 25),
        TileType.Gentle4UpRightB => new Color(75, 50, 25),
        TileType.Gentle4UpRightC => new Color(75, 50, 25),
        TileType.Gentle4UpRightD => new Color(75, 50, 25),
        TileType.Gentle4UpLeftA => new Color(75, 50, 25),
        TileType.Gentle4UpLeftB => new Color(75, 50, 25),
        TileType.Gentle4UpLeftC => new Color(75, 50, 25),
        TileType.Gentle4UpLeftD => new Color(75, 50, 25),
        TileType.Gentle4CeilRightA => new Color(50, 35, 16),
        TileType.Gentle4CeilRightB => new Color(50, 35, 16),
        TileType.Gentle4CeilRightC => new Color(50, 35, 16),
        TileType.Gentle4CeilRightD => new Color(50, 35, 16),
        TileType.Gentle4CeilLeftA => new Color(50, 35, 16),
        TileType.Gentle4CeilLeftB => new Color(50, 35, 16),
        TileType.Gentle4CeilLeftC => new Color(50, 35, 16),
        TileType.Gentle4CeilLeftD => new Color(50, 35, 16),
        TileType.Breakable => new Color(130, 110, 60),
        TileType.DamageTile => new Color(120, 30, 120),
        TileType.KnockbackTile => new Color(30, 90, 160),
        TileType.SpeedBoostTile => new Color(30, 160, 60),
        TileType.FloatTile => new Color(150, 150, 220),
        TileType.DamageNoKBTile => new Color(90, 15, 15),
        TileType.DamageFloorTile => new Color(80, 10, 10),
        TileType.DirtBg => new Color(40, 25, 12),
        TileType.StoneBg => new Color(45, 45, 45),
        TileType.GrassBg => new Color(25, 60, 10),
        TileType.WoodBg => new Color(55, 35, 16),
        TileType.SandBg => new Color(85, 77, 55),
        TileType.Water => new Color(60, 140, 220),
        TileType.Lava => new Color(255, 160, 30),
        TileType.Acid => new Color(120, 255, 80),
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
        TileType.GentleUpRight,
        TileType.GentleUpLeft,
        TileType.ShavedRight,
        TileType.ShavedLeft,
        TileType.GentleCeilRight,
        TileType.GentleCeilLeft,
        TileType.ShavedCeilRight,
        TileType.ShavedCeilLeft,
        TileType.Gentle4UpRightA,
        TileType.Gentle4UpRightB,
        TileType.Gentle4UpRightC,
        TileType.Gentle4UpRightD,
        TileType.Gentle4UpLeftA,
        TileType.Gentle4UpLeftB,
        TileType.Gentle4UpLeftC,
        TileType.Gentle4UpLeftD,
        TileType.Gentle4CeilRightA,
        TileType.Gentle4CeilRightB,
        TileType.Gentle4CeilRightC,
        TileType.Gentle4CeilRightD,
        TileType.Gentle4CeilLeftA,
        TileType.Gentle4CeilLeftB,
        TileType.Gentle4CeilLeftC,
        TileType.Gentle4CeilLeftD,
        TileType.SpikesDown,
        TileType.SpikesLeft,
        TileType.SpikesRight,
        TileType.HalfSpikesUp,
        TileType.HalfSpikesDown,
        TileType.HalfSpikesLeft,
        TileType.HalfSpikesRight,
        TileType.RetractSpikesUp,
        TileType.RetractSpikesDown,
        TileType.RetractSpikesLeft,
        TileType.RetractSpikesRight,
        TileType.RetractHalfSpikesUp,
        TileType.RetractHalfSpikesDown,
        TileType.RetractHalfSpikesLeft,
        TileType.RetractHalfSpikesRight,
        TileType.PlatformTop,
        TileType.PlatformBottom,
        TileType.Breakable,
        TileType.DamageTile,
        TileType.KnockbackTile,
        TileType.SpeedBoostTile,
        TileType.FloatTile,
        TileType.DamageNoKBTile,
        TileType.DamageFloorTile,
        TileType.DirtBg,
        TileType.StoneBg,
        TileType.GrassBg,
        TileType.WoodBg,
        TileType.SandBg,
        TileType.Water,
        TileType.Lava,
        TileType.Acid,
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

    // Retractable spike timing
    public float RetractTimer;
    public const float RetractUpTime = 1.2f;
    public const float RetractDownTime = 1.0f;
    public float RetractCycle => RetractUpTime + RetractDownTime;
    /// <summary>0..1 how far spikes are extended (1 = fully out)</summary>
    public float RetractExtension
    {
        get
        {
            float t = RetractTimer % RetractCycle;
            if (t < RetractUpTime) return 1f; // fully extended
            float downT = (t - RetractUpTime) / RetractDownTime;
            if (downT < 0.15f) return 1f - (downT / 0.15f); // retracting
            if (downT > 0.85f) return (downT - 0.85f) / 0.15f; // extending
            return 0f; // fully retracted
        }
    }
    public bool RetractExtended => RetractExtension > 0.5f;

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
        _platformRects = MergeRects(TileProperties.IsStandardPlatform);
        // Add half-platform rects individually (not merged — they're half-tile sized)
        var halfPlats = new List<Rectangle>();
        for (int ty = 0; ty < Height; ty++)
        {
            for (int tx = 0; tx < Width; tx++)
            {
                var t = Tiles[tx, ty];
                int wx = OriginX + tx * TileSize;
                int wy = OriginY + ty * TileSize;
                if (t == TileType.PlatformTop)
                    halfPlats.Add(new Rectangle(wx, wy, TileSize, TileSize / 2));
                else if (t == TileType.PlatformBottom)
                    halfPlats.Add(new Rectangle(wx, wy + TileSize / 2, TileSize, TileSize / 2));
            }
        }
        if (halfPlats.Count > 0)
        {
            var merged = new Rectangle[_platformRects.Length + halfPlats.Count];
            _platformRects.CopyTo(merged, 0);
            halfPlats.CopyTo(merged, _platformRects.Length);
            _platformRects = merged;
        }
        _hazardRects = MergeRects(TileProperties.IsFullHazard);
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

    public float GetSlopeFloorY(float worldX, float worldY, int playerWidth, int playerHeight)
    {
        float bestY = float.MaxValue;
        float centerX = worldX + playerWidth / 2f;
        float leftX = worldX + 2f;             // left foot sensor
        float rightX = worldX + playerWidth - 2f; // right foot sensor
        float[] checkXs = new float[] { centerX, leftX, rightX };
        
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var t = Tiles[x, y];
                if (!TileProperties.IsSlopeFloor(t)) continue;
                int wx = OriginX + x * TileSize;
                int wy = OriginY + y * TileSize;
                
                // Check each sensor point
                foreach (float sensorX in checkXs)
                {
                    if (sensorX < wx || sensorX > wx + TileSize) continue;
                    // Only check if player is vertically near this tile
                    if (worldY + playerHeight < wy - 8 || worldY > wy + TileSize + 4) continue;
                    float localX = MathHelper.Clamp(sensorX - wx, 0, TileSize);
                    float slopeY;
                    switch (t)
                    {
                        case TileType.SlopeUpRight:
                            slopeY = wy + TileSize - localX;
                            break;
                        case TileType.SlopeUpLeft:
                            slopeY = wy + localX;
                            break;
                        case TileType.GentleUpRight:
                            slopeY = wy + TileSize - (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.GentleUpLeft:
                            slopeY = wy + TileSize / 2f + (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.ShavedRight:
                            slopeY = wy + (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.ShavedLeft:
                            slopeY = wy + (TileSize / 2f) - (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.Gentle4UpRightA:
                            // Lowest quarter: surface from ts (left) to ts*3/4 (right)
                            slopeY = wy + TileSize - (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4UpRightB:
                            slopeY = wy + TileSize * 3f / 4f - (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4UpRightC:
                            slopeY = wy + TileSize / 2f - (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4UpRightD:
                            slopeY = wy + TileSize / 4f - (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4UpLeftA:
                            // Lowest quarter going left: surface from ts*3/4 (left) to ts (right)
                            slopeY = wy + TileSize * 3f / 4f + (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4UpLeftB:
                            slopeY = wy + TileSize / 2f + (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4UpLeftC:
                            slopeY = wy + TileSize / 4f + (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4UpLeftD:
                            slopeY = wy + (localX / TileSize) * (TileSize / 4f);
                            break;
                        default:
                            continue;
                    }
                    if (slopeY < bestY) bestY = slopeY;
                }
            }
        }
        return bestY;
    }

    public float GetSlopeCeilY(float worldX, float worldY, int playerWidth, int playerHeight)
    {
        return GetSlopeCeilY(worldX, worldY, playerWidth, playerHeight, out _);
    }

    public float GetSlopeCeilY(float worldX, float worldY, int playerWidth, int playerHeight, out TileType hitTile)
    {
        float bestY = float.MinValue;
        hitTile = TileType.Empty;
        float centerX = worldX + playerWidth / 2f;
        float leftX = worldX + 2f;
        float rightX = worldX + playerWidth - 2f;
        float[] checkXs = new float[] { centerX, leftX, rightX };
        
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var t = Tiles[x, y];
                if (!TileProperties.IsSlopeCeiling(t)) continue;
                int wx = OriginX + x * TileSize;
                int wy = OriginY + y * TileSize;
                
                foreach (float sensorX in checkXs)
                {
                    if (sensorX < wx || sensorX > wx + TileSize) continue;
                    if (worldY > wy + TileSize || worldY + playerHeight < wy) continue;
                    float localX = MathHelper.Clamp(sensorX - wx, 0, TileSize);
                    float slopeY;
                    switch (t)
                    {
                        case TileType.SlopeCeilRight:
                            slopeY = wy + localX;
                            break;
                        case TileType.SlopeCeilLeft:
                            slopeY = wy + TileSize - localX;
                            break;
                        case TileType.GentleCeilRight:
                            slopeY = wy + (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.GentleCeilLeft:
                            slopeY = wy + TileSize / 2f - (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.ShavedCeilRight:
                            // Full block with bottom-right shaved: left=wy+ts, right=wy+ts/2
                            slopeY = wy + TileSize - (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.ShavedCeilLeft:
                            // Full block with bottom-left shaved: left=wy+ts/2, right=wy+ts
                            slopeY = wy + TileSize / 2f + (localX / TileSize) * (TileSize / 2f);
                            break;
                        case TileType.Gentle4CeilRightA:
                            // Shallowest: surface from wy (left) to wy+ts/4 (right)
                            slopeY = wy + (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4CeilRightB:
                            slopeY = wy + TileSize / 4f + (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4CeilRightC:
                            slopeY = wy + TileSize / 2f + (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4CeilRightD:
                            slopeY = wy + TileSize * 3f / 4f + (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4CeilLeftA:
                            slopeY = wy + TileSize / 4f - (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4CeilLeftB:
                            slopeY = wy + TileSize / 2f - (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4CeilLeftC:
                            slopeY = wy + TileSize * 3f / 4f - (localX / TileSize) * (TileSize / 4f);
                            break;
                        case TileType.Gentle4CeilLeftD:
                            slopeY = wy + TileSize - (localX / TileSize) * (TileSize / 4f);
                            break;
                        default:
                            continue;
                    }
                    if (slopeY > bestY) { bestY = slopeY; hitTile = t; }
                }
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
