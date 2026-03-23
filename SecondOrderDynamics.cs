using System;
using Microsoft.Xna.Framework;

namespace Genesis;

/// <summary>
/// Second-order dynamics system for smooth, physical-feeling motion.
/// Based on t3ssel8r's "Giving Personality to Procedural Animations using Math."
/// 
/// Parameters:
///   f  — frequency (Hz). Speed of response. Higher = snappier.
///   z  — damping (zeta). 0=infinite vibration, 1=critical, >1=sluggish.
///   r  — initial response. 0=slow start, >1=overshoot, <0=anticipation.
/// 
/// Usage:
///   var spring = new SecondOrderDynamics(2f, 0.5f, -1f, initialValue);
///   each frame: float smoothed = spring.Update(dt, targetValue);
/// </summary>
public class SecondOrderDynamics
{
    private float _k1, _k2, _k3;
    private float _y, _yd; // state: position, velocity
    private float _xPrev;  // previous input for velocity estimation

    public float Value => _y;
    public float Velocity => _yd;

    public SecondOrderDynamics(float f, float z, float r, float x0)
    {
        _k1 = z / (MathF.PI * f);
        _k2 = 1f / (4f * MathF.PI * MathF.PI * f * f);
        _k3 = r * z / (2f * MathF.PI * f);
        _xPrev = x0;
        _y = x0;
        _yd = 0f;
    }

    /// <summary>
    /// Reconfigure parameters without resetting state.
    /// Use for personality changes (calm→panicked).
    /// </summary>
    public void SetParams(float f, float z, float r)
    {
        _k1 = z / (MathF.PI * f);
        _k2 = 1f / (4f * MathF.PI * MathF.PI * f * f);
        _k3 = r * z / (2f * MathF.PI * f);
    }

    public float Update(float dt, float x, float xd = float.NaN)
    {
        if (dt <= 0f) return _y;

        // Estimate input velocity if not provided
        if (float.IsNaN(xd))
        {
            xd = (x - _xPrev) / dt;
            _xPrev = x;
        }

        // Stability: clamp k2 to prevent divergence at low framerates
        float k2Stable = MathF.Max(_k2, MathF.Max(dt * dt / 4f + dt * _k1 / 2f,
                                                     dt * (dt + _k1) / 4f));

        // Semi-implicit Euler integration
        _y += dt * _yd;
        _yd += dt * (x + _k3 * xd - _y - _k1 * _yd) / k2Stable;

        return _y;
    }

    /// <summary>Hard-set position and zero velocity.</summary>
    public void Reset(float x)
    {
        _y = x;
        _yd = 0f;
        _xPrev = x;
    }
}

/// <summary>
/// 2D version — two independent SecondOrderDynamics axes.
/// </summary>
public class SecondOrderDynamics2D
{
    private SecondOrderDynamics _x, _y;

    public Vector2 Value => new(_x.Value, _y.Value);
    public Vector2 Velocity => new(_x.Velocity, _y.Velocity);

    public SecondOrderDynamics2D(float f, float z, float r, Vector2 initial)
    {
        _x = new SecondOrderDynamics(f, z, r, initial.X);
        _y = new SecondOrderDynamics(f, z, r, initial.Y);
    }

    public void SetParams(float f, float z, float r)
    {
        _x.SetParams(f, z, r);
        _y.SetParams(f, z, r);
    }

    public Vector2 Update(float dt, Vector2 target)
    {
        return new Vector2(_x.Update(dt, target.X), _y.Update(dt, target.Y));
    }

    public void Reset(Vector2 v)
    {
        _x.Reset(v.X);
        _y.Reset(v.Y);
    }
}
