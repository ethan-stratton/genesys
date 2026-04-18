using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class Enemy
{
    public Vector2 Position { get; private set; }
    public const int Size = 28;
    private const float Speed = 120f;
    public bool IsDead { get; set; }

    public Enemy(Vector2 start)
    {
        Position = start;
        IsDead = false;
    }

    public void Update(float dt, Vector2 playerPos)
    {
        var dir = playerPos - (Position + new Vector2(Size / 2f));
        if (dir != Vector2.Zero) dir.Normalize();
        Position += dir * Speed * dt;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel, 
            new Rectangle((int)Position.X, (int)Position.Y, Size, Size), 
            Color.IndianRed);
    }
}