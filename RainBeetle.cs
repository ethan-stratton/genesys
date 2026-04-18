using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class RainBeetle : Creature
{
    public const int Width = 20, Height = 12;

    private readonly Random _rng;
    private float _animTime;
    private float _speed = 20f;
    private bool _onGround;

    // Underground lifecycle
    public bool IsUnderground = true;
    public float EmergeProgress; // 0 = underground, 1 = fully out
    private float EmergeVisual => Easing.SmoothStop3(EmergeProgress);
    private bool _isEmerging;
    private bool _isBurrowing;
    private float _surfaceTimer;
    private float _surfaceTimerMax;

    // Defensive
    private bool _isHunkered;
    private float _hunkerTimer;

    // Flipped
    private bool _isFlipped;
    private float _flipTimer;

    // Mating
    private bool _isMating;
    private float _mateTimer;
    private bool _hasMated;

    // Legs
    private struct BeetleLeg
    {
        public Vector2 FootTarget;
        public Vector2 FootActual;
        public float StepTimer;
        public bool Stepping;
    }
    private BeetleLeg[] _legs = new BeetleLeg[6];

    // Wander
    private float _wanderTimer;

    // Colors
    private static readonly Color ElytraBase = new Color(27, 63, 47);      // #1B3F2F
    private static readonly Color ElytraHighlight = new Color(46, 139, 87); // #2E8B57
    private static readonly Color HeadColor = new Color(60, 40, 20);
    private static readonly Color LegColor = new Color(50, 35, 15);
    private static readonly Color UnderbellyColor = new Color(140, 110, 70);
    private static readonly Color AntennaColor = new Color(70, 50, 25);
    private static readonly Color MoundColor = new Color(100, 80, 50);

    public override int ContactDamage => 0;
    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public override float GetDamageMultiplier(DamageType type) => type switch
    {
        DamageType.Slash => 0.6f,
        DamageType.Pierce => 0.7f,
        DamageType.Blunt => 1.3f,
        DamageType.Fire => 1.5f,
        _ => 1f,
    };

    public RainBeetle(Vector2 pos, Random rng)
    {
        _rng = rng;
        Position = pos;
        Hp = 15; MaxHp = 15;
        SpeciesName = "rain-beetle";
        Role = EcologicalRole.Herbivore;
        Needs = CreatureNeeds.Default;
        Needs.Hunger = 0.2f + (float)rng.NextDouble() * 0.3f;
        DeathParticleColor = ElytraBase;
        HitColor = ElytraBase;
        SpawnOrigin = pos;
        HungerRate = 0.001f;
        FatigueRate = 0.0005f;
        Dir = rng.NextDouble() < 0.5 ? 1 : -1;
        _wanderTimer = 1f + (float)rng.NextDouble() * 3f;
        _surfaceTimerMax = 30f + (float)rng.NextDouble() * 30f;

        // Init legs
        for (int i = 0; i < 6; i++)
        {
            _legs[i] = new BeetleLeg
            {
                FootTarget = pos + GetLegOffset(i),
                FootActual = pos + GetLegOffset(i),
            };
        }
    }

    private Vector2 GetLegOffset(int legIndex)
    {
        int side = legIndex < 3 ? -1 : 1; // 0-2 left, 3-5 right
        int row = legIndex % 3; // 0=front, 1=mid, 2=back
        float x = (row - 1) * 6f; // -6, 0, 6
        float y = side * (Height / 2f + 2f);
        return new Vector2(x, y);
    }

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        if (!Alive) return;

        // Skip most processing when underground and not emerging
        if (IsUnderground && !_isEmerging)
        {
            // Check if should emerge
            if (ctx.IsRaining && ctx.Temperature >= 0.25f)
            {
                _isEmerging = true;
                IsUnderground = false;
                EmergeProgress = 0f;
            }
            return;
        }

        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;
        TickDirCooldown(dt);
        _animTime += dt;

        // Emergence animation
        if (_isEmerging)
        {
            EmergeProgress = MathHelper.Clamp(EmergeProgress + dt * 0.33f, 0f, 1f); // 3 seconds
            if (EmergeProgress >= 1f)
            {
                _isEmerging = false;
                _surfaceTimer = _surfaceTimerMax;
            }
            return;
        }

        // Burrowing animation
        if (_isBurrowing)
        {
            EmergeProgress = MathHelper.Clamp(EmergeProgress - dt * 0.5f, 0f, 1f); // 2 seconds
            if (EmergeProgress <= 0f)
            {
                _isBurrowing = false;
                IsUnderground = true;
            }
            return;
        }

        TickNeeds(dt);

        // Weather check: should we burrow back?
        if (!ctx.IsRaining)
        {
            _surfaceTimer -= dt;
            if (_surfaceTimer <= 0 && !_isBurrowing)
            {
                _isBurrowing = true;
                return;
            }
        }
        else
        {
            _surfaceTimer = _surfaceTimerMax; // reset timer while raining
        }

        // Storm: hunker but don't burrow
        if (ctx.IsStorming && !_isHunkered)
        {
            _isHunkered = true;
            _hunkerTimer = 999f; // stays hunkered during storm
        }
        if (_isHunkered && !ctx.IsStorming && _hunkerTimer > 10f)
        {
            _hunkerTimer = 3f; // exit hunker shortly after storm ends
        }

        // Temperature too cold: burrow
        if (ctx.Temperature < 0.25f && !_isBurrowing)
        {
            _isBurrowing = true;
            return;
        }

        // Flipped state
        if (_isFlipped)
        {
            _flipTimer -= dt;
            if (_flipTimer <= 0) _isFlipped = false;
            return; // can't do anything while flipped
        }

        // Hunker state
        if (_isHunkered)
        {
            _hunkerTimer -= dt;
            if (_hunkerTimer <= 0) _isHunkered = false;
            return; // no movement while hunkered
        }

        // Mating
        if (_isMating)
        {
            _mateTimer -= dt;
            if (_mateTimer <= 0)
            {
                _isMating = false;
                _hasMated = true;
                TrySetDir(-Dir, true); // walk away
            }
            return;
        }

        // Check for nearby mate
        if (!_hasMated && ctx.NearbyCreatures != null)
        {
            foreach (var c in ctx.NearbyCreatures)
            {
                if (c == this || !c.Alive || c is not RainBeetle other) continue;
                if (other.IsUnderground || other._isMating || other._hasMated) continue;
                float d = Vector2.Distance(Position, other.Position);
                if (d < 80f && d > 5f)
                {
                    // Walk toward mate
                    if (other.Position.X > Position.X) TrySetDir(1);
                    else TrySetDir(-1);
                }
                if (d <= 5f)
                {
                    _isMating = true;
                    _mateTimer = 3f;
                    other._isMating = true;
                    other._mateTimer = 3f;
                    break;
                }
            }
        }

        // Movement
        float effectiveSpeed = _speed;
        float activity = GetActivityLevel(ctx.WorldTime);
        effectiveSpeed *= (0.3f + 0.7f * activity);

        // Gravity
        Velocity.Y += 600f * dt;
        Position.X += Dir * effectiveSpeed * dt;
        Position.Y += Velocity.Y * dt;

        // Ground collision
        if (ctx.TileGrid != null)
        {
            int col = (int)(Position.X / ctx.TileSize);
            int row = (int)((Position.Y + Height) / ctx.TileSize);
            if (TileProperties.IsSolid(ctx.TileGrid.GetTileAt(col, row)))
            {
                Position.Y = row * ctx.TileSize - Height;
                Velocity.Y = 0;
                _onGround = true;
            }
            else _onGround = false;

            // Wall check
            if (HasWallAhead(ctx.TileGrid, ctx.TileSize, Position, Dir, 10f))
                TrySetDir(-Dir, true);
            // Ledge check
            if (_onGround && !HasFloorAhead(ctx.TileGrid, ctx.TileSize, Position + new Vector2(0, Height - 2), Dir, 12f, 48f))
                TrySetDir(-Dir, true);
        }

        // Wander direction changes
        _wanderTimer -= dt;
        if (_wanderTimer <= 0)
        {
            _wanderTimer = 2f + (float)_rng.NextDouble() * 4f;
            if (_rng.NextDouble() < 0.3) TrySetDir(-Dir, true);
        }

        // Bounds
        if (Position.X < ctx.BoundsLeft + 10) { Position.X = ctx.BoundsLeft + 10; TrySetDir(1, true); }
        if (Position.X > ctx.BoundsRight - 10) { Position.X = ctx.BoundsRight - 10; TrySetDir(-1, true); }

        // Update legs
        UpdateLegs(dt);

        // Idle body bob
        if (_onGround && !_isHunkered && !_isFlipped && !_isMating && EmergeProgress >= 1f)
        {
            float bob = MathF.Sin(_animTime * 6f) * 0.5f;
            Position.Y += bob;
        }

        // Food seeking
        if (CurrentGoal == CreatureGoal.Eat)
        {
            var food = FindFood(ctx.FoodSources, 150f);
            if (food != null)
            {
                if (food.Position.X > Position.X) TrySetDir(1);
                else TrySetDir(-1);
                float dist = Vector2.Distance(Position, food.Position);
                if (dist < 15f)
                {
                    IsEating = true;
                    EatTimer += dt;
                    if (EatTimer > 2f)
                    {
                        Needs.Hunger = MathHelper.Clamp(Needs.Hunger - food.Nutrition, 0f, 1f);
                        food.Eat(dt, 0.2f);
                        EatTimer = 0;
                        IsEating = false;
                    }
                }
            }
        }
    }

    private void UpdateLegs(float dt)
    {
        for (int i = 0; i < 6; i++)
        {
            ref var leg = ref _legs[i];
            Vector2 targetOffset = GetLegOffset(i);
            leg.FootTarget = Position + new Vector2(Width / 2f, Height) + new Vector2(targetOffset.X, 0);
            leg.FootTarget.Y = Position.Y + Height; // feet on ground

            float dist = Vector2.Distance(leg.FootActual, leg.FootTarget);
            if (dist > 5f && !leg.Stepping)
            {
                // Tripod gait: 0,2,4 step together; 1,3,5 step together
                bool canStep = (i % 2 == 0) == (((int)(_animTime * 4f)) % 2 == 0);
                if (canStep)
                {
                    leg.Stepping = true;
                    leg.StepTimer = 0;
                }
            }

            if (leg.Stepping)
            {
                leg.StepTimer += dt / 0.1f;
                if (leg.StepTimer >= 1f)
                {
                    leg.StepTimer = 1f;
                    leg.Stepping = false;
                }
                float t = Easing.SmoothStep(leg.StepTimer);
                leg.FootActual = Vector2.Lerp(leg.FootActual, leg.FootTarget, t);
                leg.FootActual.Y -= MathF.Sin(t * MathF.PI) * 3f; // step arc
            }
            else
            {
                leg.FootActual = Vector2.Lerp(leg.FootActual, leg.FootTarget, dt * 8f);
            }
        }
    }

    public override bool TakeDamage(int amount)
    {
        bool killed = base.TakeDamage(amount);
        if (!killed && Alive)
        {
            // Hunker defense
            if (!_isHunkered)
            {
                _isHunkered = true;
                _hunkerTimer = 3f;
            }
        }
        return killed;
    }

    public override bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        // Check for flip on blunt damage (30% chance) - approximate by checking knockback magnitude
        if (!_isFlipped && _rng.NextDouble() < 0.3f && MathF.Abs(knockbackX) > 50f)
        {
            _isFlipped = true;
            _flipTimer = 5f;
        }
        return base.TakeHit(damage, knockbackX, knockbackY);
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        if (IsUnderground && !_isEmerging && !_isBurrowing) return;

        Color flashMod = HitFlash > 0 ? Color.White : Color.Transparent;
        int bx = (int)Position.X;
        int by = (int)Position.Y;

        // Dirt mound during emerge/burrow
        if (EmergeProgress < 1f && (_isEmerging || _isBurrowing))
        {
            float moundSize = _isEmerging ? MathHelper.Clamp(EmergeVisual * 3f, 0f, 1f) : EmergeVisual;
            int mw = (int)(24 * moundSize);
            int mh = (int)(8 * moundSize);
            sb.Draw(pixel, new Rectangle(bx + Width / 2 - mw / 2, by + Height - mh, mw, mh / 3), MoundColor);
            sb.Draw(pixel, new Rectangle(bx + Width / 2 - mw / 2 + 2, by + Height - mh + mh / 3, mw - 4, mh / 3), MoundColor * 0.9f);
            sb.Draw(pixel, new Rectangle(bx + Width / 2 - mw / 2 + 4, by + Height - mh + 2 * mh / 3, mw - 8, mh / 3), MoundColor * 0.8f);
        }

        // Don't draw beetle body if barely emerged
        if (EmergeProgress < 0.3f) return;

        float visibleFraction = MathHelper.Clamp((EmergeProgress - 0.3f) / 0.7f, 0f, 1f);
        int visibleH = (int)(Height * visibleFraction);
        int drawY = by + Height - visibleH;

        if (_isFlipped)
        {
            // Upside down — show underbelly
            sb.Draw(pixel, new Rectangle(bx, drawY, Width, visibleH), UnderbellyColor);
            // Wiggling legs
            for (int i = 0; i < 6; i++)
            {
                float wiggle = MathF.Sin(_animTime * 15f + i * 1.5f) * 4f;
                int lx = bx + 3 + (i % 3) * 6;
                int ly = drawY - 2;
                sb.Draw(pixel, new Rectangle(lx, ly + (int)wiggle, 1, 3), LegColor);
            }
            return;
        }

        if (_isHunkered)
        {
            // Compressed — smaller, darker
            int hw = Width - 2;
            int hh = visibleH - 2;
            sb.Draw(pixel, new Rectangle(bx + 1, drawY + 2, hw, hh), ElytraBase * 0.8f);
            return;
        }

        // Normal draw
        Color bodyColor = HitFlash > 0 ? Color.Lerp(ElytraBase, Color.White, 0.5f) : ElytraBase;
        Color headCol = HitFlash > 0 ? Color.Lerp(HeadColor, Color.White, 0.5f) : HeadColor;

        // Head (front)
        int headW = 6, headH = Math.Min(8, visibleH);
        int headX = Dir > 0 ? bx : bx + Width - headW;
        sb.Draw(pixel, new Rectangle(headX, drawY + (visibleH - headH) / 2, headW, headH), headCol);

        // Antennae
        for (int side = -1; side <= 1; side += 2)
        {
            float antY = drawY + visibleH / 2f + side * 3f + MathF.Sin(_animTime * 3.5f + side * 1.2f) * 2f + MathF.Sin(_animTime * 7f + side) * 0.5f;
            int antX = Dir > 0 ? headX + headW : headX - 4;
            sb.Draw(pixel, new Rectangle(antX, (int)antY, 4, 1), AntennaColor);
        }

        // Elytra (wing covers) — two halves
        int elytraX = Dir > 0 ? bx + headW : bx;
        int elytraW = Width - headW;
        int elytraH = Math.Min(visibleH, Height);
        int halfH = elytraH / 2;
        // Top half
        sb.Draw(pixel, new Rectangle(elytraX, drawY, elytraW, halfH), bodyColor);
        sb.Draw(pixel, new Rectangle(elytraX, drawY, elytraW, 1), ElytraHighlight); // highlight strip
        float shimmer = (MathF.Sin(_animTime * 2f) + 1f) * 0.5f;
        Color shimmerColor = Color.Lerp(ElytraHighlight, new Color(60, 180, 120), shimmer * 0.3f);
        sb.Draw(pixel, new Rectangle(elytraX + 2, drawY + 1, elytraW - 4, 1), shimmerColor * 0.5f);
        // Bottom half
        sb.Draw(pixel, new Rectangle(elytraX, drawY + halfH, elytraW, elytraH - halfH), bodyColor);
        // Center line (split between elytra)
        sb.Draw(pixel, new Rectangle(elytraX + elytraW / 2, drawY, 1, elytraH), HeadColor * 0.5f);

        // Legs
        if (EmergeProgress >= 1f)
        {
            for (int i = 0; i < 6; i++)
            {
                ref var leg = ref _legs[i];
                int lx = (int)leg.FootActual.X;
                int ly = (int)leg.FootActual.Y;
                // Draw from body to foot
                int attachX = bx + Width / 2 + (i % 3 - 1) * 6;
                int attachY = (i < 3) ? by : by + Height;
                sb.Draw(pixel, new Rectangle(Math.Min(attachX, lx), Math.Min(attachY, ly),
                    Math.Abs(lx - attachX) + 1, Math.Abs(ly - attachY) + 1), LegColor);
            }
        }
    }
}
