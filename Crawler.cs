using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

public class Crawler
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool Alive = true;
    public int Hp = 3;
    public const int Width = 16, Height = 10;
    public float PatrolLeft, PatrolRight;
    public float SurfaceLeft, SurfaceRight;
    public int Dir = 1;
    public bool Aggroed;
    public float AggroRange = 200f;
    public float Speed = 60f;
    public float ChaseSpeed = 100f;
    public float DamageCooldown;
    public float MeleeHitCooldown;
    public float HitFlash;
    private bool _onGround;

    private const float Gravity = 600f;

    public Crawler(Vector2 pos, float patrolLeft, float patrolRight, float surfaceLeft, float surfaceRight)
    {
        Position = pos;
        PatrolLeft = patrolLeft;
        PatrolRight = patrolRight;
        SurfaceLeft = surfaceLeft;
        SurfaceRight = surfaceRight;
    }

    public Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    /// <summary>
    /// Update with tile-aware physics. Call UpdateSurfaceEdges after spawning or when surface changes.
    /// </summary>
    public void Update(float dt, Vector2 playerCenter,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors, float floorY)
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
            Velocity.X = Dir * ChaseSpeed;
        }
        else
        {
            Velocity.X = Dir * Speed;
            if (Position.X <= PatrolLeft) { Position.X = PatrolLeft; Dir = 1; }
            if (Position.X + Width >= PatrolRight) { Position.X = PatrolRight - Width; Dir = -1; }
        }

        // Apply gravity and tile collision
        _onGround = EnemyPhysics.ApplyGravityAndCollision(
            ref Position, ref Velocity,
            Width, Height, Gravity, dt,
            tileGrid, tileSize,
            platforms, solidFloors, floorY);

        // Clamp to surface edges
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

    /// <summary>
    /// Refresh surface edge detection using tile-aware method.
    /// </summary>
    public void UpdateSurfaceEdges(TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors,
        float boundsLeft, float boundsRight)
    {
        float footY = Position.Y + Height;
        var edges = EnemyPhysics.FindSurfaceEdges(
            Position.X, footY, Width,
            tileGrid, tileSize,
            platforms, solidFloors,
            boundsLeft, boundsRight);
        SurfaceLeft = edges.Left;
        SurfaceRight = edges.Right;
        PatrolLeft = MathF.Max(PatrolLeft, SurfaceLeft);
        PatrolRight = MathF.Min(PatrolRight, SurfaceRight);
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
