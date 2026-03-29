using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class Slug : Creature
{
    public const int Width = 16, Height = 8;
    private float _animPhase;
    private float _speed = 15f;
    private readonly Random _rng;

    // Slime trail
    public List<(Vector2 Pos, float Life)> SlimeTrail = new();
    private float _slimeDistAccum;
    private Vector2 _lastSlimePos;

    // Eye stalk retraction
    private float _eyeRetract; // 0 = extended, 1 = retracted

    // Movement
    private bool _onGround;
    private float _wanderTimer;
    private const float Gravity = 600f;

    public override bool IsNocturnal => true;
    public override bool CanBurrow => true;
    public override int ContactDamage => 0;
    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public Slug(Vector2 pos, Random rng)
    {
        Position = pos;
        _rng = rng;
        Velocity = Vector2.Zero;
        Hp = 5; MaxHp = 5;
        SpeciesName = "slug";
        Role = EcologicalRole.Herbivore;
        Needs = CreatureNeeds.Default;
        DeathParticleColor = new Color(107, 142, 35); // olive green
        HitColor = new Color(107, 142, 35);
        SpawnOrigin = pos;
        HungerRate = 0.001f;
        FatigueRate = 0.0005f;
        Needs.Hunger = 0.2f + (float)rng.NextDouble() * 0.3f;
        _lastSlimePos = pos;
        _wanderTimer = 1f + (float)rng.NextDouble() * 3f;
        Dir = rng.NextDouble() < 0.5 ? 1 : -1;
    }

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;
        TickDirCooldown(dt);

        // Needs
        TickNeeds(dt);
        CurrentGoal = SelectGoal();

        // Weather-reactive speed
        float effectiveSpeed = _speed;
        float activity = GetActivityLevel(ctx.WorldTime);
        effectiveSpeed *= (0.3f + 0.7f * activity);

        if (ctx.IsRaining)
        {
            effectiveSpeed = 30f * (0.3f + 0.7f * activity); // doubles in rain
        }
        else if (ctx.Temperature > 0.7f)
        {
            effectiveSpeed = 7f; // sluggish in heat
            // Burrow to hide in extreme heat
            if (ctx.Temperature > 0.85f && CanBurrow && BurrowProgress < 1f)
            {
                BurrowProgress = MathHelper.Clamp(BurrowProgress + dt * 0.3f, 0f, 1f);
                if (BurrowProgress >= 1f) return; // fully burrowed, skip movement
            }
        }

        // Unborrow if conditions improve
        if (BurrowProgress > 0f && (ctx.IsRaining || ctx.Temperature <= 0.7f))
        {
            BurrowProgress = MathHelper.Clamp(BurrowProgress - dt * 0.5f, 0f, 1f);
        }

        if (BurrowProgress >= 1f) return; // fully burrowed

        // Animation phase
        _animPhase += dt * (effectiveSpeed / 15f) * 3f;

        // Eye stalk retraction: check for nearby entities
        float nearestDist = float.MaxValue;
        float px = Vector2.Distance(new Vector2(Position.X + Width / 2f, Position.Y), ctx.PlayerCenter);
        nearestDist = MathF.Min(nearestDist, px);
        foreach (var c in ctx.NearbyCreatures)
        {
            if (c == this || !c.Alive) continue;
            float d = Vector2.Distance(Position, c.Position);
            if (d < nearestDist) nearestDist = d;
        }
        float targetRetract = nearestDist < 30f ? 1f : 0f;
        _eyeRetract = MathHelper.Lerp(_eyeRetract, targetRetract, dt * 8f);

        // Flee only when hit (Hp < MaxHp as proxy for "was hit")
        if (CurrentGoal == CreatureGoal.Flee && Hp < MaxHp)
        {
            float fleeDir = Position.X < ctx.PlayerCenter.X ? -1f : 1f;
            TrySetDir(fleeDir < 0 ? -1 : 1);
            Velocity.X = fleeDir * effectiveSpeed * 2f;
        }
        else if (CurrentGoal == CreatureGoal.Eat)
        {
            // Seek nearest plant food
            NavigateTo(FindNearestFood(ctx.FoodSources, FoodType.Plant), ctx);
            Velocity.X = Dir * effectiveSpeed;
        }
        else
        {
            // Wander
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = 2f + (float)_rng.NextDouble() * 4f;
                TrySetDir(_rng.NextDouble() < 0.5 ? 1 : -1, true);
            }
            Velocity.X = Dir * effectiveSpeed;
        }

        // Gravity
        Velocity.Y += Gravity * dt;

        // Tile collision
        if (ctx.TileGrid != null)
        {
            _onGround = EnemyPhysics.ApplyGravityAndCollision(
                ref Position, ref Velocity,
                Width, Height, Gravity, dt,
                ctx.TileGrid, ctx.TileSize, ctx.LevelBottom);
            // Wall detection: check if velocity was zeroed (hit wall)
            if (MathF.Abs(Velocity.X) < 0.1f && MathF.Abs(Dir * effectiveSpeed) > 1f)
                TrySetDir(-Dir, true);
        }
        else
        {
            Position += Velocity * dt;
        }

        // Bounds
        Position.X = MathHelper.Clamp(Position.X, ctx.BoundsLeft, ctx.BoundsRight - Width);

        // Slime trail
        float distFromLast = Vector2.Distance(Position, _lastSlimePos);
        _slimeDistAccum += distFromLast;
        _lastSlimePos = Position;
        float slimeInterval = ctx.IsRaining ? 4f : 8f; // more slime in rain
        if (_slimeDistAccum >= slimeInterval)
        {
            _slimeDistAccum = 0f;
            SlimeTrail.Add((new Vector2(Position.X + Width / 2f, Position.Y + Height), 5f));
        }

        // Fade slime trail
        for (int i = SlimeTrail.Count - 1; i >= 0; i--)
        {
            var (pos, life) = SlimeTrail[i];
            life -= dt;
            if (life <= 0f)
                SlimeTrail.RemoveAt(i);
            else
                SlimeTrail[i] = (pos, life);
        }
    }

    private Vector2 FindNearestFood(List<FoodSource> sources, FoodType type)
    {
        float bestDist = float.MaxValue;
        Vector2 best = Position;
        foreach (var f in sources)
        {
            if (f.Type != type || f.Nutrition <= 0f) continue;
            float d = Vector2.Distance(Position, f.Position);
            if (d < bestDist) { bestDist = d; best = f.Position; }
        }
        return best;
    }

    public override CreatureGoal SelectGoal()
    {
        if (Hp > 0 && Hp < MaxHp) return CreatureGoal.Flee; // was hit, flee
        if (Needs.Hunger > 0.7f) return CreatureGoal.Eat;
        if (Needs.Fatigue > 0.7f) return CreatureGoal.Rest;
        return CreatureGoal.Wander;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        if (BurrowProgress >= 1f) return;

        Color bodyColor = HitFlash > 0 ? Color.White : new Color(107, 142, 35); // olive green
        float burrowOffset = BurrowProgress * Height;

        // Draw slime trail
        foreach (var (pos, life) in SlimeTrail)
        {
            float alpha = (life / 5f) * 0.2f;
            sb.Draw(pixel, new Rectangle((int)pos.X - 1, (int)pos.Y - 1, 3, 2),
                Color.LimeGreen * alpha);
        }

        // Body: 4 segments with peristalsis
        float segW = Width / 4f;
        for (int i = 0; i < 4; i++)
        {
            float segPhase = _animPhase + i * 0.8f;
            float yScale = 1.0f + MathF.Sin(segPhase) * 0.15f;
            float xScale = 1.0f - MathF.Sin(segPhase) * 0.1f;

            int sx = (int)(Position.X + (Dir == 1 ? i * segW : (3 - i) * segW));
            int sy = (int)(Position.Y + Height * (1f - yScale) * 0.5f - burrowOffset);
            int sw = (int)(segW * xScale);
            int sh = (int)(Height * yScale);

            sb.Draw(pixel, new Rectangle(sx, sy, MathF.Max(sw, 1) < 1 ? 1 : sw, sh), bodyColor);
        }

        // Eye stalks
        if (BurrowProgress < 0.5f)
        {
            float stalkLength = (1f - _eyeRetract) * 5f + 1f;
            float wobble1 = MathF.Sin(_animPhase * 2f) * 1.5f;
            float wobble2 = MathF.Sin(_animPhase * 2f + 1f) * 1.5f;

            int headX = Dir == 1 ? (int)(Position.X + Width - 2) : (int)(Position.X + 2);
            int headY = (int)(Position.Y - burrowOffset);

            // Stalk 1
            int tipX1 = headX + (int)(Dir * stalkLength);
            int tipY1 = (int)(headY - stalkLength + wobble1);
            sb.Draw(pixel, new Rectangle(MathF.Min(headX, tipX1) < headX ? tipX1 : headX, MathF.Min(headY, tipY1) < headY ? tipY1 : headY, 1, (int)stalkLength + 1), new Color(80, 100, 30));
            sb.Draw(pixel, new Rectangle(tipX1, tipY1, 2, 2), Color.DarkSlateGray); // eye tip

            // Stalk 2
            int tipX2 = headX + (int)(Dir * stalkLength) + Dir * 3;
            int tipY2 = (int)(headY - stalkLength + wobble2);
            sb.Draw(pixel, new Rectangle(MathF.Min(headX + Dir * 3, tipX2) < headX + Dir * 3 ? tipX2 : headX + Dir * 3, MathF.Min(headY, tipY2) < headY ? tipY2 : headY, 1, (int)stalkLength + 1), new Color(80, 100, 30));
            sb.Draw(pixel, new Rectangle(tipX2, tipY2, 2, 2), Color.DarkSlateGray);
        }
    }

    /// <summary>
    /// Check if a position is on a slime trail. Returns true if within range of any active slime.
    /// </summary>
    public bool IsOnSlimeTrail(Vector2 pos)
    {
        foreach (var (sPos, life) in SlimeTrail)
        {
            if (life > 0f && Vector2.Distance(pos, sPos) < 6f)
                return true;
        }
        return false;
    }
}
