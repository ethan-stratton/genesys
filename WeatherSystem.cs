using System;
using Microsoft.Xna.Framework;

namespace Genesis;

public class WeatherSystem
{
    // Atmospheric state (all 0-1)
    public float Moisture;      // builds over time, near water = faster. Rain depletes it
    public float Temperature;   // follows time of day (warm midday, cool night)
    public float WindStrength;  // semi-random, increases before storms
    public float StormEnergy;   // builds during rain, triggers lightning/heavy rain
    
    // Derived weather states (computed from atmospheric state)
    public bool IsRaining => Moisture > 0.7f;
    public bool IsStorming => StormEnergy > 0.6f && IsRaining;
    public bool IsWindy => WindStrength > 0.4f;
    public bool IsFoggy => Moisture > 0.4f && Moisture < 0.7f && Temperature < 0.4f;
    public bool IsIonicStorm => StormEnergy > 0.8f && IsStorming && Temperature > 0.55f;  // rare: hot storm with high energy
    
    // Wind direction (-1 to 1, negative = left, positive = right)
    public float WindDirection;
    
    // Intensity values for rendering
    public float RainIntensity => IsRaining ? MathHelper.Clamp((Moisture - 0.7f) / 0.3f, 0f, 1f) : 0f;
    
    private Random _rng;
    private float _windShiftTimer;
    
    public WeatherSystem(Random rng = null)
    {
        _rng = rng ?? new Random();
        Moisture = 0.3f + (float)_rng.NextDouble() * 0.2f;
        Temperature = 0.5f;
        WindStrength = 0.1f;
        WindDirection = _rng.NextDouble() > 0.5 ? 0.5f : -0.5f;
    }
    
    /// <summary>
    /// Update weather each frame.
    /// waterTileRatio: fraction of nearby tiles that are water (0-1), increases moisture buildup
    /// worldTime: 0-24 hour clock
    /// </summary>
    public void Update(float dt, float worldTime, float waterTileRatio = 0f)
    {
        // Temperature follows time of day (sine curve: warm at noon, cool at midnight)
        float targetTemp = 0.5f + 0.4f * MathF.Sin((worldTime - 6f) / 24f * MathF.PI * 2f);
        Temperature = MathHelper.Lerp(Temperature, targetTemp, dt * 0.02f);
        
        // Moisture builds slowly, faster near water
        float moistureGain = 0.003f + waterTileRatio * 0.008f; // base + water bonus
        if (!IsRaining)
        {
            Moisture += dt * moistureGain;
            // Warm + moist = evaporation builds faster
            if (Temperature > 0.6f) Moisture += dt * 0.002f;
        }
        else
        {
            // Rain depletes moisture
            Moisture -= dt * 0.01f;
            // Storm energy builds during rain
            StormEnergy += dt * 0.005f;
        }
        
        // Storm energy decays when not raining
        if (!IsRaining)
            StormEnergy = MathHelper.Clamp(StormEnergy - dt * 0.02f, 0f, 1f);
        
        // Wind shifts periodically
        _windShiftTimer -= dt;
        if (_windShiftTimer <= 0)
        {
            _windShiftTimer = 10f + (float)_rng.NextDouble() * 30f; // shift every 10-40s
            float targetWind = (float)(_rng.NextDouble() * 2 - 1); // -1 to 1
            WindDirection = MathHelper.Lerp(WindDirection, targetWind, 0.3f);
            
            // Wind picks up before/during storms
            float targetStrength = IsStorming ? 0.7f + (float)_rng.NextDouble() * 0.3f
                : IsRaining ? 0.3f + (float)_rng.NextDouble() * 0.3f
                : (float)_rng.NextDouble() * 0.4f;
            WindStrength = MathHelper.Lerp(WindStrength, targetStrength, 0.3f);
        }
        
        // Clamp everything
        Moisture = MathHelper.Clamp(Moisture, 0f, 1f);
        Temperature = MathHelper.Clamp(Temperature, 0f, 1f);
        WindStrength = MathHelper.Clamp(WindStrength, 0f, 1f);
        StormEnergy = MathHelper.Clamp(StormEnergy, 0f, 1f);
    }
    
    /// <summary>Get a summary string for debug/bestiary</summary>
    public string GetDescription()
    {
        if (IsStorming) return "storm";
        if (IsRaining) return "rain";
        if (IsFoggy) return "fog";
        if (IsWindy) return "windy";
        return "clear";
    }
}
