using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public enum CrawlerVariant { Forager, Skitter, Leaper, Bombardier, Stalker, Spitter, Mimic, Resonant }

/// <summary>
/// A procedural leg with 2-segment inverse kinematics.
/// Solves joint angles via law of cosines for natural insect leg movement.
/// </summary>
public struct IKLeg
{
    public float UpperLen;     // thigh length
    public float LowerLen;     // shin length
    public Vector2 FootTarget; // where the foot WANTS to be (ground contact)
    public Vector2 FootPos;    // where the foot currently IS (lerps toward target)
    public Vector2 HipOffset;  // offset from body center (local space, facing right)
    public bool IsMoving;      // currently stepping to new position
    public float StepTimer;    // 0→1 during step animation
    public Vector2 StepFrom;   // foot position at step start

    public const float StepDuration = 0.08f; // seconds per step
    public const float StepHeight = 4f;      // arc height during step
    public const float StepThreshold = 6f;   // distance before foot re-steps

    /// <summary>
    /// Solve 2-segment IK: given hip position and foot target, returns knee position.
    /// Uses law of cosines. Returns false if target is unreachable (stretches toward it).
    /// </summary>
    public static Vector2 SolveKnee(Vector2 hip, Vector2 foot, float upperLen, float lowerLen, float sideBias)
    {
        Vector2 toFoot = foot - hip;
        float dist = toFoot.Length();
        if (dist < 0.001f) return hip + new Vector2(0, upperLen);

        float totalLen = upperLen + lowerLen;
        // Clamp distance so IK doesn't break
        if (dist > totalLen * 0.999f) dist = totalLen * 0.999f;
        if (dist < MathF.Abs(upperLen - lowerLen) + 0.01f)
            dist = MathF.Abs(upperLen - lowerLen) + 0.01f;

        // Law of cosines for knee angle
        float cosAngle = (upperLen * upperLen + dist * dist - lowerLen * lowerLen) / (2f * upperLen * dist);
        cosAngle = MathHelper.Clamp(cosAngle, -1f, 1f);
        float angle = MathF.Acos(cosAngle);

        // Direction from hip to foot
        float baseAngle = MathF.Atan2(toFoot.Y, toFoot.X);
        // sideBias > 0 = knee bends outward (left legs bend left, right legs bend right)
        float kneeAngle = baseAngle - angle * sideBias;

        return hip + new Vector2(MathF.Cos(kneeAngle), MathF.Sin(kneeAngle)) * upperLen;
    }
}

public class Crawler : Creature
{
    public const int Width = 16, Height = 10;
    public float PatrolLeft, PatrolRight;
    public float SurfaceLeft, SurfaceRight;
    public bool Aggroed;
    public float AggroRange = 200f;
    public float Speed = 60f;
    public float ChaseSpeed = 100f;
    public float SwarmSpeed = 130f;
    private bool _onGround;
    private bool _wasOnGround;

    public Vector2 KnockbackVel;
    public float SquashResistance = 0f;
    private float _squashHoldTimer;

    // Variant
    public CrawlerVariant Variant = CrawlerVariant.Forager;
    
