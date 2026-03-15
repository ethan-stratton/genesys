using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

public class Bullet
{
    public Vector2 Position;
    public Vector2 Direction { get; private set; }
    public const int Size = 6;
    private const float Speed = 600f;
    public bool IsDead { get; set; }

    public Bullet(Vector2 start, Vector2 direction)
    {
        Position = start;
        Direction = direction;
        IsDead = false;
    }

    public void Update(float dt)
    {
        Position += Direction * Speed * dt;

        // Mark as dead if off-screen
        if (Position.X < -Size || Position.X > 800 || Position.Y < -Size || Position.Y > 600)
            IsDead = true;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel, 
            new Rectangle((int)Position.X, (int)Position.Y, Size, Size), 
            Color.White);
    }
}