using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Genesis;

public enum ScanTier { L1, L2, L3 }

/// <summary>
/// A single observation EVE logged about a creature species.
/// Each encounter adds one observation. ~5-10 needed for a full L1 profile.
/// </summary>
public class BestiaryObservation
{
    [JsonPropertyName("time")] public string TimeOfDay { get; set; }
    [JsonPropertyName("weather")] public string Weather { get; set; }
    [JsonPropertyName("behavior")] public string Behavior { get; set; }
    [JsonPropertyName("biome")] public string Biome { get; set; }
}

/// <summary>
/// Bestiary entry for one creature species. Built up over multiple encounters.
/// </summary>
public class BestiaryEntry
{
    [JsonPropertyName("speciesName")] public string SpeciesName { get; set; }
    [JsonPropertyName("classification")] public string Classification { get; set; }
    [JsonPropertyName("observations")] public List<BestiaryObservation> Observations { get; set; } = new();
    [JsonPropertyName("sightCount")] public int SightCount { get; set; }
    [JsonPropertyName("scanTier")] public ScanTier HighestScanTier { get; set; } = ScanTier.L1;

    // L2 data (filled when active-scanned)
    [JsonPropertyName("l2Scanned")] public bool L2Scanned { get; set; }
    [JsonPropertyName("detailedRole")] public string DetailedRole { get; set; }
    [JsonPropertyName("temperament")] public string Temperament { get; set; }
    [JsonPropertyName("weaknesses")] public string Weaknesses { get; set; }
    [JsonPropertyName("resistances")] public string Resistances { get; set; }

    // L3 data (filled after deep scan post-Dragon)
    [JsonPropertyName("l3Scanned")] public bool L3Scanned { get; set; }
    [JsonPropertyName("loreText")] public string LoreText { get; set; }
    [JsonPropertyName("thoughts")] public string Thoughts { get; set; }
    [JsonPropertyName("originalNature")] public string OriginalNature { get; set; }

    /// <summary>How complete is the L1 profile? 0.0–1.0</summary>
    [JsonIgnore] public float L1Progress => Math.Min(1f, Observations.Count / 7f);

    /// <summary>Get EVE's current classification guess based on observation count</summary>
    [JsonIgnore] public string DisplayClassification
    {
        get
        {
            if (SightCount <= 1) return "Unknown";
            if (SightCount <= 3) return Classification + "?";
            return Classification;
        }
    }
}

public class Bestiary
{
    [JsonPropertyName("entries")] public Dictionary<string, BestiaryEntry> Entries { get; set; } = new();

    /// <summary>
    /// Log a sighting. Creates entry if new. Adds observation if cooldown allows.
    /// Returns true if a NEW observation was added (for EVE dialog).
    /// </summary>
    public bool LogSighting(string speciesName, string classification, string timeOfDay, string weather, string behavior, string biome)
    {
        if (!Entries.TryGetValue(speciesName, out var entry))
        {
            entry = new BestiaryEntry
            {
                SpeciesName = speciesName,
                Classification = classification
            };
            Entries[speciesName] = entry;
        }

        entry.SightCount++;

        if (entry.Observations.Count < 10)
        {
            bool isDuplicate = false;
            foreach (var obs in entry.Observations)
            {
                if (obs.TimeOfDay == timeOfDay && obs.Weather == weather && obs.Behavior == behavior && obs.Biome == biome)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                entry.Observations.Add(new BestiaryObservation
                {
                    TimeOfDay = timeOfDay,
                    Weather = weather,
                    Behavior = behavior,
                    Biome = biome
                });
                return true;
            }
        }

        return false;
    }
}
