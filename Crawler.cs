using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public enum CrawlerVariant { Basic, Leaper }

public class Crawler
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool Alive = true;
    public int Hp = 3;
    public const int Width = 16, Height = 10;
    public float PatrolLeft, PatrolRight;
    public float SurfaceLeft, SurfaceRight;
    public int Dir = 1;
    public bool Aggroed;
    public float AggroRange = 200f;
    public float Speed = 60f;
    public float ChaseSpeed = 100f;
    public float SwarmSpeed = 130f;
    public float DamageCooldown;
    public float MeleeHitCooldown;
    public float HitFlash;
    private bool _onGround;
    private bool _wasOnGround;

    public Vector2 KnockbackVel;
    public Vector2 VisualScale = Vector2.One;
    public float SquashResistance = 0f;
    private float _squashHoldTimer;

    // Variant
    public CrawlerVariant Variant = CrawlerVariant.Basic;

    // Jump behavior
    private float _jumpCooldown;
    private const float JumpCooldownTime = 1.5f;
    private const float NormalJumpForce = -200f;
    private const float SwarmJumpForce = -300f;
    private const float LeaperJumpForce = -280f;
    private const float NormalJumpHSpeed = 80f;
    private const float SwarmJumpHSpeed = 150f;
    private const float LeaperJumpHSpeed = 120f;

    // Insect behavior state machine
    private enum BugState { Idle, Walking, Paused, EdgeSniffing, Startled, Chasing, Swarming }
    private BugState _bugState = BugState.Walking;
    private float _bugStateTimer;
    private float _pauseDuration;
    private float _walkSpeedMult = 1f; // varies to look organic
    private float _antennaeTimer; // visual wiggle
    private bool _edgeSniffDecided; // has the crawler decided what to do at the edge?
    private float _startleTimer;
    private readonly Random _rng;

    // Dummy mode: high HP, no aggro, respawns at original position
    public bool IsDummy;
    public bool Frozen;
    public bool SwarmActive;
    public float? SwarmTargetX;
    public bool AlwaysCrit;
    public float DummyScale = 1f;
    public float DummyScaleX = 0f;
    public float DummyScaleY = 0f;

    // Latch-on behavior
    public bool IsLatched;
    public Vector2 LatchOffset;
    private float _latchDamageTick;
    private const float LatchDamageInterval = 0.3f;
    public const int LatchDamage = 1;
    private float _latchCooldown;
    private const float LatchCooldownTime = 1.5f;
    public int EffectiveWidth => IsDummy ? (int)(Width * (DummyScaleX > 0 ? DummyScaleX : DummyScale)) : Width;
    public int EffectiveHeight => IsDummy ? (int)(Height * (DummyScaleY > 0 ? DummyScaleY : DummyScale)) : Height;
    private Vector2 _spawnPos;
    public void SetSpawnPos(Vector2 pos) => _spawnPos = pos;
    private float _respawnTimer;
    private const float RespawnDelay = 2f;

    private const float Gravity = 600f;

    public Crawler(Vector2 pos, float patrolLeft, float patrolRight, float surfaceLeft, float surfaceRight, Random rng = null)
    {
        Position = pos;
        _spawnPos = pos;
        PatrolLeft = patrolLeft;
        PatrolRight = patrolRight;
        SurfaceLeft = surfaceLeft;
        SurfaceRight = surfaceRight;
        _rng = rng ?? new Random();
        // Start in a random state so groups don't move in sync
        _bugStateTimer = (float)_rng.NextDouble() * 2f;
        _walkSpeedMult = 0.6f + (float)_rng.NextDouble() * 0.8f; // 0.6–1.4x
        _antennaeTimer = (float)_rng.NextDouble() * 10f;
    }

    public Rectangle Rect => new((int)Position.X, (int)Position.Y, EffectiveWidth, EffectiveHeight);

    public void Update(float dt, Vector2 playerCenter,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors, float floorY)
    {
        if (!Alive)
        {
            if (IsDummy)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0)
                {
                    Alive = true;
                    Hp = 9999;
                    Position = _spawnPos;
                    Velocity = Vector2.Zero;
                    KnockbackVel = Vector2.Zero;
                    VisualScale = Vector2.One;
                    HitFlash = 0;
                }
            }
            return;
        }
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
        if (_latchCooldown > 0) _latchCooldown -= dt;
        _antennaeTimer += dt;

        // Latched: skip all movement
        if (IsLatched)
        {
            Velocity = Vector2.Zero;
            KnockbackVel = Vector2.Zero;
            if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
            else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);
            return;
        }

        if (KnockbackVel.LengthSquared() > 1f)
        {
            Position += KnockbackVel * dt;
            KnockbackVel *= 0.85f;
        }

        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);

        if (IsDummy || Frozen)
        {
            Velocity.X = 0;
        }
        else
        {
            if (_jumpCooldown > 0) _jumpCooldown -= dt;

            float dist = Vector2.Distance(playerCenter, Position + new Vector2(Width / 2f, Height / 2f));
            float dx = playerCenter.X - (Position.X + Width / 2f);
            float dy = playerCenter.Y - (Position.Y + Height / 2f);
            bool wasAggroed = Aggroed;
            Aggroed = dist < AggroRange;

            // Startle: transition from calm to aggroed
            if (Aggroed && !wasAggroed && _bugState != BugState.Startled)
            {
                _bugState = BugState.Startled;
                _startleTimer = 0.15f + (float)_rng.NextDouble() * 0.1f; // freeze briefly
                Velocity.X = 0;
                VisualScale = new Vector2(0.85f, 1.15f); // slight alert squash
                _squashHoldTimer = 0.08f;
            }

            if (SwarmActive && SwarmTargetX.HasValue)
            {
                _bugState = BugState.Swarming;
                float sdx = SwarmTargetX.Value - (Position.X + Width / 2f);
                Dir = sdx > 0 ? 1 : -1;
                Velocity.X = Dir * SwarmSpeed;
                if (_onGround && _jumpCooldown <= 0 && MathF.Abs(sdx) > 30f)
                {
                    Velocity.Y = SwarmJumpForce;
                    Velocity.X = Dir * SwarmJumpHSpeed;
                    _jumpCooldown = 0.6f;
                    VisualScale = new Vector2(0.7f, 1.3f);
                    _squashHoldTimer = 0.04f;
                }
            }
            else if (_bugState == BugState.Startled)
            {
                _startleTimer -= dt;
                Velocity.X = 0;
                if (_startleTimer <= 0)
                    _bugState = Aggroed ? BugState.Chasing : BugState.Walking;
            }
            else if (Aggroed)
            {
                _bugState = BugState.Chasing;
                Dir = dx > 0 ? 1 : -1;
                Velocity.X = Dir * ChaseSpeed;

                // Chase jump
                if (_onGround && _jumpCooldown <= 0 && MathF.Abs(dx) > 50f)
                {
                    float jumpF = Variant == CrawlerVariant.Leaper ? LeaperJumpForce : NormalJumpForce;
                    float jumpH = Variant == CrawlerVariant.Leaper ? LeaperJumpHSpeed : NormalJumpHSpeed;
                    Velocity.Y = jumpF;
                    Velocity.X = Dir * jumpH;
                    _jumpCooldown = Variant == CrawlerVariant.Leaper ? 0.8f : JumpCooldownTime;
                    VisualScale = new Vector2(0.8f, 1.2f);
                    _squashHoldTimer = 0.04f;
                }

                // Leaper surprise: drop off platform to chase player below
                if (Variant == CrawlerVariant.Leaper && _onGround && dy > 40f && MathF.Abs(dx) < 60f)
                {
                    // Player is significantly below — step off the edge
                    bool atEdge = Position.X <= SurfaceLeft + 2 || Position.X + Width >= SurfaceRight - 2;
                    if (atEdge)
                    {
                        Velocity.Y = -50f; // tiny hop off
                        Velocity.X = Dir * 40f;
                    }
                }
            }
            else
            {
                // ---- Insect patrol behavior ----
                _bugStateTimer -= dt;

                switch (_bugState)
                {
                    case BugState.Walking:
                        Velocity.X = Dir * Speed * _walkSpeedMult;

                        // Edge detection
                        bool atLeft = Position.X <= SurfaceLeft + 1;
                        bool atRight = Position.X + Width >= SurfaceRight - 1;
                        if (atLeft || atRight)
                        {
                            _bugState = BugState.EdgeSniffing;
                            _bugStateTimer = 0.3f + (float)_rng.NextDouble() * 0.4f;
                            _edgeSniffDecided = false;
                            Velocity.X = 0;
                            break;
                        }

                        // Random pause (insects stop and go)
                        if (_bugStateTimer <= 0)
                        {
                            float roll = (float)_rng.NextDouble();
                            if (roll < 0.3f)
                            {
                                // Pause
                                _bugState = BugState.Paused;
                                _pauseDuration = 0.4f + (float)_rng.NextDouble() * 1.5f;
                                _bugStateTimer = _pauseDuration;
                                Velocity.X = 0;
                            }
                            else if (roll < 0.5f)
                            {
                                // Change direction
                                Dir = -Dir;
                                _walkSpeedMult = 0.6f + (float)_rng.NextDouble() * 0.8f;
                                _bugStateTimer = 1f + (float)_rng.NextDouble() * 3f;
                            }
                            else if (roll < 0.65f)
                            {
                                // Brief speed burst (like a real bug scurrying)
                                _walkSpeedMult = 1.5f + (float)_rng.NextDouble() * 0.5f;
                                _bugStateTimer = 0.3f + (float)_rng.NextDouble() * 0.5f;
                            }
                            else
                            {
                                // Keep walking, new speed
                                _walkSpeedMult = 0.6f + (float)_rng.NextDouble() * 0.8f;
                                _bugStateTimer = 1.5f + (float)_rng.NextDouble() * 3f;
                            }
                        }
                        break;

                    case BugState.Paused:
                        Velocity.X = 0;
                        if (_bugStateTimer <= 0)
                        {
                            _bugState = BugState.Walking;
                            // Sometimes reverse after pausing
                            if (_rng.NextDouble() < 0.4)
                                Dir = -Dir;
                            _walkSpeedMult = 0.5f + (float)_rng.NextDouble() * 0.6f; // start slow after pause
                            _bugStateTimer = 1f + (float)_rng.NextDouble() * 2f;
                        }
                        break;

                    case BugState.EdgeSniffing:
                        Velocity.X = 0;
                        if (_bugStateTimer <= 0 && !_edgeSniffDecided)
                        {
                            _edgeSniffDecided = true;
                            // Most bugs turn around; leapers sometimes drop
                            float dropChance = Variant == CrawlerVariant.Leaper ? 0.0f : 0.0f; // only drop during chase
                            if (_rng.NextDouble() < dropChance)
                            {
                                // Future: drop off (not used for patrol currently)
                            }
                            else
                            {
                                Dir = -Dir;
                                _bugState = BugState.Walking;
                                _walkSpeedMult = 0.4f + (float)_rng.NextDouble() * 0.4f; // slow turn
                                _bugStateTimer = 0.5f + (float)_rng.NextDouble() * 1.5f;
                            }
                        }
                        break;

                    case BugState.Idle:
                        Velocity.X = 0;
                        if (_bugStateTimer <= 0)
                        {
                            _bugState = BugState.Walking;
                            _bugStateTimer = 1f + (float)_rng.NextDouble() * 2f;
                        }
                        break;

                    default:
                        // Fallback to walking
                        _bugState = BugState.Walking;
                        _bugStateTimer = 1f;
                        break;
                }
            }
        } // end non-dummy movement

        // Apply gravity and tile collision
        _onGround = EnemyPhysics.ApplyGravityAndCollision(
            ref Position, ref Velocity,
            EffectiveWidth, EffectiveHeight, Gravity, dt,
            tileGrid, tileSize,
            platforms, solidFloors, floorY);

        // Landing squash
        if (_onGround && !_wasOnGround)
        {
            VisualScale = new Vector2(1.3f, 0.7f);
            _squashHoldTimer = 0.05f;
        }
        _wasOnGround = _onGround;

        // Clamp to surface edges (skip during swarm or airborne leaper chase)
        if (!SwarmActive && _onGround)
        {
            bool clamp = true;
            // Leapers in chase mode ignore surface edges (they'll drop)
            if (Variant == CrawlerVariant.Leaper && Aggroed) clamp = false;

            if (clamp)
            {
                if (Position.X < SurfaceLeft)
                {
                    Position.X = SurfaceLeft;
                    if (!Aggroed) Dir = 1;
                }
                if (Position.X + EffectiveWidth > SurfaceRight)
                {
                    Position.X = SurfaceRight - Width;
                    if (!Aggroed) Dir = -1;
                }
            }
        }
    }

    /// <summary>
    /// Refresh surface edge detection using tile-aware method.
    /// </summary>
    public void UpdateSurfaceEdges(TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors,
        float boundsLeft, float boundsRight)
    {
        float footY = Position.Y + EffectiveHeight;
        var edges = EnemyPhysics.FindSurfaceEdges(
            Position.X, footY, EffectiveWidth,
            tileGrid, tileSize,
            platforms, solidFloors,
            boundsLeft, boundsRight);
        SurfaceLeft = edges.Left;
        SurfaceRight = edges.Right;
        PatrolLeft = MathF.Max(PatrolLeft, SurfaceLeft);
        PatrolRight = MathF.Min(PatrolRight, SurfaceRight);
    }

    public int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive || DamageCooldown > 0) return 0;
        if (Rect.Intersects(playerRect))
        {
            DamageCooldown = 1.0f;
            return 5;
        }
        return 0;
    }

    public bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        HitFlash = 0.15f;
        MeleeHitCooldown = 0.2f;
        if (IsLatched) Detach(knockbackX, knockbackY);
        else KnockbackVel = new Vector2(knockbackX, knockbackY);
        float squashAmount = 1f - SquashResistance;
        VisualScale = new Vector2(1f + 0.3f * squashAmount, 1f - 0.25f * squashAmount);
        _squashHoldTimer = 0.05f;
        if (Hp <= 0) { Alive = false; if (IsDummy) _respawnTimer = RespawnDelay; return true; }
        return false;
    }

    public bool CanLatch => Alive && !IsDummy && !Frozen && !IsLatched && _latchCooldown <= 0 && Aggroed;

    public void Latch(Vector2 playerPos, Random rng)
    {
        IsLatched = true;
        // Random offset on player body
        LatchOffset = new Vector2(
            rng.Next(-Player.Width / 2, Player.Width / 2),
            rng.Next(0, Player.Height - Height));
        _latchDamageTick = LatchDamageInterval; // small grace before first tick
        Velocity = Vector2.Zero;
        VisualScale = new Vector2(1.2f, 0.8f);
        _squashHoldTimer = 0.06f;
    }

    public void Detach(float kbX = 0, float kbY = 0)
    {
        IsLatched = false;
        _latchCooldown = LatchCooldownTime;
        KnockbackVel = new Vector2(kbX != 0 ? kbX : (Dir * -150f), kbY != 0 ? kbY : -200f);
    }

    /// <summary>Tick latch damage. Returns damage dealt this frame (0 if no tick yet).</summary>
    public int UpdateLatch(float dt)
    {
        if (!IsLatched) return 0;
        _latchDamageTick -= dt;
        if (_latchDamageTick <= 0)
        {
            _latchDamageTick = LatchDamageInterval;
            return LatchDamage;
        }
        return 0;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        int ew = EffectiveWidth;
        int eh = EffectiveHeight;
        Color bodyColor = HitFlash > 0 ? Color.Red
            : IsDummy ? new Color(140, 100, 160)
            : Frozen ? new Color(100, 160, 200)
            : IsLatched ? new Color(160, 40, 40)
            : SwarmActive ? new Color(180, 40, 20)
            : Variant == CrawlerVariant.Leaper ? (Aggroed ? new Color(140, 80, 20) : new Color(100, 70, 30))
            : (Aggroed ? new Color(120, 60, 20) : new Color(80, 50, 20));
        int scaledW = (int)(ew * VisualScale.X);
        int scaledH = (int)(eh * VisualScale.Y);
        int drawX = (int)Position.X + ew / 2 - scaledW / 2;
        int drawY = (int)Position.Y + eh - scaledH;
        sb.Draw(pixel, new Rectangle(drawX, drawY, scaledW, scaledH), bodyColor);

        // Legs
        sb.Draw(pixel, new Rectangle((int)Position.X + 2, (int)Position.Y + eh, 2, 3), new Color(60, 30, 10));
        sb.Draw(pixel, new Rectangle((int)Position.X + ew - 4, (int)Position.Y + eh, 2, 3), new Color(60, 30, 10));

        // Antennae (wiggle based on state)
        if (!IsDummy)
        {
            float wiggle = MathF.Sin(_antennaeTimer * 6f) * 2f;
            int headX = Dir > 0 ? (int)Position.X + ew - 2 : (int)Position.X;
            int antY = (int)Position.Y - 2;
            int ant1X = headX + Dir * 3 + (int)(wiggle * 0.5f);
            int ant2X = headX + Dir * 5 + (int)wiggle;
            Color antColor = new Color(60, 40, 15);
            sb.Draw(pixel, new Rectangle(ant1X, antY, 1, 3), antColor);
            sb.Draw(pixel, new Rectangle(ant2X, antY - 1, 1, 3), antColor);

            // Leaper variant: longer legs, slightly taller look
            if (Variant == CrawlerVariant.Leaper)
            {
                // Extra middle legs
                sb.Draw(pixel, new Rectangle((int)Position.X + ew / 2 - 1, (int)Position.Y + eh, 2, 4), new Color(70, 40, 15));
                // Stripe marking
                sb.Draw(pixel, new Rectangle(drawX + scaledW / 4, drawY + 1, scaledW / 2, 2), new Color(160, 100, 30) * 0.6f);
            }
        }

        // Paused/edge-sniffing visual: slight head bob
        if (_bugState == BugState.EdgeSniffing && !_edgeSniffDecided)
        {
            int bobY = (int)(MathF.Sin(_antennaeTimer * 10f) * 1.5f);
            int headDotX = Dir > 0 ? (int)Position.X + ew - 1 : (int)Position.X - 1;
            sb.Draw(pixel, new Rectangle(headDotX, (int)Position.Y + 2 + bobY, 2, 2), bodyColor * 1.2f);
        }
    }
}
