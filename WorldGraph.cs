using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genesis;

/// <summary>
/// The world graph represents the interconnected room structure of the game world.
/// Each node is a room (level file). Edges are exits connecting rooms.
/// Used for: off-screen creature simulation, Dragon pathing, weather propagation,
/// area/biome grouping, and player map rendering.
/// 
/// This REPLACES the OverworldData node-based level-select system.
/// The old overworld was a menu; this is a spatial graph of connected rooms.
/// </summary>
public class WorldGraph
{
    private const string DefaultPath = "Content/worldgraph.json";
    
    [JsonPropertyName("rooms")] public List<WorldRoom> Rooms { get; set; } = new();
    [JsonPropertyName("areas")] public List<WorldArea> Areas { get; set; } = new();
    
    // --- Lookup ---
    
    [JsonIgnore] private Dictionary<string, WorldRoom> _roomLookup;
    [JsonIgnore] private Dictionary<string, WorldArea> _areaLookup;
    
    public void BuildLookups()
    {
        _roomLookup = new Dictionary<string, WorldRoom>(Rooms.Count);
        foreach (var r in Rooms)
            _roomLookup[r.Id] = r;
        _areaLookup = new Dictionary<string, WorldArea>(Areas.Count);
        foreach (var a in Areas)
            _areaLookup[a.Id] = a;
    }
    
    public WorldRoom GetRoom(string id) =>
        _roomLookup != null && _roomLookup.TryGetValue(id, out var r) ? r : Rooms.FirstOrDefault(r => r.Id == id);
    
    public WorldArea GetArea(string id) =>
        _areaLookup != null && _areaLookup.TryGetValue(id, out var a) ? a : Areas.FirstOrDefault(a => a.Id == id);
    
    /// <summary>Get all rooms adjacent to this room (connected by exits).</summary>
    public List<WorldRoom> GetNeighbors(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null) return new();
        var result = new List<WorldRoom>();
        foreach (var exit in room.Exits)
        {
            var target = GetRoom(exit.TargetRoomId);
            if (target != null) result.Add(target);
        }
        return result;
    }
    
    /// <summary>BFS shortest path between two rooms. Returns room IDs in order (inclusive).</summary>
    public List<string> FindPath(string fromId, string toId)
    {
        if (fromId == toId) return new() { fromId };
        
        var visited = new HashSet<string> { fromId };
        var queue = new Queue<(string id, List<string> path)>();
        queue.Enqueue((fromId, new() { fromId }));
        
        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();
            var room = GetRoom(current);
            if (room == null) continue;
            
            foreach (var exit in room.Exits)
            {
                if (visited.Contains(exit.TargetRoomId)) continue;
                var newPath = new List<string>(path) { exit.TargetRoomId };
                if (exit.TargetRoomId == toId) return newPath;
                visited.Add(exit.TargetRoomId);
                queue.Enqueue((exit.TargetRoomId, newPath));
            }
        }
        return new(); // no path found
    }
    
    /// <summary>Get all rooms within N hops of a room.</summary>
    public HashSet<string> GetRoomsInRange(string roomId, int maxHops)
    {
        var result = new HashSet<string> { roomId };
        var frontier = new Queue<(string id, int dist)>();
        frontier.Enqueue((roomId, 0));
        
        while (frontier.Count > 0)
        {
            var (current, dist) = frontier.Dequeue();
            if (dist >= maxHops) continue;
            var room = GetRoom(current);
            if (room == null) continue;
            
            foreach (var exit in room.Exits)
            {
                if (result.Add(exit.TargetRoomId))
                    frontier.Enqueue((exit.TargetRoomId, dist + 1));
            }
        }
        return result;
    }
    
    /// <summary>Get all rooms belonging to a specific area.</summary>
    public List<WorldRoom> GetAreaRooms(string areaId) =>
        Rooms.Where(r => r.AreaId == areaId).ToList();
    
    // --- Serialization ---
    
    public static WorldGraph Load(string path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return CreateDefault();
        var json = File.ReadAllText(path);
        var graph = JsonSerializer.Deserialize<WorldGraph>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? CreateDefault();
        graph.BuildLookups();
        return graph;
    }
    
    public void Save(string path = null)
    {
        path ??= DefaultPath;
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
    
    /// <summary>Auto-generate graph from existing level files and their exit definitions.</summary>
    public static WorldGraph BuildFromLevels(string levelsDir = "Content/levels")
    {
        var graph = new WorldGraph();
        if (!Directory.Exists(levelsDir)) return graph;
        
        foreach (var file in Directory.GetFiles(levelsDir, "*.json"))
        {
            try
            {
                var level = LevelData.Load(file);
                var roomId = Path.GetFileNameWithoutExtension(file);
                var room = new WorldRoom
                {
                    Id = roomId,
                    Name = level.Name ?? roomId,
                    LevelFile = roomId,
                    Exits = new List<RoomExit>()
                };
                
                // Build exits from level data
                for (int i = 0; i < level.ExitRects.Length; i++)
                {
                    var target = level.ExitTargets[i];
                    if (string.IsNullOrEmpty(target) || target == "__overworld__") continue;
                    room.Exits.Add(new RoomExit
                    {
                        ExitId = level.ExitIds[i],
                        TargetRoomId = target,
                        TargetExitId = level.ExitTargetExitIds[i]
                    });
                }
                
                graph.Rooms.Add(room);
            }
            catch { /* skip broken level files */ }
        }
        
        graph.BuildLookups();
        return graph;
    }
    
    private static WorldGraph CreateDefault()
    {
        // If level files exist, auto-generate
        if (Directory.Exists("Content/levels"))
        {
            var graph = BuildFromLevels();
            if (graph.Rooms.Count > 0) return graph;
        }
        return new WorldGraph();
    }
}

