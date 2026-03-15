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

    public Player(Vector2 startPos)
    {
        Position = startPos;
        Velocity = Vector2.Zero;
        IsGrounded = false;
    }

    public void Update(float dt, KeyboardState kb, float floorY)
    {
        // Horizontal movement
        var moveX = 0f;
        if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  moveX -= 1;
        if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) moveX += 1;

        var vel = Velocity;
        vel.X = moveX * Speed;

        // Jump
        if (IsGrounded && (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up) || kb.IsKeyDown(Keys.Space)))
        {
            vel.Y = JumpForce;
            IsGrounded = false;
        }

        // Gravity
        vel.Y += Gravity * dt;

        // Apply velocity
        var pos = Position + vel * dt;

        // Floor collision
        if (pos.Y + Height >= floorY)
        {
            pos.Y = floorY - Height;
            vel.Y = 0;
            IsGrounded = true;
        }

        // Screen bounds (horizontal)
        pos.X = MathHelper.Clamp(pos.X, 0, 800 - Width);

        Position = pos;
        Velocity = vel;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel,
            new Rectangle((int)Position.X, (int)Position.Y, Width, Height),
            Color.Gray);
    }
}
