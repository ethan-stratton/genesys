using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ArenaShooter;

public class Player
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public const int Width = 32;
    public const int Height = 48;
    private const float Speed = 250f;
    private const float Gravity = 900f;
    private const float JumpForce = -420f;
    public bool IsGrounded { get; set; }
    private bool _wasGrounded;
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
    private const float RopeClimbSpeed = 200f;
    private bool _ropeDropRequested;
    private float _lastDownTapTime;
    private bool _downWasUp_rope;
    private const float RopeDoubleTapWindow = 0.3f;
    private bool _ropeDisengaged; // true after dropping — stays true until player leaves rope hitbox

    // Aim direction
    public Vector2 AimDir { get; private set; }

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

    public Rectangle MeleeHitbox
    {
        get
        {
            var center = Position + new Vector2(Width / 2f, Height / 2f);
            var dir = MeleeDirection;
            // Offset the hitbox center along the aim direction
            var hbCenter = center + dir * (MeleeRange * 0.6f);
            return new Rectangle(
                (int)(hbCenter.X - MeleeWidth / 2f),
                (int)(hbCenter.Y - MeleeWidth / 2f),
                MeleeWidth, MeleeWidth);
        }
    }

    public void Update(float dt, KeyboardState kb, float floorY, Rectangle[] platforms, float[] ropeXPositions = null, float ropeTop = 0f, float ropeBottom = 550f)
    {
        WantsToShoot = false;
        WantsToMelee = false;
        WantsDropThrough = false;
        _shootCooldown -= dt;
        _meleeCooldown -= dt;
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
            foreach (var rx in ropeXPositions)
            {
                if (MathF.Abs(playerCenterX - rx) < 16f && playerCenterY >= ropeTop && playerCenterY <= ropeBottom)
                {
                    nearAnyRope = true;
                    if (!_ropeDisengaged && !IsSliding && !IsDashing)
                    {
                        IsOnRope = true;
                        _ropeX = rx;
                        _ropeDropRequested = false;
                        _downWasUp_rope = true;
                        break;
                    }
                }
            }
            // Clear disengage flag once player has left ALL rope hitboxes
            if (!nearAnyRope) _ropeDisengaged = false;
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

        // --- Raw directional input ---
        int inputX = 0;
        int inputY = 0;
        if (kb.IsKeyDown(Keys.A)) inputX -= 1;
        if (kb.IsKeyDown(Keys.D)) inputX += 1;
        if (kb.IsKeyDown(Keys.W)) inputY -= 1;
        if (kb.IsKeyDown(Keys.S)) inputY += 1;

        // --- Dash detection (double-tap A or D) ---
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

        // --- Crouch (shift while grounded, not sliding) ---
        IsCrouching = shift && (_wasGrounded || IsOnRope) && !IsSliding;

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
        WantsDropThrough = _dropIgnoreTimer > 0f;

        // --- Slide (S + Space while grounded, OR Shift + Space with no direction from crouch) ---
        bool spacePressed = kb.IsKeyDown(Keys.Space);
        bool wantsSlide = (inputY > 0 && !IsCrouching) || (IsCrouching && inputX == 0);
        if (wantsSlide && spacePressed && !_jumpHeld && _wasGrounded && !IsSliding && !IsCartwheeling && _slideCooldownTimer <= 0f)
        {
            IsSliding = true;
            _slideTimer = SlideDuration;
            _slideCooldownTimer = SlideCooldown;
            _slideDir = FacingDir;
        }

        // --- Cartwheel (Shift + A/D + Space while grounded) ---
        if (shift && inputX != 0 && spacePressed && !_jumpHeld && _wasGrounded && !IsSliding && !IsCartwheeling && _cartwheelCooldownTimer <= 0f)
        {
            IsCartwheeling = true;
            _cartwheelTimer = CartwheelDuration;
            _cartwheelCooldownTimer = CartwheelCooldown;
            _cartwheelDir = inputX;
            IsCrouching = false; // override crouch
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
            if (shift && inputX != 0 && spaceFresh && _cartwheelCooldownTimer <= 0f)
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
            posRope.Y = MathHelper.Clamp(Position.Y + vel.Y * dt, ropeTop, ropeBottom - Height);
            Position = posRope;
            Velocity = vel;

            // Ranged/melee still available on rope
            bool jOnRope = kb.IsKeyDown(Keys.J);
            if (jOnRope && !_shootHeld && _shootCooldown <= 0f)
            {
                _shootCooldown = ShootRate;
                WantsToShoot = true;
                ShootDirection = AimDir;
            }
            _shootHeld = jOnRope;

            bool kOnRope = kb.IsKeyDown(Keys.K);
            if (kOnRope && !_meleeHeld && _meleeCooldown <= 0f)
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
        else if (IsSliding)
        {
            _slideTimer -= dt;
            // Decelerate from start speed to end speed over duration
            float t = 1f - (_slideTimer / SlideDuration); // 0 at start, 1 at end
            float speed = MathHelper.Lerp(SlideStartSpeed, SlideEndSpeed, t);
            vel.X = _slideDir * speed;
            vel.Y = 0;
            if (_slideTimer <= 0)
            {
                IsSliding = false;
                // Brief momentum carry — keep end speed in slide direction
                vel.X = _slideDir * SlideEndSpeed;
            }
        }
        else if (IsCrouching)
        {
            vel.X = 0;
            vel.Y = 0;
        }
        else
        {
            float moveSpeed = (IsDashing && inputX == _dashDir) ? DashSpeed : Speed;
            vel.X = inputX * moveSpeed;

            // Jump (Space) — only if NOT holding S (S+Space = slide)
            if (spacePressed && !_jumpHeld && _jumpsLeft > 0 && inputY <= 0)
            {
                vel.Y = JumpForce;
                _jumpsLeft--;
                IsGrounded = false;
                _wasGrounded = false;
            }
            _jumpHeld = spacePressed;

            vel.Y += Gravity * dt;
        }

        // --- Right hand / Ranged (J) ---
        bool jPressed = kb.IsKeyDown(Keys.J);
        if (jPressed && !_shootHeld && _shootCooldown <= 0f)
        {
            _shootCooldown = ShootRate;
            WantsToShoot = true;
            ShootDirection = AimDir;
        }
        _shootHeld = jPressed;

        // --- Left hand / Melee (K) ---
        bool kPressed = kb.IsKeyDown(Keys.K);
        if (kPressed && !_meleeHeld && _meleeCooldown <= 0f)
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

        // End slide/cartwheel if airborne (slide only)
        if (!IsGrounded && IsSliding) { IsSliding = false; _ropeDisengaged = true; }

        pos.X = MathHelper.Clamp(pos.X, 0, 800 - Width);

        Position = pos;
        Velocity = vel;
        _wasGrounded = IsGrounded;
        _prevKb = kb;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (IsOnRope)
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

        // Draw melee hitbox when active
        if (MeleeTimer > 0)
        {
            var box = MeleeHitbox;
            spriteBatch.Draw(pixel, box, Color.Red * 0.5f);
        }
    }
}
