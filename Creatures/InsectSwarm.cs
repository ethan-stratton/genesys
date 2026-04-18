using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public enum SwarmVariant { Normal, Firefly, Mosquito }

public class InsectSwarm
{
    public Vector2 HomePosition;
    public float AggroRange = 150f;
    public float LeashRange = 300f;
    public bool Aggroed;
    public List<Insect> Insects = new();
    public SwarmVariant Variant = SwarmVariant.Normal;

    // Swarm-level coherence: a slowly drifting "cloud center" the swarm loosely follows
    private Vector2 _cloudOffset;
    private Vector2 _cloudVelocity;
    private float _cloudDriftTimer;

    // Mosquito rain breeding
    private float _breedTimer;
    private int _baseCount;
    private float _excessDieOffTimer;

    // Mosquito buzz noise timer
    private float _buzzTimer;

    // Track noise events for mosquito buzz
    public List<NoiseEvent> PendingNoises = new();

    // Mosquito drain tracking
    public float DrainAccumulator;

    public InsectSwarm(Vector2 home, int count, Random rng, SwarmVariant variant = SwarmVariant.Normal)
    {
        HomePosition = home;
        Variant = variant;
        _baseCount = count;
        _cloudOffset = Vector2.Zero;
        _cloudVelocity = RandomDir(rng) * 15f;
        _cloudDriftTimer = 1f + (float)(rng.NextDouble() * 2f);

        float placementRadius = variant == SwarmVariant.Firefly ? 40f : 25f;

        // Variant-specific defaults
        if (variant == SwarmVariant.Firefly)
        {
            AggroRange = 0f; // never aggro
            LeashRange = 500f;
        }
        else if (variant == SwarmVariant.Mosquito)
        {
            AggroRange = 120f;
            LeashRange = 250f;
        }

        for (int i = 0; i < count; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float dist = 8f + (float)(rng.NextDouble() * placementRadius);
            float baseSpeed = variant == SwarmVariant.Mosquito
                ? 60f + (float)(rng.NextDouble() * 20f)
                : 40f + (float)(rng.NextDouble() * 40f);
            Insects.Add(new Insect
            {
                Position = home + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist),
                Velocity = RandomDir(rng) * (30f + (float)(rng.NextDouble() * 60f)),
                Alive = true,
                StingCooldown = 0,
                DartTimer = 0.3f + (float)(rng.NextDouble() * 1.5f),
                SteerAngle = (float)(rng.NextDouble() * MathF.PI * 2),
                SteerRate = 2f + (float)(rng.NextDouble() * 4f),
                BaseSpeed = baseSpeed,
                InBackground = rng.NextDouble() < 0.3,
                LayerSwitchTimer = 2f + (float)(rng.NextDouble() * 4f),
                GlowPhase = (float)(rng.NextDouble() * MathF.PI * 2),
                GlowRate = 1.5f + (float)(rng.NextDouble() * 1f),
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

    public void Update(float dt, Vector2 playerCenter, Random rng,
        float worldTime = 12f, bool isRaining = false,
        bool torchActive = false, bool lanternActive = false)
    {
        PendingNoises.Clear();

        if (Variant == SwarmVariant.Firefly)
            UpdateFirefly(dt, playerCenter, rng, worldTime);
        else if (Variant == SwarmVariant.Mosquito)
            UpdateMosquito(dt, playerCenter, rng, worldTime, isRaining, torchActive, lanternActive);
        else
            UpdateNormal(dt, playerCenter, rng);
    }

    private void UpdateNormal(float dt, Vector2 playerCenter, Random rng)
    {
        float distToPlayer = Vector2.Distance(playerCenter, HomePosition);

        if (!Aggroed && distToPlayer < AggroRange && AliveCount() > 0)
            Aggroed = true;
        if (Aggroed && distToPlayer > LeashRange)
            Aggroed = false;

        DriftCloud(dt, rng);
        Vector2 swarmCenter = Aggroed ? playerCenter : (HomePosition + _cloudOffset);

        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;
            if (ins.StingCooldown > 0) ins.StingCooldown -= dt;
            UpdateLayerSwitch(ins, dt, rng);
            ApplyFlightModel(ins, dt, rng, swarmCenter, Aggroed ? 200f : 80f);
        }
    }

    private void UpdateFirefly(float dt, Vector2 playerCenter, Random rng, float worldTime)
    {
        // Fireflies never aggro
        Aggroed = false;

        bool isNight = worldTime < 6f || worldTime > 18f;

        DriftCloud(dt, rng);
        Vector2 swarmCenter = HomePosition + _cloudOffset;

        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;

            // Update glow phase
            ins.GlowPhase += ins.GlowRate * dt;

            if (!isNight)
            {
                // Daytime: cluster tight near home, dormant
                Vector2 toHome = HomePosition - ins.Position;
                float d = toHome.Length();
                if (d > 1f)
                {
                    ins.Velocity = (toHome / d) * MathF.Min(d * 2f, 30f);
                }
                else
                {
                    ins.Velocity = Vector2.Zero;
                }
                ins.Position += ins.Velocity * dt;
                continue;
            }

            // Nighttime: active with glow-based mate attraction
            UpdateLayerSwitch(ins, dt, rng);

            float glow = MathF.Sin(ins.GlowPhase) * 0.5f + 0.5f;

            // Mate attraction/repulsion
            Vector2 mateForce = Vector2.Zero;
            if (glow > 0.7f)
            {
                foreach (var other in Insects)
                {
                    if (other == ins || !other.Alive) continue;
                    float otherGlow = MathF.Sin(other.GlowPhase) * 0.5f + 0.5f;
                    if (otherGlow > 0.7f)
                    {
                        Vector2 diff = other.Position - ins.Position;
                        float dist = diff.Length();
                        if (dist < 60f && dist > 0.1f)
                        {
                            Vector2 dir = diff / dist;
                            if (dist < 10f)
                                mateForce -= dir * 40f; // repulsion
                            else
                                mateForce += dir * 25f; // attraction
                        }
                    }
                }
            }

            ApplyFlightModel(ins, dt, rng, swarmCenter, 60f, mateForce);
        }
    }

    private void UpdateMosquito(float dt, Vector2 playerCenter, Random rng,
        float worldTime, bool isRaining, bool torchActive, bool lanternActive)
    {
        float effectiveAggro = lanternActive ? 300f : AggroRange;
        float distToPlayer = Vector2.Distance(playerCenter, HomePosition);

        if (!Aggroed && distToPlayer < effectiveAggro && AliveCount() > 0)
            Aggroed = true;
        if (Aggroed && distToPlayer > LeashRange)
            Aggroed = false;

        // Rain breeding
        if (isRaining)
        {
            _breedTimer += dt;
            if (_breedTimer >= 10f && AliveCount() < 20)
            {
                _breedTimer = 0f;
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                Insects.Add(new Insect
                {
                    Position = HomePosition + new Vector2(MathF.Cos(angle) * 15f, MathF.Sin(angle) * 15f),
                    Velocity = RandomDir(rng) * 50f,
                    Alive = true,
                    StingCooldown = 0,
                    DartTimer = 0.2f + (float)(rng.NextDouble() * 0.8f),
                    SteerAngle = (float)(rng.NextDouble() * MathF.PI * 2),
                    SteerRate = 2f + (float)(rng.NextDouble() * 4f),
                    BaseSpeed = 60f + (float)(rng.NextDouble() * 20f),
                    InBackground = rng.NextDouble() < 0.3,
                    LayerSwitchTimer = 2f + (float)(rng.NextDouble() * 4f),
                });
            }
            _excessDieOffTimer = 0f;
        }
        else
        {
            _breedTimer = 0f;
            // Die off excess over base count
            int alive = AliveCount();
            if (alive > _baseCount)
            {
                _excessDieOffTimer += dt;
                if (_excessDieOffTimer >= 30f / (alive - _baseCount))
                {
                    _excessDieOffTimer = 0f;
                    // Kill one excess
                    for (int i = Insects.Count - 1; i >= 0; i--)
                    {
                        if (Insects[i].Alive) { Insects[i].Alive = false; break; }
                    }
                }
            }
        }

        // Buzz noise
        _buzzTimer -= dt;
        if (_buzzTimer <= 0f)
        {
            _buzzTimer = 3f;
            if (Vector2.Distance(playerCenter, HomePosition) < 200f && AliveCount() > 0)
            {
                PendingNoises.Add(new NoiseEvent(HomePosition, 150f, 0.3f, "bzzz", Color.Gray * 0.5f, 0.8f));
            }
        }

        DriftCloud(dt, rng);

        // Torch repulsion
        Vector2 swarmCenter;
        if (torchActive && Aggroed)
        {
            // Flee from player
            Vector2 awayFromPlayer = HomePosition - playerCenter;
            float aDist = awayFromPlayer.Length();
            if (aDist > 1f) awayFromPlayer /= aDist;
            swarmCenter = HomePosition + awayFromPlayer * 100f;
            Aggroed = false; // break aggro when torch is active
        }
        else
        {
            swarmCenter = Aggroed ? playerCenter : (HomePosition + _cloudOffset);
        }

        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;
            if (ins.StingCooldown > 0) ins.StingCooldown -= dt;
            UpdateLayerSwitch(ins, dt, rng);
            ApplyFlightModel(ins, dt, rng, swarmCenter, Aggroed ? 250f : 100f);
        }
    }

