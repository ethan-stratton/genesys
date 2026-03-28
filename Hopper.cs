using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

/// <summary>
/// Slime-type enemy: hops toward player with rhythm.
/// Pauses between hops — player can exploit the timing.
/// Now uses EnemyPhysics for tile-aware collision.
/// </summary>
public class Hopper : Creature
{
    public const int Width = 20, Height = 16;
    public float AggroRange = 180f;
    public bool Aggroed;
    public Vector2 KnockbackVel;
    public float SquashResistance = 0.1f;
    private float _squashHoldTimer;

    // Hop state machine
    private enum State { Grounded, Winding, Airborne, Landing }
    private State _state = State.Grounded;
    private float _stateTimer;
    private bool _onGround;

    // Tuning
    private const float Gravity = 600f;
    private const float SmallHopForce = -180f;
    private const float BigHopForce = -300f;
    private const float HopSpeedX = 80f;
    private const float RestTime = 0.6f;
    private const float WindUpTime = 0.2f;
    private const float LandingTime = 0.15f;
    private const int HopContactDamage = 1;

    private int _hopCount;
    private int _dir;

    public override int ContactDamage => HopContactDamage;

    public Hopper(Vector2 pos, float groundY)
    {
        Position = pos;
        Velocity = Vector2.Zero;
        Hp = 4; MaxHp = 4;
        SpeciesName = "hopper";
        Role = EcologicalRole.Herbivore;
        Needs = CreatureNeeds.Default;
        DeathParticleColor = new Color(100, 80, 60);
        HitColor = new Color(100, 80, 60);
        _state = State.Grounded;
        _stateTimer = RestTime * 0.5f;
        // Need rates & randomize
        HungerRate = 0.0025f;
        FatigueRate = 0.001f;
        Needs.Hunger = 0.2f + (float)Random.Shared.NextDouble() * 0.3f;
        Needs.Fatigue = (float)Random.Shared.NextDouble() * 0.2f;
    }

    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        var playerCenter = ctx.PlayerCenter;
        var tileGrid = ctx.TileGrid;
        var tileSize = ctx.TileSize;
        var levelBottom = ctx.LevelBottom;
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;

        // --- Needs system ---
        TickNeeds(dt);