    /// <summary>Call after setting Variant to sync ecological role.</summary>
    public void ApplyVariantRole()
    {
        Role = Variant switch
        {
            CrawlerVariant.Forager => EcologicalRole.Herbivore,
            CrawlerVariant.Skitter => EcologicalRole.Flighty,
            CrawlerVariant.Leaper => EcologicalRole.Predator,
            CrawlerVariant.Bombardier => EcologicalRole.Defensive,
            CrawlerVariant.Stalker => EcologicalRole.Predator,
            CrawlerVariant.Spitter => EcologicalRole.Predator,
            CrawlerVariant.Mimic => EcologicalRole.Predator,
            CrawlerVariant.Resonant => EcologicalRole.Defensive,
            _ => EcologicalRole.Herbivore,
        };
        SpeciesName = Variant switch
        {
            CrawlerVariant.Stalker => "stalker",
            CrawlerVariant.Spitter => "spitter",
            CrawlerVariant.Mimic => "mimic",
            CrawlerVariant.Resonant => "resonant",
            _ => Variant.ToString().ToLower(),
        };
        
        // Personality through animation — variant-specific spring parameters
        // f=frequency(speed), z=damping(bounce), r=response(anticipation)
        (float f, float z, float r) = Variant switch
        {
            CrawlerVariant.Forager => (3f, 0.6f, 0f),       // calm, steady, no overshoot
            CrawlerVariant.Skitter => (6f, 0.3f, -1.5f),    // nervous, jittery, strong anticipation
            CrawlerVariant.Leaper => (5f, 0.4f, -2f),       // explosive, springy, big anticipation
            CrawlerVariant.Bombardier => (2f, 0.9f, 0f),    // sluggish, heavy, critically damped
            CrawlerVariant.Stalker => (4f, 0.5f, -1.5f),    // measured, deliberate, patient
            CrawlerVariant.Spitter => (3.5f, 0.4f, -0.5f),  // cautious, keeps distance
            CrawlerVariant.Mimic => (3f, 0.6f, 0f),         // identical to Forager until attack
            CrawlerVariant.Resonant => (1.5f, 0.95f, 0f),   // slow, heavy, electromagnetic hum
            _ => (4f, 0.5f, -1f),
        };
        _bodyBobSpring?.SetParams(f, z, r);
        ApplyVariantNeedRates();
    }
    public string ScanName => Variant switch
    {
        CrawlerVariant.Forager => "Forest Crawler — Forager",
        CrawlerVariant.Skitter => "Forest Crawler — Skitter",
        CrawlerVariant.Leaper => "Forest Crawler — Leaper",
        CrawlerVariant.Bombardier => "Forest Crawler — Bombardier",
        _ => "Unknown Crawler"
    };
    public string ScanDescription => Variant switch
    {
        CrawlerVariant.Forager => "Harmless detritivore. Feeds on decaying plant matter. Dies easily underfoot.",
        CrawlerVariant.Skitter => "Nervous herbivore. Flees at the first sign of danger. Completely harmless.",
        CrawlerVariant.Leaper => "Aggressive ambush predator. Leaps at prey from cover. Can latch onto larger organisms.",
        CrawlerVariant.Bombardier => "Chemical defense specialist. Sprays superheated fluid when threatened. Keep your distance.",
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
    
    // Second-order dynamics for organic body motion
    private SecondOrderDynamics _bodyBobSpring; // vertical bob responds to speed changes
    
    // Cached tile grid reference for line-of-sight checks
    private TileGrid _tileGridRef;
    private int _tileSizeRef;
    private bool _hasTileGridRef;

    // Dummy mode: high HP, no aggro, respawns at original position
    public bool IsDummy;
    public bool Frozen;
    public bool SwarmActive;
    public float? SwarmTargetX;
    public bool AlwaysCrit;
    public float DummyScale = 1f;

    // Bombardier spray system
    public float BombardierChargeTimer;     // telegraph timer (glows before spray)
    public float BombardierCooldown;        // time between sprays
    public bool BombardierCharging;         // currently telegraphing
    private const float BombardierChargeTime = 0.5f;  // telegraph duration
    private const float BombardierCooldownTime = 3.0f; // seconds between sprays
    private const float BombardierRange = 150f;        // aggro range for spray
    private const float BombardierSpraySpeed = 300f;   // pixel speed of spray particles
    private const int BombardierSprayCount = 8;        // particles per spray
    
    // Bombardier spray event: Game1 reads this each frame and spawns particles
    public bool BombardierSprayed;          // true on the frame spray fires
    public Vector2 BombardierSprayDir;      // direction of spray (away from player, predicted)

    // --- Procedural Legs ---
    public IKLeg[] Legs;
    private int _gaitGroup; // 0 or 1 — which group of legs is currently stepping
    private const int LegCount = 6; // 3 per side
    public float DummyScaleX = 0f;
    public float DummyScaleY = 0f;

    // Creature hunting
    private Creature _huntTarget;
    private float _huntTimer;

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
    public override int CreatureWidth => EffectiveWidth;
    public override int CreatureHeight => EffectiveHeight;
    public override bool IsNocturnal => Variant is CrawlerVariant.Stalker or CrawlerVariant.Leaper;
    public override bool IsCrepuscular => Variant is CrawlerVariant.Skitter;

    public override (int min, int max) PreySize => Variant switch
    {
        CrawlerVariant.Leaper => (30, 300),
        CrawlerVariant.Stalker => (30, 300),
        CrawlerVariant.Spitter => (50, 500),
        _ => (0, 0),
    };

    // Wall/ceiling walking (Stalker only)
    private enum GravityDir { Down, Left, Right, Up }
    private GravityDir _gravDir = GravityDir.Down;
    private float _wallCheckTimer;
    private Vector2 _spawnPos;
    public void SetSpawnPos(Vector2 pos) => _spawnPos = pos;
    private float _respawnTimer;
    private const float RespawnDelay = 2f;

    private const float Gravity = 600f;

    public Crawler(Vector2 pos, float patrolLeft, float patrolRight, float surfaceLeft, float surfaceRight, Random rng = null)
    {
        Position = pos;
        _spawnPos = pos;
        SpawnOrigin = pos;
        PatrolLeft = patrolLeft;
        PatrolRight = patrolRight;
        SurfaceLeft = surfaceLeft;
        SurfaceRight = surfaceRight;
        Hp = 3;
        MaxHp = 3;
        SpeciesName = "crawler";
        Needs = CreatureNeeds.Default;
        _rng = rng ?? new Random();
        _bodyBobSpring = new SecondOrderDynamics(4f, 0.4f, -1f, 0f); // underdamped + anticipation
        // Start in a random state so groups don't move in sync
        _bugStateTimer = (float)_rng.NextDouble() * 2f;
        _walkSpeedMult = 0.6f + (float)_rng.NextDouble() * 0.8f; // 0.6–1.4x
        _antennaeTimer = (float)_rng.NextDouble() * 10f;
        DeathParticleColor = new Color(120, 60, 20);
        HitColor = new Color(120, 60, 20);
        InitLegs();
        // Randomize starting needs — predators start hungrier
        bool isPredator = Role is EcologicalRole.Predator or EcologicalRole.Apex;
        Needs.Hunger = isPredator
            ? 0.5f + (float)Random.Shared.NextDouble() * 0.25f
            : 0.2f + (float)Random.Shared.NextDouble() * 0.3f;
        Needs.Fatigue = (float)Random.Shared.NextDouble() * 0.2f;
    }

    /// <summary>Set species-specific need rates after variant is known. Call after ApplyVariantRole.</summary>
    private void ApplyVariantNeedRates()
    {
        switch (Variant)
        {
            case CrawlerVariant.Forager: HungerRate = 0.003f; break;
            case CrawlerVariant.Skitter: FatigueRate = 0.002f; break;
            case CrawlerVariant.Leaper: HungerRate = 0.005f; FatigueRate = 0.0015f; break;
            case CrawlerVariant.Bombardier: FatigueRate = 0.0008f; break;
            case CrawlerVariant.Stalker: HungerRate = 0.0025f; break;
            case CrawlerVariant.Spitter: FatigueRate = 0.0012f; break;
        }
    }

    public override CreatureGoal SelectGoal()
    {
        if (Hp > 0 && Hp <= MaxHp * 0.3f) return CreatureGoal.Flee;
        if (Needs.Safety < 0.3f) return CreatureGoal.Flee;
        // Predators hunt earlier
        float eatThresh = (Role is EcologicalRole.Predator or EcologicalRole.Apex) ? 0.45f : 0.7f;
        if (Needs.Hunger > eatThresh) return CreatureGoal.Eat;
        if (Needs.Fatigue > 0.7f) return CreatureGoal.Rest;
        return CreatureGoal.Wander;
    }

    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, EffectiveWidth, EffectiveHeight);

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        var playerCenter = ctx.PlayerCenter;
        var tileGrid = ctx.TileGrid;
        var tileSize = ctx.TileSize;
        var levelBottom = ctx.LevelBottom;
        // Cache tile grid for LOS checks
        if (tileGrid != null) { _tileGridRef = tileGrid; _tileSizeRef = tileSize; _hasTileGridRef = true; }
        
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

