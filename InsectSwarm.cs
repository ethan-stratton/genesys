using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

public class InsectSwarm
{
    public Vector2 HomePosition;
    public float AggroRange = 150f;
    public float LeashRange = 300f;
    public bool Aggroed;
    public List<Insect> Insects = new();

    public InsectSwarm(Vector2 home, int count, Random rng)
    {
        HomePosition = home;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float dist = 20f + (float)(rng.NextDouble() * 30f);
            Insects.Add(new Insect
            {
                Position = home + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist),
                Alive = true,
                OrbitAngle = angle,
                OrbitSpeed = 1.5f + (float)(rng.NextDouble() * 1.5f),
                OrbitRadius = dist,
                StingCooldown = 0,
                Jitter = new Vector2((float)(rng.NextDouble() - 0.5) * 2, (float)(rng.NextDouble() - 0.5) * 2),
                InBackground = rng.NextDouble() < 0.3,  // 30% start behind trees
                LayerSwitchTimer = 2f + (float)(rng.NextDouble() * 4f),
            });
        }
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

        foreach (var ins in Insects)
        {
            if (!ins.Alive) continue;

            if (ins.StingCooldown > 0) ins.StingCooldown -= dt;

            // Layer switching — bugs fly in and out of tree canopy
            ins.LayerSwitchTimer -= dt;
            if (ins.LayerSwitchTimer <= 0)
            {
                ins.InBackground = !ins.InBackground;
                ins.LayerSwitchTimer = 2f + (float)(rng.NextDouble() * 5f);
                // Background bugs don't sting
                if (ins.InBackground) ins.StingCooldown = MathF.Max(ins.StingCooldown, 0.5f);
            }

            if (Aggroed)
            {
                var dir = playerCenter - ins.Position;
                if (dir.LengthSquared() > 1f)
                {
                    dir.Normalize();
                    ins.JitterTimer -= dt;
                    if (ins.JitterTimer <= 0)
                    {
                        ins.Jitter = new Vector2((float)(rng.NextDouble() - 0.5) * 100, (float)(rng.NextDouble() - 0.5) * 100);
                        ins.JitterTimer = 0.2f + (float)(rng.NextDouble() * 0.3f);
                    }
                    var moveDir = dir * 120f + ins.Jitter;
                    if (moveDir.LengthSquared() > 1f)
                    {
                        moveDir.Normalize();
                        ins.Position += moveDir * 110f * dt;
                    }
                }
            }
            else
            {
                ins.OrbitAngle += ins.OrbitSpeed * dt;
                var target = HomePosition + new Vector2(
                    MathF.Cos(ins.OrbitAngle) * ins.OrbitRadius,
                    MathF.Sin(ins.OrbitAngle) * ins.OrbitRadius);
                ins.Position = Vector2.Lerp(ins.Position, target, 3f * dt);
            }
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
            // Smaller + darker = behind trees
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
    public bool Alive;
    public float OrbitAngle;
    public float OrbitSpeed;
    public float OrbitRadius;
    public float StingCooldown;
    public Vector2 Jitter;
    public float JitterTimer;
    public bool InBackground;       // true = behind trees (darker, smaller)
    public float LayerSwitchTimer;   // countdown to next layer switch
}
