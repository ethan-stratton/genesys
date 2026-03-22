using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class InsectSwarm
{
    public Vector2 HomePosition;
    public float AggroRange = 150f;
    public float LeashRange = 300f;
    public bool Aggroed;
    public List<Insect> Insects = new();

    // Swarm-level coherence: a slowly drifting "cloud center" the swarm loosely follows
    private Vector2 _cloudOffset;
    private Vector2 _cloudVelocity;
    private float _cloudDriftTimer;

    public InsectSwarm(Vector2 home, int count, Random rng)
    {
        HomePosition = home;
        _cloudOffset = Vector2.Zero;
        _cloudVelocity = RandomDir(rng) * 15f;
        _cloudDriftTimer = 1f + (float)(rng.NextDouble() * 2f);

        for (int i = 0; i < count; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float dist = 8f + (float)(rng.NextDouble() * 25f);
            Insects.Add(new Insect
            {
                Position = home + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist),
                Velocity = RandomDir(rng) * (30f + (float)(rng.NextDouble() * 60f)),
                Alive = true,
                StingCooldown = 0,

                // Lévy flight: time until next sudden dart
                DartTimer = 0.3f + (float)(rng.NextDouble() * 1.5f),
                // Perlin-ish smooth steering
                SteerAngle = (float)(rng.NextDouble() * MathF.PI * 2),
                SteerRate = 2f + (float)(rng.NextDouble() * 4f), // how fast steering angle rotates
                BaseSpeed = 40f + (float)(rng.NextDouble() * 40f),

                InBackground = rng.NextDouble() < 0.3,
                LayerSwitchTimer = 2f + (float)(rng.NextDouble() * 4f),
            });
        }
    }

    private static Vector2 RandomDir(Random rng)
    {
        float a = (float)(rng.NextDouble() * MathF.PI * 2);
        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    public int AliveCount()
    {
        int c = 0;
        foreach (var ins in Insects) if (ins.Alive) c++;
        return c;
    }

    public void Update(float dt, Vector2 playerCenter, Random rng)
    {
        float distToPlayer = Vector2.Distance(playerCenter, HomePosition);

        if (!Aggroed && distToPlayer < AggroRange && AliveCount() > 0)
            Aggroed = true;
        if (Aggroed && distToPlayer > LeashRange)
            Aggroed = false;

        // Drift the cloud center slowly (gives the whole swarm a gentle sway)
        _cloudDriftTimer -= dt;
        if (_cloudDriftTimer <= 0)
        {
            _cloudVelocity = RandomDir(rng) * (10f + (float)(rng.NextDouble() * 20f));
            _cloudDriftTimer = 1.5f + (float)(rng.NextDouble() * 3f);
        }
        _cloudOffset += _cloudVelocity * dt;
        // Spring the cloud back toward home so it doesn't wander too far
        _cloudOffset *= MathF.Pow(0.3f, dt); // exponential decay toward zero

        Vector2 swarmCenter = Aggroed ? playerCenter : (HomePosition + _cloudOffset);

        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;

            if (ins.StingCooldown > 0) ins.StingCooldown -= dt;

            // Layer switching
            ins.LayerSwitchTimer -= dt;
            if (ins.LayerSwitchTimer <= 0)
            {
                ins.InBackground = !ins.InBackground;
                ins.LayerSwitchTimer = 2f + (float)(rng.NextDouble() * 5f);
                if (ins.InBackground) ins.StingCooldown = MathF.Max(ins.StingCooldown, 0.5f);
            }

            // --- Fly movement model ---
            // 1. Smooth random steering (Perlin-like: rotating angle gives curved paths)
            ins.SteerAngle += ins.SteerRate * dt;
            Vector2 steerForce = new Vector2(
                MathF.Cos(ins.SteerAngle),
                MathF.Sin(ins.SteerAngle)) * ins.BaseSpeed * 0.6f;

            // 2. Lévy flight: occasional sudden dart
            ins.DartTimer -= dt;
            if (ins.DartTimer <= 0)
            {
                // Sudden burst in random direction
                float dartSpeed = 80f + (float)(rng.NextDouble() * 160f);
                ins.Velocity = RandomDir(rng) * dartSpeed;
                // Vary the steering to create a new flight pattern after dart
                ins.SteerRate = 2f + (float)(rng.NextDouble() * 5f);
                ins.BaseSpeed = 40f + (float)(rng.NextDouble() * 40f);
                // Next dart: mostly short waits, occasionally long (Lévy-ish)
                double r = rng.NextDouble();
                ins.DartTimer = r < 0.7f
                    ? 0.2f + (float)(rng.NextDouble() * 0.8f)   // short: frequent jittery darts
                    : 1.5f + (float)(rng.NextDouble() * 3f);    // long: calm cruising
            }

            // 3. Cohesion: pull toward swarm center
            Vector2 toCenter = swarmCenter - ins.Position;
            float distFromCenter = toCenter.Length();
            Vector2 cohesion = Vector2.Zero;
            if (distFromCenter > 1f)
            {
                toCenter /= distFromCenter; // normalize
                // Stronger pull the further away (quadratic ramp)
                float pullStrength = Aggroed ? 200f : 80f;
                float ramp = MathF.Min(distFromCenter / 50f, 3f);
                cohesion = toCenter * pullStrength * ramp;
            }

            // 4. Separation: push away from very close neighbors
            Vector2 separation = Vector2.Zero;
            foreach (var other in Insects)
            {
                if (other == ins || !other.Alive) continue;
                Vector2 diff = ins.Position - other.Position;
                float d = diff.Length();
                if (d < 12f && d > 0.1f)
                {
                    separation += (diff / d) * (12f - d) * 8f;
                }
            }

            // Combine forces
            Vector2 acceleration = steerForce + cohesion + separation;

            // Apply acceleration with drag (terminal velocity feel)
            ins.Velocity += acceleration * dt;
            // Drag: flies decelerate quickly when not being pushed
            ins.Velocity *= MathF.Pow(0.05f, dt); // heavy drag = snappy stops
            // But maintain a minimum buzzing speed
            float speed = ins.Velocity.Length();
            if (speed < 20f && speed > 0.1f)
                ins.Velocity = (ins.Velocity / speed) * 20f;

            ins.Position += ins.Velocity * dt;
        }
    }

    public int CheckPlayerDamage(Rectangle playerRect)
    {
        int totalDmg = 0;
        foreach (var ins in Insects)
        {
            if (!ins.Alive || ins.InBackground || ins.StingCooldown > 0) continue;
            var insRect = new Rectangle((int)ins.Position.X - 2, (int)ins.Position.Y - 2, 4, 4);
            if (insRect.Intersects(playerRect))
            {
                totalDmg += 2;
                ins.StingCooldown = 0.8f;
            }
        }
        return totalDmg;
    }

    public int CheckMeleeHit(Rectangle meleeHitbox)
    {
        int kills = 0;
        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;
            var insRect = new Rectangle((int)ins.Position.X - 2, (int)ins.Position.Y - 2, 4, 4);
            if (insRect.Intersects(meleeHitbox))
            {
                ins.Alive = false;
                kills++;
            }
        }
        return kills;
    }

    public bool CheckBulletHit(Rectangle bulletRect)
    {
        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;
            var insRect = new Rectangle((int)ins.Position.X - 2, (int)ins.Position.Y - 2, 4, 4);
            if (insRect.Intersects(bulletRect))
            {
                ins.Alive = false;
                return true;
            }
        }
        return false;
    }

    public void DrawBackground(SpriteBatch sb, Texture2D pixel)
    {
        foreach (var ins in Insects)
        {
            if (!ins.Alive || !ins.InBackground) continue;
            sb.Draw(pixel, new Rectangle((int)ins.Position.X - 1, (int)ins.Position.Y - 1, 3, 3),
                (Aggroed ? Color.OrangeRed : Color.DarkOliveGreen) * 0.4f);
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        foreach (var ins in Insects)
        {
            if (!ins.Alive || ins.InBackground) continue;
            sb.Draw(pixel, new Rectangle((int)ins.Position.X - 2, (int)ins.Position.Y - 2, 4, 4),
                Aggroed ? Color.OrangeRed : Color.DarkOliveGreen);
        }
    }
}

public class Insect
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool Alive;
    public float StingCooldown;

    // Smooth steering: rotating angle creates curved flight paths
    public float SteerAngle;
    public float SteerRate;
    public float BaseSpeed;

    // Lévy flight: timer until next sudden direction change
    public float DartTimer;

    // Layer depth (3D illusion)
    public bool InBackground;
    public float LayerSwitchTimer;
}
