using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

public class Thornback
{
    public Vector2 Position;
    public bool Alive = true;
    public int Hp = 20;
    public const int Width = 32, Height = 28;
    public float DamageCooldown;
    public float HitFlash;

    public Thornback(Vector2 pos)
    {
        Position = pos;
    }

    public Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public void Update(float dt)
    {
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
    }

    public int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive || DamageCooldown > 0) return 0;
        if (Rect.Intersects(playerRect))
        {
            DamageCooldown = 0.5f;
            return 8;
        }
        return 0;
    }

    public bool TakeHit(int damage)
    {
        if (!Alive) return false;
        Hp -= damage;
        HitFlash = 0.1f;
        if (Hp <= 0) { Alive = false; return true; }
        return false;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        Color baseColor = HitFlash > 0 ? Color.White : new Color(60, 100, 30);
        sb.Draw(pixel, Rect, baseColor);
        sb.Draw(pixel, new Rectangle((int)Position.X - 3, (int)Position.Y + 6, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 1, (int)Position.Y + 8, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + 8, (int)Position.Y - 3, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 12, (int)Position.Y - 3, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + 4, (int)Position.Y + 4, Width - 8, Height - 8), new Color(40, 70, 20));
    }
}