    private void DriftCloud(float dt, Random rng)
    {
        _cloudDriftTimer -= dt;
        if (_cloudDriftTimer <= 0)
        {
            float driftStrength = Variant == SwarmVariant.Firefly ? 30f : 20f;
            _cloudVelocity = RandomDir(rng) * (10f + (float)(rng.NextDouble() * driftStrength));
            _cloudDriftTimer = 1.5f + (float)(rng.NextDouble() * 3f);
        }
        _cloudOffset += _cloudVelocity * dt;
        _cloudOffset *= MathF.Pow(0.3f, dt);
    }

    private void UpdateLayerSwitch(Insect ins, float dt, Random rng)
    {
        ins.LayerSwitchTimer -= dt;
        if (ins.LayerSwitchTimer <= 0)
        {
            ins.InBackground = !ins.InBackground;
            ins.LayerSwitchTimer = 2f + (float)(rng.NextDouble() * 5f);
            if (ins.InBackground) ins.StingCooldown = MathF.Max(ins.StingCooldown, 0.5f);
        }
    }

    private void ApplyFlightModel(Insect ins, float dt, Random rng, Vector2 swarmCenter,
        float cohesionStrength, Vector2 extraForce = default)
    {
        // 1. Smooth random steering
        ins.SteerAngle += ins.SteerRate * dt;
        Vector2 steerForce = new Vector2(
            MathF.Cos(ins.SteerAngle),
            MathF.Sin(ins.SteerAngle)) * ins.BaseSpeed * 0.6f;

        // 2. Lévy flight
        ins.DartTimer -= dt;
        if (ins.DartTimer <= 0)
        {
            float dartSpeed = 80f + (float)(rng.NextDouble() * 160f);
            ins.Velocity = RandomDir(rng) * dartSpeed;
            ins.SteerRate = 2f + (float)(rng.NextDouble() * 5f);
            ins.BaseSpeed = Variant == SwarmVariant.Mosquito
                ? 60f + (float)(rng.NextDouble() * 20f)
                : 40f + (float)(rng.NextDouble() * 40f);
            double r = rng.NextDouble();
            ins.DartTimer = r < 0.7f
                ? 0.2f + (float)(rng.NextDouble() * 0.8f)
                : 1.5f + (float)(rng.NextDouble() * 3f);
        }

        // 3. Cohesion
        Vector2 toCenter = swarmCenter - ins.Position;
        float distFromCenter = toCenter.Length();
        Vector2 cohesion = Vector2.Zero;
        if (distFromCenter > 1f)
        {
            toCenter /= distFromCenter;
            float ramp = MathF.Min(distFromCenter / 50f, 3f);
            cohesion = toCenter * cohesionStrength * ramp;
        }

        // 4. Separation
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

        Vector2 acceleration = steerForce + cohesion + separation + extraForce;
        ins.Velocity += acceleration * dt;
        ins.Velocity *= MathF.Pow(0.05f, dt);
        float speed = ins.Velocity.Length();
        if (speed < 20f && speed > 0.1f)
            ins.Velocity = (ins.Velocity / speed) * 20f;

        ins.Position += ins.Velocity * dt;
    }