        // --- Needs system ---
        TickNeeds(dt);
        // Weather effects
        if (ctx.IsRaining)
        {
            Needs.Hunger += dt * HungerRate * 0.5f;
            if (Role is not (EcologicalRole.Predator or EcologicalRole.Apex or EcologicalRole.Defensive))
                Needs.Safety = Math.Min(Needs.Safety, 0.6f);
        }
        // Time-of-day activity
        float activity = GetActivityLevel(ctx.WorldTime);
        if (activity < 0.2f && CurrentGoal != CreatureGoal.Flee)
            CurrentGoal = CreatureGoal.Rest;
        float weatherDetectMult = ctx.IsStorming ? 0.5f : ctx.IsRaining ? 0.7f : 1f;
        // Safety from player proximity (variant-specific safe distance)
        float _safeDistForVariant = Variant switch
        {
            CrawlerVariant.Forager => 150f,
            CrawlerVariant.Skitter => 200f,
            CrawlerVariant.Leaper => 120f,
            _ => 150f,
        };
        {
            float distToPlayer = Vector2.Distance(Position, ctx.PlayerCenter);
            float safetyFromPlayer = MathHelper.Clamp(distToPlayer / _safeDistForVariant, 0f, 1f);
            Needs.Safety = Math.Min(Needs.Safety, safetyFromPlayer);
            CurrentGoal = SelectGoal();
        }

        // Creature awareness
        var (threat, threatDist, prey, preyDist) = ScanCreatures(ctx.NearbyCreatures, _safeDistForVariant, 180f);
        switch (Variant)
        {
            case CrawlerVariant.Forager:
                if (threat != null && threatDist < 80f)
                    Dir = threat.Position.X > Position.X ? -1 : 1;
                break;
            case CrawlerVariant.Skitter:
                if (threat != null && threatDist < 120f)
                {
                    Dir = threat.Position.X > Position.X ? -1 : 1;
                    Needs.Safety = Math.Min(Needs.Safety, 0.1f);
                }
                break;
            case CrawlerVariant.Leaper:
            case CrawlerVariant.Stalker:
            case CrawlerVariant.Spitter:
                if (CurrentGoal == CreatureGoal.Eat && prey != null && preyDist < 300f)
                {
                    _huntTarget = prey;
                    Dir = prey.Position.X > Position.X ? 1 : -1;
                }
                else
                {
                    _huntTarget = null;
                }
                break;
            case CrawlerVariant.Bombardier:
            case CrawlerVariant.Resonant:
                break;
        }
        // Don't override goal if actively burrowing (mid-progress)
        if (BurrowProgress > 0 && BurrowProgress < 1f)
        { /* keep current goal — burrowing in progress */ }
        else
            CurrentGoal = SelectGoal();

        // Predator attack — damage prey on contact
        if (_huntTarget != null && _huntTarget.Alive && Rect.Intersects(_huntTarget.Rect) && _huntTimer <= 0)
        {
            _huntTarget.TakeHit(1, Dir * 40f, -30f);
            Needs.Hunger = MathHelper.Clamp(Needs.Hunger - 0.3f, 0f, 1f);
            _huntTarget = null;
            _huntTimer = 1.5f;
        }
        if (_huntTimer > 0) _huntTimer -= dt;

