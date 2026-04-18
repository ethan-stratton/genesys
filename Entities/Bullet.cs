using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class Bullet
{
    public Vector2 Position;
    public Vector2 Direction { get; private set; }
    public const int Size = 6;
    private const float Speed = 1000f;
    private float _lifetime = 3f;
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
        _lifetime -= dt;
        if (_lifetime <= 0f)
            IsDead = true;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel, 
            new Rectangle((int)Position.X, (int)Position.Y, Size, Size), 
            Color.White);
    }
}