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

    // Jump
    private int _jumpsLeft;
    private const int MaxJumps = 2;
    private bool _jumpHeld;

    // Dash
    private const float DashSpeed = 700f;
    private const float DashDuration = 0.15f;
    private const float DashCooldown = 0.6f;

    private float _dashTimer;
    private float _dashCooldownTimer;
    private int _dashDir; // -1 left, 1 right
    private bool _hasAirDashed;
    private bool _dashHeld;
    public bool IsDashing => _dashTimer > 0f;

    private KeyboardState _prevKb;

    public Player(Vector2 startPos)
    {
        Position = startPos;
        Velocity = Vector2.Zero;
        IsGrounded = false;
    }

    public void Update(float dt, KeyboardState kb, float floorY, Rectangle[] platforms)
    {
        _dashCooldownTimer -= dt;

        // Detect shift+direction for dash
        bool shiftPressed = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
        if (shiftPressed && !_dashHeld && !IsDashing && _dashCooldownTimer <= 0f && !(!IsGrounded && _hasAirDashed))
        {
            int dir = 0;
            if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  dir = -1;
            if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) dir = 1;
            if (dir != 0) StartDash(dir);
        }
        _dashHeld = shiftPressed;

        var vel = Velocity;

        if (IsDashing)
        {
            _dashTimer -= dt;
            vel.X = _dashDir * DashSpeed;
            vel.Y = 0; // flat dash, no gravity during dash
        }
        else
        {
            // Horizontal movement
            var moveX = 0f;
            if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  moveX -= 1;
            if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) moveX += 1;
            vel.X = moveX * Speed;

            // Jump
            bool jumpPressed = kb.IsKeyDown(Keys.Space);
            if (jumpPressed && !_jumpHeld && _jumpsLeft > 0)
            {
                vel.Y = JumpForce;
                _jumpsLeft--;
                IsGrounded = false;
            }
            _jumpHeld = jumpPressed;

            // Gravity
            vel.Y += Gravity * dt;
        }

        // Apply velocity
        var pos = Position + vel * dt;

        // Floor collision
        IsGrounded = false;
        if (pos.Y + Height >= floorY)
        {
            pos.Y = floorY - Height;
            vel.Y = 0;
            IsGrounded = true;
            _jumpsLeft = MaxJumps;
            _hasAirDashed = false;
        }

        // Platform collisions (only when falling)
        if (Velocity.Y >= 0)
        {
            foreach (var plat in platforms)
            {
                // Were we above the platform last frame?
                float prevBottom = Position.Y + Height;
                float newBottom = pos.Y + Height;
                if (prevBottom <= plat.Y + 2 && newBottom >= plat.Y &&
                    pos.X + Width > plat.X && pos.X < plat.X + plat.Width)
                {
                    pos.Y = plat.Y - Height;
                    vel.Y = 0;
                    IsGrounded = true;
                    _jumpsLeft = MaxJumps;
                    _hasAirDashed = false;
                }
            }
        }

        // Screen bounds (horizontal)
        pos.X = MathHelper.Clamp(pos.X, 0, 800 - Width);

        Position = pos;
        Velocity = vel;
        _prevKb = kb;
    }

    private void StartDash(int dir)
    {
        _dashDir = dir;
        _dashTimer = DashDuration;
        _dashCooldownTimer = DashCooldown;
        if (!IsGrounded) _hasAirDashed = true;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Flash white during dash
        var color = IsDashing ? Color.White : Color.Gray;
        spriteBatch.Draw(pixel,
            new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
            color);
    }
}