    public int CheckPlayerDamage(Rectangle playerRect)
    {
        // Fireflies do no damage
        if (Variant == SwarmVariant.Firefly) return 0;

        // Mosquitoes use drain (handled externally), no burst damage
        if (Variant == SwarmVariant.Mosquito) return 0;

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

    /// <summary>
    /// Mosquito blood drain: returns HP to drain this frame (continuous, not burst).
    /// Call only for Mosquito variant.
    /// </summary>
    public float CheckMosquitoDrain(Rectangle playerRect, float dt, bool playerSprinting)
    {
        if (Variant != SwarmVariant.Mosquito || !Aggroed || playerSprinting) return 0f;

        // Check if player is in swarm area
        bool inRange = false;
        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;
            if (Vector2.Distance(ins.Position, new Vector2(playerRect.Center.X, playerRect.Center.Y)) < 40f)
            {
                inRange = true;
                break;
            }
        }

        if (!inRange) return 0f;

        DrainAccumulator += 0.5f * dt; // 0.5 HP/sec
        if (DrainAccumulator >= 1f)
        {
            DrainAccumulator -= 1f;
            return 1f;
        }
        return 0f;
    }

    public int CheckMeleeHit(Rectangle meleeHitbox)
    {
        int kills = 0;
        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;
            int sz = Variant == SwarmVariant.Mosquito ? 2 : 4;
            var insRect = new Rectangle((int)ins.Position.X - sz/2, (int)ins.Position.Y - sz/2, sz, sz);
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
            Color color;
            int size;
            switch (Variant)
            {
                case SwarmVariant.Firefly:
                    float glow = MathF.Sin(ins.GlowPhase) * 0.5f + 0.5f;
                    color = Color.YellowGreen * (0.2f + glow * 0.3f);
                    size = 2;
                    break;
                case SwarmVariant.Mosquito:
                    color = (Aggroed ? new Color(80, 40, 40) : new Color(60, 50, 40)) * 0.4f;
                    size = 1;
                    break;
                default:
                    color = (Aggroed ? Color.OrangeRed : Color.DarkOliveGreen) * 0.4f;
                    size = 3;
                    break;
            }
            sb.Draw(pixel, new Rectangle((int)ins.Position.X - size/2, (int)ins.Position.Y - size/2, size, size), color);
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (Variant == SwarmVariant.Firefly)
        {
            DrawFireflies(sb, pixel);
            return;
        }

        foreach (var ins in Insects)
        {
            if (!ins.Alive || ins.InBackground) continue;

            if (Variant == SwarmVariant.Mosquito)
            {
                Color c = Aggroed ? Color.DarkRed * 0.7f : new Color(70, 55, 45);
                sb.Draw(pixel, new Rectangle((int)ins.Position.X - 1, (int)ins.Position.Y - 1, 2, 2), c);
            }
            else
            {
                sb.Draw(pixel, new Rectangle((int)ins.Position.X - 2, (int)ins.Position.Y - 2, 4, 4),
                    Aggroed ? Color.OrangeRed : Color.DarkOliveGreen);
            }
        }
    }