        // Noise detection
        var noise = CheckNoise(ctx.NoiseEvents);
        if (noise != null)
        {
            if (Role is EcologicalRole.Herbivore or EcologicalRole.Flighty or EcologicalRole.Scavenger)
            {
                Needs.Safety = Math.Min(Needs.Safety, 1f - noise.Intensity);
                Dir = noise.Position.X > Position.X ? -1 : 1;
            }
            else if (Role is EcologicalRole.Predator or EcologicalRole.Apex)
            {
                if (Needs.Hunger > 0.5f && CurrentGoal != CreatureGoal.Flee)
                    Dir = noise.Position.X > Position.X ? 1 : -1;
            }
        }

        // Startle propagation
        if (CurrentGoal == CreatureGoal.Flee && _prevGoal != CreatureGoal.Flee)
            PropagateStartle(this, ctx.NearbyCreatures);

        // Lantern reaction
        int lanternReaction = ReactToLantern(ctx);
        if (lanternReaction == 1) // attracted to light
        {
            float ldx = ctx.LanternPos.X - (Position.X + EffectiveWidth / 2f);
            if (MathF.Abs(ldx) > 10f) Dir = ldx > 0 ? 1 : -1;
        }
        else if (lanternReaction == -1) // nocturnal — flee light
        {
            float ldx = ctx.LanternPos.X - (Position.X + EffectiveWidth / 2f);
            Dir = ldx > 0 ? -1 : 1;
        }

        float fleeSpeedBoost = CurrentGoal == CreatureGoal.Flee ? 1.3f : 1f;

        // Burrowing behavior — rest, hide from threats, or cornered
        bool wantsBurrow = (CurrentGoal == CreatureGoal.Rest || 
            (Needs.Safety < 0.2f && CurrentGoal == CreatureGoal.Flee)) && CanBurrow;
        if (wantsBurrow && !IsBurrowed)
        {
            BurrowProgress = MathHelper.Clamp(BurrowProgress + dt * 0.5f, 0f, 1f);
            if (BurrowProgress >= 1f) IsBurrowed = true;
            Velocity = Vector2.Zero;
        }
        else if (CurrentGoal != CreatureGoal.Rest && IsBurrowed)
        {
            BurrowProgress = MathHelper.Clamp(BurrowProgress - dt * 1.0f, 0f, 1f);
            if (BurrowProgress <= 0) IsBurrowed = false;
        }
        // Burrowed creature wakes on very close player
        if (IsBurrowed && Vector2.Distance(Position, ctx.PlayerCenter) < 20f)
        {
            Needs.Safety = 0f;
            CurrentGoal = CreatureGoal.Flee;
            IsBurrowed = false;
            BurrowProgress = 0f;
        }

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

        // Latched: skip all movement
        if (IsLatched)
        {
            Velocity = Vector2.Zero;
            KnockbackVel = Vector2.Zero;
            if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
            else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);
            UpdateLegs(dt);
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
                tileGrid, tileSize,
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
            // Line-of-sight required for aggro/flee triggers
            bool canSeePlayer = !_hasTileGridRef || HasLineOfSight(
                Position + new Vector2(Width / 2f, Height / 2f), playerCenter);
            
            // Needs-influenced aggro range: fleeing creatures react from farther
            float effectiveAggroRange = AggroRange * weatherDetectMult;
            if (CurrentGoal == CreatureGoal.Flee) effectiveAggroRange *= 1.3f;
            
            if (Variant == CrawlerVariant.Leaper)
                Aggroed = dist < effectiveAggroRange && canSeePlayer;
            else if (Variant == CrawlerVariant.Skitter)
                Aggroed = false;
            else if (Variant == CrawlerVariant.Forager)
                Aggroed = false; // foragers don't chase, but they DO flee (below)
            else
                Aggroed = false;

