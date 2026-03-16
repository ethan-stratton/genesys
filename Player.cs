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

    public void Update(float dt, KeyboardState kb, float floorY, Rectangle[] platforms)
    {
        WantsToShoot = false;
        WantsToMelee = false;
        WantsDropThrough = false;
        _shootCooldown -= dt;
        _meleeCooldown -= dt;
        _slideCooldownTimer -= dt;
        if (MeleeTimer > 0) MeleeTimer -= dt;

        bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);

        // --- Raw directional input ---
        int inputX = 0;
        int inputY = 0;
        if (kb.IsKeyDown(Keys.A)) inputX -= 1;
        if (kb.IsKeyDown(Keys.D)) inputX += 1;
        if (kb.IsKeyDown(Keys.W)) inputY -= 1;
        if (kb.IsKeyDown(Keys.S)) inputY += 1;

        // --- Crouch (shift while grounded, not sliding) ---
        IsCrouching = shift && _wasGrounded && !IsSliding;

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

        // --- Slide (S + Space while grounded, not already sliding) ---
        bool spacePressed = kb.IsKeyDown(Keys.Space);
        if (inputY > 0 && spacePressed && !_jumpHeld && _wasGrounded && !IsSliding && !IsCrouching && _slideCooldownTimer <= 0f)
        {
            IsSliding = true;
            _slideTimer = SlideDuration;
            _slideCooldownTimer = SlideCooldown;
            _slideDir = FacingDir;
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

        if (IsSliding)
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
            vel.X = inputX * Speed;

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

        // End slide if airborne
        if (!IsGrounded && IsSliding) IsSliding = false;

        pos.X = MathHelper.Clamp(pos.X, 0, 800 - Width);

        Position = pos;
        Velocity = vel;
        _wasGrounded = IsGrounded;
        _prevKb = kb;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (IsSliding)
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
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
                Color.Gray);

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
