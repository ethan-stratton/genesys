using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Genesis;

public class Player
{
    public static float WorldLeft = 0;
    public static float WorldRight = 800;

    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public const int Width = 32;  // Sprite width (visual)
    public const int CollisionWidth = 21; // Hitbox width (narrower, centered)
    public const int CollisionOffsetX = (Width - CollisionWidth) / 2; // 5px from left edge
    public const int Height = 48;
    // === MOVEMENT TIER SYSTEM ===
    public enum MoveTier { Tech, Bio, Cipher }
    public MoveTier CurrentTier { get; set; } = MoveTier.Bio;

    // Per-tier constants: [Tech, Bio, Cipher]
    private static readonly float[] TierSpeed =          { 200f,  250f,  320f  };
    private static readonly float[] TierGravity =        { 900f,  750f,  950f  };
    private static readonly float[] TierJumpForce =      { -370f, -500f, -400f };  // Bio: big single jump
    private static readonly float[] TierRunAccel =       { 2000f, 1000f, 1600f };  // Tech: snap (high accel, low speed)
    private static readonly float[] TierRunDecel =       { 2000f, 1000f, 2500f };  // Cipher: stops on a dime
    private static readonly float[] TierAirAccel =       { 250f,  800f,  500f  };  // Tech: very low air control
    private static readonly float[] TierAirDecel =       { 200f,  350f,  300f  };  // Tech: can barely redirect in air
    private static readonly float[] TierAirMult =        { 0.35f, 0.9f,  0.7f  };  // Tech: committed to your jump arc
    private static readonly float[] TierMaxFall =        { 380f,  350f,  450f  };  // Bio: slower terminal velocity
    private static readonly float[] TierHalfGravThresh = { 20f,   80f,   40f   };  // Bio: huge apex hang
    private static readonly float[] TierJumpCutMult =    { 0.5f,  0.35f, 0.45f };  // Bio: most variable height
    private static readonly float[] TierFallGravMult =   { 1.3f,  1.5f,  1.6f  };  // Bio: floaty but pulls you down

    // Active physics (resolved from tier)
    private float _speed;
    private float _gravity;
    private float _jumpForce;
    private float _runAccel;
    private float _runDecel;
    private float _airAccel;
    private float _airDecel;
    private float _airMult;
    private float _maxFall;
    private float _halfGravThreshold;
    private float _jumpCutMultiplier;
    private float _fallGravMultiplier;

    public void CureInjury()
    {
        IsInjured = false;
        _limpTimer = 0f;
        ApplyTierConstants(); // recalculate speed without debuff
    }

    public void ApplyTierConstants()
    {
        int i = (int)CurrentTier;
        _speed = TierSpeed[i];
        _gravity = TierGravity[i];
        _jumpForce = TierJumpForce[i];
        _runAccel = TierRunAccel[i];
        _runDecel = TierRunDecel[i];
        _airAccel = TierAirAccel[i];
        _airDecel = TierAirDecel[i];
        _airMult = TierAirMult[i];
        _maxFall = TierMaxFall[i];
        _halfGravThreshold = TierHalfGravThresh[i];
        _jumpCutMultiplier = TierJumpCutMult[i];
        _fallGravMultiplier = TierFallGravMult[i];

        // Injured debuff
        if (IsInjured)
        {
            _speed *= InjuredSpeedMult;
            _jumpForce *= 0.7f; // weaker jumps
        }

        // Ability gating per tier
        switch (CurrentTier)
        {
            case MoveTier.Tech:
                EnableBladeDash = false;
                EnableUppercut = false;
                EnableWallClimb = false;
                EnableSlide = false;
                EnableVaultKick = false;
                EnableCartwheel = false;
                EnableFlip = false;
                EnableDoubleJump = false;
                break;
            case MoveTier.Bio:
                EnableBladeDash = false;
                EnableUppercut = false;
                EnableWallClimb = true;
                EnableSlide = true;
                EnableVaultKick = false;
                EnableCartwheel = true;
                EnableFlip = false;
                EnableDoubleJump = false;
                break;
            case MoveTier.Cipher:
                EnableBladeDash = true;
                EnableUppercut = true;
                EnableWallClimb = true;
                EnableSlide = true;
                EnableVaultKick = true;
                EnableCartwheel = true;
                EnableFlip = true;
                EnableDoubleJump = true;
                break;
        }
    }

    private const float CornerCorrectionPx = 4;    // nudge pixels for ceiling bonks
    public bool IsGrounded { get; set; }
    private bool _wasGrounded;
    private bool _wasOnSlope;
    public int FacingDir { get; private set; } = 1;

    // Jump
    private int _jumpsLeft;
    private const int MaxJumps = 2;
    private bool _jumpHeld;

    // Crouch / Aim lock
    public bool IsCrouching { get; private set; }
    public bool Paused; // set by Game1 to freeze animations
    public const int CrouchHeight = 30;

    // Platform drop-through
    public bool WantsDropThrough { get; private set; }
    private float _dropIgnoreTimer;
    private const float DropIgnoreTime = 0.2f;
    private float _lastSTapTime;
    private bool _sWasUp;
    private const float DoubleTapWindow = 0.3f;

    // Slide (S + Space while grounded)
    public bool IsSliding { get; private set; }
    private float _slideTimer;
    private const float SlideDuration = 0.3f;
    private const float SlideStartSpeed = 550f;
    private const float SlideEndSpeed = 300f; // decelerates to this
    private const float SlideCooldown = 0.5f;
    private float _slideCooldownTimer;
    private int _slideDir;
    private int _slideAirFrames;
    public const int SlideHeight = 24;
    public bool SlideInvulnerable => IsSliding; // for future use

    // Right hand - ranged (J)
    private float _shootCooldown;
    private const float ShootRate = 0.25f;
    private bool _shootHeld;
    public bool WantsToShoot { get; private set; }
    public Vector2 ShootDirection { get; private set; }

    // Left hand - melee (K)
    private float _meleeCooldown;
    private bool _meleeHeld;
    public bool WantsToMelee { get; private set; }
    public Vector2 MeleeDirection { get; private set; }
    public float MeleeTimer { get; private set; }
    public float MeleeSwingAngle { get; private set; } // current angle of overhead swing (radians)
    public const int MeleeRange = 40;

    // Weapon stats driven by Game1 each frame
    public float CurrentMeleeRate { get; set; } = 0.3f;
    public float CurrentMeleeActiveTime { get; set; } = 0.25f;
    public float CurrentComboWindow { get; set; } = 0.4f;
    public float CurrentComboCooldown { get; set; } = 0.35f;

    // Combo chain system
    private int _comboStep; // 0, 1, 2 — current position in combo chain
    private float _comboWindow; // time remaining to input next hit
    private float _comboCooldown; // cooldown after combo ends
    private bool[] _comboHit = new bool[3]; // tracks whether each hit connected
    public int ComboStep => _comboStep;
    public bool IsComboFinisher => _comboStep == 2 && MeleeTimer > 0;
    public WeaponType CurrentWeapon { get; set; }
    private float _prevMeleeTimer; // for detecting melee active→inactive transition

    // Spinning melee (hold K for 0.2s+ while grounded)
    public bool IsSpinningMelee { get; private set; }
    private float _meleeHoldTimer; // how long K has been held
    private const float SpinMeleeActivateTime = 0.2f; // hold threshold
    private const float SpinMeleeRate = 0.08f; // much faster than normal melee
    private float _spinMeleeCooldown;
    private static readonly Vector2[] SpinDirections = new Vector2[]
    {
        new(1, 0), new(1, 1), new(0, 1), new(-1, 1),
        new(-1, 0), new(-1, -1), new(0, -1), new(1, -1),
    };

    // Frontflip/backflip (double-tap jump quickly)
    public bool IsFlipping { get; private set; }
    private float _flipTimer;
    
    // Ceiling deflection momentum preservation
    private float _ceilDeflectTimer;
    private float _ceilDeflectVelX;
    private const float FlipDuration = 0.3f;
    private const float FlipSpeed = 450f;
    private const float FlipJumpForce = -350f;
    private int _flipDir; // +1 = frontflip (facing dir), -1 = backflip
    private float _firstJumpTime; // timestamp of first jump for flip window
    private const float FlipInputWindow = 0.15f;

    // Blade dash (quarter circle forward + K: ↓↘→+K)
    public bool IsBladeDashing { get; private set; }
    private float _bladeDashTimer;
    private const float BladeDashDuration = 0.35f;
    private const float BladeDashSpeed = 700f;
    private const float BladeDashJumpForce = -100f; // slight lift
    private int _bladeDashDir;
    public const int BladeDashHitboxW = 36;
    public const int BladeDashHitboxH = 24;
    // QCF input tracking: 0=waiting for ↓, 1=got ↓ waiting for ↘, 2=got ↘ waiting for →+K
    private int _qcfStage;
    private float _qcfTimer;
    private const float QcfInputWindow = 0.4f; // total window for the motion
    public const int MeleeWidth = 20;

    // Cartwheel/dash flip (Shift + A/D + Space)
    public bool IsCartwheeling { get; private set; }
    private float _cartwheelTimer;
    private const float CartwheelDuration = 0.3f;
    private const float CartwheelSpeed = 500f;
    private const float CartwheelJumpForce = -300f;
    private const float CartwheelCooldown = 0.6f;
    private float _cartwheelCooldownTimer;
    private int _cartwheelDir;

    // Vault kick (Jump during slide)
    public bool IsVaultKicking { get; private set; }
    private float _vaultKickTimer;
    private const float VaultKickDuration = 0.35f;
    private const float VaultKickSpeed = 600f;
    private const float VaultKickJumpForce = -350f;
    private const float VaultKickMinSlideTime = 0.12f; // must slide at least this long before cancelling
    private int _vaultKickDir;
    public const int VaultKickHitboxW = 28;
    public const int VaultKickHitboxH = 20;

    // Uppercut (down → up + jump) — high jump with melee hurtbox, i-frames
    public bool IsUppercutting { get; private set; }
    private float _uppercutTimer;
    private const float UppercutDuration = 0.4f;
    private const float UppercutJumpForce = -650f; // higher than normal jump
    private const float UppercutHSpeed = 80f; // slight horizontal drift allowed
    private bool _uppercutInputReady; // true after pressing down, waiting for up+jump
    private float _uppercutInputWindow; // time window after pressing down
    private const float UppercutInputWindowTime = 0.4f;
    public const int UppercutHitboxW = 20;
    public const int UppercutHitboxH = 30;

    // Dash (double-tap A or D) — universal burst, tier-flavored
    public bool IsDashing { get; private set; }
    private float _dashTimer;
    private const float TechDashSpeed = 350f;
    private const float TechDashDuration = 0.15f; // short jet burst
    private const float BioDashSpeed = 300f;
    private const float BioDashDuration = 0.12f; // quick leap
    private const float CipherDashSpeed = 400f;
    private const float CipherDashDuration = 0.18f; // longest, can aim
    private const float DashCooldown = 0.4f;
    private float _dashCooldownTimer;
    private float _lastATapTime;
    private float _lastDTapTime;
    private bool _aWasUp;
    private bool _dWasUp;
    private const float DashDoubleTapWindow = 0.25f;
    private int _dashDir;

    // Tech: Ground Pound (enhanced fast-fall with stomp)
    public bool IsGroundPounding { get; private set; }
    public const float GroundPoundSpeed = 800f;
    public const float GroundPoundDamage = 3f;
    public const float GroundPoundRadius = 40f; // hitbox width on landing
    public bool GroundPoundLanded { get; set; } // read by Game1 for shake/dust/damage
    
    // Tech: Brace (hold crouch to reduce damage/knockback)
    public bool IsBracing { get; private set; }
    private const float BraceDamageReduction = 0.5f; // take 50% damage
    private const float BraceKnockbackReduction = 0.2f; // 80% less knockback
    private float _braceTimer;
    
    // Tech: Sprint (hold direction to build speed — Wario style)
    public bool IsSprinting { get; private set; }
    private float _sprintBuildup; // 0→1 over time
    private const float SprintBuildTime = 0.8f; // seconds to reach full sprint
    private const float SprintMaxSpeedMult = 1.4f; // 40% faster than base walk at full sprint
    private const float SprintMinTime = 0.3f; // must hold direction this long before sprint kicks in
    private int _sprintDir;

    // Rope climbing
    public bool IsOnRope { get; private set; }
    private float _ropeX; // X position of the rope we're attached to
    private float _ropeTopCurrent; // top of current rope
    private float _ropeBottomCurrent; // bottom of current rope
    private const float RopeClimbSpeed = 200f;
    private bool _ropeDropRequested;
    private float _lastDownTapTime;
    private bool _downWasUp_rope;
    private const float RopeDoubleTapWindow = 0.3f;
    private bool _ropeDisengaged; // true after dropping — stays true until player leaves rope hitbox

    // Wall climbing
    public bool IsOnWall { get; private set; }
    private Rectangle _currentWall;
    private int _currentWallClimbSide; // 1 = climbable on right, -1 = climbable on left
    private const float WallClimbSpeed = 180f;
    private bool _wallDisengaged;
    private float _wallHopCooldown; // brief cooldown before wall can re-grab
    private const float WallHopCooldownTime = 0.25f;

    // Wall vault (smooth climb-over animation)
    public bool IsVaulting { get; private set; }
    private float _vaultTimer;
    private const float VaultDuration = 0.3f;
    private Vector2 _vaultStart;
    private Vector2 _vaultEnd;

    // Aim direction
    public Vector2 AimDir { get; private set; }

    // Health
    public int MaxHp { get; set; } = 10;
    public int Hp { get; set; } = 10;
    public float DamageCooldown { get; set; }
    private const float DamageCooldownTime = 1.0f;
    private const float KnockbackSpeed = 200f;
    private const float KnockbackUpSpeed = -180f;
    private const float KnockbackDuration = 0.25f;
    private float _knockbackTimer;
    private float _regenDelay; // time since last damage
    private const float RegenStartDelay = 8.0f; // 8s before regen kicks in
    private float _regenAccum; // fractional HP accumulator
    private const float RegenRate = 0.25f; // HP per second (very slow — 4s per 1 HP)
    