            // Skitter AND Forager flee behavior — both are prey that flee
            bool fleeing = (Variant == CrawlerVariant.Skitter || Variant == CrawlerVariant.Forager) 
                && dist < effectiveAggroRange && canSeePlayer;

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
                if (BurrowProgress > 0) { Velocity.X = 0; } // burrowing — don't move
                else
                {
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
                } // end burrowing else
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
                        float goalSpeedMult = CurrentGoal == CreatureGoal.Eat ? 1.15f
                            : CurrentGoal == CreatureGoal.Rest ? 0.7f : 1f;
                        goalSpeedMult *= MathHelper.Clamp(activity, 0.3f, 1f);
                        Velocity.X = Dir * Speed * _walkSpeedMult * goalSpeedMult;

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
                            // Needs influence: hungry creatures pause less, tired ones pause more
                            float pauseChance = CurrentGoal == CreatureGoal.Eat ? 0.15f
                                : CurrentGoal == CreatureGoal.Rest ? 0.5f : 0.3f;
                            if (roll < pauseChance)
                            {
                                // Pause
                                _bugState = BugState.Paused;
                                _pauseDuration = 0.4f + (float)_rng.NextDouble() * 1.5f;
                                if (CurrentGoal == CreatureGoal.Rest) _pauseDuration *= 1.4f;
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

        // Apply flee speed boost
        if (fleeSpeedBoost > 1f)
            Velocity.X *= fleeSpeedBoost;

        // Terrain navigation — ledge and wall detection
        if (!IsDummy && !Frozen && !IsLatched && _onGround && tileGrid != null)
        {
            bool wallAhead = HasWallAhead(tileGrid, tileSize,
                new Vector2(Position.X + (Dir > 0 ? EffectiveWidth : 0), Position.Y + EffectiveHeight / 2f), Dir);
            if (wallAhead)
            {
                if (CurrentGoal == CreatureGoal.Flee)
                {
                    if (CanBurrow && !IsBurrowed) { CurrentGoal = CreatureGoal.Rest; BurrowProgress = 0.3f; Velocity.X = 0; }
                    else { Dir = -Dir; Needs.Safety = MathHelper.Clamp(Needs.Safety + 0.3f, 0f, 1f); }
                }
                else Dir = -Dir;
            }
            if (CurrentGoal != CreatureGoal.Flee)
            {
                bool floorAhead = HasFloorAhead(tileGrid, tileSize,
                    new Vector2(Position.X + EffectiveWidth / 2f, Position.Y + EffectiveHeight), Dir);
                if (!floorAhead) Dir = -Dir;
            }
        }

        // Wander range expansion when hungry
        if (CurrentGoal == CreatureGoal.Eat)
        {
            var food = FindFood(ctx.FoodSources);
            if (food == null)
                WanderRadius = MathHelper.Clamp(WanderRadius + dt * 20f, 100f, MaxWanderRadius);
            else
                WanderRadius = 100f;
        }
        float distFromSpawn = Math.Abs(Position.X - SpawnOrigin.X);
        if (distFromSpawn > WanderRadius && CurrentGoal == CreatureGoal.Wander)
            Dir = Position.X > SpawnOrigin.X ? -1 : 1;

        // Bounds awareness
        if (Position.X < ctx.BoundsLeft + 10 || Position.X + EffectiveWidth > ctx.BoundsRight - 10)
        {
            if (CurrentGoal == CreatureGoal.Flee)
            {
                if (CanBurrow && !IsBurrowed) { CurrentGoal = CreatureGoal.Rest; BurrowProgress = 0.3f; }
                else { Dir = -Dir; Needs.Safety = MathHelper.Clamp(Needs.Safety + 0.2f, 0f, 1f); }
            }
            else Dir = -Dir;
        }

        // Wall/ceiling walking for Stalker
        if (Variant == CrawlerVariant.Stalker && tileGrid != null)
            UpdateWallWalk(dt, tileGrid, tileSize);

        // Apply gravity and tile collision
        _onGround = EnemyPhysics.ApplyGravityAndCollision(
            ref Position, ref Velocity,
            EffectiveWidth, EffectiveHeight, Gravity, dt,
            tileGrid, tileSize, levelBottom);

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
        
        // Update procedural legs
        UpdateLegs(dt);
        
        // Update body bob spring — driven by speed, responds physically
        float speedNorm = MathF.Min(MathF.Abs(Velocity.X) / 100f, 1f);
        float bobTarget = speedNorm * MathF.Sin(_antennaeTimer * 12f) * 1.5f;
        _bodyBobSpring.Update(dt, bobTarget);

        // === BOMBARDIER SPRAY LOGIC ===
        BombardierSprayed = false;
        if (Variant == CrawlerVariant.Bombardier && !Frozen && !IsLatched)
        {
            BombardierCooldown -= dt;
            float dist = Vector2.Distance(Position + new Vector2(EffectiveWidth / 2f, EffectiveHeight / 2f), playerCenter);
            
            if (dist < BombardierRange && BombardierCooldown <= 0 && !BombardierCharging)
            {
                // Line-of-sight check — don't spray through walls
                var myCenter = Position + new Vector2(EffectiveWidth / 2f, EffectiveHeight / 2f);
                if (_hasTileGridRef && HasLineOfSight(myCenter, playerCenter))
                {
                    // Start telegraph
                    BombardierCharging = true;
                    BombardierChargeTimer = BombardierChargeTime;
                    // Stop moving during charge
                    Velocity = new Vector2(0, Velocity.Y);
                }
            }
            
            if (BombardierCharging)
            {
                BombardierChargeTimer -= dt;
                // Face away from player (beetles spray backward)
                var toPlayer = playerCenter - Position;
                Dir = toPlayer.X > 0 ? -1 : 1; // face AWAY
                
                if (BombardierChargeTimer <= 0)
                {
                    // FIRE — predict where player will be
                    var myCenter = Position + new Vector2(EffectiveWidth / 2f, EffectiveHeight / 2f);
                    BombardierSprayed = true;
                    // Spray direction: away from self toward player (from abdomen)
                    var sprayDir = playerCenter - myCenter;
                    if (sprayDir.Length() > 0.001f) sprayDir = Vector2.Normalize(sprayDir);
                    else sprayDir = new Vector2(-Dir, 0);
                    BombardierSprayDir = sprayDir;
                    
                    BombardierCharging = false;
                    BombardierCooldown = BombardierCooldownTime;
                }
            }
        }
        _prevGoal = CurrentGoal;
    }
    private void UpdateWallWalk(float dt, TileGrid tg, int ts)
    {
        _wallCheckTimer -= dt;
        if (_wallCheckTimer > 0) return;
        _wallCheckTimer = 0.5f;

        int cx = (int)(Position.X / ts);
        int cy = (int)(Position.Y / ts);

        switch (_gravDir)
        {
            case GravityDir.Down:
                if (TileProperties.IsSolid(tg.GetTileAt(cx + Dir, cy)))
                    _gravDir = Dir > 0 ? GravityDir.Right : GravityDir.Left;
                break;
            case GravityDir.Right:
                if (!TileProperties.IsSolid(tg.GetTileAt(cx + 1, cy)))
                    _gravDir = GravityDir.Up;
                break;
            case GravityDir.Up:
                if (!TileProperties.IsSolid(tg.GetTileAt(cx, cy - 1)))
                    _gravDir = GravityDir.Down;
                break;
            case GravityDir.Left:
                if (!TileProperties.IsSolid(tg.GetTileAt(cx - 1, cy)))
                    _gravDir = GravityDir.Up;
                break;
        }
    }

