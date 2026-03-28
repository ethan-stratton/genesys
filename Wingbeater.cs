using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

/// <summary>
/// Flying enemy that patrols the air and dive-bombs the player.
/// After a dive-bomb, returns to its starting position.
/// Good for teaching the flip/backflip dodge.
/// </summary>
public class Wingbeater : Creature
{
    public Vector2 SpawnPos;
    public override int ContactDamage => WingbeaterContactDamage;
    private const int WingbeaterContactDamage = 2;
    public bool Passive = false;
    public const int Width = 20, Height = 14;

    public Vector2 KnockbackVel;
    private float _squashHoldTimer;

    private enum State { Hovering, Tracking, DiveBomb, Returning, Stunned }
    private State _state = State.Hovering;
    private float _stateTimer;
    private int _dir = 1;

    // Hover: gentle float at spawn height
    private float _hoverPhase;

    // Tracking: locks on before diving
    private const float TrackTime = 0.6f;

    // Dive-bomb
    private const float DiveSpeed = 450f;
    private Vector2 _diveTarget;
    private const float DiveMaxDist = 300f;

    // Return to start
    private const float ReturnSpeed = 120f;

    // Stun after hitting ground/player
    private const float StunTime = 0.8f;

    private Creature _huntTarget;
    private float _huntCooldown;

    // Detection
    public float DetectRange = 200f;
    public float DiveRange = 180f;

    // Wing animation
    private float _wingTimer;
    private int _wingFrame;

    public Wingbeater(Vector2 pos)
    {
        Position = pos;
        SpawnPos = pos;
        Hp = 3;
        MaxHp = 3;
        SpeciesName = "wingbeater";
        Role = EcologicalRole.Predator;
        Needs = CreatureNeeds.Default;
        DeathParticleColor = new Color(160, 60, 40);
        HitColor = new Color(160, 60, 40);
        _stateTimer = 1f;
        _hoverPhase = pos.X * 0.1f;
        HungerRate = 0.003f;
        FatigueRate = 0.002f;
        Needs.Hunger = 0.2f + (float)Random.Shared.NextDouble() * 0.3f;
        Needs.Fatigue = (float)Random.Shared.NextDouble() * 0.2f;
    }

    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override bool IsCrepuscular => true;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        var playerPos = ctx.PlayerCenter;
        var floorY = ctx.LevelBottom;
        if (!Alive) return;

        MeleeHitCooldown -= dt;

        // --- Needs system ---
        TickNeeds(dt);
        // Weather effects
        if (ctx.IsRaining)
        {
            Needs.Hunger += dt * HungerRate * 0.5f;
        }
        // Time-of-day activity
        float activity = GetActivityLevel(ctx.WorldTime);
        if (activity < 0.2f && CurrentGoal != CreatureGoal.Flee)
            CurrentGoal = CreatureGoal.Rest;
        float weatherDetectMult = ctx.IsStorming ? 0.5f : ctx.IsRaining ? 0.7f : 1f;
        float distToPlayer = Vector2.Distance(Position, playerPos);
        Needs.Safety = Math.Min(Needs.Safety, MathHelper.Clamp(distToPlayer / 100f, 0f, 1f));
        CurrentGoal = SelectGoal();

        // Creature awareness — wingbeater is an active hunter
        var (_, _, wbPrey, wbPreyDist) = ScanCreatures(ctx.NearbyCreatures, 100f, 200f);
        if (CurrentGoal == CreatureGoal.Eat && wbPrey != null && wbPreyDist < 180f && _huntCooldown <= 0)
        {
            _huntTarget = wbPrey;
            Dir = wbPrey.Position.X > Position.X ? 1 : -1;
        }
        else if (_huntTarget != null && (!_huntTarget.Alive || Vector2.Distance(Position, _huntTarget.Position) > 250f))
        {
            _huntTarget = null;
        }

        // Attack prey on contact
        if (_huntTarget != null && _huntTarget.Alive && Rect.Intersects(_huntTarget.Rect))
        {
            _huntTarget.TakeHit(2, Dir * 50f, -40f);
            _huntTarget = null;
            _huntCooldown = 2f;
            Needs.Hunger = MathHelper.Clamp(Needs.Hunger - 0.3f, 0f, 1f);
        }
        if (_huntCooldown > 0) _huntCooldown -= dt;

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
                    Dir = food.Position.X > Position.X ? 1 : -1;
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

        // Goal-influenced detection ranges
        float effectiveDetectRange = DetectRange * weatherDetectMult * MathHelper.Clamp(activity, 0.3f, 1f);
        if (CurrentGoal == CreatureGoal.Eat) effectiveDetectRange *= 1.3f;
        else if (CurrentGoal == CreatureGoal.Rest) effectiveDetectRange *= 0.6f;
        else if (CurrentGoal == CreatureGoal.Flee) effectiveDetectRange *= 0.4f;

        // Knockback
        if (KnockbackVel.LengthSquared() > 1f)
        {
            Position += KnockbackVel * dt;
            KnockbackVel *= 0.88f;
        }

