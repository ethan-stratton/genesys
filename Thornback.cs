using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class Thornback : Creature
{
    public const int Width = 32, Height = 28;
    public float SquashResistance = 0.7f;
    private float _squashHoldTimer;

    public override int ContactDamage => 2;

    public override float GetDamageMultiplier(DamageType type) => type switch
    {
        DamageType.Slash => 0.5f,
        DamageType.Blunt => 1.5f,
        DamageType.Pierce => 0.75f,
        _ => 1f,
    };

    public Thornback(Vector2 pos)
    {
        Position = pos;
        Hp = 20; MaxHp = 20;
        SpeciesName = "thornback";
        Role = EcologicalRole.Defensive;
        Needs = CreatureNeeds.Default;
        DeathParticleColor = new Color(60, 100, 40);
        HitColor = new Color(60, 100, 40);
        HungerRate = 0.001f;
        FatigueRate = 0.0005f;
        Needs.Hunger = 0.2f + (float)Random.Shared.NextDouble() * 0.3f;
        Needs.Fatigue = (float)Random.Shared.NextDouble() * 0.2f;
    }

    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override float GetActivityLevel(float worldTime) => 0.8f;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public override CreatureGoal SelectGoal()
    {
        if (Needs.Safety < 0.3f || (Hp > 0 && Hp <= MaxHp * 0.3f))
            return CreatureGoal.Rest; // hunker down
        if (Needs.Hunger > 0.7f) return CreatureGoal.Eat;
        if (Needs.Fatigue > 0.7f) return CreatureGoal.Rest;
        return CreatureGoal.Wander;
    }

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;

        // --- Needs system ---
        TickNeeds(dt);
        // Weather effects
        if (ctx.IsRaining)
        {
            Needs.Hunger += dt * HungerRate * 0.5f;
        }
        float distToPlayer = Vector2.Distance(Position, ctx.PlayerCenter);
        Needs.Safety = Math.Min(Needs.Safety, MathHelper.Clamp(distToPlayer / 80f, 0f, 1f));
        CurrentGoal = SelectGoal();

        // Noise detection — thornback doesn't flee, but gets defensive
        var noise = CheckNoise(ctx.NoiseEvents);
        if (noise != null && noise.Intensity > 0.5f)
            Needs.Safety = Math.Min(Needs.Safety, 0.4f);

        // Food seeking (thornbacks eat slowly)
        if (CurrentGoal == CreatureGoal.Eat && !IsEating)
        {
            var food = FindFood(ctx.FoodSources);
            if (food != null)
            {
                float distToFood = Vector2.Distance(Position, food.Position);
                if (distToFood < 10f)
                {
                    IsEating = true;
                    EatingTarget = food;
                    EatTimer = 0f;
                }
            }
        }
        if (IsEating)
        {
            EatTimer += dt;
            if (EatingTarget == null || EatingTarget.Depleted || Needs.Hunger < 0.15f)
            {
                IsEating = false;
                EatingTarget = null;
            }
            else
            {
                float gained = EatingTarget.Eat(dt, 0.15f); // slow eater
                Needs.Hunger = MathHelper.Clamp(Needs.Hunger - gained, 0f, 1f);
                return;
            }
        }

        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);

        // When fleeing (very scared), slowly move away from player
        if (CurrentGoal == CreatureGoal.Flee)
        {
            float dx = ctx.PlayerCenter.X - (Position.X + Width / 2f);
            int fleeDir = dx > 0 ? -1 : 1;
            Position.X += fleeDir * 8f * dt; // very slow movement
        }
        _prevGoal = CurrentGoal;
    }

    /// <summary>Contact damage suppressed when resting.</summary>
    public override int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive || DamageCooldown > 0) return 0;
        if (CurrentGoal == CreatureGoal.Rest) return 0; // passive when resting
        if (Rect.Intersects(playerRect))
        {
            DamageCooldown = 0.5f;
            return ContactDamage;
        }
        return 0;
    }

    public override bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        HitFlash = 0.15f;
        MeleeHitCooldown = 0.2f;
        // Ignore knockback — thornback is stationary
        float squashAmount = 1f - SquashResistance;
        VisualScale = new Vector2(1f + 0.3f * squashAmount, 1f - 0.25f * squashAmount);
        _squashHoldTimer = 0.05f;
        if (Hp <= 0) { Alive = false; return true; }
        return false;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        Color baseColor = HitFlash > 0 ? Color.White : new Color(60, 100, 30);
        int scaledW = (int)(Width * VisualScale.X);
        int scaledH = (int)(Height * VisualScale.Y);
        int drawX = (int)Position.X + Width / 2 - scaledW / 2;
        int drawY = (int)Position.Y + Height - scaledH;
        sb.Draw(pixel, new Rectangle(drawX, drawY, scaledW, scaledH), baseColor);
        sb.Draw(pixel, new Rectangle((int)Position.X - 3, (int)Position.Y + 6, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 1, (int)Position.Y + 8, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + 8, (int)Position.Y - 3, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 12, (int)Position.Y - 3, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + 4, (int)Position.Y + 4, Width - 8, Height - 8), new Color(40, 70, 20));
    }
}
