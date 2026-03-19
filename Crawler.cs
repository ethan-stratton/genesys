using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

public class Crawler
{
    public Vector2 Position;
    public bool Alive = true;
    public int Hp = 3;
    public const int Width = 16, Height = 10;
    public float PatrolLeft, PatrolRight;
    public float SurfaceLeft, SurfaceRight; // hard edges — crawler won't walk off
    public int Dir = 1;
    public bool Aggroed;
    public float AggroRange = 200f;
    public float Speed = 60f;
    public float ChaseSpeed = 100f;
    public float DamageCooldown;
    public float MeleeHitCooldown; // prevents being hit multiple times by same melee swing
    public float HitFlash;

    public Crawler(Vector2 pos, float patrolLeft, float patrolRight, float surfaceLeft, float surfaceRight)
    {
        Position = pos;
        PatrolLeft = patrolLeft;
        PatrolRight = patrolRight;
        SurfaceLeft = surfaceLeft;
        SurfaceRight = surfaceRight;
    }

    public Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public void Update(float dt, Vector2 playerCenter)
    {
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;

        float dist = Vector2.Distance(playerCenter, Position + new Vector2(Width / 2f, Height / 2f));
        Aggroed = dist < AggroRange;

        if (Aggroed)
        {
            float dx = playerCenter.X - (Position.X + Width / 2f);
            Dir = dx > 0 ? 1 : -1;
            Position.X += Dir * ChaseSpeed * dt;
        }
        else
        {
            Position.X += Dir * Speed * dt;
            if (Position.X <= PatrolLeft) { Position.X = PatrolLeft; Dir = 1; }
            if (Position.X + Width >= PatrolRight) { Position.X = PatrolRight - Width; Dir = -1; }
        }

        // Clamp to surface edges — never walk off platforms
        if (Position.X < SurfaceLeft)
        {
            Position.X = SurfaceLeft;
            if (!Aggroed) Dir = 1;
        }
        if (Position.X + Width > SurfaceRight)
        {
            Position.X = SurfaceRight - Width;
            if (!Aggroed) Dir = -1;
        }
    }

    public int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive || DamageCooldown > 0) return 0;
        if (Rect.Intersects(playerRect))
        {
            DamageCooldown = 1.0f;
            return 5;
        }
        return 0;
    }

    public bool TakeHit(int damage)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        HitFlash = 0.15f;
        MeleeHitCooldown = 0.2f;
        if (Hp <= 0) { Alive = false; return true; }
        return false;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        Color bodyColor = HitFlash > 0 ? Color.Red : (Aggroed ? new Color(120, 60, 20) : new Color(80, 50, 20));
        sb.Draw(pixel, Rect, bodyColor);
        sb.Draw(pixel, new Rectangle((int)Position.X + 2, (int)Position.Y + Height, 2, 3), new Color(60, 30, 10));
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 4, (int)Position.Y + Height, 2, 3), new Color(60, 30, 10));
    }
}