        // Squash/stretch recovery
        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 6f * dt);

        // Wing animation
        _wingTimer += dt;
        if (_wingTimer > 0.12f)
        {
            _wingTimer = 0;
            _wingFrame = (_wingFrame + 1) % 4;
        }

        float dist = Vector2.Distance(playerPos, Position + new Vector2(Width / 2f, Height / 2f));
        float dx = playerPos.X - (Position.X + Width / 2f);

        switch (_state)
        {
            case State.Hovering:
                _hoverPhase += dt * 2f;
                Position.Y = SpawnPos.Y + MathF.Sin(_hoverPhase) * 8f;
                // Gentle horizontal drift
                Position.X = SpawnPos.X + MathF.Sin(_hoverPhase * 0.4f) * 15f;
                _dir = dx > 0 ? 1 : -1;

                if (dist < effectiveDetectRange && !Passive)
                {
                    _state = State.Tracking;
                    _stateTimer = TrackTime;
                }
                break;

            case State.Tracking:
                // Lock on — hover in place, maybe shake
                _stateTimer -= dt;
                Position.X += MathF.Sin(_stateTimer * 30f) * 0.5f; // Vibrate
                _dir = dx > 0 ? 1 : -1;

                if (_stateTimer <= 0)
                {
                    // Dive toward player's current position
                    _diveTarget = playerPos;
                    var diveDir = _diveTarget - Position;
                    if (diveDir.LengthSquared() > 0)
                        diveDir.Normalize();
                    Velocity = diveDir * DiveSpeed;
                    _state = State.DiveBomb;
                    _stateTimer = DiveMaxDist / DiveSpeed + 0.2f; // timeout
                    SetSquash(0.7f, 1.4f);
                }
                break;

            case State.DiveBomb:
                Position += Velocity * dt;
                _dir = Velocity.X > 0 ? 1 : -1;
                _stateTimer -= dt;

                // Hit the floor or timeout
                if (Position.Y + Height >= floorY || _stateTimer <= 0)
                {
                    if (Position.Y + Height >= floorY)
                        Position.Y = floorY - Height;
                    Velocity = Vector2.Zero;
                    _state = State.Stunned;
                    _stateTimer = StunTime;
                    SetSquash(1.5f, 0.6f);
                }
                break;

            case State.Stunned:
                _stateTimer -= dt;
                if (_stateTimer <= 0)
                {
                    _state = State.Returning;
                }
                break;

            case State.Returning:
                var toSpawn = SpawnPos - Position;
                float distToSpawn = toSpawn.Length();
                if (distToSpawn < 4f)
                {
                    Position = SpawnPos;
                    _state = State.Hovering;
                    _hoverPhase = 0;
                }
                else
                {
                    toSpawn.Normalize();
                    Position += toSpawn * ReturnSpeed * dt;
                }
                break;
        }
    }

    public override int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive) return 0;
        if (_state != State.DiveBomb && _state != State.Hovering) return 0;
        if (Rect.Intersects(playerRect))
            return ContactDamage;
        return 0;
    }

    public override bool TakeHit(int damage, float kbX = 0, float kbY = 0)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        MeleeHitCooldown = 0.15f;
        KnockbackVel = new Vector2(kbX, kbY);
        SetSquash(1.3f, 0.7f);
        if (Hp <= 0)
        {
            Alive = false;
            return true;
        }
        // Getting hit during dive stuns it
        if (_state == State.DiveBomb)
        {
            Velocity = Vector2.Zero;
            _state = State.Stunned;
            _stateTimer = StunTime * 1.5f;
        }
        return false;
    }

    public void SetSquash(float sx, float sy)
    {
        VisualScale = new Vector2(sx, sy);
        _squashHoldTimer = 0.05f;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;

        float drawX = Position.X;
        float drawY = Position.Y;

        int w = Width;
        int h = Height;
        int scaledW = (int)(w * VisualScale.X);
        int scaledH = (int)(h * VisualScale.Y);
        int offsetX = (w - scaledW) / 2;
        int offsetY = h - scaledH;

        // Body color — red-brown, flashes white when stunned
        Color bodyColor = _state == State.Stunned
            ? Color.Lerp(new Color(160, 60, 40), Color.White, MathF.Sin(_stateTimer * 20f) * 0.5f + 0.5f)
            : _state == State.Tracking
                ? Color.Lerp(new Color(160, 60, 40), Color.Red, 0.5f)
                : new Color(160, 60, 40);

        // Body
        sb.Draw(pixel, new Rectangle(
            (int)(drawX + offsetX), (int)(drawY + offsetY),
            scaledW, scaledH), bodyColor);

        // Wing animation
        int wingSpan = _wingFrame switch
        {
            0 => 6,
            1 => 3,
            2 => -1, // wings down
            3 => 3,
            _ => 6
        };

        // Faster wing flap during dive
        Color wingColor = new Color(120, 45, 30);
        int wingW = 5;
        int wingY = (int)(drawY + offsetY + 2);

        // Left wing
        sb.Draw(pixel, new Rectangle(
            (int)(drawX + offsetX - wingW), wingY - wingSpan,
            wingW, 4), wingColor);
        // Right wing
        sb.Draw(pixel, new Rectangle(
            (int)(drawX + offsetX + scaledW), wingY - wingSpan,
            wingW, 4), wingColor);

        // Eye (facing direction)
        int eyeX = _dir == 1
            ? (int)(drawX + offsetX + scaledW - 4)
            : (int)(drawX + offsetX + 2);
        sb.Draw(pixel, new Rectangle(eyeX, (int)(drawY + offsetY + 2), 2, 2), Color.Yellow);

        // Beak
        int beakX = _dir == 1
            ? (int)(drawX + offsetX + scaledW)
            : (int)(drawX + offsetX - 3);
        sb.Draw(pixel, new Rectangle(beakX, (int)(drawY + offsetY + 4), 3, 2), Color.Orange);
    }
}
