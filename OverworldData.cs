using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genesis;

public class OverworldData
{
    [JsonPropertyName("nodes")] public OverworldNode[] Nodes { get; set; } = Array.Empty<OverworldNode>();
    [JsonPropertyName("startNode")] public string StartNode { get; set; } = "";

    public static OverworldData Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<OverworldData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new OverworldData();
    }

    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public OverworldNode FindNode(string id)
    {
        foreach (var n in Nodes)
            if (n.Id == id) return n;
        return null;
    }
}

public class OverworldNode
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "???";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("level")] public string Level { get; set; } = "";
    [JsonPropertyName("discovered")] public bool Discovered { get; set; }
    [JsonPropertyName("cleared")] public bool Cleared { get; set; }
    [JsonPropertyName("connections")] public string[] Connections { get; set; } = Array.Empty<string>();

    public string ShownName => Discovered ? (!string.IsNullOrEmpty(DisplayName) ? DisplayName : Name) : "???";
}
