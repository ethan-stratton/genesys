using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

public class Bird
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool Alive = true;
    public const int Width = 10, Height = 8;
    public Vector2 KnockbackVel;
    public Vector2 VisualScale = Vector2.One;
    public float SquashResistance = 0.2f;
    private float _squashHoldTimer;

    private enum State { Perched, Pecking, Hopping, Fleeing, Flying }
    private State _state = State.Perched;
    private float _stateTimer;
    private float _peckTimer;
    private int _peckFrame;
    private int _dir = 1;
    private float _fleeSpeed = 180f;
    private float _hopSpeed = 30f;
    private float _flightTime;
    private readonly Random _rng;
    private bool _onGround;

    public float FleeRange = 80f;
    public float AlertRange = 140f;

    public float GroundY;
    public float SurfaceLeft, SurfaceRight;

    private const float FlyGravity = 100f;

    public Bird(Vector2 pos, float surfaceLeft, float surfaceRight, Random rng)
    {
        Position = pos;
        GroundY = pos.Y;
        SurfaceLeft = surfaceLeft;
        SurfaceRight = surfaceRight;
        _rng = rng;
        _stateTimer = 1f + (float)rng.NextDouble() * 3f;
    }

    public Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public void Update(float dt, Vector2 playerPos,
        TileGrid tileGrid, int tileSize,
        Rectangle[] platforms, Rectangle[] solidFloors, float floorY)
    {
        if (!Alive) return;

        if (KnockbackVel.LengthSquared() > 1f)
        {
            Position += KnockbackVel * dt;
            KnockbackVel *= 0.85f;
        }

        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);

        float dist = Vector2.Distance(playerPos, Position + new Vector2(Width / 2f, Height / 2f));
        float dx = playerPos.X - (Position.X + Width / 2f);

        // React to player proximity
        if (_state != State.Flying && _state != State.Fleeing && dist < FleeRange)
        {
            _state = State.Flying;
            Velocity.X = dx > 0 ? -_fleeSpeed : _fleeSpeed;
            Velocity.Y = -200f;
            _flightTime = 0;
            _dir = Velocity.X > 0 ? 1 : -1;
        }
        else if (_state != State.Flying && _state != State.Fleeing && dist < AlertRange)
        {
            if (_state != State.Fleeing)
            {
                _state = State.Fleeing;
                _dir = dx > 0 ? -1 : 1;
                _stateTimer = 0.8f + (float)_rng.NextDouble() * 0.5f;
            }
        }

        _stateTimer -= dt;

        switch (_state)
        {
            case State.Perched:
                if (_stateTimer <= 0)
                {
                    float roll = (float)_rng.NextDouble();
                    if (roll < 0.5f)
                    {
                        _state = State.Pecking;
                        _stateTimer = 1.5f + (float)_rng.NextDouble() * 2f;
                        _peckTimer = 0;
                    }
                    else if (roll < 0.85f)
                    {
                        _state = State.Hopping;
                        _dir = _rng.NextDouble() < 0.5 ? -1 : 1;
                        _stateTimer = 0.5f + (float)_rng.NextDouble() * 1f;
                    }
                    else
                    {
                        _dir = -_dir;
                        _stateTimer = 1f + (float)_rng.NextDouble() * 2f;
                    }
                }
                break;

            case State.Pecking:
                _peckTimer += dt;
                _peckFrame = ((int)(_peckTimer * 4f)) % 2;
                if (_stateTimer <= 0)
                {
                    _state = State.Perched;
                    _peckFrame = 0;
                    _stateTimer = 1f + (float)_rng.NextDouble() * 3f;
                }
                break;

            case State.Hopping:
                Velocity.X = _dir * _hopSpeed;
                Velocity.Y = 0;
                _onGround = EnemyPhysics.ApplyGravityAndCollision(
                    ref Position, ref Velocity, Width, Height, 600f, dt,
                    tileGrid, tileSize, platforms, solidFloors, floorY);

                // Clamp to surface edges
                if (Position.X < SurfaceLeft) { Position.X = SurfaceLeft; _dir = 1; }
                if (Position.X + Width > SurfaceRight) { Position.X = SurfaceRight - Width; _dir = -1; }
                if (_stateTimer <= 0)
                {
                    _state = State.Perched;
                    _stateTimer = 0.5f + (float)_rng.NextDouble() * 2f;
                }
                break;

            case State.Fleeing:
                Velocity.X = _dir * _fleeSpeed * 0.5f;
                Velocity.Y = 0;
                _onGround = EnemyPhysics.ApplyGravityAndCollision(
                    ref Position, ref Velocity, Width, Height, 600f, dt,
                    tileGrid, tileSize, platforms, solidFloors, floorY);

                if (Position.X < SurfaceLeft || Position.X + Width > SurfaceRight)
                {
                    _state = State.Flying;
                    Velocity.X = _dir * _fleeSpeed;
                    Velocity.Y = -180f;
                    _flightTime = 0;
                }
                else if (_stateTimer <= 0)
                {
                    if (dist < AlertRange)
                        _stateTimer = 0.3f;
                    else
                    {
                        _state = State.Perched;
                        _stateTimer = 0.5f + (float)_rng.NextDouble() * 1f;
                    }
                }
                break;

            case State.Flying:
                _flightTime += dt;
                // Flying uses direct position movement (birds defy tile physics)
                Position.X += Velocity.X * dt;
                Position.Y += Velocity.Y * dt;
                Velocity.Y += FlyGravity * dt;
                Velocity.Y += MathF.Sin(_flightTime * 12f) * 8f * dt;

                if (_flightTime > 4f)
                    Alive = false;

                if (_flightTime > 1.5f && Position.Y >= GroundY && Velocity.Y > 0)
                {
                    Position.Y = GroundY;
                    Velocity = Vector2.Zero;
                    _state = State.Perched;
                    _stateTimer = 2f + (float)_rng.NextDouble() * 3f;
                }
                break;
        }

        // Keep on ground when not flying (snap to ground Y)
        if (_state != State.Flying && _state != State.Hopping && _state != State.Fleeing)
            Position.Y = GroundY;
    }

    public bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        if (!Alive) return false;
        KnockbackVel = new Vector2(knockbackX * 0.5f, knockbackY);
        float squashAmount = 1f - SquashResistance;
        VisualScale = new Vector2(1f + 0.3f * squashAmount, 1f - 0.25f * squashAmount);
        _squashHoldTimer = 0.05f;
        Alive = false;
        return true;
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
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;

        float ox = Position.X;
        float oy = Position.Y;

        if (_state == State.Flying)
        {
            sb.Draw(pixel, new Rectangle((int)(ox + 3), (int)(oy + 3), 4, 3), new Color(80, 60, 40));
            bool wingsUp = ((int)(_flightTime * 8f)) % 2 == 0;
            if (wingsUp)
            {
                sb.Draw(pixel, new Rectangle((int)(ox), (int)(oy), 3, 2), new Color(80, 60, 40));
                sb.Draw(pixel, new Rectangle((int)(ox + 7), (int)(oy), 3, 2), new Color(80, 60, 40));
            }
            else
            {
                sb.Draw(pixel, new Rectangle((int)(ox), (int)(oy + 4), 3, 2), new Color(80, 60, 40));
                sb.Draw(pixel, new Rectangle((int)(ox + 7), (int)(oy + 4), 3, 2), new Color(80, 60, 40));
            }
        }
        else
        {
            Color bodyColor = new Color(110, 85, 55);
            Color headColor = new Color(90, 70, 45);
            Color beakColor = new Color(180, 140, 40);

            bool facingRight = _dir > 0;

            sb.Draw(pixel, new Rectangle((int)(ox + 2), (int)(oy + 3), 6, 5), bodyColor);
            int headX = facingRight ? 7 : -1;
            int headY = _peckFrame == 1 ? 4 : 1;
            sb.Draw(pixel, new Rectangle((int)(ox + headX), (int)(oy + headY), 4, 4), headColor);
            int beakX = facingRight ? 10 : -2;
            int beakY = headY + 1;
            sb.Draw(pixel, new Rectangle((int)(ox + beakX), (int)(oy + beakY), 2, 1), beakColor);
            int eyeX = facingRight ? 9 : 0;
            int eyeY = headY + 1;
            sb.Draw(pixel, new Rectangle((int)(ox + eyeX), (int)(oy + eyeY), 1, 1), Color.Black);
            sb.Draw(pixel, new Rectangle((int)(ox + 3), (int)(oy + 7), 1, 2), new Color(160, 120, 40));
            sb.Draw(pixel, new Rectangle((int)(ox + 6), (int)(oy + 7), 1, 2), new Color(160, 120, 40));
            int tailX = facingRight ? 0 : 8;
            sb.Draw(pixel, new Rectangle((int)(ox + tailX), (int)(oy + 2), 2, 2), bodyColor * 0.8f);
        }
    }
}