/// <summary>
/// A room in the world graph. Maps 1:1 to a level JSON file.
/// </summary>
public class WorldRoom
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("levelFile")] public string LevelFile { get; set; } = ""; // filename without path/ext
    [JsonPropertyName("areaId")] public string AreaId { get; set; } = ""; // which WorldArea this belongs to
    [JsonPropertyName("exits")] public List<RoomExit> Exits { get; set; } = new();
    
    // Map display position (for rendering the world map)
    [JsonPropertyName("mapX")] public float MapX { get; set; }
    [JsonPropertyName("mapY")] public float MapY { get; set; }
    
    // Discovery state (saved per-playthrough, not in graph file)
    [JsonIgnore] public bool Discovered { get; set; }
    [JsonIgnore] public bool Visited { get; set; }
    
    // Off-screen simulation: creatures currently in this room
    [JsonIgnore] public List<Guid> CreatureIds { get; set; } = new();
    
    // Weather: base variable overrides for this room (null = use area defaults)
    [JsonPropertyName("baseConductivity")] public float? BaseConductivity { get; set; }
    [JsonPropertyName("baseViscosity")] public float? BaseViscosity { get; set; }
    [JsonPropertyName("baseParticulate")] public float? BaseParticulate { get; set; }
}

/// <summary>
/// A connection from one room to another via an exit zone.
/// </summary>
public class RoomExit
{
    [JsonPropertyName("exitId")] public string ExitId { get; set; } = ""; // matches LevelData exit id
    [JsonPropertyName("targetRoomId")] public string TargetRoomId { get; set; } = "";
    [JsonPropertyName("targetExitId")] public string TargetExitId { get; set; } = "";
}

/// <summary>
/// An area/biome grouping of rooms. Each area has base weather variables,
/// fauna density, and maps to a Stage of the Fall.
/// </summary>
public class WorldArea
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("stageOfFall")] public string StageOfFall { get; set; } = ""; // Shock, Wonder, Humility, etc.
    
    // Base weather variables for this area (0.0–1.0)
    [JsonPropertyName("baseConductivity")] public float BaseConductivity { get; set; } = 0.2f;
    [JsonPropertyName("baseViscosity")] public float BaseViscosity { get; set; } = 0.3f;
    [JsonPropertyName("baseParticulate")] public float BaseParticulate { get; set; } = 0.2f;
    [JsonPropertyName("baseResonance")] public float BaseResonance { get; set; } = 0.1f;
    
    // Fauna
    [JsonPropertyName("creatureDensity")] public float CreatureDensity { get; set; } = 1f; // multiplier
    [JsonPropertyName("dominantSpecies")] public List<string> DominantSpecies { get; set; } = new(); // creature types common here
    
    // Visual/audio
    [JsonPropertyName("ambientColor")] public string AmbientColor { get; set; } = ""; // hex color for area lighting
    [JsonPropertyName("musicTrack")] public string MusicTrack { get; set; } = "";
}
