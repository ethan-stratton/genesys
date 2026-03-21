using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace ArenaShooter;

public class LevelData
{
    [JsonPropertyName("name")] public string Name { get; set; } = "Untitled";
    [JsonPropertyName("author")] public string Author { get; set; } = "";
    [JsonPropertyName("playerSpawn")] public PointData PlayerSpawn { get; set; } = new() { X = 400, Y = 500 };
    [JsonPropertyName("bounds")] public BoundsData Bounds { get; set; } = new();
    [JsonPropertyName("floor")] public FloorData Floor { get; set; } = new();
    [JsonPropertyName("platforms")] public RectData[] Platforms { get; set; } = Array.Empty<RectData>();
    [JsonPropertyName("ropes")] public RopeData[] Ropes { get; set; } = Array.Empty<RopeData>();
    [JsonPropertyName("walls")] public WallData[] Walls { get; set; } = Array.Empty<WallData>();
    [JsonPropertyName("spikes")] public RectData[] Spikes { get; set; } = Array.Empty<RectData>();
    [JsonPropertyName("ceilings")] public RectData[] Ceilings { get; set; } = Array.Empty<RectData>();
    [JsonPropertyName("solidFloors")] public RectData[] SolidFloors { get; set; } = Array.Empty<RectData>();
    [JsonPropertyName("wallSpikes")] public WallSpikeData[] WallSpikes { get; set; } = Array.Empty<WallSpikeData>();
    [JsonPropertyName("exits")] public ExitData[] Exits { get; set; } = Array.Empty<ExitData>();
    [JsonPropertyName("npcs")] public NpcData[] Npcs { get; set; } = Array.Empty<NpcData>();
    [JsonPropertyName("items")] public ItemData[] Items { get; set; } = Array.Empty<ItemData>();
    [JsonPropertyName("objects")] public EnvObjectData[] Objects { get; set; } = Array.Empty<EnvObjectData>();
    [JsonPropertyName("enemies")] public EnemySpawnData[] Enemies { get; set; } = Array.Empty<EnemySpawnData>();
    [JsonPropertyName("switches")] public SwitchData[] Switches { get; set; } = Array.Empty<SwitchData>();
    [JsonPropertyName("labels")] public LabelData[] Labels { get; set; } = Array.Empty<LabelData>();
    [JsonPropertyName("tileGrid")] public TileGridData TileGrid { get; set; }

    [JsonIgnore] public TileGrid TileGridInstance { get; set; }

