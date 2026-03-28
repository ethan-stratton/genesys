using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class Scavenger : Creature
{
    public const int Width = 12, Height = 8;
    
    private enum State { Wander, Freeze, Flee }
    private State _state = State.Wander;
    private float _stateTimer;
    private int _dir = 1;
    private float _wanderSpeed = 35f;
    private float _fleeSpeed = 140f;
    private bool _onGround;
    
    private const float FreezeRange = 100f;
    private const float FleeRange = 55f;
    private const float SafeRange = 160f;
    private const float Gravity = 600f;
    
    private float _wanderChangeTimer;
    
    public Scavenger(Vector2 pos)
    {
        Position = pos;
        Velocity = Vector2.Zero;
        Hp = 1; MaxHp = 1;
        SpeciesName = "scavenger";
        Role = EcologicalRole.Scavenger;
        Needs = CreatureNeeds.Default;
        DeathParticleColor = new Color(90, 75, 60);
        HitColor = new Color(90, 75, 60);
        _wanderChangeTimer = 1f + Random.Shared.NextSingle() * 2f;
        _dir = Random.Shared.NextSingle() > 0.5f ? 1 : -1;
        HungerRate = 0.004f;
        FatigueRate = 0.0015f;
        Needs.Hunger = 0.2f + (float)Random.Shared.NextDouble() * 0.3f;
        Needs.Fatigue = (float)Random.Shared.NextDouble() * 0.2f;
    }
    
    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override bool IsNocturnal => true;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);
    
    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        var playerCenter = ctx.PlayerCenter;
        var tileGrid = ctx.TileGrid;
        var tileSize = ctx.TileSize;
        var levelBottom = ctx.LevelBottom;
        if (!Alive) return;
        if (HitFlash > 0) HitFlash -= dt;
        
        // --- Needs system ---
        TickNeeds(dt);
        // Weather effects
        if (ctx.IsRaining)
        {
            Needs.Hunger += dt * HungerRate * 0.5f;
            if (Role is not (EcologicalRole.Predator or EcologicalRole.Apex or EcologicalRole.Defensive))
                Needs.Safety = Math.Min(Needs.Safety, 0.6f);
        }
        // Time-of-day activity
        float activity = GetActivityLevel(ctx.WorldTime);
        if (activity < 0.2f && CurrentGoal != CreatureGoal.Flee)
            CurrentGoal = CreatureGoal.Rest;
        float weatherDetectMult = ctx.IsStorming ? 0.5f : ctx.IsRaining ? 0.7f : 1f;
        
        var center = Position + new Vector2(Width / 2f, Height / 2f);
        float dist = Vector2.Distance(center, playerCenter);
        float dx = playerCenter.X - center.X;
        
        // Safety from player proximity
        Needs.Safety = MathHelper.Clamp(dist / 160f, 0f, 1f);
        CurrentGoal = SelectGoal();

        // Creature awareness — scavengers fear everyone
        var (threat, threatDist, _, _) = ScanCreatures(ctx.NearbyCreatures, 160f, 0f);
        if (threat != null && threatDist < 100f)
        {
            _dir = threat.Position.X > Position.X ? -1 : 1;
            Needs.Safety = Math.Min(Needs.Safety, 0.15f);
        }
        CurrentGoal = SelectGoal();

        // Food seeking
        if (CurrentGoal == CreatureGoal.Eat && !IsEating)
        {
            var food = FindFood(ctx.FoodSources);
            if (food != null)
            {
                float distToFood = Vector2.Distance(Position, food.Position);
                if (distToFood < 10f)
                {
                    IsEating = true;
                    EatingTarget = food;
                    EatTimer = 0f;
                    Velocity = Vector2.Zero;
                }
                else
                {
                    _dir = food.Position.X > Position.X ? 1 : -1;
                }
            }
        }
        if (IsEating)
        {
            EatTimer += dt;
            if (EatingTarget == null || EatingTarget.Depleted || Needs.Hunger < 0.15f)
            {
                IsEating = false;
                EatingTarget = null;
            }
            else
            {
                float gained = EatingTarget.Eat(dt);
                Needs.Hunger = MathHelper.Clamp(Needs.Hunger - gained, 0f, 1f);
                Velocity = Vector2.Zero;
                return;
            }
        }
        
        // Goal-influenced detection ranges
        float effectiveFleeRange = FleeRange * weatherDetectMult;
        float effectiveFreezeRange = FreezeRange * weatherDetectMult;
        if (CurrentGoal == CreatureGoal.Flee) { effectiveFleeRange *= 1.3f; effectiveFreezeRange *= 1.3f; }
        
        switch (_state)
        {
            case State.Wander:
                if (dist < effectiveFleeRange)
                {
                    _state = State.Flee;
                    _dir = dx > 0 ? -1 : 1;
                    _stateTimer = 0;
                }
                else if (dist < effectiveFreezeRange)
                {
                    _state = State.Freeze;
                    _stateTimer = 0;
                    Velocity.X = 0;
                }
                else
                {
                    // Rest goal: stay still
                    if (CurrentGoal == CreatureGoal.Rest)
                    {
                        Velocity.X = 0;
                        break;
                    }
                    _wanderChangeTimer -= dt;
                    if (_wanderChangeTimer <= 0)
                    {
                        _dir = -_dir;
                        _wanderChangeTimer = 1.5f + Random.Shared.NextSingle() * 3f;
                    }
                    float wanderMult = CurrentGoal == CreatureGoal.Eat ? 1.2f : 1f;
                    wanderMult *= MathHelper.Clamp(activity, 0.3f, 1f);
                    Velocity.X = _dir * _wanderSpeed * wanderMult;
                }
                break;
                
            case State.Freeze:
                Velocity.X = 0;
                if (dist < effectiveFleeRange)
                {
                    _state = State.Flee;
                    _dir = dx > 0 ? -1 : 1;
                    _stateTimer = 0;
                }
                else if (dist > SafeRange)
                {
                    _state = State.Wander;
                    _wanderChangeTimer = 0.5f + Random.Shared.NextSingle() * 1f;
                }
                break;
                
            case State.Flee:
                Velocity.X = _dir * _fleeSpeed;
                _stateTimer += dt;
                if (dist > SafeRange && _stateTimer > 1f)
                {
                    _state = State.Wander;
                    _wanderChangeTimer = 2f + Random.Shared.NextSingle() * 2f;
                }
                break;
        }
        
        _onGround = EnemyPhysics.ApplyGravityAndCollision(
            ref Position, ref Velocity,
            Width, Height, Gravity, dt,
            tileGrid, tileSize,
            levelBottom);
        
        if (_state == State.Flee && MathF.Abs(Velocity.X) < 1f)
            _dir = -_dir;
            
        Dir = _dir;
    }
    
    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        
        Color bodyColor = HitFlash > 0 ? Color.Red : new Color(90, 75, 60);
        
        int bx = (int)Position.X, by = (int)Position.Y;
        sb.Draw(pixel, new Rectangle(bx + 1, by + 2, Width - 2, Height - 2), bodyColor);
        sb.Draw(pixel, new Rectangle(bx + 2, by + Height - 3, Width - 4, 2), new Color(110, 95, 75));
        
        int headX = _dir > 0 ? bx + Width - 4 : bx;
        sb.Draw(pixel, new Rectangle(headX, by + 1, 4, 5), bodyColor);
        
        int eyeX = _dir > 0 ? bx + Width - 3 : bx + 1;
        sb.Draw(pixel, new Rectangle(eyeX, by + 2, 2, 2), Color.White);
        sb.Draw(pixel, new Rectangle(eyeX + (_dir > 0 ? 1 : 0), by + 3, 1, 1), Color.Black);
        
        int tailX = _dir > 0 ? bx - 1 : bx + Width;
        int tailLen = 4;
        float tailWave = MathF.Sin((float)Environment.TickCount64 / 200f + Position.X) * 2f;
        sb.Draw(pixel, new Rectangle(tailX - (_dir > 0 ? tailLen : 0), by + 3 + (int)tailWave, tailLen, 1), bodyColor * 0.7f);
        
        if (_state == State.Freeze)
        {
            float tremble = MathF.Sin((float)Environment.TickCount64 / 50f) * 0.5f;
            if ((int)(tremble * 10) % 2 == 0)
                sb.Draw(pixel, new Rectangle(bx, by + 1, 1, Height - 2), Color.White * 0.15f);
        }
        
        if (_state != State.Freeze)
        {
            float legAnim = MathF.Sin((float)Environment.TickCount64 / 80f + Position.X * 0.1f);
            int leg1Y = by + Height - 1 + (int)(legAnim * 1);
            int leg2Y = by + Height - 1 - (int)(legAnim * 1);
            sb.Draw(pixel, new Rectangle(bx + 3, leg1Y, 1, 2), bodyColor * 0.6f);
            sb.Draw(pixel, new Rectangle(bx + Width - 4, leg2Y, 1, 2), bodyColor * 0.6f);
        }
        else
        {
            sb.Draw(pixel, new Rectangle(bx + 3, by + Height - 1, 1, 1), bodyColor * 0.6f);
            sb.Draw(pixel, new Rectangle(bx + Width - 4, by + Height - 1, 1, 1), bodyColor * 0.6f);
        }
    }
}