    // Effect tile state
    public float SpeedBoostTimer { get; set; }
    private const float SpeedBoostDuration = 3.0f;
    private const float SpeedBoostMultiplier = 1.8f;
    public float FloatTimer { get; set; }
    private const float FloatDuration = 2.0f;
    private const float FloatLiftSpeed = -120f; // upward velocity

    public void TriggerKnockbackTimer(float duration)
    {
        _knockbackTimer = duration;
    }

    private void SetSquash(float scaleX, float scaleY)
    {
        _visualScale = new Vector2(scaleX, scaleY);
        _squashHoldTimer = 0.05f; // hold for 3 frames before lerping back
    }

    public void TakeDamage(int amount, float knockbackDirX = 0f)
    {
        if (DamageCooldown > 0) return;
        
        // Tech Brace: reduce damage and knockback
        float kbMult = 1f;
        if (IsBracing)
        {
            amount = Math.Max(1, (int)(amount * BraceDamageReduction));
            kbMult = BraceKnockbackReduction;
        }
        
        Hp -= amount;
        DamageCooldown = DamageCooldownTime;
        _regenDelay = 0f;
        _regenAccum = 0f;
        if (Hp <= 0) Hp = 0;
        
        // Knockback
        float kbX = knockbackDirX != 0f ? Math.Sign(knockbackDirX) * KnockbackSpeed * kbMult : -FacingDir * KnockbackSpeed * kbMult;
        var vel = Velocity;
        vel.X = kbX;
        vel.Y = KnockbackUpSpeed * kbMult;
        Velocity = vel;
        _knockbackTimer = KnockbackDuration;
        SetSquash(1.3f, 0.75f);
        
        // Cancel any active special states
        IsSliding = false;
        IsUppercutting = false;
        IsFlipping = false;
        IsBladeDashing = false;
        IsCartwheeling = false;
        IsVaultKicking = false;
        IsDashing = false;
    }

    /// <summary>True if currently in i-frames (use for flashing/transparency).</summary>
    public bool IsInvincible => DamageCooldown > 0;

    public void UpdateRegen(float dt)
    {
        if (Hp >= MaxHp || Hp <= 0) return;
        _regenDelay += dt;
        if (_regenDelay >= RegenStartDelay)
        {
            _regenAccum += RegenRate * dt;
            if (_regenAccum >= 1f)
            {
                int heal = (int)_regenAccum;
                Hp = Math.Min(Hp + heal, MaxHp);
                _regenAccum -= heal;
            }
        }
    }

    // Feature toggles (set by Game1 from settings menu)
    public bool EnableSlide { get; set; } = true;
    public bool EnableCartwheel { get; set; } = true;
    public bool EnableDash { get; set; } = true;
    public bool EnableDoubleJump { get; set; } = true;
    public bool EnableDropThrough { get; set; } = true;
    public bool EnableVaultKick { get; set; } = true;
    public bool EnableUppercut { get; set; } = true;
    public bool EnableSpinMelee { get; set; } = true;
    public bool EnableFlip { get; set; } = true;
    public bool EnableBladeDash { get; set; } = true;
    public bool EnableWallClimb { get; set; } = true;

    // Tech charged jump: hold Space on ground to charge, release to jump higher
    private float _chargeJumpTimer;
    private const float ChargeJumpMaxTime = 0.5f;     // max charge time
    private const float ChargeJumpMinForce = -350f;    // tap jump
    private const float ChargeJumpMaxForce = -720f;    // full charge (very high)
    private bool _chargingJump;

    // Weapon gating (set by Game1)
    public bool HasMeleeWeapon { get; set; }
    public bool HasRangedWeapon { get; set; }
    public int MeleeRangeOverride { get; set; } = MeleeRange;


    // Coyote time
    private float _coyoteTimer;
    private const float CoyoteTime = 0.1f;

    // Jump buffering
    private float _jumpBufferTimer;
    private const float JumpBufferTime = 0.1f;

    // Squash & stretch
    private Vector2 _visualScale = Vector2.One;
    private const float ScaleLerpSpeed = 5f;
    private float _squashHoldTimer; // hold squash before lerping back

    // Afterimage trail
    private struct Afterimage
    {
        public Vector2 Position;
        public int SpriteRow;
        public int SpriteFrame;
        public int FacingDir;
        public int DrawHeight; // height at time of spawn (for crouch/slide)
        public float Alpha;
        public float TimeLeft;
    }
    private const int MaxAfterimages = 8;
    private Afterimage[] _afterimages = new Afterimage[MaxAfterimages];
    private int _afterimageIndex;
    private float _afterimageSpawnTimer;
    private const float AfterimageSpawnRate = 0.03f;
    private const float AfterimageLifetime = 0.35f;

    private KeyboardState _prevKb;

    public Player(Vector2 startPos)
    {
        Position = startPos;
        Velocity = Vector2.Zero;
        IsGrounded = false;
        _wasGrounded = false;
        AimDir = new Vector2(1, 0);
        ApplyTierConstants();
    }

    // Debug getters for tier display
    public float GetTierSpeed() => _speed;
    public float GetTierAccel() => _runAccel;
    public float GetDashSpeed() => CurrentTier switch { MoveTier.Tech => TechDashSpeed, MoveTier.Bio => BioDashSpeed, _ => CipherDashSpeed };
    public float GetTierAirMult() => _airMult;
    public float GetTierJump() => _jumpForce;
    public bool IsChargingJump => _chargingJump;
    public float ChargeJumpProgress => _chargeJumpTimer / ChargeJumpMaxTime;

    // Wake-up / lying down state
    public bool IsLyingDown;        // true = flat on ground, no input
    public float StandUpProgress;   // 0 = lying flat, 1 = fully standing
    private float _standUpTimer;
    private const float StandUpDuration = 1.5f; // seconds to stand up

    // Injured state (post-crash, before EVE patch)
    public bool IsInjured;
    private float _limpTimer;
    private const float LimpCycleDuration = 0.6f; // one limp cycle
    private const float InjuredSpeedMult = 0.45f; // 45% normal speed
    
    /// <summary>Begin the stand-up animation from lying down.</summary>
    public void BeginStandUp()
    {
        _standUpTimer = 0f;
        StandUpProgress = 0f;
    }
    
    /// <summary>Update stand-up animation. Returns true when complete.</summary>
    public bool UpdateStandUp(float dt)
    {
        if (!IsLyingDown) return true;
        _standUpTimer += dt;
        // Ease-out curve for natural motion (slow at end)
        float t = Math.Min(1f, _standUpTimer / StandUpDuration);
        StandUpProgress = 1f - (1f - t) * (1f - t); // quadratic ease-out
        if (t >= 1f)
        {
            IsLyingDown = false;
            StandUpProgress = 1f;
            return true;
        }
        return false;
    }

    public int CurrentHeight => IsSliding ? SlideHeight : (IsCrouching ? CrouchHeight : Height);

    // === GRAPPLE SYSTEM ===
    public bool HasGrapple;                  // whether player has found the grapple module
    public bool IsGrappling { get; private set; }  // actively swinging on terrain
    public bool IsGrappleFiring { get; private set; } // hook in flight
    public bool IsGrappleRetracting { get; private set; } // hook flying back (visual only)
    public bool IsGrapplePulling { get; private set; } // pulling an enemy toward player (or vice versa)
    public Vector2 GrappleAnchor;            // world position of the hook point
    public Vector2 GrappleHookPos;           // current hook projectile position (during firing/retracting)
    public float GrappleRopeLength;          // current rope length
    
    // Enemy grapple state
    public int GrappleEnemyIndex = -1;       // DEPRECATED — use GrappleCreatureId
    public string GrappleEnemyType = "";     // DEPRECATED — use GrappleCreatureId
    public Guid GrappleCreatureId = Guid.Empty; // creature being pulled (Guid-based, safe across list changes)
    private const float GrapplePullSpeed = 400f;  // speed to pull small enemies toward player
    private const float GrappleEnemyDamage = 2f;  // damage on arrival
    
    // Player weight — affects grapple swing speed, fall speed with grapple, etc.
    // Base = 1.0. Heavier equipment increases this. Lighter bio upgrades decrease it.
    public float PlayerWeight = 1.0f;        // placeholder stat, modified by equipment later
    
    private Vector2 _grappleVel;             // player velocity while grappling (Cartesian, not angular)
    private bool _grappleJustReleased;       // flag: grapple released this frame, use its velocity
    private const float GrappleHookSpeed = 1000f;
    private const float GrappleMaxLength = 200f;    // ~6 tiles
    private const float GrappleReelSpeed = 200f;
    
    private const float GrappleGravityBase = 1300f;  // high gravity = heavy slingshot feel
    private const float GrappleDampingRate = 0.15f;   // minimal damping — preserve ALL momentum
    private const float GrappleMinLength = 24f;
    private const float GrappleReleaseBoost = 1.3f;
    private const float GrappleInputForce = 650f;   // left/right pump strength
    
    private Vector2 _grappleHookDir;
    private Vector2 _grappleHookVel;         // hook velocity (includes gravity for downward shots)
    private bool _prevEPressed;
    
    /// <summary>Fire grapple toward target.</summary>
    public bool FireGrapple(Vector2 target)
    {
        if (!HasGrapple || IsGrappling || IsGrappleFiring || IsGrappleRetracting) return false;
        IsGrappleFiring = true;
        var center = Position + new Vector2(Width / 2f, Height / 2f);
        var dir = target - center;
        float len = dir.Length();
        if (len < 1f) { IsGrappleFiring = false; return false; }
        dir /= len;
        GrappleHookPos = center;
        _grappleHookDir = dir;
        _grappleHookVel = dir * GrappleHookSpeed;
        return true;
    }
    
    /// <summary>Hook hit terrain — immediately start swinging (ESA-style).</summary>
    public void AttachGrapple(Vector2 anchor)
    {
        IsGrappleFiring = false;
        IsGrappling = true;
        IsGroundPounding = false;
        GrappleAnchor = anchor;
        
        var center = Position + new Vector2(Width / 2f, Height / 2f);
        GrappleRopeLength = Vector2.Distance(center, anchor);
        
        // Carry current velocity into the swing
        // Falling fast = big swing. Standing still = gentle dangle
        _grappleVel = Velocity;
    }
    
    /// <summary>Release grapple and keep velocity with a small boost.</summary>
    public void ReleaseGrapple()
    {
        if (!IsGrappling && !IsGrappleFiring && !IsGrapplePulling) return;
        
        if (IsGrappling)
        {
            Velocity = _grappleVel * GrappleReleaseBoost;
            if (Velocity.Y < -600f) Velocity = new Vector2(Velocity.X, -600f);
            _grappleJustReleased = true;
        }
        
        IsGrappling = false;
        IsGrappleFiring = false;
        IsGrapplePulling = false;
        GrappleEnemyIndex = -1;
        GrappleEnemyType = "";
        GrappleCreatureId = Guid.Empty;
    }
    
    /// <summary>Start pulling an enemy (hook hit an enemy, not terrain).</summary>
    public void GrappleEnemy(int enemyIndex, string enemyType, Vector2 enemyCenter, Guid creatureId = default)
    {
        IsGrappleFiring = false;
        IsGrapplePulling = true;
        GrappleEnemyIndex = enemyIndex;
        GrappleEnemyType = enemyType;
        GrappleCreatureId = creatureId;
        GrappleAnchor = enemyCenter; // track enemy pos (updated externally each frame)
    }
    
    /// <summary>Nudge grapple velocity (for terrain bounce during swing).</summary>
    public void GrappleNudgeVel(Vector2 nudge)
    {
        _grappleVel += nudge;
        Velocity = _grappleVel;
    }
    
    /// <summary>Cancel grapple firing — start retract animation.</summary>
    public void CancelGrappleFire()
    {
        IsGrappleFiring = false;
        IsGrappleRetracting = true;
    }
    
    /// <summary>Update retract animation — hook flies back to player.</summary>
    public void UpdateGrappleRetract(float dt)
    {
        if (!IsGrappleRetracting) return;
        var center = Position + new Vector2(Width / 2f, Height / 2f);
        var dir = center - GrappleHookPos;
        float dist = dir.Length();
        if (dist < 16f)
        {
            IsGrappleRetracting = false;
            return;
        }
        dir /= dist;
        GrappleHookPos += dir * GrappleHookSpeed * 1.5f * dt;
    }
    