    // Derived arrays (populated after load)
    [JsonIgnore] public Rectangle[] PlatformRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] WallRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public int[] WallClimbSides { get; private set; } = Array.Empty<int>();
    [JsonIgnore] public Rectangle[] WallLedges { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] AllPlatforms { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] SpikeRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] CeilingRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] SolidFloorRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] WallSpikeRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] AllSpikeRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] ExitRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public string[] ExitTargets { get; private set; } = Array.Empty<string>();
    [JsonIgnore] public string[] ExitIds { get; private set; } = Array.Empty<string>();
    [JsonIgnore] public string[] ExitTargetExitIds { get; private set; } = Array.Empty<string>();
    [JsonIgnore] public Rectangle[] NpcRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public Rectangle[] ItemRects { get; private set; } = Array.Empty<Rectangle>();
    [JsonIgnore] public float[] RopeXPositions { get; private set; } = Array.Empty<float>();
    [JsonIgnore] public float[] RopeTops { get; private set; } = Array.Empty<float>();
    [JsonIgnore] public float[] RopeBottoms { get; private set; } = Array.Empty<float>();
    [JsonIgnore] public Rectangle[] SlopeRects { get; private set; } = Array.Empty<Rectangle>();

    public void Build()
    {
        // Platforms
        PlatformRects = new Rectangle[Platforms.Length];
        for (int i = 0; i < Platforms.Length; i++)
        {
            var p = Platforms[i];
            PlatformRects[i] = new Rectangle(p.X, p.Y, p.W, p.H);
        }

        // Walls
        WallRects = new Rectangle[Walls.Length];
        WallClimbSides = new int[Walls.Length];
        WallLedges = new Rectangle[Walls.Length];
        for (int i = 0; i < Walls.Length; i++)
        {
            var w = Walls[i];
            WallRects[i] = new Rectangle(w.X, w.Y, w.W, w.H);
            WallClimbSides[i] = w.ClimbSide;
            WallLedges[i] = new Rectangle(w.X, w.Y, w.W, 12);
        }

        // AllPlatforms = platforms + wall ledges
        AllPlatforms = new Rectangle[PlatformRects.Length + WallLedges.Length];
        PlatformRects.CopyTo(AllPlatforms, 0);
        WallLedges.CopyTo(AllPlatforms, PlatformRects.Length);

        // Spikes
        SpikeRects = new Rectangle[Spikes.Length];
        for (int i = 0; i < Spikes.Length; i++)
        {
            var s = Spikes[i];
            SpikeRects[i] = new Rectangle(s.X, s.Y, s.W, s.H);
        }

        // Ceilings
        CeilingRects = new Rectangle[Ceilings.Length];
        for (int i = 0; i < Ceilings.Length; i++)
        {
            var c = Ceilings[i];
            CeilingRects[i] = new Rectangle(c.X, c.Y, c.W, c.H);
        }

        // Solid floors
        SolidFloorRects = new Rectangle[SolidFloors.Length];
        for (int i = 0; i < SolidFloors.Length; i++)
        {
            var sf = SolidFloors[i];
            SolidFloorRects[i] = new Rectangle(sf.X, sf.Y, sf.W, sf.H);
        }

        // Wall spikes
        WallSpikeRects = new Rectangle[WallSpikes.Length];
        for (int i = 0; i < WallSpikes.Length; i++)
        {
            var ws = WallSpikes[i];
            WallSpikeRects[i] = new Rectangle(ws.X, ws.Y, ws.W, ws.H);
        }

        // All spikes combined
        AllSpikeRects = new Rectangle[SpikeRects.Length + WallSpikeRects.Length];
        SpikeRects.CopyTo(AllSpikeRects, 0);
        WallSpikeRects.CopyTo(AllSpikeRects, SpikeRects.Length);

        // Exits
        ExitRects = new Rectangle[Exits.Length];
        ExitTargets = new string[Exits.Length];
        ExitIds = new string[Exits.Length];
        ExitTargetExitIds = new string[Exits.Length];
        for (int i = 0; i < Exits.Length; i++)
        {
            var e = Exits[i];
            ExitRects[i] = new Rectangle(e.X, e.Y, e.W, e.H);
            ExitTargets[i] = e.TargetLevel;
            ExitIds[i] = e.Id;
            ExitTargetExitIds[i] = e.TargetExitId;
        }

        // NPCs
        NpcRects = new Rectangle[Npcs.Length];
        for (int i = 0; i < Npcs.Length; i++)
        {
            var n = Npcs[i];
            NpcRects[i] = new Rectangle(n.X, n.Y, n.W, n.H);
        }

        // Ropes
        RopeXPositions = new float[Ropes.Length];
        RopeTops = new float[Ropes.Length];
        RopeBottoms = new float[Ropes.Length];
        for (int i = 0; i < Ropes.Length; i++)
        {
            RopeXPositions[i] = Ropes[i].X;
            RopeTops[i] = Ropes[i].Top;
            RopeBottoms[i] = Ropes[i].Bottom;
        }

        // Items
        ItemRects = new Rectangle[Items.Length];
        for (int i = 0; i < Items.Length; i++)
        {
            var item = Items[i];
            ItemRects[i] = new Rectangle((int)item.X, (int)item.Y, item.W, item.H);
        }

        // Tile grid collision integration
        if (TileGrid != null)
        {
            TileGridInstance = ArenaShooter.TileGrid.FromData(TileGrid);
        }
        if (TileGridInstance != null)
        {
            RebuildTileCollision();
        }
    }

    /// <summary>Rebuild merged collision rects from tile grid (call after tile changes like breakable destruction).</summary>
    public void RebuildTileCollision()
    {
        if (TileGridInstance == null) return;
        
        var tileSolids = TileGridInstance.GetSolidRects();
        var tilePlatforms = TileGridInstance.GetPlatformRects();
        var tileHazards = TileGridInstance.GetHazardRects();

        // Reset to base rects (non-tile)
        SolidFloorRects = new Rectangle[SolidFloors.Length];
        for (int i = 0; i < SolidFloors.Length; i++)
        {
            var sf = SolidFloors[i];
            SolidFloorRects[i] = new Rectangle(sf.X, sf.Y, sf.W, sf.H);
        }
        CeilingRects = new Rectangle[Ceilings.Length];
        for (int i = 0; i < Ceilings.Length; i++)
        {
            var c = Ceilings[i];
            CeilingRects[i] = new Rectangle(c.X, c.Y, c.W, c.H);
        }
        AllPlatforms = new Rectangle[PlatformRects.Length + WallLedges.Length];
        PlatformRects.CopyTo(AllPlatforms, 0);
        WallLedges.CopyTo(AllPlatforms, PlatformRects.Length);
        AllSpikeRects = new Rectangle[SpikeRects.Length + WallSpikeRects.Length];
        SpikeRects.CopyTo(AllSpikeRects, 0);
        WallSpikeRects.CopyTo(AllSpikeRects, SpikeRects.Length);

        // Merge tile rects
        if (tileSolids.Length > 0)
        {
            var merged = new Rectangle[SolidFloorRects.Length + tileSolids.Length];
            SolidFloorRects.CopyTo(merged, 0);
            tileSolids.CopyTo(merged, SolidFloorRects.Length);
            SolidFloorRects = merged;

            var mergedCeil = new Rectangle[CeilingRects.Length + tileSolids.Length];
            CeilingRects.CopyTo(mergedCeil, 0);
            tileSolids.CopyTo(mergedCeil, CeilingRects.Length);
            CeilingRects = mergedCeil;
        }
        if (tilePlatforms.Length > 0)
        {
            var merged = new Rectangle[AllPlatforms.Length + tilePlatforms.Length];
            AllPlatforms.CopyTo(merged, 0);
            tilePlatforms.CopyTo(merged, AllPlatforms.Length);
            AllPlatforms = merged;
        }
        if (tileHazards.Length > 0)
        {
            var merged = new Rectangle[AllSpikeRects.Length + tileHazards.Length];
            AllSpikeRects.CopyTo(merged, 0);
            tileHazards.CopyTo(merged, AllSpikeRects.Length);
            AllSpikeRects = merged;
        }
        SlopeRects = TileGridInstance.GetSlopeRects();
    }

    public static LevelData Load(string path)
    {
        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var level = JsonSerializer.Deserialize<LevelData>(json, opts) ?? new LevelData();
        level.Build();
        return level;
    }
}

