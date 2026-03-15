using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ArenaShooter;

public class Player
{
    public Vector2 Position { get; set; }
    public const int Size = 32;
    private const float Speed = 300f;

    public Player(Vector2 startPos)
    {
        Position = startPos;
    }

    public void Update(float dt, KeyboardState kb)
    {
        var move = Vector2.Zero;
        if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up))    move.Y -= 1;
        if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down))  move.Y += 1;
        if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  move.X -= 1;
        if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) move.X += 1;

        if (move != Vector2.Zero) move.Normalize();

        Position = Position + move * Speed * dt;
        Position = new Vector2(
            MathHelper.Clamp(Position.X, 0, 800 - Size),
            MathHelper.Clamp(Position.Y, 0, 600 - Size)
        );
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel, 
            new Rectangle((int)Position.X, (int)Position.Y, Size, Size), 
            Color.Gray);
    }
}