    /// Refresh surface edge detection using tile-aware method.
    /// </summary>
    public void UpdateSurfaceEdges(TileGrid tileGrid, int tileSize,
        float boundsLeft, float boundsRight)
    {
        float footY = Position.Y + EffectiveHeight;
        var edges = EnemyPhysics.FindSurfaceEdges(
            Position.X, footY, EffectiveWidth,
            tileGrid, tileSize,
            boundsLeft, boundsRight);
        SurfaceLeft = edges.Left;
        SurfaceRight = edges.Right;
        PatrolLeft = MathF.Max(PatrolLeft, SurfaceLeft);
        PatrolRight = MathF.Min(PatrolRight, SurfaceRight);
    }

    public int CheckPlayerDamage(Rectangle playerRect, float playerVelY)
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

    /// <summary>Override base for unified loop — delegates to variant-aware version with 0 velY.</summary>
    public override int CheckPlayerDamage(Rectangle playerRect) => CheckPlayerDamage(playerRect, 0f);

    public override bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
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

    // ──────────── PROCEDURAL LEGS ────────────

    private void InitLegs()
    {
        Legs = new IKLeg[LegCount];
        float upper, lower;
        switch (Variant)
        {
            case CrawlerVariant.Skitter: upper = 9f; lower = 9f; break;
            case CrawlerVariant.Leaper: upper = 7f; lower = 11f; break;
            case CrawlerVariant.Bombardier: upper = 5f; lower = 5f; break;
            default: upper = 7f; lower = 7f; break; // Forager
        }

        float bodyW = EffectiveWidth;
        float bodyH = EffectiveHeight;
        for (int i = 0; i < LegCount; i++)
        {
            bool rightSide = i < 3;
            int legIndex = rightSide ? i : i - 3;
            float xFrac = 0.15f + legIndex * 0.35f; // 0.15, 0.5, 0.85

            float thisUpper = upper;
            float thisLower = lower;
            if (Variant == CrawlerVariant.Leaper && legIndex == 2)
            {
                thisUpper *= 1.5f;
                thisLower *= 1.5f;
            }

            Legs[i].UpperLen = thisUpper;
            Legs[i].LowerLen = thisLower;
            Legs[i].HipOffset = new Vector2(
                (xFrac - 0.5f) * bodyW,
                bodyH * 0.3f
            );

            float hipWorldX = Position.X + bodyW * xFrac;
            float footY = Position.Y + bodyH + thisUpper * 0.6f;
            float footXSpread = rightSide ? 6f : -6f;
            Legs[i].FootPos = new Vector2(hipWorldX + footXSpread, footY);
            Legs[i].FootTarget = Legs[i].FootPos;
        }
    }

    public void UpdateLegs(float dt)
    {
        if (!Alive || IsDummy || Legs == null) return;

        float bodyW = EffectiveWidth;
        float bodyH = EffectiveHeight;
        Vector2 bodyCenter = new(Position.X + bodyW * 0.5f, Position.Y + bodyH * 0.5f);
        float speed = MathF.Abs(Velocity.X);

        // When latched or airborne, legs dangle — no ground targeting
        bool dangling = IsLatched || !_onGround;

        for (int i = 0; i < LegCount; i++)
        {
            bool rightSide = i < 3;
            int legIndex = rightSide ? i : i - 3;
            float xFrac = 0.15f + legIndex * 0.35f;

            Vector2 hipLocal = Legs[i].HipOffset;
            if (Dir < 0) hipLocal.X = -hipLocal.X;
            Vector2 hip = bodyCenter + hipLocal;

            if (dangling)
            {
                // Legs dangle downward with slight sway
                float sway = MathF.Sin((_antennaeTimer + i * 1.1f) * 4f) * 2f;
                float spreadX = rightSide ? 2f : -2f;
                Legs[i].FootTarget = hip + new Vector2(spreadX + sway, Legs[i].UpperLen + Legs[i].LowerLen * 0.7f);
                // Smoothly move feet toward dangle position
                Legs[i].FootPos = Vector2.Lerp(Legs[i].FootPos, Legs[i].FootTarget, dt * 12f);
                Legs[i].IsMoving = false;
                continue;
            }

            // Ground-based foot targeting
            float spreadXGround = rightSide ? 7f : -7f;
            float velOffset = Velocity.X * 0.05f;
            float footTargetX = hip.X + spreadXGround + velOffset;
            float footTargetY = Position.Y + bodyH + 1f;

            Legs[i].FootTarget = new Vector2(footTargetX, footTargetY);

            // Clamp target to max reach
            Vector2 hipLocal2 = Legs[i].HipOffset;
            if (Dir < 0) hipLocal2.X = -hipLocal2.X;
            Vector2 hipW = bodyCenter + hipLocal2;
            float maxR = Legs[i].UpperLen + Legs[i].LowerLen - 1f;
            Vector2 toTarget = Legs[i].FootTarget - hipW;
            if (toTarget.Length() > maxR && toTarget.Length() > 0.001f)
                Legs[i].FootTarget = hipW + toTarget * (maxR / toTarget.Length());

            float distToTarget = Vector2.Distance(Legs[i].FootPos, Legs[i].FootTarget);
            int myGroup = (i % 2 == 0) ? 0 : 1;

            if (!Legs[i].IsMoving && distToTarget > IKLeg.StepThreshold && myGroup == _gaitGroup)
            {
                Legs[i].IsMoving = true;
                Legs[i].StepTimer = 0f;
                Legs[i].StepFrom = Legs[i].FootPos;
            }

            if (Legs[i].IsMoving)
            {
                Legs[i].StepTimer += dt / IKLeg.StepDuration;
                if (Legs[i].StepTimer >= 1f)
                {
                    Legs[i].StepTimer = 1f;
                    Legs[i].IsMoving = false;
                    Legs[i].FootPos = Legs[i].FootTarget;
                }
                else
                {
                    float t = Legs[i].StepTimer;
                    float smoothT = t * t * (3f - 2f * t);
                    Legs[i].FootPos = Vector2.Lerp(Legs[i].StepFrom, Legs[i].FootTarget, smoothT);
                    Legs[i].FootPos.Y -= MathF.Sin(t * MathF.PI) * IKLeg.StepHeight;
                }
            }
        }

        // Alternate gait groups
        bool anyMoving = false;
        for (int i = 0; i < LegCount; i++)
        {
            int myGroup = (i % 2 == 0) ? 0 : 1;
            if (myGroup == _gaitGroup && Legs[i].IsMoving) anyMoving = true;
        }
        if (!anyMoving && speed > 5f)
            _gaitGroup = 1 - _gaitGroup;
    }

