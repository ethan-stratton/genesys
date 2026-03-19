using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ArenaShooter;

/// <summary>
/// Slime-type enemy: hops toward player with rhythm.
/// Pauses between hops — player can exploit the timing.
/// Personality: patient, bouncy, relentless.
/// </summary>
public class Hopper
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool Alive = true;
    public int Hp = 4;
    public const int Width = 20, Height = 16;
    public float AggroRange = 180f;
    public bool Aggroed;
    public float DamageCooldown;
    public float HitFlash;

    // Hop state machine
    private enum State { Grounded, Winding, Airborne, Landing }
    private State _state = State.Grounded;
    private float _stateTimer;
    private float _groundY; // Y of the surface we're sitting on
    private bool _onGround;

    // Tuning
    private const float Gravity = 600f;
    private const float SmallHopForce = -180f;
    private const float BigHopForce = -300f;
    private const float HopSpeedX = 80f;
    private const float RestTime = 0.6f;     // pause between hops
    private const float WindUpTime = 0.2f;   // squish before jumping
    private const float LandingTime = 0.15f; // squash on landing
    private const int ContactDamage = 6;

    private int _hopCount;   // small hops before a big hop
    private int _dir;        // -1 or 1 toward player

    public Hopper(Vector2 pos, float groundY)
    {
        Position = pos;
        _groundY = groundY;
        Velocity = Vector2.Zero;
        _state = State.Grounded;
        _stateTimer = RestTime * 0.5f; // stagger initial hop timing
    }

    public Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public void Update(float dt, Vector2 playerCenter, Rectangle[] solidFloors, Rectangle[] platforms, float floorY)
    {
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;

        float dist = Vector2.Distance(playerCenter, Position + new Vector2(Width / 2f, Height / 2f));
        Aggroed = dist < AggroRange;

        // Face the player
        float dx = playerCenter.X - (Position.X + Width / 2f);
        if (MathF.Abs(dx) > 4f)
            _dir = dx > 0 ? 1 : -1;

        switch (_state)
        {
            case State.Grounded:
                _stateTimer -= dt;
                Velocity.X = 0; // stationary on ground
                if (_stateTimer <= 0 && Aggroed)
                {
                    // Wind up before hop
                    _state = State.Winding;
                    _stateTimer = WindUpTime;
                }
                break;

            case State.Winding:
                _stateTimer -= dt;
                Velocity.X = 0;
                if (_stateTimer <= 0)
                {
                    // Jump!
                    _hopCount++;
                    bool bigHop = _hopCount >= 3;
                    if (bigHop) _hopCount = 0;

                    Velocity.Y = bigHop ? BigHopForce : SmallHopForce;
                    Velocity.X = _dir * HopSpeedX * (bigHop ? 1.6f : 1f);
                    _state = State.Airborne;
                    _onGround = false;
                }
                break;

            case State.Airborne:
                // Gravity
                Velocity.Y += Gravity * dt;
                Position.X += Velocity.X * dt;
                Position.Y += Velocity.Y * dt;

                // Check landing on surfaces
                if (Velocity.Y > 0)
                {
                    // Check solid floors
                    foreach (var sf in solidFloors)
                    {
                        if (Position.X + Width > sf.X && Position.X < sf.Right &&
                            Position.Y + Height >= sf.Y && Position.Y + Height <= sf.Y + Velocity.Y * dt + 8)
                        {
                            Land(sf.Y - Height);
                            break;
                        }
                    }
                    // Check platforms (land on top only)
                    if (!_onGround)
                    {
                        foreach (var p in platforms)
                        {
                            if (Position.X + Width > p.X && Position.X < p.Right &&
                                Position.Y + Height >= p.Y && Position.Y + Height <= p.Y + Velocity.Y * dt + 8)
                            {
                                Land(p.Y - Height);
                                break;
                            }
                        }
                    }
                    // Check main floor
                    if (!_onGround && Position.Y + Height >= floorY)
                    {
                        Land(floorY - Height);
                    }
                }
                return; // skip position clamping below

            case State.Landing:
                _stateTimer -= dt;
                Velocity.X = 0;
                if (_stateTimer <= 0)
                {
                    _state = State.Grounded;
                    _stateTimer = RestTime;
                }
                break;
        }
    }

    private void Land(float surfaceY)
    {
        Position.Y = surfaceY;
        _groundY = surfaceY + Height;
        Velocity = Vector2.Zero;
        _onGround = true;
        _state = State.Landing;
        _stateTimer = LandingTime;
    }

    public int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive || DamageCooldown > 0) return 0;
        if (Rect.Intersects(playerRect))
        {
            DamageCooldown = 1.0f;
            return ContactDamage;
        }
        return 0;
    }

    public bool TakeHit(int damage)
    {
        if (!Alive) return false;
        Hp -= damage;
        HitFlash = 0.15f;
        if (Hp <= 0) { Alive = false; return true; }
        return false;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;

        // Squash & stretch based on state
        int drawW = Width, drawH = Height;
        int drawX = (int)Position.X, drawY = (int)Position.Y;

        switch (_state)
        {
            case State.Winding:
                // Squash down (winding up to jump)
                drawH = (int)(Height * 0.7f);
                drawW = (int)(Width * 1.2f);
                drawX -= (drawW - Width) / 2;
                drawY += Height - drawH;
                break;
            case State.Airborne:
                if (Velocity.Y < -50f)
                {
                    // Stretch tall going up
                    drawH = (int)(Height * 1.3f);
                    drawW = (int)(Width * 0.85f);
                    drawX += (Width - drawW) / 2;
                }
                else if (Velocity.Y > 50f)
                {
                    // Squash wide falling down
                    drawH = (int)(Height * 0.8f);
                    drawW = (int)(Width * 1.15f);
                    drawX -= (drawW - Width) / 2;
                    drawY += Height - drawH;
                }
                break;
            case State.Landing:
                // Squash on impact
                float t = _stateTimer / LandingTime;
                drawH = (int)(Height * MathHelper.Lerp(1f, 0.65f, t));
                drawW = (int)(Width * MathHelper.Lerp(1f, 1.3f, t));
                drawX -= (drawW - Width) / 2;
                drawY += Height - drawH;
                break;
        }

        // Body
        Color bodyColor = HitFlash > 0 ? Color.Red : (Aggroed ? new Color(80, 140, 60) : new Color(60, 120, 50));
        sb.Draw(pixel, new Rectangle(drawX, drawY, drawW, drawH), bodyColor);

        // Highlight on top
        sb.Draw(pixel, new Rectangle(drawX + 2, drawY, drawW - 4, 3), Color.LightGreen * 0.4f);

        // Eyes (face the direction of movement)
        int eyeOffsetX = _dir > 0 ? drawW - 6 : 2;
        sb.Draw(pixel, new Rectangle(drawX + eyeOffsetX, drawY + 3, 3, 3), Color.White);
        sb.Draw(pixel, new Rectangle(drawX + eyeOffsetX + 1, drawY + 4, 1, 1), Color.Black);
    }
}