public class PointData
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
}

public class BoundsData
{
    [JsonPropertyName("left")] public int Left { get; set; } = -600;
    [JsonPropertyName("right")] public int Right { get; set; } = 1400;
    [JsonPropertyName("top")] public int Top { get; set; } = -200;
    [JsonPropertyName("bottom")] public int Bottom { get; set; } = 600;
}

public class FloorData
{
    [JsonPropertyName("y")] public int Y { get; set; } = 550;
    [JsonPropertyName("height")] public int Height { get; set; } = 50;
}

public class RectData
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }
}

public class RopeData
{
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("top")] public float Top { get; set; }
    [JsonPropertyName("bottom")] public float Bottom { get; set; }
}

public class WallData
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }
    [JsonPropertyName("climbSide")] public int ClimbSide { get; set; } = 1;
}

public class WallSpikeData
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; } = 12;
    [JsonPropertyName("h")] public int H { get; set; } = 48;
    [JsonPropertyName("side")] public int Side { get; set; } = 1; // 1=right side of wall, -1=left side
}

public class ExitData
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; } = 20;
    [JsonPropertyName("h")] public int H { get; set; } = 48;
    [JsonPropertyName("targetLevel")] public string TargetLevel { get; set; } = "";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("targetExitId")] public string TargetExitId { get; set; } = "";
}

public class ItemData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; } = 20;
    [JsonPropertyName("h")] public int H { get; set; } = 20;
}

public class NpcData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "NPC";
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; } = 24;
    [JsonPropertyName("h")] public int H { get; set; } = 48;
    [JsonPropertyName("color")] public string Color { get; set; } = "Purple";
    [JsonPropertyName("dialogue")] public string[] Dialogue { get; set; } = Array.Empty<string>();
    [JsonPropertyName("dialogueSpeakers")] public string[] DialogueSpeakers { get; set; } = Array.Empty<string>();
}

public class EnvObjectData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; } = 40;
    [JsonPropertyName("h")] public int H { get; set; } = 80;
}

public class EnemySpawnData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; } = 1;
    [JsonPropertyName("scale")] public float Scale { get; set; } = 1f;
    [JsonPropertyName("frozen")] public bool Frozen { get; set; } = false;
}

public class LabelData
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("color")] public string Color { get; set; } = "White";
    [JsonPropertyName("size")] public string Size { get; set; } = "small"; // "small", "normal", "large"
}

public class SwitchData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; } = 16;
    [JsonPropertyName("h")] public int H { get; set; } = 24;
    [JsonPropertyName("action")] public string Action { get; set; } = ""; // "unfreeze-crawlers", etc.
    [JsonPropertyName("label")] public string Label { get; set; } = "";
}