    /// <summary>
    /// Raycast through tile grid to check line of sight between two world points.
    /// Returns false if any solid tile blocks the path.
    /// </summary>
    private bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        if (!_hasTileGridRef) return true;
        var tg = _tileGridRef;
        int ts = _tileSizeRef;
        
        var dir = to - from;
        float dist = dir.Length();
        if (dist < 1f) return true;
        dir /= dist;
        
        // Step along ray in half-tile increments
        float step = ts * 0.5f;
        for (float d = step; d < dist; d += step)
        {
            float wx = from.X + dir.X * d;
            float wy = from.Y + dir.Y * d;
            var tile = tg.GetTile((int)wx, (int)wy);
            if (TileProperties.IsSolid(tile))
                return false;
        }
        return true;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
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
            : Variant == CrawlerVariant.Skitter ? new Color(60, 80, 50)
            : Variant == CrawlerVariant.Bombardier ? (BombardierCharging ? new Color(255, 120, 30) : new Color(120, 60, 20))
            : Variant == CrawlerVariant.Stalker ? (Aggroed ? new Color(60, 40, 60) : new Color(45, 30, 50))       // dark purple — shadowy
            : Variant == CrawlerVariant.Spitter ? new Color(80, 120, 40)                                           // sickly green
            : Variant == CrawlerVariant.Mimic ? new Color(80, 50, 20)                                              // identical to Forager
            : Variant == CrawlerVariant.Resonant ? new Color(60, 60, 100)                                          // steel blue, electromagnetic
            : new Color(80, 50, 20);
        int scaledW = (int)(ew * VisualScale.X);
        int scaledH = (int)(eh * VisualScale.Y);
        int drawX = (int)Position.X + ew / 2 - scaledW / 2;
        int drawY = (int)Position.Y + eh - scaledH;

        // Burrowing visual — clip height
        if (BurrowProgress > 0f)
        {
            int burrowClip = (int)(scaledH * BurrowProgress * 0.7f);
            scaledH -= burrowClip;
            drawY += burrowClip;
        }

        // Wall-walking Stalker: flip for ceiling
        bool ceilingFlip = Variant == CrawlerVariant.Stalker && _gravDir == GravityDir.Up;

        // --- PROCEDURAL LEGS ---
        if (!IsDummy && Legs != null && Legs.Length == LegCount)
        {
            Color legColor = new Color(
                (int)(bodyColor.R * 0.7f),
                (int)(bodyColor.G * 0.7f),
                (int)(bodyColor.B * 0.7f));

            Vector2 bodyCenter = new(Position.X + ew * 0.5f, Position.Y + eh * 0.5f);

            for (int i = 0; i < LegCount; i++)
            {
                bool rightSide = i < 3;
                Vector2 hipLocal = Legs[i].HipOffset;
                if (Dir < 0) hipLocal.X = -hipLocal.X;
                Vector2 hip = bodyCenter + hipLocal;
                Vector2 foot = Legs[i].FootPos;

                // Solve IK for knee position
                float sideBias = rightSide ? 1f : -1f;
                Vector2 knee = IKLeg.SolveKnee(hip, foot, Legs[i].UpperLen, Legs[i].LowerLen, sideBias);

                // Clamp foot to max leg reach to prevent stretching glitches
                float maxReach = Legs[i].UpperLen + Legs[i].LowerLen - 0.5f;
                Vector2 hipToFoot = foot - hip;
                float dist = hipToFoot.Length();
                if (dist > maxReach && dist > 0.001f)
                    foot = hip + hipToFoot * (maxReach / dist);

                // Draw upper leg (hip → knee)
                DrawLine(sb, pixel, hip, knee, 3, legColor);
                // Draw lower leg (knee → foot)
                DrawLine(sb, pixel, knee, foot, 2, legColor);
                // Foot dot
                sb.Draw(pixel, new Rectangle((int)foot.X - 1, (int)foot.Y, 3, 2), legColor);
            }
        }
        else
        {
            // Fallback for dummies: simple stub legs
            sb.Draw(pixel, new Rectangle((int)Position.X + 2, (int)Position.Y + eh, 2, 3), new Color(60, 30, 10));
            sb.Draw(pixel, new Rectangle((int)Position.X + ew - 4, (int)Position.Y + eh, 2, 3), new Color(60, 30, 10));
        }