    private void DrawFireflies(SpriteBatch sb, Texture2D pixel)
    {
        foreach (var ins in Insects)
        {
            if (!ins.Alive || ins.InBackground) continue;

            float glow = MathF.Sin(ins.GlowPhase) * 0.5f + 0.5f;

            // Body: 3x3 yellow-green
            sb.Draw(pixel, new Rectangle((int)ins.Position.X - 1, (int)ins.Position.Y - 1, 3, 3),
                Color.YellowGreen * (0.5f + glow * 0.5f));

            // Glow: 8x8 additive soft circle (just scaled pixel)
            sb.Draw(pixel, new Rectangle((int)ins.Position.X - 4, (int)ins.Position.Y - 4, 8, 8),
                Color.LightGoldenrodYellow * (glow * 0.4f));
        }
    }

    /// <summary>Draw firefly glow with additive blending. Call between Begin(Additive)/End.</summary>
    public void DrawAdditiveGlow(SpriteBatch sb, Texture2D pixel)
    {
        if (Variant != SwarmVariant.Firefly) return;
        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;
            float glow = MathF.Sin(ins.GlowPhase) * 0.5f + 0.5f;
            sb.Draw(pixel, new Rectangle((int)ins.Position.X - 6, (int)ins.Position.Y - 6, 12, 12),
                Color.LightGoldenrodYellow * (glow * 0.3f));
        }
    }
}

public class Insect
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool Alive;
    public float StingCooldown;

    // Smooth steering
    public float SteerAngle;
    public float SteerRate;
    public float BaseSpeed;

    // Lévy flight
    public float DartTimer;

    // Layer depth
    public bool InBackground;
    public float LayerSwitchTimer;

    // Firefly glow
    public float GlowPhase;
    public float GlowRate = 2f;
}