        if (KnockbackVel.LengthSquared() > 1f)
        {
            Position += KnockbackVel * dt;
            KnockbackVel *= 0.85f;
        }

        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);

        float dist = Vector2.Distance(playerCenter, Position + new Vector2(Width / 2f, Height / 2f));
        // Safety from player proximity
        Needs.Safety = MathHelper.Clamp(dist / 200f, 0f, 1f);
        CurrentGoal = SelectGoal();

        // Creature awareness
        var (hopThreat, hopThreatDist, _, _) = ScanCreatures(ctx.NearbyCreatures, 200f, 0f);
        if (hopThreat != null && hopThreatDist < 120f)
        {
            _dir = hopThreat.Position.X > Position.X ? -1 : 1;
            Needs.Safety = Math.Min(Needs.Safety, 0.2f);
        }
        CurrentGoal = SelectGoal();

        // Food seeking
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
                    Velocity = Vector2.Zero;
                }
                else
                {
                    _dir = food.Position.X > Position.X ? 1 : -1;
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
                float gained = EatingTarget.Eat(dt);
                Needs.Hunger = MathHelper.Clamp(Needs.Hunger - gained, 0f, 1f);
                Velocity = Vector2.Zero;
                return;
            }
        }

        Aggroed = dist < AggroRange;

        float dx = playerCenter.X - (Position.X + Width / 2f);
        if (MathF.Abs(dx) > 4f)
            _dir = dx > 0 ? 1 : -1;

        switch (_state)
        {
            case State.Grounded:
                _stateTimer -= dt;
                Velocity.X = 0;
                if (_stateTimer <= 0 && Aggroed)
                {
                    _state = State.Winding;
                    _stateTimer = WindUpTime;
                }
                break;

            case State.Winding:
                _stateTimer -= dt;
                Velocity.X = 0;
                if (_stateTimer <= 0)
                {
                    _hopCount++;
                    bool bigHop = _hopCount >= 3;
                    if (bigHop) _hopCount = 0;

                    // Goal-based hop modifiers
                    float hopForceMult = CurrentGoal == CreatureGoal.Rest ? 0.75f
                        : CurrentGoal == CreatureGoal.Flee ? 1.3f : 1f;
                    float hopSpeedMult = CurrentGoal == CreatureGoal.Eat ? 1.15f
                        : CurrentGoal == CreatureGoal.Flee ? 1.2f : 1f;

                    Velocity.Y = (bigHop ? BigHopForce : SmallHopForce) * hopForceMult;
                    Velocity.X = _dir * HopSpeedX * (bigHop ? 1.6f : 1f) * hopSpeedMult;
                    _state = State.Airborne;
                    _onGround = false;
                }
                break;

            case State.Airborne:
                _onGround = EnemyPhysics.ApplyGravityAndCollision(
                    ref Position, ref Velocity,
                    Width, Height, Gravity, dt,
                    tileGrid, tileSize,
                    levelBottom);

                if (_onGround)
                {
                    _state = State.Landing;
                    _stateTimer = LandingTime;
                    Velocity = Vector2.Zero;
                }
                return;

            case State.Landing:
                _stateTimer -= dt;
                Velocity.X = 0;
                if (_stateTimer <= 0)
                {
                    _state = State.Grounded;
                    // Rest longer when tired, shorter when hungry
                    float restMult = CurrentGoal == CreatureGoal.Rest ? 1.6f
                        : CurrentGoal == CreatureGoal.Eat ? 0.6f : 1f;
                    _stateTimer = RestTime * restMult;
                }
                break;
        }
    }


    public override bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        HitFlash = 0.15f;
        MeleeHitCooldown = 0.2f;
        KnockbackVel = new Vector2(knockbackX, knockbackY);
        float squashAmount = 1f - SquashResistance;
        VisualScale = new Vector2(1f + 0.3f * squashAmount, 1f - 0.25f * squashAmount);
        _squashHoldTimer = 0.05f;
        if (Hp <= 0) { Alive = false; return true; }
        return false;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;

        int drawW = Width, drawH = Height;
        int drawX = (int)Position.X, drawY = (int)Position.Y;

        switch (_state)
        {
            case State.Winding:
                drawH = (int)(Height * 0.7f);
                drawW = (int)(Width * 1.2f);
                drawX -= (drawW - Width) / 2;
                drawY += Height - drawH;
                break;
            case State.Airborne:
                if (Velocity.Y < -50f)
                {
                    drawH = (int)(Height * 1.3f);
                    drawW = (int)(Width * 0.85f);
                    drawX += (Width - drawW) / 2;
                }
                else if (Velocity.Y > 50f)
                {
                    drawH = (int)(Height * 0.8f);
                    drawW = (int)(Width * 1.15f);
                    drawX -= (drawW - Width) / 2;
                    drawY += Height - drawH;
                }
                break;
            case State.Landing:
                float t = _stateTimer / LandingTime;
                drawH = (int)(Height * MathHelper.Lerp(1f, 0.65f, t));
                drawW = (int)(Width * MathHelper.Lerp(1f, 1.3f, t));
                drawX -= (drawW - Width) / 2;
                drawY += Height - drawH;
                break;
        }

        Color bodyColor = HitFlash > 0 ? Color.Red : (Aggroed ? new Color(80, 140, 60) : new Color(60, 120, 50));
        // Apply hit squash on top of state animation
        int finalW = (int)(drawW * VisualScale.X);
        int finalH = (int)(drawH * VisualScale.Y);
        int finalX = drawX + drawW / 2 - finalW / 2;
        int finalY = drawY + drawH - finalH;
        sb.Draw(pixel, new Rectangle(finalX, finalY, finalW, finalH), bodyColor);
        sb.Draw(pixel, new Rectangle(finalX + 2, finalY, finalW - 4, 3), Color.LightGreen * 0.4f);

        int eyeOffsetX = _dir > 0 ? finalW - 6 : 2;
        sb.Draw(pixel, new Rectangle(finalX + eyeOffsetX, finalY + 3, 3, 3), Color.White);
        sb.Draw(pixel, new Rectangle(finalX + eyeOffsetX + 1, finalY + 4, 1, 1), Color.Black);
    }
}
