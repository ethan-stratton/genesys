using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public enum CrawlerVariant { Forager, Skitter, Leaper }

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
    public CrawlerVariant Variant = CrawlerVariant.Forager;
    public string ScanName => Variant switch
    {
        CrawlerVariant.Forager => "Forest Crawler — Forager",
        CrawlerVariant.Skitter => "Forest Crawler — Skitter",
        CrawlerVariant.Leaper => "Forest Crawler — Leaper",
        _ => "Unknown Crawler"
    };
    public string ScanDescription => Variant switch
    {
        CrawlerVariant.Forager => "Harmless detritivore. Feeds on decaying plant matter. Dies easily underfoot.",
        CrawlerVariant.Skitter => "Nervous herbivore. Flees at the first sign of danger. Completely harmless.",
        CrawlerVariant.Leaper => "Aggressive ambush predator. Leaps at prey from cover. Can latch onto larger organisms.",
        _ => ""
    };

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
    private enum BugState { Idle, Walking, Paused, EdgeSniffing, Startled, Chasing, Swarming, Fleeing }
    private BugState _bugState = BugState.Walking;
    private float _bugStateTimer;
    private float _pauseDuration;
    private float _walkSpeedMult = 1f; // varies to look organic
    private float _antennaeTimer; // visual wiggle
    private bool _edgeSniffDecided; // has the crawler decided what to do at the edge?
    private float _startleTimer;
    private float _fleeTimer; // skitters keep running after player leaves range
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

        // Refresh walkable surface edges dynamically (not just at spawn)
        if (_onGround && tileGrid != null)
        {
            float footY = Position.Y + EffectiveHeight;
            var edges = EnemyPhysics.FindSurfaceEdges(
                Position.X, footY, EffectiveWidth,
                tileGrid, tileSize, platforms, solidFloors,
                SurfaceLeft, SurfaceRight);
            SurfaceLeft = edges.Left;
            SurfaceRight = edges.Right;
        }

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

            // Only Leapers aggro; Foragers ignore player; Skitters flee
            if (Variant == CrawlerVariant.Leaper)
                Aggroed = dist < AggroRange;
            else if (Variant == CrawlerVariant.Skitter)
                Aggroed = false; // skitters don't aggro, they flee (handled below)
            else
                Aggroed = false; // foragers never aggro

            // Skitter flee behavior
            bool fleeing = Variant == CrawlerVariant.Skitter && dist < AggroRange;

            // Keep flee timer alive
            if (fleeing) _fleeTimer = 1.5f + (float)_rng.NextDouble() * 1f; // flee 1.5-2.5s after last trigger
            bool isFleeing = fleeing || _fleeTimer > 0;

            // Startle: transition from calm to aggroed/fleeing
            if ((Aggroed || isFleeing) && !wasAggroed && _bugState != BugState.Startled && _bugState != BugState.Fleeing)
            {
                _bugState = BugState.Startled;
                _startleTimer = 0.1f + (float)_rng.NextDouble() * 0.1f;
                Velocity.X = 0;
                VisualScale = new Vector2(0.85f, 1.15f);
                _squashHoldTimer = 0.08f;
            }

            if (SwarmActive && SwarmTargetX.HasValue)
            {
                _bugState = BugState.Swarming;
                float sdx = SwarmTargetX.Value - (Position.X + Width / 2f);
                Dir = sdx > 0 ? 1 : -1;

                // Variant-specific swarm behavior
                float swarmSpd, swarmJF, swarmJH;
                float jCooldown;
                switch (Variant)
                {
                    case CrawlerVariant.Forager:
                        // Slow, clumsy, hesitant — mob mentality overriding instinct
                        swarmSpd = Speed * 1.2f; // barely faster than normal walk
                        swarmJF = -180f;
                        swarmJH = 80f;
                        jCooldown = 1.5f;
                        // Random hesitation — foragers sometimes pause mid-swarm
                        if (_rng.NextDouble() < 0.01) { Velocity.X = 0; break; }
                        break;
                    case CrawlerVariant.Skitter:
                        // Fast and erratic — panicked, not brave
                        swarmSpd = ChaseSpeed * 1.5f;
                        swarmJF = -250f;
                        swarmJH = 130f;
                        jCooldown = 0.5f;
                        // Skitters jitter direction occasionally (nervous even in swarm)
                        if (_rng.NextDouble() < 0.03) Dir = -Dir;
                        break;
                    default: // Leaper
                        swarmSpd = SwarmSpeed;
                        swarmJF = SwarmJumpForce;
                        swarmJH = SwarmJumpHSpeed;
                        jCooldown = 0.4f;
                        break;
                }

                Velocity.X = Dir * swarmSpd * _walkSpeedMult; // use per-crawler speed variation
                if (_onGround && _jumpCooldown <= 0 && MathF.Abs(sdx) > 30f)
                {
                    Velocity.Y = swarmJF;
                    Velocity.X = Dir * swarmJH;
                    _jumpCooldown = jCooldown;
                    VisualScale = new Vector2(0.7f, 1.3f);
                    _squashHoldTimer = 0.04f;
                }
            }
            else if (_bugState == BugState.Startled)
            {
                _startleTimer -= dt;
                Velocity.X = 0;
                if (_startleTimer <= 0)
                {
                    if (Aggroed) _bugState = BugState.Chasing;
                    if (Aggroed) _bugState = BugState.Chasing;
                    else if (isFleeing) _bugState = BugState.Fleeing;
                    else _bugState = BugState.Walking;
                }
            }
            else if (_bugState == BugState.Fleeing || isFleeing)
            {
                // Skitter: run away from player at high speed
                _bugState = BugState.Fleeing;
                if (fleeing) Dir = dx > 0 ? -1 : 1; // update direction only while player is near
                float fleeSpeed = ChaseSpeed * 2f; // much faster flee
                Velocity.X = Dir * fleeSpeed;
                _fleeTimer -= dt;
                // Stop fleeing when timer expires
                if (_fleeTimer <= 0)
                {
                    _bugState = BugState.Paused;
                    _bugStateTimer = 0.5f + (float)_rng.NextDouble() * 1f;
                    Velocity.X = 0;
                }
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

    public int CheckPlayerDamage(Rectangle playerRect, float playerVelY = 0f)
    {
        if (!Alive || DamageCooldown > 0) return 0;
        if (!Rect.Intersects(playerRect)) return 0;
        
        // Foragers die when stomped (player falling onto them from above)
        if (Variant == CrawlerVariant.Forager)
        {
            if (playerVelY > 0 && playerRect.Bottom <= Rect.Top + 8)
            {
                Alive = false; // squished!
            }
            return 0; // never deals damage
        }
        // Skitters don't deal contact damage either
        if (Variant == CrawlerVariant.Skitter) return 0;
        
        // Leapers deal damage
        DamageCooldown = 1.0f;
        return 5;
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

    public bool CanLatch => Alive && !IsDummy && !Frozen && !IsLatched && _latchCooldown <= 0 && Aggroed && Variant == CrawlerVariant.Leaper;

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
            : SwarmActive ? (Variant == CrawlerVariant.Leaper ? new Color(200, 40, 20) : Variant == CrawlerVariant.Skitter ? new Color(140, 60, 40) : new Color(160, 50, 30))
            : Variant == CrawlerVariant.Leaper ? (Aggroed ? new Color(140, 80, 20) : new Color(100, 70, 30))
            : Variant == CrawlerVariant.Skitter ? new Color(60, 80, 50) // greenish
            : new Color(80, 50, 20); // forager: plain brown
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
            // Skitter: thin legs, lighter body spot (looks nervous/fast)
            else if (Variant == CrawlerVariant.Skitter)
            {
                // Extra thin middle legs (speed look)
                sb.Draw(pixel, new Rectangle((int)Position.X + ew / 3, (int)Position.Y + eh, 1, 4), new Color(50, 60, 35));
                sb.Draw(pixel, new Rectangle((int)Position.X + ew * 2 / 3, (int)Position.Y + eh, 1, 4), new Color(50, 60, 35));
                // Light spot on back
                sb.Draw(pixel, new Rectangle(drawX + scaledW / 3, drawY + 2, scaledW / 3, 2), new Color(90, 110, 70) * 0.5f);
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
