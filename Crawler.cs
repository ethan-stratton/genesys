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

    public Vector2 KnockbackVel;
    public Vector2 VisualScale = Vector2.One;
    public float SquashResistance = 0f;
    private float _squashHoldTimer;

    // Dummy mode: high HP, no aggro, respawns at original position
    public bool IsDummy;
    public bool AlwaysCrit;
    private Vector2 _spawnPos;
    private float _respawnTimer;
    private const float RespawnDelay = 2f;

    private const float Gravity = 600f;

    public Crawler(Vector2 pos, float patrolLeft, float patrolRight, float surfaceLeft, float surfaceRight)
    {
        Position = pos;
        _spawnPos = pos;
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
        if (!Alive)
        {
            // Dummy respawn
            if (IsDummy)
            {
                _respawnTimer -= dt;
                if (_respawnTimer <= 0)
                {
                    Alive = true;
                    Hp = 9999;
                    Position = _spawnPos;
                    Velocity = Vector2.Zero;
                    KnockbackVel = Vector2.Zero;
                    VisualScale = Vector2.One;
                    HitFlash = 0;
                }
            }
            return;
        }
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;

        if (KnockbackVel.LengthSquared() > 1f)
        {
            Position += KnockbackVel * dt;
            KnockbackVel *= 0.85f;
        }

        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);

        // Dummies don't aggro or move
        if (IsDummy)
        {
            Velocity.X = 0;
        }
        else
        {
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
        } // end non-dummy movement

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

    public bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        HitFlash = 0.15f;
        MeleeHitCooldown = 0.2f;
        KnockbackVel = new Vector2(knockbackX, knockbackY);
        float squashAmount = 1f - SquashResistance;
        VisualScale = new Vector2(1f + 0.3f * squashAmount, 1f - 0.25f * squashAmount);
        _squashHoldTimer = 0.05f;
        if (Hp <= 0) { Alive = false; if (IsDummy) _respawnTimer = RespawnDelay; return true; }
        return false;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        Color bodyColor = HitFlash > 0 ? Color.Red : IsDummy ? new Color(140, 100, 160) : (Aggroed ? new Color(120, 60, 20) : new Color(80, 50, 20));
        int scaledW = (int)(Width * VisualScale.X);
        int scaledH = (int)(Height * VisualScale.Y);
        int drawX = (int)Position.X + Width / 2 - scaledW / 2;
        int drawY = (int)Position.Y + Height - scaledH;
        sb.Draw(pixel, new Rectangle(drawX, drawY, scaledW, scaledH), bodyColor);
        sb.Draw(pixel, new Rectangle((int)Position.X + 2, (int)Position.Y + Height, 2, 3), new Color(60, 30, 10));
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 4, (int)Position.Y + Height, 2, 3), new Color(60, 30, 10));
    }
}
