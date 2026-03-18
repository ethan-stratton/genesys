using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

public class Bird
{
    public Vector2 Position;
    public bool Alive = true;
    public const int Width = 10, Height = 8;

    // Behavior
    private enum State { Perched, Pecking, Hopping, Fleeing, Flying }
    private State _state = State.Perched;
    private float _stateTimer;
    private float _peckTimer;
    private int _peckFrame; // 0 = head up, 1 = head down
    private int _dir = 1; // facing direction
    private float _fleeSpeed = 180f;
    private float _hopSpeed = 30f;
    private float _flyVelX, _flyVelY;
    private float _flightTime;
    private readonly Random _rng;

    // Awareness
    public float FleeRange = 80f;
    public float AlertRange = 140f;

    // Ground tracking
    public float GroundY;
    public float SurfaceLeft, SurfaceRight;

    public Bird(Vector2 pos, float surfaceLeft, float surfaceRight, Random rng)
    {
        Position = pos;
        GroundY = pos.Y;
        SurfaceLeft = surfaceLeft;
        SurfaceRight = surfaceRight;
        _rng = rng;
        _stateTimer = 1f + (float)rng.NextDouble() * 3f; // stagger initial behavior
    }

    public Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public void Update(float dt, Vector2 playerPos)
    {
        if (!Alive) return;

        float dist = Vector2.Distance(playerPos, Position + new Vector2(Width / 2f, Height / 2f));
        float dx = playerPos.X - (Position.X + Width / 2f);

        // React to player proximity
        if (_state != State.Flying && _state != State.Fleeing && dist < FleeRange)
        {
            // Panic — take flight!
            _state = State.Flying;
            _flyVelX = dx > 0 ? -_fleeSpeed : _fleeSpeed; // fly away from player
            _flyVelY = -200f; // launch upward
            _flightTime = 0;
            _dir = _flyVelX > 0 ? 1 : -1;
        }
        else if (_state != State.Flying && _state != State.Fleeing && dist < AlertRange)
        {
            // Alert — hop away nervously
            if (_state != State.Fleeing)
            {
                _state = State.Fleeing;
                _dir = dx > 0 ? -1 : 1; // face away from player
                _stateTimer = 0.8f + (float)_rng.NextDouble() * 0.5f;
            }
        }

        _stateTimer -= dt;

        switch (_state)
        {
            case State.Perched:
                // Just sitting there looking around
                if (_stateTimer <= 0)
                {
                    // Transition to pecking or hopping
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
                        // Just turn around
                        _dir = -_dir;
                        _stateTimer = 1f + (float)_rng.NextDouble() * 2f;
                    }
                }
                break;

            case State.Pecking:
                _peckTimer += dt;
                _peckFrame = ((int)(_peckTimer * 4f)) % 2; // bob head 4 times/sec
                if (_stateTimer <= 0)
                {
                    _state = State.Perched;
                    _peckFrame = 0;
                    _stateTimer = 1f + (float)_rng.NextDouble() * 3f;
                }
                break;

            case State.Hopping:
                Position.X += _dir * _hopSpeed * dt;
                // Clamp to surface
                if (Position.X < SurfaceLeft) { Position.X = SurfaceLeft; _dir = 1; }
                if (Position.X + Width > SurfaceRight) { Position.X = SurfaceRight - Width; _dir = -1; }
                if (_stateTimer <= 0)
                {
                    _state = State.Perched;
                    _stateTimer = 0.5f + (float)_rng.NextDouble() * 2f;
                }
                break;

            case State.Fleeing:
                // Nervous hop away from player
                Position.X += _dir * _fleeSpeed * 0.5f * dt;
                if (Position.X < SurfaceLeft || Position.X + Width > SurfaceRight)
                {
                    // Ran out of ground — take flight!
                    _state = State.Flying;
                    _flyVelX = _dir * _fleeSpeed;
                    _flyVelY = -180f;
                    _flightTime = 0;
                }
                else if (_stateTimer <= 0)
                {
                    // Check if player is still close
                    if (dist < AlertRange)
                    {
                        // Still nervous, keep fleeing
                        _stateTimer = 0.3f;
                    }
                    else
                    {
                        _state = State.Perched;
                        _stateTimer = 0.5f + (float)_rng.NextDouble() * 1f;
                    }
                }
                break;

            case State.Flying:
                _flightTime += dt;
                Position.X += _flyVelX * dt;
                Position.Y += _flyVelY * dt;
                _flyVelY += 100f * dt; // gentle gravity — birds don't fall like rocks

                // Slight sinusoidal flutter
                _flyVelY += MathF.Sin(_flightTime * 12f) * 8f * dt;

                // Despawn if off-screen for a while or flew too far
                if (_flightTime > 4f)
                    Alive = false;

                // Can land back if far enough from where they spooked
                if (_flightTime > 1.5f && Position.Y >= GroundY && _flyVelY > 0)
                {
                    Position.Y = GroundY;
                    _state = State.Perched;
                    _stateTimer = 2f + (float)_rng.NextDouble() * 3f;
                }
                break;
        }

        // Keep on ground when not flying
        if (_state != State.Flying)
            Position.Y = GroundY;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;

        float ox = Position.X;
        float oy = Position.Y;

        if (_state == State.Flying)
        {
            // Flying bird — V shape wings
            // Body
            sb.Draw(pixel, new Rectangle((int)(ox + 3), (int)(oy + 3), 4, 3), new Color(80, 60, 40));
            // Wings — flap based on time
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
            // Grounded bird
            Color bodyColor = new Color(110, 85, 55); // brown sparrow
            Color headColor = new Color(90, 70, 45);
            Color beakColor = new Color(180, 140, 40); // yellow beak

            bool facingRight = _dir > 0;

            // Body (oval-ish)
            sb.Draw(pixel, new Rectangle((int)(ox + 2), (int)(oy + 3), 6, 5), bodyColor);
            // Head
            int headX = facingRight ? 7 : -1;
            int headY = _peckFrame == 1 ? 4 : 1; // pecking lowers head
            sb.Draw(pixel, new Rectangle((int)(ox + headX), (int)(oy + headY), 4, 4), headColor);
            // Beak
            int beakX = facingRight ? 10 : -2;
            int beakY = headY + 1;
            sb.Draw(pixel, new Rectangle((int)(ox + beakX), (int)(oy + beakY), 2, 1), beakColor);
            // Eye
            int eyeX = facingRight ? 9 : 0;
            int eyeY = headY + 1;
            sb.Draw(pixel, new Rectangle((int)(ox + eyeX), (int)(oy + eyeY), 1, 1), Color.Black);
            // Legs
            sb.Draw(pixel, new Rectangle((int)(ox + 3), (int)(oy + 7), 1, 2), new Color(160, 120, 40));
            sb.Draw(pixel, new Rectangle((int)(ox + 6), (int)(oy + 7), 1, 2), new Color(160, 120, 40));
            // Tail
            int tailX = facingRight ? 0 : 8;
            sb.Draw(pixel, new Rectangle((int)(ox + tailX), (int)(oy + 2), 2, 2), bodyColor * 0.8f);
        }
    }
}
