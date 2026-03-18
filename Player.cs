using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ArenaShooter;

public class Player
{
    public static float WorldLeft = 0;
    public static float WorldRight = 800;

    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public const int Width = 32;
    public const int Height = 48;
    private const float Speed = 250f;
    private const float Gravity = 900f;
    private const float JumpForce = -420f;
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
    private const float MeleeRate = 0.3f;
    private bool _meleeHeld;
    public bool WantsToMelee { get; private set; }
    public Vector2 MeleeDirection { get; private set; }
    public float MeleeTimer { get; private set; }
    private const float MeleeActiveTime = 0.12f; // how long the hitbox stays out
    public const int MeleeRange = 40;

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

    // Dash (double-tap A or D)
    public bool IsDashing { get; private set; }
    private const float DashSpeed = 420f;
    private float _lastATapTime;
    private float _lastDTapTime;
    private bool _aWasUp;
    private bool _dWasUp;
    private const float DashDoubleTapWindow = 0.25f;
    private int _dashDir;

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
    public int MaxHp { get; set; } = 100;
    public int Hp { get; set; } = 100;
    public float DamageCooldown { get; set; }
    private const float DamageCooldownTime = 0.3f;
    private float _regenDelay; // time since last damage
    private const float RegenStartDelay = 2.0f; // 2s before regen kicks in
    private float _regenAccum; // fractional HP accumulator
    private const float RegenRate = 5f; // HP per second (slow)

    public void TakeDamage(int amount)
    {
        if (DamageCooldown > 0) return;
        Hp -= amount;
        DamageCooldown = DamageCooldownTime;
        _regenDelay = 0f;
        _regenAccum = 0f;
        if (Hp <= 0) Hp = 0;
    }

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

    // Weapon gating (set by Game1)
    public bool HasMeleeWeapon { get; set; }
    public bool HasRangedWeapon { get; set; }
    public int MeleeRangeOverride { get; set; } = MeleeRange;

    private KeyboardState _prevKb;

    public Player(Vector2 startPos)
    {
        Position = startPos;
        Velocity = Vector2.Zero;
        IsGrounded = false;
        _wasGrounded = false;
        AimDir = new Vector2(1, 0);
    }