        // --- SEGMENTED BODY ---
        // Head (front), thorax (middle), abdomen (rear)
        if (!IsDummy)
        {
            int headW = scaledW / 3;
            int thoraxW = scaledW / 3 + 2;
            int abdomenW = scaledW - headW - thoraxW + 4;
            int headX, thoraxX, abdomenX;
            if (Dir > 0)
            {
                abdomenX = drawX - 2;
                thoraxX = abdomenX + abdomenW - 2;
                headX = thoraxX + thoraxW - 2;
            }
            else
            {
                headX = drawX - 2;
                thoraxX = headX + headW - 2;
                abdomenX = thoraxX + thoraxW - 2;
            }
            // Body bob from second-order dynamics (physical response to speed changes)
            float bob = _bodyBobSpring.Value;
            int headY = drawY + (int)(bob);
            int thoraxY = drawY;
            int abdomenY = drawY + (int)(-bob * 0.5f);

            // Abdomen (largest segment)
            Color abdColor = bodyColor;
            if (Variant == CrawlerVariant.Bombardier)
            {
                float glow = BombardierCharging ? 0.8f + 0.2f * MathF.Sin(BombardierChargeTimer * 20f) : 0.3f;
                abdColor = Color.Lerp(bodyColor, new Color(255, 80, 20), glow);
            }
            sb.Draw(pixel, new Rectangle(abdomenX, abdomenY + 1, abdomenW, scaledH - 1), abdColor);
            // Thorax
            sb.Draw(pixel, new Rectangle(thoraxX, thoraxY, thoraxW, scaledH), bodyColor);
            // Head (slightly smaller vertically)
            sb.Draw(pixel, new Rectangle(headX, headY + 1, headW, scaledH - 2), bodyColor);

            // Eyes (tiny bright dots on head)
            Color eyeColor = Aggroed || SwarmActive ? new Color(255, 60, 30) : new Color(200, 200, 180);
            int eyeX = Dir > 0 ? headX + headW - 2 : headX + 1;
            sb.Draw(pixel, new Rectangle(eyeX, headY + 2, 1, 1), eyeColor);

            // Antennae (wiggle based on state)
            float wiggle = MathF.Sin(_antennaeTimer * 6f) * 2f;
            int antBaseX = Dir > 0 ? headX + headW : headX;
            int antY = headY;
            int ant1X = antBaseX + Dir * 3 + (int)(wiggle * 0.5f);
            int ant2X = antBaseX + Dir * 5 + (int)wiggle;
            Color antColor = new Color(60, 40, 15);
            sb.Draw(pixel, new Rectangle(ant1X, antY, 1, 3), antColor);
            sb.Draw(pixel, new Rectangle(ant2X, antY - 1, 1, 3), antColor);

            // Leaper stripe marking
            if (Variant == CrawlerVariant.Leaper)
                sb.Draw(pixel, new Rectangle(thoraxX + 1, thoraxY + 1, thoraxW - 2, 2), new Color(160, 100, 30) * 0.6f);
            // Skitter light spot
            else if (Variant == CrawlerVariant.Skitter)
                sb.Draw(pixel, new Rectangle(thoraxX + 2, thoraxY + 2, thoraxW - 4, 2), new Color(90, 110, 70) * 0.5f);
        }
        else
        {
            // Dummies: simple rectangle
            sb.Draw(pixel, new Rectangle(drawX, drawY, scaledW, scaledH), bodyColor);
        }

        // Paused/edge-sniffing visual: slight head bob
        if (_bugState == BugState.EdgeSniffing && !_edgeSniffDecided)
        {
            int bobY = (int)(MathF.Sin(_antennaeTimer * 10f) * 1.5f);
            int headDotX = Dir > 0 ? (int)Position.X + ew - 1 : (int)Position.X - 1;
            sb.Draw(pixel, new Rectangle(headDotX, (int)Position.Y + 2 + bobY, 2, 2), bodyColor * 1.2f);
        }
    }

    /// <summary>Draws a line between two points as a rotated rectangle.</summary>
    private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b, int thickness, Color color)
    {
        Vector2 diff = b - a;
        float length = diff.Length();
        if (length < 0.5f) return;
        float angle = MathF.Atan2(diff.Y, diff.X);
        sb.Draw(pixel,
            new Rectangle((int)a.X, (int)a.Y, (int)length, thickness),
            null, color, angle, new Vector2(0, thickness * 0.5f), SpriteEffects.None, 0f);
    }
}