    /// <summary>Raycast hook movement — returns steps along the path for Game1 to collision-check.</summary>
    public Vector2[] GetHookRaySteps(float dt)
    {
        if (!IsGrappleFiring) return Array.Empty<Vector2>();
        // Predict where hook will be after gravity is applied
        var velAfter = _grappleHookVel;
        velAfter.Y += GrappleGravityBase * 0.5f * dt; // half-step gravity for prediction
        float dist = velAfter.Length() * dt;
        int steps = Math.Max(1, (int)MathF.Ceiling(dist / 8f));
        var dir = velAfter;
        if (dir.Length() > 0.001f) dir = Vector2.Normalize(dir);
        var result = new Vector2[steps];
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 1f) / steps;
            result[i] = GrappleHookPos + velAfter * dt * t;
        }
        return result;
    }
    
    /// <summary>Advance hook position after raycast check passes.</summary>
    public void AdvanceHook(float dt)
    {
        if (!IsGrappleFiring) return;
        // Apply gravity to hook velocity (makes downward shots accelerate)
        _grappleHookVel.Y += GrappleGravityBase * 0.5f * dt;
        GrappleHookPos += _grappleHookVel * dt;
        // Also update direction for rendering consistency
        if (_grappleHookVel.Length() > 0.001f)
            _grappleHookDir = Vector2.Normalize(_grappleHookVel);
        var center = Position + new Vector2(Width / 2f, Height / 2f);
        if (Vector2.Distance(GrappleHookPos, center) > GrappleMaxLength)
        {
            CancelGrappleFire();
        }
    }
    
    /// <summary>
    /// Cartesian pendulum physics. Simpler and more correct than angular.
    /// Gravity + rope constraint (snap to circle + remove radial velocity).
    /// </summary>
    public void UpdateGrapple(float dt, KeyboardState kb)
    {
        if (!IsGrappling) return;
        
        // Reel in/out
        bool reelIn = kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up);
        bool reelOut = kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down);
        if (reelIn)
        {
            float newLen = MathF.Max(GrappleMinLength, GrappleRopeLength - GrappleReelSpeed * dt);
            // Don't reel so close that player overlaps the anchor tile
            // Check: would the new position be inside the anchor's tile?
            var diff0 = (Position + new Vector2(Width / 2f, Height / 2f)) - GrappleAnchor;
            float curDist = diff0.Length();
            if (curDist > 0.001f)
            {
                var radial = diff0 / curDist;
                var newCenter = GrappleAnchor + radial * newLen;
                // Stop reeling if new center is within 20px of anchor (inside terrain)
                if (newLen > 20f)
                    GrappleRopeLength = newLen;
            }
        }
        if (reelOut)
            GrappleRopeLength = MathF.Min(GrappleMaxLength, GrappleRopeLength + GrappleReelSpeed * dt);
        
        // Gravity (affected by player weight)
        // Higher gravity = more momentum preservation on swing
        _grappleVel.Y += GrappleGravityBase * PlayerWeight * dt;
        
        // Player input: left/right pumping
        float inputX = 0f;
        if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left)) inputX -= GrappleInputForce;
        if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) inputX += GrappleInputForce;
        _grappleVel.X += inputX * dt;
        
        // Very light damping — preserve momentum for slingshot feel
        float damp = MathF.Exp(-GrappleDampingRate * dt);
        _grappleVel *= damp;
        
        // Integrate position
        var center = Position + new Vector2(Width / 2f, Height / 2f);
        center += _grappleVel * dt;
        
        // === ROPE CONSTRAINT ===
        // If distance > rope length, snap to circle and remove outward radial velocity
        var diff = center - GrappleAnchor;
        float dist2 = diff.Length();
        if (dist2 > GrappleRopeLength && dist2 > 0.001f)
        {
            var radial = diff / dist2;
            center = GrappleAnchor + radial * GrappleRopeLength;
            float radialVel = Vector2.Dot(_grappleVel, radial);
            if (radialVel > 0)
                _grappleVel -= radial * radialVel;
        }
        
        Position = new Vector2(center.X - Width / 2f, center.Y - Height / 2f);
        Velocity = _grappleVel;
    }


    /// <summary>Check if there's headroom to expand to the given height at current position.</summary>
    private bool HasHeadroom(int targetHeight, Rectangle[] ceilings, Rectangle[] solidFloors, TileGrid tileGrid)
    {
        int currentH = CurrentHeight;
        if (targetHeight <= currentH) return true;

        // The top of the player if they stood to targetHeight (bottom-aligned)
        int newTop = (int)Position.Y + Height - targetHeight;
        int left = (int)Position.X + CollisionOffsetX;
        int right = left + CollisionWidth - 1;

        // Check ceiling rects
        if (ceilings != null)
        {
            var testRect = new Rectangle(left, newTop, CollisionWidth, targetHeight);
            foreach (var c in ceilings)
                if (testRect.Intersects(c)) return false;
        }
        if (solidFloors != null)
        {
            var testRect = new Rectangle(left, newTop, CollisionWidth, targetHeight);
            foreach (var sf in solidFloors)
                if (testRect.Intersects(sf)) return false;
        }

        // Check tile grid
        if (tileGrid != null)
        {
            int ts = tileGrid.TileSize;
            int ox = tileGrid.OriginX;
            int oy = tileGrid.OriginY;
            int topRow = (newTop - oy) / ts;
            int bottomRow = ((int)Position.Y + Height - 1 - oy) / ts;
            int leftCol = (left - ox) / ts;
            int rightCol = (right - ox) / ts;
            // Clamp negative divisions
            if (newTop < oy) topRow--;
            if (left < ox) leftCol--;
            for (int ty = topRow; ty <= bottomRow; ty++)
            {
                for (int tx = leftCol; tx <= rightCol; tx++)
                {
                    if (tx < 0 || tx >= tileGrid.Width || ty < 0 || ty >= tileGrid.Height) continue;
                    if (TileProperties.IsSolid(tileGrid.GetTileAt(tx, ty)))
                    {
                        // Check if this tile actually overlaps the new head area
                        int tileTop = oy + ty * ts;
                        int tileBottom = tileTop + ts;
                        if (tileBottom > newTop && tileTop < newTop + (Height - currentH))
                            return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Collision rect: narrower than sprite, centered horizontally.</summary>
    public Rectangle CollisionRect
    {
        get
        {
            int h = CurrentHeight;
            int y = (int)Position.Y + Height - h; // bottom-aligned
            return new Rectangle((int)Position.X + CollisionOffsetX, y, CollisionWidth, h);
        }
    }

    public Rectangle VaultKickHitbox
    {
        get
        {
            // Hitbox extends from player's feet in the kick direction
            int hbX = _vaultKickDir == 1 ? (int)Position.X + Width : (int)Position.X - VaultKickHitboxW;
            int hbY = (int)Position.Y + Height - VaultKickHitboxH - 4;
            return new Rectangle(hbX, hbY, VaultKickHitboxW, VaultKickHitboxH);
        }
    }

    public Rectangle UppercutHitbox
    {
        get
        {
            // Bounding box of both L-shape arms (backward compat)
            var boxes = UppercutHitboxes;
            return Rectangle.Union(boxes[0], boxes[1]);
        }
    }

    public Rectangle[] UppercutHitboxes
    {
        get
        {
            // Vertical arm (above head): 16w × 28h, centered horizontally
            int vX = (int)Position.X + (Width - 16) / 2;
            int vY = (int)Position.Y - 28;
            // Horizontal arm (body-height, forward): 24w × 20h
            int hX = FacingDir == 1 ? (int)Position.X + Width / 2 : (int)Position.X + Width / 2 - 24;
            int hY = (int)Position.Y + 8;
            return new Rectangle[]
            {
                new Rectangle(vX, vY, 16, 28),
                new Rectangle(hX, hY, 24, 20)
            };
        }
    }

    public void RegisterComboHit()
    {
        if (_comboStep >= 0 && _comboStep < _comboHit.Length)
            _comboHit[_comboStep] = true;
    }

    private void ResetCombo()
    {
        _comboStep = 0;
        _comboWindow = 0;
        _comboHit[0] = _comboHit[1] = _comboHit[2] = false;
    }

    public Rectangle BladeDashHitbox
    {
        get
        {
            int hbX = _bladeDashDir == 1 ? (int)Position.X + Width : (int)Position.X - BladeDashHitboxW;
            int hbY = (int)Position.Y + (Height - BladeDashHitboxH) / 2;
            return new Rectangle(hbX, hbY, BladeDashHitboxW, BladeDashHitboxH);
        }
    }

    public Rectangle MeleeHitbox
    {
        get
        {
            var center = Position + new Vector2(Width / 2f, Height / 2f);
            float range = MeleeRangeOverride;

            if (CurrentWeapon == WeaponType.None)
            {
                // Fist combo: SOTN-style rapid jabs (unchanged)
                int aimX = MathF.Abs(AimDir.X) > 0.1f ? Math.Sign(AimDir.X) : FacingDir;
                float aimY = AimDir.Y;
                int hw = 18, hh = 20;
                float extend = _comboStep == 0 ? 18f : 24f;
                float hbCenterX = center.X + aimX * extend;
                float hbCenterY = center.Y + aimY * 16f;
                return new Rectangle(
                    (int)(hbCenterX - hw / 2f), (int)(hbCenterY - hh / 2f), hw, hh);
            }

            if (CurrentWeapon == WeaponType.Knife)
            {
                // Combat knife: directional stab
                int dir = FacingDir; // always use facing direction, not aim
                
                int knifeW = 28;
                int knifeH = 12;
                
                // Extension animation: snap out fast, hold briefly, retract
                float kt = MeleeTimer > 0 ? 1f - (MeleeTimer / CurrentMeleeActiveTime) : 1f;
                float extend;
                if (kt < 0.15f)
                    extend = kt / 0.15f;
                else if (kt < 0.7f)
                    extend = 1f;
                else
                    extend = 1f - (kt - 0.7f) / 0.3f;
                
                // Combo stabs: each step extends slightly further
                float baseReach = 16f + _comboStep * 4f;
                float reachNow = baseReach * extend;
                
                // Determine attack type based on crouch state + input
                bool holdingDown = Keyboard.GetState().IsKeyDown(Keys.S) || Keyboard.GetState().IsKeyDown(Keys.Down);
                bool holdingHoriz = Keyboard.GetState().IsKeyDown(Keys.A) || Keyboard.GetState().IsKeyDown(Keys.D)
                                 || Keyboard.GetState().IsKeyDown(Keys.Left) || Keyboard.GetState().IsKeyDown(Keys.Right);
                
                float hbX, hbY;
                if (IsCrouching && holdingDown && holdingHoriz)
                {
                    // Angled downward stab (crouch + down-left/down-right)
                    float diagReach = reachNow * 0.7071f; // cos(45°)
                    hbX = center.X + dir * (Width / 2f + diagReach);
                    hbY = center.Y + diagReach + 4f; // below + forward
                    MeleeSwingAngle = dir > 0 ? MathF.PI / 4f : MathF.PI * 3f / 4f;
                }
                else if (IsCrouching)
                {
                    // Crouch stab: same reach, lower position
                    hbX = center.X + dir * (Width / 2f + reachNow);
                    hbY = center.Y + 8f; // lower (knee height)
                    MeleeSwingAngle = dir > 0 ? 0f : MathF.PI;
                }
                else
                {
                    // Standing forward stab
                    hbX = center.X + dir * (Width / 2f + reachNow);
                    hbY = center.Y - 2f; // chest height
                    MeleeSwingAngle = dir > 0 ? 0f : MathF.PI;
                }
                
                return new Rectangle(
                    (int)(hbX - knifeW / 2f), (int)(hbY - knifeH / 2f), knifeW, knifeH);
            }

            // Tier-aware melee styles
            float t = MeleeTimer > 0 ? 1f - (MeleeTimer / CurrentMeleeActiveTime) : 1f;
            float baseAngle = MathF.Atan2(MeleeDirection.Y, MeleeDirection.X);

            if (CurrentTier == MoveTier.Tech)
            {
                // TECH: Forward thrust/stab (SOTN style) — small arc, forward-biased
                float eased = t; // linear, snappy
                float startAngle = baseAngle - MathF.PI * 0.15f; // slight upward start
                float endAngle = baseAngle + MathF.PI * 0.2f;    // ends slightly downward (72° total)
                float angle = startAngle + (endAngle - startAngle) * eased;
                MeleeSwingAngle = angle;
                // Long narrow hitbox (thrust)
                int sw = 14, sh = 32;
                float hbX = center.X + MathF.Cos(angle) * range * 0.65f;
                float hbY = center.Y + MathF.Sin(angle) * range * 0.65f;
                return new Rectangle(
                    (int)(hbX - sw / 2f), (int)(hbY - sh / 2f), sw, sh);
            }
            else if (CurrentTier == MoveTier.Bio)
            {
                // BIO: Overhead arc swing (Terraria style) — wide sweeping arc
                float eased = t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
                float startAngle = baseAngle - MathF.PI * 0.6f;
                float endAngle = baseAngle + MathF.PI * 0.6f; // 216° total
                float angle = startAngle + (endAngle - startAngle) * eased;
                MeleeSwingAngle = angle;
                int sw = 28, sh = 28;
                float hbX = center.X + MathF.Cos(angle) * range * 0.55f;
                float hbY = center.Y + MathF.Sin(angle) * range * 0.55f;
                return new Rectangle(
                    (int)(hbX - sw / 2f), (int)(hbY - sh / 2f), sw, sh);
            }
            else
            {
                // CIPHER: Fast multi-hit slash — two quick arcs (there and back)
                float pingPong = t < 0.5f ? t * 2f : 2f - t * 2f; // 0→1→0
                float eased = pingPong;
                float startAngle = baseAngle - MathF.PI * 0.5f;
                float endAngle = baseAngle + MathF.PI * 0.5f; // 180° sweep, twice
                float angle = startAngle + (endAngle - startAngle) * eased;
                MeleeSwingAngle = angle;
                int sw = 24, sh = 24;
                float hbX = center.X + MathF.Cos(angle) * range * 0.6f;
                float hbY = center.Y + MathF.Sin(angle) * range * 0.6f;
                return new Rectangle(
                    (int)(hbX - sw / 2f), (int)(hbY - sh / 2f), sw, sh);
            }
        }
    }

    public void Update(float dt, KeyboardState kb, float floorY, Rectangle[] platforms, float[] ropeXPositions = null, float[] ropeTops = null, float[] ropeBottoms = null, Rectangle[] walls = null, int[] wallClimbSides = null, Rectangle[] solidWalls = null, Rectangle[] ceilings = null, Rectangle[] solidFloors = null, TileGrid tileGrid = null, Vector2? mouseWorldPos = null, MouseState? mouseState = null)
    {
        WantsToShoot = false;
        WantsToMelee = false;
        WantsDropThrough = false;
        _shootCooldown -= dt;
        _meleeCooldown -= dt;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (SpeedBoostTimer > 0) SpeedBoostTimer -= dt;
        if (FloatTimer > 0) FloatTimer -= dt;
        _slideCooldownTimer -= dt;
        _cartwheelCooldownTimer -= dt;
        if (IsInjured) _limpTimer += dt;
        if (MeleeTimer > 0) MeleeTimer -= dt;

        // Combo timers
        if (_comboCooldown > 0) _comboCooldown -= dt;
        if (_comboWindow > 0)
        {
            _comboWindow -= dt;
            if (_comboWindow <= 0)
            {
                // Combo window expired — reset combo and start cooldown
                float cd = CurrentWeapon == WeaponType.Stick ? 0.35f :
                           CurrentWeapon == WeaponType.Knife ? 0.18f : 0.25f;
                _comboCooldown = cd;
                ResetCombo();
            }
        }

        bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);

        // --- Rope attachment check ---
        if (!IsOnRope && ropeXPositions != null)
        {
            float playerCenterX = Position.X + Width / 2f;
            float playerCenterY = Position.Y + Height / 2f;
            bool nearAnyRope = false;
            for (int i = 0; i < ropeXPositions.Length; i++)
            {
                float rx = ropeXPositions[i];
                float rt = ropeTops != null ? ropeTops[i] : 0f;
                float rb = ropeBottoms != null ? ropeBottoms[i] : 550f;
                if (MathF.Abs(playerCenterX - rx) < 16f && playerCenterY >= rt && playerCenterY <= rb)
                {
                    nearAnyRope = true;
                    if (!_ropeDisengaged && !IsSliding && !IsDashing && !IsVaultKicking && !IsBladeDashing && !IsCartwheeling && !IsUppercutting && !IsFlipping && !IsGrappling && !IsGrappleFiring && !IsGrappleRetracting)
                    {
                        IsOnRope = true;
                        _ropeX = rx;
                        _ropeTopCurrent = rt;
                        _ropeBottomCurrent = rb;
                        _ropeDropRequested = false;
                        _downWasUp_rope = true;
                        break;
                    }
                }
            }
            // Clear disengage flag once player has left ALL rope hitboxes
            if (!nearAnyRope) _ropeDisengaged = false;
        }

        // --- Raw directional input ---
        int inputX = 0;
        int inputY = 0;
        if (kb.IsKeyDown(Keys.A)) inputX -= 1;
        if (kb.IsKeyDown(Keys.D)) inputX += 1;
        if (kb.IsKeyDown(Keys.W)) inputY -= 1;
        if (kb.IsKeyDown(Keys.S)) inputY += 1;

        // --- Wall attachment check ---
        _wallHopCooldown -= dt;
        if (!IsOnWall && !IsOnRope && walls != null && EnableWallClimb && !IsSliding && _wallHopCooldown <= 0f)
        {
            float playerCenterY = Position.Y + Height / 2f;
            bool nearAnyWall = false;
            for (int i = 0; i < walls.Length; i++)
            {
                var w = walls[i];
                int side = wallClimbSides[i];
                // climbSide: 1=right face, -1=left face, 0=both sides, 99=no climb
                if (side == 99) continue;
                // Determine effective side based on player position relative to wall
                int effectiveSide = side;
                if (side == 0)
                {
                    float wallCenter = w.Left + w.Width / 2f;
                    float playerCenter = Position.X + Width / 2f;
                    // Player to the left of wall → they approach the left face (side=-1, face away = right)
                    // Player to the right → they approach the right face (side=1, face away = left)
                    effectiveSide = playerCenter < wallCenter ? -1 : 1;
                }
                float climbX = effectiveSide == 1 ? w.Right : w.Left;
                float playerEdge = effectiveSide == 1 ? Position.X : Position.X + Width;
                bool xTouch = MathF.Abs(playerEdge - climbX) < 6f;
                bool yInRange = playerCenterY >= w.Top && playerCenterY <= w.Bottom;
                // Require pressing toward wall to attach (not automatic after hop)
                bool pressingToward = inputX == -effectiveSide;
                bool groundedNear = _wasGrounded;
                if (xTouch && yInRange && (pressingToward || groundedNear))
                {
                    nearAnyWall = true;
                    if (!_wallDisengaged)
                    {
                        IsOnWall = true;
                        IsDashing = false; // cancel dash on wall grab
                        IsVaultKicking = false; // cancel vault kick on wall grab
                        IsBladeDashing = false; // cancel blade dash on wall grab
                        IsCrouching = false; // clear crouch from slide/vault kick
                        _visualScale = Vector2.One; // reset squash/stretch
                        _squashHoldTimer = 0;
                        _currentWall = w;
                        _currentWallClimbSide = effectiveSide;
                        FacingDir = effectiveSide; // face away from wall
                        break;
                    }
                }
            }
            if (!nearAnyWall) _wallDisengaged = false;
        }
        else if (IsOnWall)
        {
            // Check if still near wall
            float playerEdge = _currentWallClimbSide == 1 ? Position.X : Position.X + Width;
            float climbX = _currentWallClimbSide == 1 ? _currentWall.Right : _currentWall.Left;
            if (MathF.Abs(playerEdge - climbX) > 10f)
            {
                IsOnWall = false;
                _wallDisengaged = false;
            }
        }

        // --- Rope double-tap S to drop ---
        if (IsOnRope)
        {
            bool sDownRope = kb.IsKeyDown(Keys.S);
            if (!sDownRope) _downWasUp_rope = true;
            if (sDownRope && _downWasUp_rope)
            {
                _downWasUp_rope = false;
                float now = (float)System.DateTime.UtcNow.TimeOfDay.TotalSeconds;
                if (now - _lastDownTapTime < RopeDoubleTapWindow)
                    _ropeDropRequested = true;
                _lastDownTapTime = now;
            }
        }

        // (raw input moved up, was here)

        // --- Dash detection (double-tap A or D) — burst dash, tier-flavored ---
        _dashCooldownTimer = Math.Max(0, _dashCooldownTimer - dt);
        if (!EnableDash) IsDashing = false;
        else
        {
        bool aDown = kb.IsKeyDown(Keys.A);
        bool dDown = kb.IsKeyDown(Keys.D);
        float now_dash = (float)System.DateTime.UtcNow.TimeOfDay.TotalSeconds;
        if (!aDown) _aWasUp = true;
        if (aDown && _aWasUp)
        {
            _aWasUp = false;
            bool canDash = _dashCooldownTimer <= 0 && (CurrentTier == MoveTier.Cipher || _wasGrounded);
            if (now_dash - _lastATapTime < DashDoubleTapWindow && canDash)
            { IsDashing = true; _dashDir = -1; _dashTimer = 0; _dashCooldownTimer = DashCooldown; SetSquash(1.25f, 0.8f); }
            _lastATapTime = now_dash;
        }
        if (!dDown) _dWasUp = true;
        if (dDown && _dWasUp)
        {
            _dWasUp = false;
            bool canDash = _dashCooldownTimer <= 0 && (CurrentTier == MoveTier.Cipher || _wasGrounded);
            if (now_dash - _lastDTapTime < DashDoubleTapWindow && canDash)
            { IsDashing = true; _dashDir = 1; _dashTimer = 0; _dashCooldownTimer = DashCooldown; SetSquash(1.25f, 0.8f); }
            _lastDTapTime = now_dash;
        }
        if (IsDashing)
        {
            float dashDur = CurrentTier switch { MoveTier.Tech => TechDashDuration, MoveTier.Bio => BioDashDuration, _ => CipherDashDuration };
            _dashTimer += dt;
            if (_dashTimer >= dashDur || inputX == -_dashDir)
            {
                IsDashing = false;
                // Tech: dash ends into sprint if still holding direction
                if (CurrentTier == MoveTier.Tech && inputX == _dashDir && IsGrounded)
                {
                    IsSprinting = true;
                    _sprintDir = _dashDir;
                }
            }
        }
        }

        // --- Crouch (shift while grounded, not sliding) ---
        bool wantsCrouch = shift && (_wasGrounded || IsOnRope || IsOnWall) && !IsSliding;
        if (wantsCrouch)
            IsCrouching = true;
        else if (IsCrouching)
        {
            // Releasing crouch — check if there's headroom to stand
            if (HasHeadroom(Height, ceilings, solidFloors, tileGrid))
                IsCrouching = false;
            // else stay crouched until there's room
        }

        // --- Drop through platform (double-tap S only) ---
        _dropIgnoreTimer -= dt;

        // --- Tech Brace (hold crouch while grounded in Tech tier) ---
        if (CurrentTier == MoveTier.Tech && IsCrouching && IsGrounded && inputX == 0)
        {
            IsBracing = true;
            _braceTimer += dt;
        }
        else
        {
            IsBracing = false;
            _braceTimer = 0f;
        }
        bool sDown = kb.IsKeyDown(Keys.S);
        if (!sDown) _sWasUp = true;
        if (sDown && _sWasUp)
        {
            _sWasUp = false;
            float now = (float)System.DateTime.UtcNow.TimeOfDay.TotalSeconds;
            if (now - _lastSTapTime < DoubleTapWindow && _wasGrounded)
                _dropIgnoreTimer = DropIgnoreTime;
            _lastSTapTime = now;
        }
        WantsDropThrough = EnableDropThrough && _dropIgnoreTimer > 0f;

        // --- Slide (S + Space while grounded, OR Shift + Space with no direction from crouch, OR Space when stuck crouching) ---
        bool spacePressed = kb.IsKeyDown(Keys.Space);
        bool stuckCrouching = IsCrouching && !HasHeadroom(Height, ceilings, solidFloors, tileGrid);
        bool wantsCrouchSlide = IsCrouching && inputX == 0;
        bool wantsSlide = (inputY > 0 && !IsCrouching) || wantsCrouchSlide || stuckCrouching;
        if (EnableSlide && wantsSlide && spacePressed && !_jumpHeld && _wasGrounded && !IsSliding && !IsCartwheeling && _slideCooldownTimer <= 0f)
        {
            IsSliding = true;
            _slideTimer = SlideDuration;
            _slideCooldownTimer = SlideCooldown;
            SetSquash(1.4f, 0.7f);
            _slideDir = FacingDir;
        }

        // --- Cartwheel (Shift + A/D + Space while grounded) ---
        if (EnableCartwheel && shift && inputX != 0 && spacePressed && !_jumpHeld && _wasGrounded && !IsSliding && !IsCartwheeling && _cartwheelCooldownTimer <= 0f)
        {
            IsCartwheeling = true;
            _cartwheelTimer = CartwheelDuration;
            _cartwheelCooldownTimer = CartwheelCooldown;
            _cartwheelDir = inputX;
            IsCrouching = false; // override crouch
            SetSquash(1.2f, 0.85f);
        }

        // --- Uppercut input detection (down → up + jump) ---
        if (EnableUppercut)
        {
            // Track: pressing S sets the input ready, then W+Space within window triggers it
            if (inputY > 0 || IsSliding || IsCartwheeling || IsVaultKicking) // pressing down, or already in a down-motion move
            {
                _uppercutInputReady = true;
                _uppercutInputWindow = UppercutInputWindowTime;
            }
            if (_uppercutInputReady)
            {
                _uppercutInputWindow -= dt;
                if (_uppercutInputWindow <= 0) _uppercutInputReady = false;
            }
            if (_uppercutInputReady && inputY < 0 && spacePressed && !_jumpHeld && !IsUppercutting
                && HasHeadroom(Height, ceilings, solidFloors, tileGrid))
            {
                _uppercutInputReady = false;
                IsUppercutting = true;
                _uppercutTimer = UppercutDuration;
                SetSquash(0.65f, 1.4f);
                // Cancel any active move
                IsSliding = false;
                IsCartwheeling = false;
                IsVaultKicking = false;
                IsCrouching = false;
                // Detach from wall if on one
                if (IsOnWall)
                {
                    IsOnWall = false;
                    _wallDisengaged = true;
                }
                _jumpHeld = true;
            }
        }

        // --- Blade dash QCF detection (↓↘→+K, mirrored when facing left) ---
        if (EnableBladeDash && !IsBladeDashing && _meleeHoldTimer < SpinMeleeActivateTime)
        {
            bool kDown = kb.IsKeyDown(Keys.K);
            int fwd = FacingDir; // +1 right, -1 left
            bool pressingDown = inputY > 0;
            bool pressingFwd = inputX == fwd;
            bool pressingDiag = pressingDown && pressingFwd;

            if (_qcfStage > 0)
            {
                _qcfTimer -= dt;
                if (_qcfTimer <= 0) _qcfStage = 0; // timed out
            }

            if (_qcfStage == 0 && pressingDown && !pressingFwd)
            {
                _qcfStage = 1;
                _qcfTimer = QcfInputWindow;
            }
            else if (_qcfStage == 1 && pressingDiag)
            {
                _qcfStage = 2;
            }
            else if (_qcfStage == 2 && pressingFwd && !pressingDown && kDown)
            if (HasHeadroom(Height, ceilings, solidFloors, tileGrid))
            {
                // QCF + K complete!
                _qcfStage = 0;
                IsBladeDashing = true;
                _bladeDashTimer = BladeDashDuration;
                _bladeDashDir = fwd;
                SetSquash(1.4f, 0.7f);
                // Cancel other states
                IsSliding = false;
                IsCartwheeling = false;
                IsVaultKicking = false;
                IsCrouching = false;
            }
        }
        else if (IsBladeDashing)
        {
            _qcfStage = 0;
        }

        // --- Facing direction ---
        if (inputX != 0 && !IsSliding) FacingDir = inputX;

        // --- Aim direction (mouse-based) ---
        if (mouseWorldPos.HasValue)
        {
            var center = Position + new Vector2(Width / 2f, Height / 2f);
            var toMouse = mouseWorldPos.Value - center;
            if (toMouse.LengthSquared() > 4f) // deadzone
            {
                toMouse.Normalize();
                AimDir = toMouse;
            }
        }
        else
        {
            // Fallback: keyboard aim
            var aim = Vector2.Zero;
            aim.X = inputX != 0 ? inputX : FacingDir;
            if (inputY != 0) aim.Y = inputY;
            if (aim != Vector2.Zero) aim.Normalize();
            AimDir = aim;
        }

        var vel = Velocity;

        if (IsOnRope && !_ropeDropRequested)
        {
            // On rope: no gravity, free vertical movement, can crouch to stay still
            vel.Y = 0;
            vel.X = 0;

            if (IsCrouching)
            {
                // Stationary on rope — all normal actions available (shoot, melee, etc.)
            }
            else
            {
                if (inputY != 0) vel.Y = inputY * RopeClimbSpeed;
                if (inputX != 0) FacingDir = inputX;
            }

            bool spaceRope = kb.IsKeyDown(Keys.Space);
            bool spaceFresh = spaceRope && !_jumpHeld; // only on new press

            // Cartwheel off rope (Shift + direction + Space) — check first since it's more specific
            if (EnableCartwheel && shift && inputX != 0 && spaceFresh && _cartwheelCooldownTimer <= 0f)
            {
                IsOnRope = false;
                _ropeDisengaged = true;
                IsCartwheeling = true;
                _cartwheelTimer = CartwheelDuration;
                _cartwheelCooldownTimer = CartwheelCooldown;
                _cartwheelDir = inputX;
                IsCrouching = false;
                _jumpHeld = true;
                _jumpsLeft = MaxJumps - 1; // one jump used for cartwheel launch
            }
            // Jump off rope
            else if (spaceFresh)
            {
                IsOnRope = false;
                _ropeDisengaged = true;
                vel.Y = _jumpForce;
                if (inputX != 0) vel.X = inputX * _speed;
                _jumpsLeft = MaxJumps - 1;
                SetSquash(0.7f, 1.3f);
                IsGrounded = false;
                _wasGrounded = false;
                _jumpHeld = true;
            }

            // Track space held state even if we didn't jump
            _jumpHeld = spaceRope;

            // Snap X to rope
            var posRope = Position;
            posRope.X = _ropeX - Width / 2f;
            posRope.Y = MathHelper.Clamp(Position.Y + vel.Y * dt, _ropeTopCurrent, _ropeBottomCurrent - Height);
            Position = posRope;
            Velocity = vel;

            // Ranged/melee still available on rope
            bool jOnRope = kb.IsKeyDown(Keys.J);
            if (HasRangedWeapon && jOnRope && !_shootHeld && _shootCooldown <= 0f)
            {
                _shootCooldown = ShootRate;
                WantsToShoot = true;
                ShootDirection = AimDir;
            }
            _shootHeld = jOnRope;

            bool kOnRope = kb.IsKeyDown(Keys.K);
            if (HasMeleeWeapon && kOnRope && !_meleeHeld && _meleeCooldown <= 0f)
            {
                _meleeCooldown = CurrentMeleeRate;
                WantsToMelee = true;
                MeleeDirection = AimDir;
                MeleeTimer = CurrentMeleeActiveTime;
            }
            _meleeHeld = kOnRope;

            // Decay afterimages even while on rope
            for (int i = 0; i < MaxAfterimages; i++)
            {
                if (_afterimages[i].TimeLeft > 0)
                {
                    _afterimages[i].TimeLeft -= dt;
                    _afterimages[i].Alpha = 0.8f * (_afterimages[i].TimeLeft / AfterimageLifetime);
                }
            }

            _prevKb = kb;
            return; // skip normal physics
        }
        else if (IsOnRope && _ropeDropRequested)
        {
            IsOnRope = false;
            _ropeDropRequested = false;
            _ropeDisengaged = true;
            vel.Y = 0;
            _jumpsLeft = MaxJumps;
        }

        if (IsVaulting)
        {
            _vaultTimer -= dt;
            float t = 1f - (_vaultTimer / VaultDuration); // 0→1
            float arcY = -MathF.Sin(t * MathF.PI) * 20f; // arc height
            var vPos = Vector2.Lerp(_vaultStart, _vaultEnd, t);
            vPos.Y += arcY;
            Position = vPos;
            Velocity = Vector2.Zero;

            if (_vaultTimer <= 0)
            {
                IsVaulting = false;
                Position = _vaultEnd;
                Velocity = Vector2.Zero;
                IsGrounded = true;
                _wasGrounded = true;
                _jumpsLeft = MaxJumps;
            }
            // Decay afterimages during vault
            for (int i = 0; i < MaxAfterimages; i++)
            {
                if (_afterimages[i].TimeLeft > 0)
                {
                    _afterimages[i].TimeLeft -= dt;
                    _afterimages[i].Alpha = 0.8f * (_afterimages[i].TimeLeft / AfterimageLifetime);
                }
            }
            _prevKb = kb;
            return;
        }

        if (IsOnWall)
        {
            vel.Y = 0;
            vel.X = 0;

            if (IsCrouching)
            {
                // Stationary on wall
            }
            else
            {
                if (inputY != 0) vel.Y = inputY * WallClimbSpeed;
            }

            // Block climbing up through solid floors / platforms
            if (vel.Y < 0 && solidFloors != null)
            {
                foreach (var sf in solidFloors)
                {
                    if (Position.X + Width > sf.X && Position.X < sf.X + sf.Width &&
                        Position.Y >= sf.Y + sf.Height && Position.Y + vel.Y * dt < sf.Y + sf.Height)
                    {
                        Position = new Vector2(Position.X, sf.Y + sf.Height);
                        vel.Y = 0;
                        break;
                    }
                }
            }

            // Drop off bottom of wall (holding S at the bottom edge OR reaching floor)
            float wallClimbBottom = MathF.Min(_currentWall.Bottom, floorY) - Height;
            if (Position.Y >= wallClimbBottom - 2)
            {
                Position = new Vector2(Position.X, wallClimbBottom);
            }
            if (inputY > 0 && Position.Y >= wallClimbBottom - 2)
            {
                IsOnWall = false;
                _wallDisengaged = true;
                _jumpsLeft = MaxJumps;
                Velocity = Vector2.Zero;
            }

            // Vault onto top of wall (press W at the very top edge)
            if (inputY < 0 && Position.Y <= _currentWall.Top + 5)
            {
                IsOnWall = false;
                IsVaulting = true;
                _wallDisengaged = true;
                _vaultTimer = VaultDuration;
                _vaultStart = Position;
                // Land on top of the wall, centered
                _vaultEnd = new Vector2(
                    MathHelper.Clamp(Position.X, _currentWall.Left, _currentWall.Right - Width),
                    _currentWall.Top - Height);
                _prevKb = kb;
                return;
            }

            bool spaceWall = kb.IsKeyDown(Keys.Space);
            bool spaceFreshWall = spaceWall && !_jumpHeld;

            // Cartwheel off wall (Shift + direction + Space)
            if (EnableCartwheel && shift && inputX != 0 && inputX == _currentWallClimbSide && spaceFreshWall && _cartwheelCooldownTimer <= 0f)
            {
                IsOnWall = false;
                _wallDisengaged = true;
                IsCartwheeling = true;
                _cartwheelTimer = CartwheelDuration;
                _cartwheelCooldownTimer = CartwheelCooldown;
                _cartwheelDir = inputX;
                IsCrouching = false;
                _jumpHeld = true;
                _jumpsLeft = MaxJumps - 1;
            }
            // Jump off wall — press away to detach, otherwise wall hop (pop up, stay on)
            else if (spaceFreshWall)
            {
                if (inputX == _currentWallClimbSide)
                {
                    // Jumping away from wall — detach
                    IsOnWall = false;
                    _wallDisengaged = true;
                    vel.Y = _jumpForce;
                    vel.X = _currentWallClimbSide * _speed;
                    _jumpsLeft = MaxJumps - 1;
                    SetSquash(0.7f, 1.3f);
                    IsGrounded = false;
                    _wasGrounded = false;
                }
                else
                {
                    // Wall hop — launch up, reattach by pressing toward wall
                    IsOnWall = false;
                    _wallHopCooldown = WallHopCooldownTime;
                    vel.Y = _jumpForce * 0.7f;
                    vel.X = 0;
                    _jumpsLeft = 0;
                    IsGrounded = false;
                    _wasGrounded = false;
                }
                _jumpHeld = true;
            }

            _jumpHeld = spaceWall;

            // Aim: tri-directional away from wall
            {
                int awayDir = _currentWallClimbSide;
                var aim = new Vector2(awayDir, 0);
                if (inputY != 0 && !IsCrouching) aim.Y = inputY;
                else if (IsCrouching)
                {
                    if (inputY != 0) aim.Y = inputY;
                    // Can also aim straight away
                }
                aim.Normalize();
                AimDir = aim;
            }

            // Snap X to wall face
            var posWall = Position;
            if (_currentWallClimbSide == 1)
                posWall.X = _currentWall.Right;
            else
                posWall.X = _currentWall.Left - Width;
            posWall.Y = MathHelper.Clamp(Position.Y + vel.Y * dt, _currentWall.Top, _currentWall.Bottom - Height);
            Position = posWall;
            Velocity = vel;

            // Combat on wall
            bool jOnWall = kb.IsKeyDown(Keys.J);
            if (HasRangedWeapon && jOnWall && !_shootHeld && _shootCooldown <= 0f)
            {
                _shootCooldown = ShootRate;
                WantsToShoot = true;
                ShootDirection = AimDir;
            }
            _shootHeld = jOnWall;

            bool kOnWall = kb.IsKeyDown(Keys.K);
            if (HasMeleeWeapon && kOnWall && !_meleeHeld && _meleeCooldown <= 0f)
            {
                _meleeCooldown = CurrentMeleeRate;
                WantsToMelee = true;
                MeleeDirection = AimDir;
                MeleeTimer = CurrentMeleeActiveTime;
            }
            _meleeHeld = kOnWall;

            // Decay afterimages even while on wall
            for (int i = 0; i < MaxAfterimages; i++)
            {
                if (_afterimages[i].TimeLeft > 0)
                {
                    _afterimages[i].TimeLeft -= dt;
                    _afterimages[i].Alpha = 0.8f * (_afterimages[i].TimeLeft / AfterimageLifetime);
                }
            }

            _prevKb = kb;
            return;
        }

        if (IsCartwheeling)
        {
            _cartwheelTimer -= dt;
            float t = 1f - (_cartwheelTimer / CartwheelDuration);
            vel.X = _cartwheelDir * CartwheelSpeed;
            if (t < 0.15f) vel.Y = CartwheelJumpForce; // initial pop
            else vel.Y += _gravity * (vel.Y > 0 ? _fallGravMultiplier : 1f) * dt;
            if (_cartwheelTimer <= 0)
            {
                IsCartwheeling = false;
                vel.X = _cartwheelDir * _speed;
            }
        }
        else if (IsVaultKicking)
        {
            _vaultKickTimer -= dt;
            vel.X = _vaultKickDir * VaultKickSpeed;
            vel.Y += _gravity * (vel.Y > 0 ? _fallGravMultiplier : 1f) * dt;
            _jumpHeld = kb.IsKeyDown(Keys.Space); // track space so uppercut can trigger
            if (_vaultKickTimer <= 0)
            {
                IsVaultKicking = false;
                IsCrouching = false;
                vel.X = _vaultKickDir * _speed; // return to normal speed
            }
        }
        else if (IsUppercutting)
        {
            _uppercutTimer -= dt;
            if (_uppercutTimer > UppercutDuration - 0.05f)
            {
                // Initial burst frame
                vel.Y = UppercutJumpForce;
                vel.X = inputX * UppercutHSpeed;
            }
            else
            {
                vel.Y += _gravity * (vel.Y > 0 ? _fallGravMultiplier : 1f) * dt;
                vel.X = inputX * UppercutHSpeed;
            }
            IsGrounded = false;
            _wasGrounded = false;
            if (_uppercutTimer <= 0)
            {
                IsUppercutting = false;
                _jumpsLeft = 0; // no double jump after uppercut — it IS the double jump substitute
            }
        }
        else if (IsFlipping)
        {
            _flipTimer -= dt;
            vel.X = _flipDir * FlipSpeed;
            vel.Y += _gravity * (vel.Y > 0 ? _fallGravMultiplier : 1f) * dt;
            if (_flipTimer <= 0)
            {
                IsFlipping = false;
                vel.X = inputX * _speed; // return to normal
            }
        }
        else if (IsBladeDashing)
        {
            _bladeDashTimer -= dt;
            vel.X = _bladeDashDir * BladeDashSpeed;
            vel.Y = BladeDashJumpForce; // slight hover
            if (_bladeDashTimer <= 0)
            {
                IsBladeDashing = false;
                // Chain into dash automatically
                IsDashing = true;
                _dashDir = _bladeDashDir;
                vel.X = _bladeDashDir * GetDashSpeed();
            }
        }
        else if (IsSliding)
        {
            _slideTimer -= dt;
            float elapsed = SlideDuration - _slideTimer;
            // Vault kick: jump during slide (after minimum slide time, requires fresh space press)
            bool spaceSlide = kb.IsKeyDown(Keys.Space);
            if (EnableVaultKick && spaceSlide && !_jumpHeld && elapsed >= VaultKickMinSlideTime
                && HasHeadroom(Height, ceilings, solidFloors, tileGrid))
            {
                IsSliding = false;
                IsVaultKicking = true;
                _vaultKickTimer = VaultKickDuration;
                _vaultKickDir = _slideDir;
                SetSquash(1.3f, 0.75f);
                vel.X = _vaultKickDir * VaultKickSpeed;
                vel.Y = VaultKickJumpForce;
                IsGrounded = false;
                _wasGrounded = false;
                _jumpsLeft = MaxJumps - 1;
                _jumpHeld = true;
            }
            else
            {
                _jumpHeld = spaceSlide; // track space held state during slide
                // Normal slide deceleration
                float t = 1f - (_slideTimer / SlideDuration);
                float speed = MathHelper.Lerp(SlideStartSpeed, SlideEndSpeed, t);
                vel.X = _slideDir * speed;
                vel.Y = 0;
                if (_slideTimer <= 0)
                {
                    // Check if there's headroom to stand up
                    if (HasHeadroom(Height, ceilings, solidFloors, tileGrid))
                    {
                        IsSliding = false;
                        vel.X = _slideDir * SlideEndSpeed;
                    }
                    else if (HasHeadroom(CrouchHeight, ceilings, solidFloors, tileGrid))
                    {
                        // Can't stand, but can crouch — force crouch
                        IsSliding = false;
                        IsCrouching = true;
                        vel.X = 0;
                    }
                    else
                    {
                        // Can't even crouch — keep sliding (extend timer)
                        _slideTimer = 0.05f; // re-check shortly
                    }
                }
            }
        }
        else if (IsCrouching)
        {
            vel.X = 0;
            vel.Y = 0;
            // Track space so vault kick/jump works properly after crouch-slide
            _jumpHeld = kb.IsKeyDown(Keys.Space);
        }
        else if (_ceilDeflectTimer > 0)
        {
            _ceilDeflectTimer -= dt;
            // Gradually blend deflection momentum with input (drag toward input speed)
            float drag = 1f - dt * 3f; // ~3x per second decay
            _ceilDeflectVelX *= drag;
            vel.X = _ceilDeflectVelX + inputX * _speed * 0.3f;
            vel.Y += _gravity * (vel.Y > 0 ? _fallGravMultiplier : 1f) * dt;
            if (IsGrounded) _ceilDeflectTimer = 0; // landing cancels deflect state
        }
        else if (_knockbackTimer > 0)
        {
            _knockbackTimer -= dt;
            // Don't override vel.X — let knockback velocity play out
            vel.Y += _gravity * (vel.Y > 0 ? _fallGravMultiplier : 1f) * dt;
        }
        else
        {
            float moveSpeed = (IsDashing && inputX == _dashDir) ? GetDashSpeed() : _speed;
            if (SpeedBoostTimer > 0) moveSpeed *= SpeedBoostMultiplier;

            // Tech Sprint: dash transitions into sprint (hold direction after dash ends)
            if (CurrentTier == MoveTier.Tech && IsSprinting && IsGrounded && inputX == _sprintDir)
            {
                moveSpeed *= SprintMaxSpeedMult;
            }
            else if (!IsDashing)
            {
                IsSprinting = false;
            }

            // Acceleration-based movement (Celeste-style)
            float targetX = inputX * moveSpeed;
            if (IsGrounded || _coyoteTimer > 0)
            {
                // Ground: fast accel/decel
                if (inputX != 0)
                    vel.X = ApproachF(vel.X, targetX, _runAccel * dt);
                else
                    vel.X = ApproachF(vel.X, 0f, _runDecel * dt);
            }
            else
            {
                // Air: reduced control, capped speed
                float airMax = moveSpeed * _airMult;
                if (inputX != 0)
                {
                    // Don't reduce speed if already going faster (momentum preservation)
                    if (Math.Abs(vel.X) <= airMax || Math.Sign(vel.X) != inputX)
                        vel.X = ApproachF(vel.X, inputX * airMax, _airAccel * dt);
                }
                else
                    vel.X = ApproachF(vel.X, 0f, _airDecel * dt);
            }

            // Jump (Space) — only if NOT holding S (S+Space = slide)
            bool canJump = _jumpsLeft > (EnableDoubleJump ? 0 : 1);

            // Tech charged jump: hold to charge on ground, release to launch
            if (CurrentTier == MoveTier.Tech && (IsGrounded || _coyoteTimer > 0))
            {
                // Tech: normal jump (no charge)
                if (spacePressed && inputY <= 0 && !_jumpHeld)
                {
                    vel.Y = _jumpForce;
                    _jumpsLeft--;
                    _chargingJump = false;
                    SetSquash(0.7f, 1.3f);
                    IsGrounded = false;
                    _wasGrounded = false;
                    _coyoteTimer = 0;
                }
            }
            else if (spacePressed && !_jumpHeld && canJump && inputY <= 0)
            {
                _chargingJump = false;
                float now_flip = (float)System.DateTime.UtcNow.TimeOfDay.TotalSeconds;
                bool isSecondJump = _jumpsLeft < MaxJumps; // already used first jump

                // Flip: second jump within tight window
                if (EnableFlip && isSecondJump && (now_flip - _firstJumpTime) < FlipInputWindow)
                {
                    IsFlipping = true;
                    _flipTimer = FlipDuration;
                    SetSquash(0.75f, 1.3f);
                    // Frontflip if pressing facing dir, backflip otherwise
                    if (inputX == FacingDir)
                        _flipDir = FacingDir;
                    else if (inputX == -FacingDir)
                        _flipDir = -FacingDir;
                    else
                        _flipDir = -FacingDir; // no input = backflip
                    vel.Y = FlipJumpForce;
                    vel.X = _flipDir * FlipSpeed;
                    _jumpsLeft = 0; // consumed
                }
                else
                {
                    // Normal jump
                    vel.Y = _jumpForce;
                    _jumpsLeft--;
                    SetSquash(0.7f, 1.3f);
                    if (!isSecondJump) _firstJumpTime = now_flip;
                }
                IsGrounded = false;
                _wasGrounded = false;
                _coyoteTimer = 0;
            }
            else if (spacePressed && !_jumpHeld && inputY <= 0)
            {
                // Can't jump — buffer the input
                _jumpBufferTimer = JumpBufferTime;
            }
            bool wasHoldingJump = _jumpHeld;
            _jumpHeld = spacePressed;

            // Variable jump height: cut upward velocity on the frame Space is released mid-air
            if (wasHoldingJump && !spacePressed && vel.Y < 0 && !IsGrounded && !IsFlipping && !IsUppercutting && !IsVaultKicking)
            {
                vel.Y *= _jumpCutMultiplier;
            }

            // _gravity with half-gravity at apex (Celeste-style) + terminal velocity
            float gravMult = 1f;
            if (vel.Y > 0)
                gravMult = _fallGravMultiplier; // falling: heavier
            else if (Math.Abs(vel.Y) < _halfGravThreshold)
                gravMult = 0.5f; // near apex: floaty hang time
            vel.Y += _gravity * gravMult * dt;

            // Bio fast-fall: press S in air to plummet
            float termVel = _maxFall;
            if (CurrentTier == MoveTier.Bio && !IsGrounded && inputY > 0)
            {
                vel.Y += _gravity * 1.5f * dt; // extra gravity burst
                termVel = _maxFall * 1.4f;     // higher terminal velocity
            }
            // Tech ground pound: press S in air — heavy stomp with afterimage
            if (CurrentTier == MoveTier.Tech && !IsGrounded && inputY > 0 && !IsGroundPounding)
            {
                IsGroundPounding = true;
                GroundPoundLanded = false;
                vel.X = 0; // commit to straight down
                vel.Y = GroundPoundSpeed;
            }
            if (IsGroundPounding)
            {
                vel.X = 0;
                vel.Y = Math.Max(vel.Y, GroundPoundSpeed); // lock to stomp speed
                termVel = GroundPoundSpeed * 1.2f;
                if (IsGrounded)
                {
                    IsGroundPounding = false;
                    GroundPoundLanded = true; // Game1 reads this for shake/dust/hitbox
                    SetSquash(0.6f, 1.5f); // wide squash on impact
                }
            }
            if (vel.Y > termVel) vel.Y = termVel;
        }

        // --- Mouse button state ---
        bool leftClick = mouseState.HasValue && mouseState.Value.LeftButton == ButtonState.Pressed;
        bool rightClick = mouseState.HasValue && mouseState.Value.RightButton == ButtonState.Pressed;

        // --- Right hand / Ranged (J or Left Click) ---
        bool jPressed = kb.IsKeyDown(Keys.J) || leftClick;
        if (HasRangedWeapon && jPressed && !_shootHeld && _shootCooldown <= 0f)
        {
            _shootCooldown = ShootRate;
            WantsToShoot = true;
            ShootDirection = AimDir;
        }
        _shootHeld = jPressed;

        // --- Left hand / Melee (K or Right Click) ---
        bool kPressed = kb.IsKeyDown(Keys.K) || rightClick;

        // Spinning melee: hold K while grounded
        if (HasMeleeWeapon && EnableSpinMelee && kPressed && _wasGrounded)
        {
            _meleeHoldTimer += dt;
            if (_meleeHoldTimer >= SpinMeleeActivateTime)
            {
                IsSpinningMelee = true;
                vel.X = 0; // stationary
                _spinMeleeCooldown -= dt;
                if (_spinMeleeCooldown <= 0)
                {
                    _spinMeleeCooldown = SpinMeleeRate;
                    // Use player's WASD input for direction
                    var dir = Vector2.Zero;
                    if (inputX != 0) dir.X = inputX;
                    if (inputY != 0) dir.Y = inputY;
                    if (dir == Vector2.Zero) dir.X = FacingDir; // default to facing
                    dir.Normalize();
                    WantsToMelee = true;
                    MeleeDirection = dir;
                    MeleeTimer = SpinMeleeRate * 0.9f;
                }
            }
        }
        else
        {
            _meleeHoldTimer = 0;
            if (IsSpinningMelee)
            {
                IsSpinningMelee = false;
            }
        }

        if (HasMeleeWeapon && !IsSpinningMelee && kPressed && !_meleeHeld && _comboCooldown <= 0f)
        {
            int maxCombo = CurrentWeapon == WeaponType.None ? 1 : 2; // Fists=2-hit, weapons=3-hit
            if (_comboStep == 0 && _comboWindow <= 0 && MeleeTimer <= 0)
            {
                // First hit
                WantsToMelee = true;
                MeleeDirection = new Vector2(FacingDir, 0);
                MeleeTimer = CurrentMeleeActiveTime;
                _comboWindow = 0;
            }
            else if (_comboStep == 0 && _comboWindow > 0)
            {
                // Advance to hit 2
                _comboStep = 1;
                WantsToMelee = true;
                MeleeDirection = new Vector2(FacingDir, 0);
                MeleeTimer = CurrentMeleeActiveTime;
                _comboWindow = 0;
            }
            else if (_comboStep == 1 && _comboWindow > 0 && _comboHit[0] && _comboHit[1] && maxCombo >= 2)
            {
                // Hit 3 (finisher) — only if hits 1 and 2 connected
                _comboStep = 2;
                WantsToMelee = true;
                MeleeDirection = new Vector2(FacingDir, 0);
                // Finisher active time scales with weapon weight
                float finisherTime = CurrentWeapon switch
                {
                    WeaponType.None => 0.08f,
                    WeaponType.Knife => 0.08f,  // fast stab, same as fists
                    WeaponType.Dagger => 0.12f,
                    WeaponType.Stick => 0.14f,
                    WeaponType.Whip => 0.14f,
                    WeaponType.Sword => 0.16f,
                    WeaponType.Axe => 0.18f,
                    WeaponType.Club => 0.18f,
                    WeaponType.Hammer => 0.2f,
                    WeaponType.GreatSword => 0.2f,
                    WeaponType.GreatClub => 0.2f,
                    _ => 0.16f
                };
                MeleeTimer = finisherTime;
                _comboWindow = 0;
                // Forward burst scales by weapon
                float burst = CurrentWeapon switch
                {
                    WeaponType.Knife => 80f,    // small forward lunge
                    WeaponType.Dagger => 100f,
                    WeaponType.Stick => 150f,
                    WeaponType.Sword => 180f,
                    WeaponType.GreatSword => 200f,
                    WeaponType.Club => 120f,
                    WeaponType.GreatClub => 120f,
                    WeaponType.Axe => 120f,
                    WeaponType.Hammer => 120f,
                    WeaponType.Whip => 100f,
                    _ => 100f
                };
                var v = Velocity;
                v.X += FacingDir * burst;
                Velocity = v;
                vel = Velocity;
            }
        }
        _meleeHeld = kPressed;

        // Set combo window when melee active time ends (transition from active to waiting)
        if (MeleeTimer <= 0 && _comboWindow <= 0 && _comboCooldown <= 0)
        {
            // Check if we just finished an active hit
            if (_prevMeleeTimer > 0)
            {
                int maxStep = CurrentWeapon == WeaponType.None ? 1 : 2;
                if (_comboStep < maxStep)
                {
                    _comboWindow = CurrentComboWindow;
                }
                else
                {
                    // Combo finished
                    _comboCooldown = CurrentComboCooldown;
                    ResetCombo();
                }
            }
        }
        _prevMeleeTimer = MeleeTimer;

        // === Grapple input ===
        bool ePressed = kb.IsKeyDown(Keys.E);
        if (HasGrapple && ePressed && !_prevEPressed && !IsGrappling && !IsGrappleFiring)
        {
            // Fire toward mouse cursor
            var center = Position + new Vector2(Width / 2f, Height / 2f);
            var target = center + AimDir * GrappleMaxLength;
            FireGrapple(target);
        }
        // Release grapple on E release while swinging
        if (IsGrappling && !ePressed && _prevEPressed)
            ReleaseGrapple();
        // Also release on jump (Space) — gives a jump boost feel
        if (IsGrappling && kb.IsKeyDown(Keys.Space) && !_prevKb.IsKeyDown(Keys.Space))
            ReleaseGrapple();
        _prevEPressed = ePressed;
        
        // Update grapple pendulum physics (does nothing if not grappling)
        UpdateGrapple(dt, kb);
        UpdateGrappleRetract(dt);
        
        // If grappling, pendulum handles position — skip normal movement
        if (IsGrappling)
        {
            _prevKb = kb;
            return;
        }

        // If grapple just released this frame, use the release velocity
        if (_grappleJustReleased)
        {
            vel = Velocity;
            _grappleJustReleased = false;
        }
        
        // Apply velocity
        var pos = Position + vel * dt;

        // --- Collision (always full Height) ---
        IsGrounded = false;

        if (pos.Y + Height >= floorY)
        {
            pos.Y = floorY - Height;
            vel.Y = 0;
            IsGrounded = true;
            _jumpsLeft = MaxJumps;
        }

        // Slope floor collision (skip if player is actively jumping upward — don't snap back to slope)
        bool _onSlope = false;
        float _slopeFloorY = float.MaxValue;
        if (tileGrid != null && vel.Y >= 0)
        {
            float slopeY = tileGrid.GetSlopeFloorY(pos.X, pos.Y, Width, Height);
            if (slopeY < float.MaxValue && pos.Y + Height >= slopeY - 6)
            {
                pos.Y = slopeY - Height;
                vel.Y = 0;
                IsGrounded = true;
                _jumpsLeft = MaxJumps;
                _onSlope = true;
                _slopeFloorY = slopeY;
            }
            // Slope sticking: when was grounded and moving, probe below for slope/ground continuity
            else if ((_wasGrounded || _coyoteTimer > 0) && vel.Y >= -50f)
            {
                // Probe further below based on horizontal speed (faster = bigger gap to bridge)
                float probeDepth = MathHelper.Clamp(MathF.Abs(vel.X) * 0.06f, 4f, 24f);
                float stickCheckY = pos.Y + probeDepth;
                float stickSlopeY = tileGrid.GetSlopeFloorY(pos.X, stickCheckY, Width, Height);
                if (stickSlopeY < float.MaxValue && stickSlopeY - (pos.Y + Height) < probeDepth + 4f)
                {
                    pos.Y = stickSlopeY - Height;
                    vel.Y = 0;
                    IsGrounded = true;
                    _jumpsLeft = MaxJumps;
                    _onSlope = true;
                    _slopeFloorY = stickSlopeY;
                }
            }
        }

        if (Velocity.Y >= 0 && !WantsDropThrough)
        {
            foreach (var plat in platforms)
            {
                float prevBottom = Position.Y + Height;
                float newBottom = pos.Y + Height;
                if (prevBottom <= plat.Y + 2 && newBottom >= plat.Y &&
                    pos.X + Width > plat.X && pos.X < plat.X + plat.Width)
                {
                    pos.Y = plat.Y - Height;
                    vel.Y = 0;
                    IsGrounded = true;
                    _jumpsLeft = MaxJumps;
                }
            }
        }

        // Slope ceiling collision — slide along surface
        bool _onCeilSlope = false;
        if (tileGrid != null)
        {
            TileType ceilTile;
            float slopeCeilY = tileGrid.GetSlopeCeilY(pos.X, pos.Y, Width, Height, out ceilTile);
            if (slopeCeilY > float.MinValue && pos.Y <= slopeCeilY + 4)
            {
                pos.Y = slopeCeilY;
                _onCeilSlope = true;
                
                // Snap to surface and zero upward velocity
                // Player keeps moving horizontally from their existing vel.X
                // _gravity still pulls down — once it overcomes the jump, player falls off
                if (vel.Y < 0) vel.Y = 0;
            }
        }

        // Ceiling collision (bonk head) — skip if near ceiling slope tiles
        if (ceilings != null && vel.Y < 0)
        {
            int collLeft = (int)pos.X + CollisionOffsetX;
            int collRight = collLeft + CollisionWidth;
            foreach (var ceil in ceilings)
            {
                float slideOffset = Height - CurrentHeight;
                float prevTop = Position.Y + slideOffset;
                float newTop = pos.Y + slideOffset;
                if (prevTop >= ceil.Bottom - 2 && newTop < ceil.Bottom &&
                    collRight > ceil.X && collLeft < ceil.X + ceil.Width)
                {
                    // Check if there's a ceiling slope tile near the player's head
                    bool nearCeilSlope = false;
                    if (tileGrid != null)
                    {
                        // Check a wide area: player center ± 2 tiles, at and above the ceiling
                        for (int dx = -2; dx <= 2 && !nearCeilSlope; dx++)
                        {
                            int checkX = (int)(pos.X + Width / 2f) + dx * 32;
                            for (int dy = 0; dy <= 1 && !nearCeilSlope; dy++)
                            {
                                int checkY = ceil.Y + dy * 32;
                                var t = tileGrid.GetTile(checkX, checkY);
                                if (TileProperties.IsSlopeCeiling(t))
                                    nearCeilSlope = true;
                            }
                        }
                    }
                    if (!nearCeilSlope)
                    {
                        // Corner correction: try nudging left/right to clear near-miss bonks
                        bool corrected = false;
                        for (int nudge = 1; nudge <= (int)CornerCorrectionPx; nudge++)
                        {
                            // Try nudging right
                            int tryLeft = collLeft + nudge;
                            int tryRight = collRight + nudge;
                            if (!(tryRight > ceil.X && tryLeft < ceil.X + ceil.Width))
                            {
                                pos.X += nudge;
                                corrected = true;
                                break;
                            }
                            // Try nudging left
                            tryLeft = collLeft - nudge;
                            tryRight = collRight - nudge;
                            if (!(tryRight > ceil.X && tryLeft < ceil.X + ceil.Width))
                            {
                                pos.X -= nudge;
                                corrected = true;
                                break;
                            }
                        }
                        if (!corrected)
                        {
                            pos.Y = ceil.Bottom;
                            vel.Y = 0;
                        }
                    }
                }
            }
        }

        // Solid floor collision (stand on top + block from below)
        if (solidFloors != null)
        {
            int collLeft = (int)pos.X + CollisionOffsetX;
            int collRight = collLeft + CollisionWidth;
            foreach (var sf in solidFloors)
            {
                bool xOverlap = collRight > sf.X && collLeft < sf.X + sf.Width;
                if (xOverlap)
                {
                    // Landing on top
                    if (vel.Y >= 0)
                    {
                        float prevBottom = Position.Y + Height;
                        float newBottom = pos.Y + Height;
                        // When coming from a slope or was just grounded, use generous landing threshold
                        float landThreshold = (_onSlope || _wasOnSlope) ? 12f : 2f;
                        if (prevBottom <= sf.Y + landThreshold && newBottom >= sf.Y)
                        {
                            pos.Y = sf.Y - Height;
                            vel.Y = 0;
                            IsGrounded = true;
                            _jumpsLeft = MaxJumps;
                        }
                    }
                    // Hitting from below
                    if (vel.Y < 0)
                    {
                        float slideOff = Height - CurrentHeight;
                        float prevTop = Position.Y + slideOff;
                        float newTop = pos.Y + slideOff;
                        if (prevTop >= sf.Bottom - 2 && newTop < sf.Bottom)
                        {
                            System.Console.WriteLine($"[SOLIDFLOOR BONK] sf=({sf.X},{sf.Y},{sf.Width},{sf.Height}) pos.Y={pos.Y:F1} vel=({vel.X:F1},{vel.Y:F1})");
                            pos.Y = sf.Bottom;
                            vel.Y = 0;
                        }
                    }
                    // Push out horizontally if inside
                    // Skip push-out only if a slope tile is actually adjacent to this solid block's edge
                    float slideOff2 = Height - CurrentHeight;
                    float playerBottom = pos.Y + Height;
                    float playerTop = pos.Y + slideOff2;
                    bool slopeAdjacent = false;
                    if (tileGrid != null)
                    {
                        float playerCenterX = pos.X + Width / 2f;
                        float sfCenterX = sf.X + sf.Width / 2f;
                        int checkX = playerCenterX < sfCenterX ? sf.X - 1 : sf.X + sf.Width + 1;
                        // Check multiple heights along the edge for adjacent slope tiles
                        for (int checkY = (int)(playerBottom - 1); checkY >= (int)playerTop && checkY >= sf.Y; checkY -= 16)
                        {
                            var adjTile = tileGrid.GetTile(checkX, checkY);
                            if (TileProperties.IsSlopeFloor(adjTile))
                            { slopeAdjacent = true; break; }
                        }
                    }
                    if (!slopeAdjacent && playerBottom > sf.Y + 4 && playerTop < sf.Bottom - 4)
                    {
                        float playerCenterX = pos.X + CollisionOffsetX + CollisionWidth / 2f;
                        float sfCenterX = sf.X + sf.Width / 2f;
                        if (playerCenterX < sfCenterX)
                            pos.X = sf.X - CollisionOffsetX - CollisionWidth;
                        else
                            pos.X = sf.X + sf.Width - CollisionOffsetX;
                        vel.X = 0;
                    }
                }
            }
        }

        // End slide/cartwheel if airborne for more than a few frames (grace period for slope transitions)
        if (!IsGrounded && IsSliding)
        {
            _slideAirFrames++;
            if (_slideAirFrames > 6) { IsSliding = false; _ropeDisengaged = true; }
        }
        else if (IsGrounded && IsSliding) { _slideAirFrames = 0; }

        // --- Wall solid collision (can't walk through walls) ---
        if (solidWalls != null)
        {
            var pRect = new Rectangle((int)pos.X + CollisionOffsetX, (int)pos.Y + Height - CurrentHeight, CollisionWidth, CurrentHeight);
            foreach (var w in solidWalls)
            {
                if (pRect.Intersects(w))
                {
                    // Push out horizontally
                    float playerCenter = pos.X + CollisionOffsetX + CollisionWidth / 2f;
                    float wallCenter = w.X + w.Width / 2f;
                    if (playerCenter < wallCenter)
                        pos.X = w.Left - CollisionOffsetX - CollisionWidth;
                    else
                        pos.X = w.Right - CollisionOffsetX;
                    vel.X = 0;
                    // Cancel momentum-based moves on wall impact
                    if (IsVaultKicking) { IsVaultKicking = false; IsCrouching = false; }
                    if (IsDashing) IsDashing = false;
                    if (IsBladeDashing) IsBladeDashing = false;
                }
            }
        }

        pos.X = MathHelper.Clamp(pos.X, WorldLeft, WorldRight - Width);

        // Float tile effect: gentle upward lift
        if (FloatTimer > 0 && vel.Y > FloatLiftSpeed)
        {
            vel.Y = MathHelper.Lerp(vel.Y, FloatLiftSpeed, 0.1f);
        }

        Position = pos;
        Velocity = vel;

        // Landing squash
        if (!_wasGrounded && IsGrounded)
            SetSquash(1.3f, 0.7f);

        // Coyote time: grace period after leaving ground
        if (_wasGrounded && !IsGrounded && vel.Y >= 0 && !_jumpHeld)
            _coyoteTimer = CoyoteTime;
        if (_coyoteTimer > 0)
        {
            _coyoteTimer -= dt;
            if (_coyoteTimer > 0) _jumpsLeft = MaxJumps;
        }

        // Jump buffering: auto-jump on landing
        if (_jumpBufferTimer > 0)
            _jumpBufferTimer -= dt;
        if (!_wasGrounded && IsGrounded && _jumpBufferTimer > 0)
        {
            vel.Y = _jumpForce;
            _jumpsLeft = MaxJumps - 1;
            IsGrounded = false;
            _jumpBufferTimer = 0;
            SetSquash(0.7f, 1.3f);
            Velocity = vel;
        }

        // Squash & stretch lerp back to normal
        if (_squashHoldTimer > 0)
            _squashHoldTimer -= dt;
        else if (!IsGrounded && vel.Y > 300)
            _visualScale = Vector2.Lerp(_visualScale, new Vector2(0.9f, 1.1f), ScaleLerpSpeed * dt);
        else
            _visualScale = Vector2.Lerp(_visualScale, Vector2.One, ScaleLerpSpeed * dt);

        // Afterimage trail
        bool wantsTrail = IsDashing || IsVaultKicking || IsBladeDashing || IsCartwheeling || IsFlipping || IsUppercutting || IsSliding || IsGroundPounding;
        if (wantsTrail)
        {
            _afterimageSpawnTimer -= dt;
            if (_afterimageSpawnTimer <= 0)
            {
                _afterimageSpawnTimer = AfterimageSpawnRate;
                var (aimgRow, aimgFrame) = GetSpriteAnim();
                _afterimages[_afterimageIndex] = new Afterimage
                {
                    Position = Position,
                    SpriteRow = aimgRow,
                    SpriteFrame = aimgFrame,
                    FacingDir = FacingDir,
                    DrawHeight = CurrentHeight,
                    Alpha = 0.8f,
                    TimeLeft = AfterimageLifetime
                };
                _afterimageIndex = (_afterimageIndex + 1) % MaxAfterimages;
            }
        }
        else
        {
            _afterimageSpawnTimer = 0;
        }
        for (int i = 0; i < MaxAfterimages; i++)
        {
            if (_afterimages[i].TimeLeft > 0)
            {
                _afterimages[i].TimeLeft -= dt;
                _afterimages[i].Alpha = 0.8f * (_afterimages[i].TimeLeft / AfterimageLifetime);
            }
        }

        _wasGrounded = IsGrounded;
        _wasOnSlope = _onSlope;
        _prevKb = kb;
    }

    // Sprite sheet layout: 48x48 per frame, multi-row
    // Row 0: idle(4) 1: walk(7) 2: run(8) 3: jump(4) 4: crouch(2) 
    // 5: whip/attack(6) 6: backflip(6) 7: damaged(3) 8: superjump(6) 9: dash(3)
    public const int SpriteW = 48, SpriteH = 48;
    private const int RowIdle = 0, RowCrouch = 1, RowJump = 2, RowWalk = 3, RowRun = 4;
    private const int RowWhip = 5, RowBackflip = 6, RowDamaged = 7, RowSuperjump = 8, RowDash = 9;
    private static readonly int[] RowFrameCounts = { 4, 3, 8, 8, 8, 10, 10, 3, 6, 3 };
    private float _idleTimer; // tracks how long standing still
    private bool _wasCrouching; // for crouch transition detection
    private float _crouchTransTimer; // >0 = transitioning into/out of crouch
    private bool _crouchTransDown; // true = going down, false = standing up
    private float _animTimer;

    private (int row, int frame) GetSpriteAnim()
    {
        // Damaged/knockback
        if (IsInvincible && DamageCooldown > 0.15f)
        {
            int f = Math.Min((int)((0.25f - DamageCooldown) * 12f), 2);
            return (RowDamaged, Math.Max(0, f));
        }
        // Attack (whip animation for all melee)
        if (MeleeTimer > 0)
        {
            float progress = 1f - (MeleeTimer / 0.15f); // 0→1 over attack duration
            int f = Math.Min((int)(progress * 10), 9);
            return (RowWhip, f);
        }
        // Uppercut → super jump
        if (IsUppercutting)
        {
            float progress = 1f - (_uppercutTimer / UppercutDuration);
            int f = Math.Min((int)(progress * 6), 5);
            return (RowSuperjump, f);
        }
        // Backflip
        if (IsFlipping)
        {
            float progress = 1f - (_flipTimer / FlipDuration);
            int f = Math.Min((int)(progress * 10), 9);
            return (RowBackflip, f);
        }
        // Slide
        if (IsSliding)
        {
            int f = ((int)(_animTimer * 10f)) % 3;
            return (RowDash, f);
        }
        // Blade dash — forward dash animation
        if (IsBladeDashing)
        {
            int f = ((int)(_animTimer * 10f)) % 3;
            return (RowDash, f);
        }
        if (IsCrouching)
        {
            // Crouch transition: 3 frames going down (0→1→2), hold frame 2
            if (_crouchTransTimer > 0 && _crouchTransDown)
            {
                int f = Math.Min(2, (int)((1f - _crouchTransTimer / 0.15f) * 3));
                return (RowCrouch, f);
            }
            return (RowCrouch, 2); // hold crouched pose
        }
        // Stand-up transition: reverse (2→1→0)
        if (_crouchTransTimer > 0 && !_crouchTransDown)
        {
            int f = Math.Min(2, (int)((1f - _crouchTransTimer / 0.15f) * 3));
            return (RowCrouch, 2 - f);
        }
        if (!IsGrounded)
            return Velocity.Y < 0 ? (RowJump, 1) : (RowJump, 3);
        if (MathF.Abs(Velocity.X) > 10f)
        {
            if (IsDashing)
            {
                int f = ((int)(_animTimer * 12f)) % 8;
                return (RowRun, f);
            }
            int wf = ((int)(_animTimer * 8f)) % 8;
            return (RowWalk, wf);
        }
        // Idle: 4-frame loop
        int idleF = ((int)(_animTimer * 3f)) % 4;
        return (RowIdle, idleF);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D spriteSheet = null)
    {
        // Flash during i-frames: skip drawing every other 4-frame chunk
        if (IsInvincible)
        {
            int frame = (int)(DamageCooldown * 60f);
            if ((frame / 4) % 2 == 0) return;
        }

        // Accumulate animation timer (skip when paused)
        if (!Paused)
        {
            _animTimer += 1f / 60f; // approximate

            // Idle timer — reset when moving, crouch transitions
            if (MathF.Abs(Velocity.X) > 10f || !IsGrounded || IsCrouching)
                _idleTimer = 0f;
            else
                _idleTimer += 1f / 60f;

            // Crouch transition timer
            if (IsCrouching && !_wasCrouching) { _crouchTransTimer = 0.15f; _crouchTransDown = true; }
            if (!IsCrouching && _wasCrouching) { _crouchTransTimer = 0.15f; _crouchTransDown = false; }
            _wasCrouching = IsCrouching;
            if (_crouchTransTimer > 0) _crouchTransTimer -= 1f / 60f;
        }

        if (spriteSheet != null)
        {
            // Draw afterimage trail
            for (int i = 0; i < MaxAfterimages; i++)
            {
                if (_afterimages[i].TimeLeft > 0)
                {
                    var ai = _afterimages[i];
                    var aiSrcRect = new Rectangle(ai.SpriteFrame * SpriteW, ai.SpriteRow * SpriteH, SpriteW, SpriteH);
                    var aiFlip = ai.FacingDir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    int aiDrawX = (int)ai.Position.X - (SpriteW - Width) / 2;
                    int aiDrawY = (int)ai.Position.Y + Height - SpriteH + 1;
                    var aiColor = Color.White * ai.Alpha;
                    spriteBatch.Draw(spriteSheet, new Rectangle(aiDrawX, aiDrawY, SpriteW, SpriteH), aiSrcRect, aiColor, 0f, Vector2.Zero, aiFlip, 0f);
                }
            }

            var (row, frame) = GetSpriteAnim();
            if (row < 0) { DrawFallback(spriteBatch, pixel); goto afterSprite; }
            var srcRect = new Rectangle(frame * SpriteW, row * SpriteH, SpriteW, SpriteH);
            var flip = FacingDir < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            var tint = Color.White;
            if (IsVaulting || IsCartwheeling) tint = Color.CornflowerBlue;
            else if (IsVaultKicking) tint = Color.Orange;
            else if (IsUppercutting) tint = Color.Yellow;
            else if (IsFlipping) tint = Color.Magenta * 0.85f;
            else if (IsBladeDashing) tint = Color.White * 0.9f;
            else if (IsDashing) tint = new Color(200, 200, 200);

            // Squash & stretch: scale sprite from bottom-center
            int scaledW = (int)(SpriteW * _visualScale.X);
            int scaledH = (int)(SpriteH * _visualScale.Y);
            int drawX = (int)Position.X + Width / 2 - scaledW / 2;
            int drawY = (int)Position.Y + Height - scaledH + 1;
            var destRect = new Rectangle(drawX, drawY, scaledW, scaledH);
            spriteBatch.Draw(spriteSheet, destRect, srcRect, tint, 0f, Vector2.Zero, flip, 0f);
        }
        else
        {
            // Fallback: colored rectangles (original code)
            DrawFallback(spriteBatch, pixel);
        }
        afterSprite:

        // Melee/special hitbox overlays
        if (IsSpinningMelee && MeleeTimer > 0)
            spriteBatch.Draw(pixel, MeleeHitbox, Color.Red * 0.5f);
        if (MeleeTimer > 0)
            spriteBatch.Draw(pixel, MeleeHitbox, Color.Red * 0.5f);
        if (IsVaultKicking)
            spriteBatch.Draw(pixel, VaultKickHitbox, Color.Red * 0.4f);
        if (IsUppercutting)
            spriteBatch.Draw(pixel, UppercutHitbox, Color.Red * 0.4f);
        if (IsBladeDashing)
            spriteBatch.Draw(pixel, BladeDashHitbox, Color.Purple * 0.4f);
    }

    private void DrawFallback(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Lying down / standing up animation
        if (IsLyingDown)
        {
            float p = StandUpProgress; // 0 = flat, 1 = standing
            // Interpolate from horizontal (lying) to vertical (standing)
            int bodyW, bodyH;
            if (p < 0.01f)
            {
                // Fully lying down — horizontal rectangle
                bodyW = Height; // width = normal height (body length)
                bodyH = Width / 2; // thin profile
            }
            else
            {
                // Transition: shrink width, grow height
                bodyW = (int)MathHelper.Lerp(Height, Width, p);
                bodyH = (int)MathHelper.Lerp(Width / 2f, Height, p);
            }
            // Bottom-anchored (feet stay on ground)
            int bx = (int)Position.X + Width / 2 - bodyW / 2;
            int by = (int)Position.Y + Height - bodyH;
            
            spriteBatch.Draw(pixel, new Rectangle(bx, by, bodyW, bodyH), Color.Gray);
            
            // Head — shifts from side to top as Adam stands
            int headSize = 6;
            float headLerpX = MathHelper.Lerp(bx + bodyW - headSize, bx + bodyW / 2 - headSize / 2, p);
            float headLerpY = MathHelper.Lerp(by + bodyH / 2 - headSize / 2, by - headSize + 1, p);
            spriteBatch.Draw(pixel, new Rectangle((int)headLerpX, (int)headLerpY, headSize, headSize), Color.LightGray);
            
            // Arm pushing off ground during mid-stand (0.2–0.6)
            if (p > 0.15f && p < 0.7f)
            {
                float armT = (p - 0.15f) / 0.55f; // 0→1 within arm range
                int armLen = (int)(8f * MathF.Sin(armT * MathF.PI)); // extends then retracts
                int armX = bx + bodyW / 2 + (FacingDir == 1 ? 4 : -4 - armLen);
                int armY = by + bodyH - 2;
                spriteBatch.Draw(pixel, new Rectangle(armX, armY, armLen, 2), Color.LightGray * 0.8f);
            }
            return;
        }

        // Afterimage trail (fallback: colored ghost rectangles)
        for (int i = 0; i < MaxAfterimages; i++)
        {
            if (_afterimages[i].TimeLeft > 0)
            {
                var ai = _afterimages[i];
                int aiW = Width;
                int aiH = ai.DrawHeight > 0 ? ai.DrawHeight : Height;
                var aiColor = new Color(150, 180, 255) * ai.Alpha;
                // Bottom-aligned: afterimage foot = Position.Y + Height
                int aiY = (int)ai.Position.Y + Height - aiH;
                spriteBatch.Draw(pixel,
                    new Rectangle((int)ai.Position.X, aiY, aiW, aiH),
                    aiColor);
            }
        }

        // Apply squash & stretch to fallback rectangles
        int sqW = (int)(Width * _visualScale.X);
        int sqH = (int)(Height * _visualScale.Y);
        int sqX = (int)Position.X + Width / 2 - sqW / 2;
        int sqY = (int)Position.Y + Height - sqH; // bottom-aligned

        // Injured limp: asymmetric bob + slight lean
        float limpOffsetY = 0f;
        float limpLean = 0f;
        if (IsInjured && IsGrounded && MathF.Abs(Velocity.X) > 10f)
        {
            float limpPhase = (_limpTimer % LimpCycleDuration) / LimpCycleDuration;
            // Asymmetric: one leg dips more than the other
            limpOffsetY = MathF.Sin(limpPhase * MathF.PI * 2f) * 3f;
            limpOffsetY += MathF.Sin(limpPhase * MathF.PI * 4f) * 1.5f; // double-frequency wobble
            limpLean = MathF.Sin(limpPhase * MathF.PI * 2f) * 1.5f * FacingDir;
            sqY += (int)limpOffsetY;
            sqX += (int)limpLean;
        }

        if (IsVaulting)
        {
            int size = (int)(Width * 0.8f);
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X + (Width - size) / 2, (int)Position.Y + Height - size, size, size),
                Color.LightGray * 0.9f);
        }
        else if (IsOnWall)
        {
            spriteBatch.Draw(pixel,
                new Rectangle(sqX, sqY, sqW, sqH),
                IsCrouching ? Color.DarkGray : new Color(100, 140, 100));
            int gripX = _currentWallClimbSide == 1 ? (int)Position.X : (int)Position.X + Width - 4;
            spriteBatch.Draw(pixel,
                new Rectangle(gripX, (int)Position.Y + 8, 4, 6), Color.White * 0.5f);
            spriteBatch.Draw(pixel,
                new Rectangle(gripX, (int)Position.Y + Height - 14, 4, 6), Color.White * 0.5f);
        }
        else if (IsOnRope)
        {
            spriteBatch.Draw(pixel,
                new Rectangle(sqX, sqY, sqW, sqH),
                IsCrouching ? Color.DarkGray : new Color(160, 120, 80));
            int gripY = (int)Position.Y + Height / 2 - 2;
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X + Width / 2 - 3, gripY, 6, 4),
                Color.White * 0.7f);
        }
        else if (IsSliding)
        {
            int slideW = (int)(Width * _visualScale.X);
            int slideH = (int)(SlideHeight * _visualScale.Y);
            int slideX = (int)Position.X + Width / 2 - slideW / 2;
            int slideY = (int)Position.Y + Height - slideH;
            spriteBatch.Draw(pixel,
                new Rectangle(slideX, slideY, slideW, slideH),
                Color.CornflowerBlue);
        }
        else if (IsCrouching)
        {
            int crouchW = (int)(Width * _visualScale.X);
            int crouchH = (int)(CrouchHeight * _visualScale.Y);
            int crouchX = (int)Position.X + Width / 2 - crouchW / 2;
            int crouchY = (int)Position.Y + Height - crouchH;
            
            // Brace visual: orange-tinted shield stance
            var crouchColor = IsBracing ? new Color(200, 160, 80) : Color.DarkGray;
            spriteBatch.Draw(pixel,
                new Rectangle(crouchX, crouchY, crouchW, crouchH),
                crouchColor);
            if (IsBracing)
            {
                // Shield arms — wider stance
                int shieldW = crouchW + 6;
                int shieldX = crouchX - 3;
                spriteBatch.Draw(pixel,
                    new Rectangle(shieldX, crouchY + 2, shieldW, 3),
                    new Color(255, 200, 100) * 0.6f);
            }
            else
            {
                var center = new Vector2(crouchX + crouchW / 2f, crouchY + crouchH / 2f);
                for (int i = 0; i < 15; i++)
                {
                    var p = center + AimDir * (i * 2);
                    spriteBatch.Draw(pixel, new Rectangle((int)p.X, (int)p.Y, 2, 2), Color.Yellow * 0.6f);
                }
            }
        }
        else
        {
            var bodyColor = IsDashing ? new Color(180, 180, 180) : 
                            IsSprinting ? new Color(200, 200, 180) :
                            IsGroundPounding ? new Color(255, 180, 80) : Color.Gray;
            spriteBatch.Draw(pixel,
                new Rectangle(sqX, sqY, sqW, sqH),
                bodyColor);
            int notchX = FacingDir == 1 ? sqX + sqW - 4 : sqX;
            spriteBatch.Draw(pixel,
                new Rectangle(notchX, sqY + sqH / 2 - 3, 4, 6),
                Color.LightGray);
            
            // Knife visual: small blade extending from hand
            if (CurrentWeapon == WeaponType.Knife)
            {
                int handY = sqY + sqH / 2 - 1;
                if (MeleeTimer > 0)
                {
                    float t = 1f - (MeleeTimer / CurrentMeleeActiveTime);
                    float extend;
                    if (t < 0.15f) extend = t / 0.15f;
                    else if (t < 0.7f) extend = 1f;
                    else extend = 1f - (t - 0.7f) / 0.3f;
                    int bladeLen = (int)(12f * extend);
                    
                    // Check if angled downward stab
                    bool isDownAngle = MeleeSwingAngle > 0.3f && MeleeSwingAngle < 2.8f; // roughly PI/4 or 3PI/4
                    if (isDownAngle && bladeLen > 0)
                    {
                        // Diagonal blade: draw at ~45° down-forward
                        int diagX = FacingDir == 1 ? sqX + sqW : sqX;
                        int diagSteps = bladeLen;
                        for (int ds = 0; ds < diagSteps; ds++)
                        {
                            int px = diagX + FacingDir * ds;
                            int py = handY + ds;
                            spriteBatch.Draw(pixel, new Rectangle(px, py, 2, 2), Color.Silver);
                        }
                    }
                    else
                    {
                        // Horizontal stab (standing or crouch)
                        int stabY = IsCrouching ? handY + 8 : handY;
                        int bladeX = FacingDir == 1 ? sqX + sqW : sqX - bladeLen;
                        spriteBatch.Draw(pixel, new Rectangle(bladeX, stabY, bladeLen, 2), Color.Silver);
                        if (bladeLen > 3)
                        {
                            int tipX = FacingDir == 1 ? bladeX + bladeLen - 2 : bladeX;
                            spriteBatch.Draw(pixel, new Rectangle(tipX, stabY - 1, 2, 1), Color.White);
                        }
                    }
                }
                else
                {
                    // Idle: short blade at side
                    int bladeLen = 6;
                    int bladeX = FacingDir == 1 ? sqX + sqW : sqX - bladeLen;
                    spriteBatch.Draw(pixel, new Rectangle(bladeX, handY, bladeLen, 2), Color.Silver * 0.8f);
                }
            }
        }
    }

    /// <summary>Move value toward target by maxDelta per call (Celeste's Calc.Approach).</summary>
    private static float ApproachF(float val, float target, float maxDelta)
    {
        if (val < target)
            return Math.Min(val + maxDelta, target);
        else
            return Math.Max(val - maxDelta, target);
    }
}