    public int CurrentHeight => IsSliding ? SlideHeight : (IsCrouching ? CrouchHeight : Height);

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
            // Hitbox above the player's head
            int hbX = (int)Position.X + (Width - UppercutHitboxW) / 2;
            int hbY = (int)Position.Y - UppercutHitboxH;
            return new Rectangle(hbX, hbY, UppercutHitboxW, UppercutHitboxH);
        }
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
            var dir = MeleeDirection;
            // Offset the hitbox center along the aim direction
            var hbCenter = center + dir * (MeleeRangeOverride * 0.6f);
            return new Rectangle(
                (int)(hbCenter.X - MeleeWidth / 2f),
                (int)(hbCenter.Y - MeleeWidth / 2f),
                MeleeWidth, MeleeWidth);
        }
    }

    public void Update(float dt, KeyboardState kb, float floorY, Rectangle[] platforms, float[] ropeXPositions = null, float[] ropeTops = null, float[] ropeBottoms = null, Rectangle[] walls = null, int[] wallClimbSides = null, Rectangle[] solidWalls = null, Rectangle[] ceilings = null, Rectangle[] solidFloors = null, TileGrid tileGrid = null)
    {
        WantsToShoot = false;
        WantsToMelee = false;
        WantsDropThrough = false;
        _shootCooldown -= dt;
        _meleeCooldown -= dt;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        _slideCooldownTimer -= dt;
        _cartwheelCooldownTimer -= dt;
        if (MeleeTimer > 0) MeleeTimer -= dt;

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
                    if (!_ropeDisengaged && !IsSliding && !IsDashing && !IsVaultKicking && !IsBladeDashing && !IsCartwheeling && !IsUppercutting && !IsFlipping)
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
        if (!IsOnWall && !IsOnRope && walls != null && !IsSliding && _wallHopCooldown <= 0f)
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

        // --- Dash detection (double-tap A or D) ---
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
            if (now_dash - _lastATapTime < DashDoubleTapWindow && _wasGrounded)
            { IsDashing = true; _dashDir = -1; }
            _lastATapTime = now_dash;
        }
        if (!dDown) _dWasUp = true;
        if (dDown && _dWasUp)
        {
            _dWasUp = false;
            if (now_dash - _lastDTapTime < DashDoubleTapWindow && _wasGrounded)
            { IsDashing = true; _dashDir = 1; }
            _lastDTapTime = now_dash;
        }
        // Stop dashing when you release the dash direction key or press opposite
        if (IsDashing)
        {
            if ((_dashDir == -1 && !aDown) || (_dashDir == 1 && !dDown) || inputX == -_dashDir)
                IsDashing = false;
        }
        }

        // --- Crouch (shift while grounded, not sliding) ---
        IsCrouching = shift && (_wasGrounded || IsOnRope || IsOnWall) && !IsSliding;

        // --- Drop through platform (double-tap S only) ---
        _dropIgnoreTimer -= dt;
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

        // --- Slide (S + Space while grounded, OR Shift + Space with no direction from crouch — one per crouch hold) ---
        bool spacePressed = kb.IsKeyDown(Keys.Space);
        bool wantsCrouchSlide = IsCrouching && inputX == 0;
        bool wantsSlide = (inputY > 0 && !IsCrouching) || wantsCrouchSlide;
        if (EnableSlide && wantsSlide && spacePressed && !_jumpHeld && _wasGrounded && !IsSliding && !IsCartwheeling && _slideCooldownTimer <= 0f)
        {
            IsSliding = true;
            _slideTimer = SlideDuration;
            _slideCooldownTimer = SlideCooldown;
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
            if (_uppercutInputReady && inputY < 0 && spacePressed && !_jumpHeld && !IsUppercutting)
            {
                _uppercutInputReady = false;
                IsUppercutting = true;
                _uppercutTimer = UppercutDuration;
                // Cancel any active move
                IsSliding = false;
                IsCartwheeling = false;
                IsVaultKicking = false;
                IsCrouching = false;
                _jumpHeld = true;
            }
        }

        // --- Blade dash QCF detection (↓↘→+K, mirrored when facing left) ---
        if (EnableBladeDash && !IsBladeDashing)
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
            {
                // QCF + K complete!
                _qcfStage = 0;
                IsBladeDashing = true;
                _bladeDashTimer = BladeDashDuration;
                _bladeDashDir = fwd;
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
        if (inputX != 0 && !IsCrouching && !IsSliding) FacingDir = inputX;

        // --- Aim direction ---
        if (IsCrouching)
        {
            var aim = Vector2.Zero;
            if (inputX != 0) aim.X = inputX;
            if (inputY != 0) aim.Y = inputY;
            if (aim == Vector2.Zero) aim.X = FacingDir;
            aim.Normalize();
            AimDir = aim;
        }
        else
        {
            // 8-way aim based on input, default to facing
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
                vel.Y = JumpForce;
                if (inputX != 0) vel.X = inputX * Speed;
                _jumpsLeft = MaxJumps - 1;
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
                _meleeCooldown = MeleeRate;
                WantsToMelee = true;
                MeleeDirection = AimDir;
                MeleeTimer = MeleeActiveTime;
            }
            _meleeHeld = kOnRope;

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
                    vel.Y = JumpForce;
                    vel.X = _currentWallClimbSide * Speed;
                    _jumpsLeft = MaxJumps - 1;
                    IsGrounded = false;
                    _wasGrounded = false;
                }
                else
                {
                    // Wall hop — launch up, reattach by pressing toward wall
                    IsOnWall = false;
                    _wallHopCooldown = WallHopCooldownTime;
                    vel.Y = JumpForce * 0.7f;
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
                _meleeCooldown = MeleeRate;
                WantsToMelee = true;
                MeleeDirection = AimDir;
                MeleeTimer = MeleeActiveTime;
            }
            _meleeHeld = kOnWall;

            _prevKb = kb;
            return;
        }

        if (IsCartwheeling)
        {
            _cartwheelTimer -= dt;
            float t = 1f - (_cartwheelTimer / CartwheelDuration);
            vel.X = _cartwheelDir * CartwheelSpeed;
            if (t < 0.15f) vel.Y = CartwheelJumpForce; // initial pop
            else vel.Y += Gravity * dt;
            if (_cartwheelTimer <= 0)
            {
                IsCartwheeling = false;
                vel.X = _cartwheelDir * Speed;
            }
        }
        else if (IsVaultKicking)
        {
            _vaultKickTimer -= dt;
            vel.X = _vaultKickDir * VaultKickSpeed;
            vel.Y += Gravity * dt;
            _jumpHeld = kb.IsKeyDown(Keys.Space); // track space so uppercut can trigger
            if (_vaultKickTimer <= 0)
            {
                IsVaultKicking = false;
                vel.X = _vaultKickDir * Speed; // return to normal speed
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
                vel.Y += Gravity * dt;
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
            vel.Y += Gravity * dt;
            if (_flipTimer <= 0)
            {
                IsFlipping = false;
                vel.X = inputX * Speed; // return to normal
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
                vel.X = _bladeDashDir * DashSpeed;
            }
        }
        else if (IsSliding)
        {
            _slideTimer -= dt;
            float elapsed = SlideDuration - _slideTimer;
            // Vault kick: jump during slide (after minimum slide time, requires fresh space press)
            bool spaceSlide = kb.IsKeyDown(Keys.Space);
            if (EnableVaultKick && spaceSlide && !_jumpHeld && elapsed >= VaultKickMinSlideTime)
            {
                IsSliding = false;
                IsVaultKicking = true;
                _vaultKickTimer = VaultKickDuration;
                _vaultKickDir = _slideDir;
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
                    IsSliding = false;
                    vel.X = _slideDir * SlideEndSpeed;
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
        else
        {
            float moveSpeed = (IsDashing && inputX == _dashDir) ? DashSpeed : Speed;
            vel.X = inputX * moveSpeed;

            // Jump (Space) — only if NOT holding S (S+Space = slide)
            int minJumpsLeft = EnableDoubleJump ? 0 : 1; // 1 = block second jump
            if (spacePressed && !_jumpHeld && _jumpsLeft > minJumpsLeft && inputY <= 0)
            {
                float now_flip = (float)System.DateTime.UtcNow.TimeOfDay.TotalSeconds;
                bool isSecondJump = _jumpsLeft < MaxJumps; // already used first jump

                // Flip: second jump within tight window
                if (EnableFlip && isSecondJump && (now_flip - _firstJumpTime) < FlipInputWindow)
                {
                    IsFlipping = true;
                    _flipTimer = FlipDuration;
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
                    vel.Y = JumpForce;
                    _jumpsLeft--;
                    if (!isSecondJump) _firstJumpTime = now_flip;
                }
                IsGrounded = false;
                _wasGrounded = false;
            }
            _jumpHeld = spacePressed;

            vel.Y += Gravity * dt;
        }

        // --- Right hand / Ranged (J) ---
        bool jPressed = kb.IsKeyDown(Keys.J);
        if (HasRangedWeapon && jPressed && !_shootHeld && _shootCooldown <= 0f)
        {
            _shootCooldown = ShootRate;
            WantsToShoot = true;
            ShootDirection = AimDir;
        }
        _shootHeld = jPressed;

        // --- Left hand / Melee (K) ---
        bool kPressed = kb.IsKeyDown(Keys.K);

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

        if (HasMeleeWeapon && !IsSpinningMelee && kPressed && !_meleeHeld && _meleeCooldown <= 0f)
        {
            _meleeCooldown = MeleeRate;
            WantsToMelee = true;
            MeleeDirection = AimDir;
            MeleeTimer = MeleeActiveTime;
        }
        _meleeHeld = kPressed;

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

        // Slope floor collision
        bool _onSlope = false;
        float _slopeFloorY = float.MaxValue;
        if (tileGrid != null)
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
            else if (_wasGrounded && vel.Y >= 0)
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

        // Slope ceiling collision — deflect momentum along surface
        if (tileGrid != null && vel.Y <= 0)
        {
            TileType ceilTile;
            float slopeCeilY = tileGrid.GetSlopeCeilY(pos.X, pos.Y, Width, Height, out ceilTile);
            if (slopeCeilY > float.MinValue && pos.Y <= slopeCeilY + 4)
            {
                pos.Y = slopeCeilY;
                
                // Convert upward momentum into horizontal momentum along the ceiling slope
                if (vel.Y < 0 && ceilTile != TileType.Empty)
                {
                    float upSpeed = MathF.Abs(vel.Y);
                    float deflectRatio = 0.7f;
                    if (ceilTile == TileType.GentleCeilRight || ceilTile == TileType.GentleCeilLeft)
                        deflectRatio = 0.85f;
                    if (IsUppercutting) deflectRatio = 0.95f;
                    
                    float hBoost = upSpeed * deflectRatio;
                    if (ceilTile == TileType.SlopeCeilRight || ceilTile == TileType.GentleCeilRight)
                        vel.X += hBoost;
                    else
                        vel.X -= hBoost;
                    
                    // Push player below the ceiling surface so they don't re-collide next frame
                    vel.Y = upSpeed * 0.3f;
                    pos.Y = slopeCeilY + 4;
                    
                    // Cancel uppercut so the deflection momentum isn't overridden
                    if (IsUppercutting)
                    {
                        IsUppercutting = false;
                        _uppercutTimer = 0;
                    }
                }
                else
                {
                    vel.Y = 0;
                }
            }
        }
            }
        }

        // Ceiling collision (bonk head) — skip if near ceiling slope tiles
        if (ceilings != null && vel.Y < 0)
        {
            foreach (var ceil in ceilings)
            {
                float prevTop = Position.Y;
                float newTop = pos.Y;
                if (prevTop >= ceil.Bottom - 2 && newTop < ceil.Bottom &&
                    pos.X + Width > ceil.X && pos.X < ceil.X + ceil.Width)
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
                        pos.Y = ceil.Bottom;
                        vel.Y = 0;
                    }
                }
            }
        }

        // Solid floor collision (stand on top + block from below)
        if (solidFloors != null)
        {
            foreach (var sf in solidFloors)
            {
                bool xOverlap = pos.X + Width > sf.X && pos.X < sf.X + sf.Width;
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
                        float prevTop = Position.Y;
                        float newTop = pos.Y;
                        if (prevTop >= sf.Bottom - 2 && newTop < sf.Bottom)
                        {
                            pos.Y = sf.Bottom;
                            vel.Y = 0;
                        }
                    }
                    // Push out horizontally if inside
                    // Skip push-out only if a slope tile is actually adjacent to this solid block's edge
                    float playerBottom = pos.Y + Height;
                    float playerTop = pos.Y;
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
                        float playerCenterX = pos.X + Width / 2f;
                        float sfCenterX = sf.X + sf.Width / 2f;
                        if (playerCenterX < sfCenterX)
                            pos.X = sf.X - Width;
                        else
                            pos.X = sf.X + sf.Width;
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
            var pRect = new Rectangle((int)pos.X, (int)pos.Y, Width, Height);
            foreach (var w in solidWalls)
            {
                if (pRect.Intersects(w))
                {
                    // Push out horizontally
                    float playerCenter = pos.X + Width / 2f;
                    float wallCenter = w.X + w.Width / 2f;
                    if (playerCenter < wallCenter)
                        pos.X = w.Left - Width;
                    else
                        pos.X = w.Right;
                    vel.X = 0;
                }
            }
        }

        pos.X = MathHelper.Clamp(pos.X, WorldLeft, WorldRight - Width);

        Position = pos;
        Velocity = vel;
        _wasGrounded = IsGrounded;
        _wasOnSlope = _onSlope;
        _prevKb = kb;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
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
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                IsCrouching ? Color.DarkGray : new Color(100, 140, 100));
            int gripX = _currentWallClimbSide == 1 ? (int)Position.X : (int)Position.X + Width - 4;
            spriteBatch.Draw(pixel,
                new Rectangle(gripX, (int)Position.Y + 8, 4, 6), Color.White * 0.5f);
            spriteBatch.Draw(pixel,
                new Rectangle(gripX, (int)Position.Y + Height - 14, 4, 6), Color.White * 0.5f);
        }
        else if (IsOnRope)
        {
            // Draw player on rope — slightly different color to indicate rope state
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                IsCrouching ? Color.DarkGray : new Color(160, 120, 80));
            // Draw grip indicator
            int gripY = (int)Position.Y + Height / 2 - 2;
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X + Width / 2 - 3, gripY, 6, 4),
                Color.White * 0.7f);
        }
        else if (IsCartwheeling)
        {
            // Flash blue like a dash
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                Color.CornflowerBlue * 0.8f);
        }
        else if (IsVaultKicking)
        {
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                Color.Orange * 0.9f);
            var kickBox = VaultKickHitbox;
            spriteBatch.Draw(pixel, kickBox, Color.Red * 0.6f);
        }
        else if (IsUppercutting)
        {
            // Yellow body with red hurtbox above head
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                Color.Yellow * 0.9f);
            var ucBox = UppercutHitbox;
            spriteBatch.Draw(pixel, ucBox, Color.Red * 0.6f);
        }
        else if (IsFlipping)
        {
            // Compact tuck — square shape, magenta tint
            float t = 1f - (_flipTimer / FlipDuration);
            int size = (int)(Width * 0.75f);
            int cx = (int)Position.X + (Width - size) / 2;
            int cy = (int)Position.Y + (Height - size) / 2;
            spriteBatch.Draw(pixel,
                new Rectangle(cx, cy, size, size),
                Color.Magenta * 0.85f);
        }
        else if (IsBladeDashing)
        {
            // White/purple flash with wide hurtbox
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                Color.White * 0.9f);
            var bdBox = BladeDashHitbox;
            spriteBatch.Draw(pixel, bdBox, Color.Purple * 0.7f);
        }
        else if (IsSliding)
        {
            int drawY = (int)Position.Y + Height - SlideHeight;
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, drawY, Width, SlideHeight),
                Color.CornflowerBlue);
        }
        else if (IsCrouching)
        {
            int drawY = (int)Position.Y + Height - CrouchHeight;
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, drawY, Width, CrouchHeight),
                Color.DarkGray);

            // Aim line
            var center = new Vector2((int)Position.X + Width / 2f, drawY + CrouchHeight / 2f);
            for (int i = 0; i < 15; i++)
            {
                var p = center + AimDir * (i * 2);
                spriteBatch.Draw(pixel, new Rectangle((int)p.X, (int)p.Y, 2, 2), Color.Yellow * 0.6f);
            }
        }
        else
        {
            var bodyColor = IsDashing ? new Color(180, 180, 180) : Color.Gray;
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                bodyColor);

            int notchX = FacingDir == 1 ? (int)Position.X + Width - 4 : (int)Position.X;
            spriteBatch.Draw(pixel,
                new Rectangle(notchX, (int)Position.Y + Height / 2 - 3, 4, 6),
                Color.LightGray);
        }

        // Spinning melee overlay — draw the current melee hitbox in red
        if (IsSpinningMelee && MeleeTimer > 0)
        {
            spriteBatch.Draw(pixel, MeleeHitbox, Color.Red * 0.5f);
        }

        // Draw melee hitbox when active
        if (MeleeTimer > 0)
        {
            var box = MeleeHitbox;
            spriteBatch.Draw(pixel, box, Color.Red * 0.5f);
        }
    }
}
