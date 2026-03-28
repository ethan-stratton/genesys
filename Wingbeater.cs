using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

/// <summary>
/// Flying enemy that patrols the air and dive-bombs the player.
/// After a dive-bomb, returns to its starting position.
/// Good for teaching the flip/backflip dodge.
/// </summary>
public class Wingbeater : Creature
{
    public Vector2 SpawnPos;
    public override int ContactDamage => WingbeaterContactDamage;
    private const int WingbeaterContactDamage = 2;
    public bool Passive = true; // passive toward player by default
    public const int Width = 20, Height = 14;

    public Vector2 KnockbackVel;
    private float _squashHoldTimer;

    private enum State { Hovering, Tracking, DiveBomb, Returning, Stunned, Nesting, GatheringPlant }
    private State _state = State.Hovering;
    private float _stateTimer;
    private int _dir = 1;

    // Target type for dive-bomb
    private enum TargetType { Player, Prey }
    private TargetType _currentTargetType = TargetType.Player;

    // Player aggro
    private bool _playerAggro = false;

    // Hover: gentle float at spawn height
    private float _hoverPhase;

    // Tracking: locks on before diving
    private const float TrackTime = 0.6f;

    // Dive-bomb
    private const float DiveSpeed = 450f;
    private Vector2 _diveTarget;
    private const float DiveMaxDist = 300f;

    // Return to start
    private const float ReturnSpeed = 120f;

    // Stun after hitting ground/player
    private const float StunTime = 0.8f;

    private Creature _huntTarget;
    private float _huntCooldown;

    // Idle patrol
    private Vector2? _patrolTarget;
    private float _patrolTimer;
    private float _perchTimer;
    private bool _isPerched;

    // Detection
    public float DetectRange = 200f;
    public float DiveRange = 180f;

    // Wing animation
    private float _wingTimer;
    private int _wingFrame;

    // Nesting
    private Vector2? _nestPosition = null;
    private float _nestMaterial = 0f;
    private const float NestComplete = 1f;
    private bool _hasNest = false;
    private bool _carryingPlant = false;
    public bool HasNest => _hasNest;
    public Vector2? NestPosition => _nestPosition;
    public float NestMaterial => _nestMaterial;
    public bool WantsToLeaveLevel { get; private set; }
    public string PreferredExitDirection { get; private set; }
    private FoodSource _gatherTarget;

    public override bool CanBurrow => false;
    public override (int min, int max) PreySize => (50, 400);

    public override CreatureGoal SelectGoal()
    {
        if (Hp > 0 && Hp <= MaxHp * 0.3f) return CreatureGoal.Flee;
        if (Needs.Safety < 0.3f) return CreatureGoal.Flee;
        if (Needs.Hunger > 0.45f) return CreatureGoal.Eat; // wingbeaters hunt earlier than others
        if (Needs.Fatigue > 0.7f) return CreatureGoal.Rest;
        return CreatureGoal.Wander;
    }

    public Wingbeater(Vector2 pos)
    {
        Position = pos;
        SpawnPos = pos;
        Hp = 3;
        MaxHp = 3;
        SpeciesName = "wingbeater";
        Role = EcologicalRole.Predator;
        Needs = CreatureNeeds.Default;
        DeathParticleColor = new Color(160, 60, 40);
        HitColor = new Color(160, 60, 40);
        _stateTimer = 1f;
        _hoverPhase = pos.X * 0.1f;
        HungerRate = 0.006f;
        FatigueRate = 0.002f;
        Needs.Hunger = 0.55f + (float)Random.Shared.NextDouble() * 0.25f;
        Needs.Fatigue = (float)Random.Shared.NextDouble() * 0.2f;
    }

    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override bool IsCrepuscular => true;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        var playerPos = ctx.PlayerCenter;
        var floorY = ctx.LevelBottom;
        if (!Alive) return;

        MeleeHitCooldown -= dt;

        // --- Needs system ---
        TickNeeds(dt);
        if (ctx.IsRaining)
            Needs.Hunger += dt * HungerRate * 0.5f;
        float activity = GetActivityLevel(ctx.WorldTime);
        if (activity < 0.2f && CurrentGoal != CreatureGoal.Flee)
            CurrentGoal = CreatureGoal.Rest;
        float weatherDetectMult = ctx.IsStorming ? 0.5f : ctx.IsRaining ? 0.7f : 1f;
        float distToPlayer = Vector2.Distance(Position, playerPos);
        Needs.Safety = Math.Min(Needs.Safety, MathHelper.Clamp(distToPlayer / 100f, 0f, 1f));
        CurrentGoal = SelectGoal();
        float fleeSpeedBoost = CurrentGoal == CreatureGoal.Flee ? 1.3f : 1f;

        // Aggro rework: only attack player if provoked, very close+hungry, or nest threatened
        bool shouldAggroPlayer = false;
        if (_playerAggro) shouldAggroPlayer = true;
        else if (distToPlayer < 60f && Needs.Hunger > 0.5f) shouldAggroPlayer = true;
        else if (_hasNest && _nestPosition.HasValue && Vector2.Distance(playerPos, _nestPosition.Value) < 100f)
            shouldAggroPlayer = true;
        Passive = !shouldAggroPlayer;

        // Creature awareness — wingbeater is an active hunter
        var (_, _, wbPrey, wbPreyDist) = ScanCreatures(ctx.NearbyCreatures, 200f, 500f);
        if (CurrentGoal == CreatureGoal.Eat && wbPrey != null && wbPreyDist < 500f && _huntCooldown <= 0 && WillHunt(wbPrey))
        {
            _huntTarget = wbPrey;
            Dir = wbPrey.Position.X > Position.X ? 1 : -1;
        }
        else if (_huntTarget != null && (!_huntTarget.Alive || Vector2.Distance(Position, _huntTarget.Position) > 600f))
        {
            _huntTarget = null;
        }

        // Attack prey on contact
        if (_huntTarget != null && _huntTarget.Alive && Rect.Intersects(_huntTarget.Rect))
        {
            _huntTarget.TakeHit(2, Dir * 50f, -40f);
            _huntTarget = null;
            _huntCooldown = 2f;
            Needs.Hunger = MathHelper.Clamp(Needs.Hunger - 0.3f, 0f, 1f);
        }
        if (_huntCooldown > 0) _huntCooldown -= dt;

        // Noise detection
        var noise = CheckNoise(ctx.NoiseEvents);
        if (noise != null)
        {
            if (noise.Intensity >= 0.7f)
            {
                // Loud noise — flee away from source
                Needs.Safety = Math.Min(Needs.Safety, 0.15f);
                Dir = noise.Position.X > Position.X ? -1 : 1; // flee AWAY
                _isPerched = false; // take off if perched
            }
            else if (Role is EcologicalRole.Predator or EcologicalRole.Apex)
            {
                if (Needs.Hunger > 0.5f && CurrentGoal != CreatureGoal.Flee)
                    Dir = noise.Position.X > Position.X ? 1 : -1;
            }
        }

        // Startle propagation
        if (CurrentGoal == CreatureGoal.Flee && _prevGoal != CreatureGoal.Flee)
            PropagateStartle(this, ctx.NearbyCreatures);

        // Nesting behavior
        if (_state == State.Hovering || _state == State.Nesting || _state == State.GatheringPlant)
        {
            // Search for nest spot
            if (!_hasNest && _nestPosition == null && Needs.Fatigue > 0.5f && ctx.TileGrid != null)
            {
                _nestPosition = FindNestSpot(ctx.TileGrid, ctx.TileSize, ctx.LevelBottom - 800f);
            }

            // Gather plants for nest
            if (_nestPosition.HasValue && _nestMaterial < NestComplete && _state != State.GatheringPlant && _state != State.Tracking && _state != State.DiveBomb && _state != State.Stunned)
            {
                _state = State.GatheringPlant;
            }

            // Rest at nest
            if (_hasNest && CurrentGoal == CreatureGoal.Rest && _state != State.GatheringPlant)
            {
                _state = State.Nesting;
            }
        }

        // Food seeking (food sources)
        if (CurrentGoal == CreatureGoal.Eat && !IsEating && _state == State.Hovering)
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
                    Dir = food.Position.X > Position.X ? 1 : -1;
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
                _prevGoal = CurrentGoal;
                return;
            }
        }

        // Goal-influenced detection ranges
        float effectiveDetectRange = DetectRange * weatherDetectMult * MathHelper.Clamp(activity, 0.3f, 1f);
        if (CurrentGoal == CreatureGoal.Eat) effectiveDetectRange *= 1.3f;
        else if (CurrentGoal == CreatureGoal.Rest) effectiveDetectRange *= 0.6f;
        else if (CurrentGoal == CreatureGoal.Flee) effectiveDetectRange *= 0.4f;
        // Nest defense: increased detect range
        if (_hasNest && _nestPosition.HasValue && Vector2.Distance(playerPos, _nestPosition.Value) < 150f)
            effectiveDetectRange *= 1.5f;

        // Knockback
        if (KnockbackVel.LengthSquared() > 1f)
        {
            Position += KnockbackVel * dt;
            KnockbackVel *= 0.88f;
        }

        // Squash/stretch recovery
        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 6f * dt);

        // Wing animation
        _wingTimer += dt;
        if (_wingTimer > 0.12f)
        {
            _wingTimer = 0;
            _wingFrame = (_wingFrame + 1) % 4;
        }

        float dist = Vector2.Distance(playerPos, Position + new Vector2(Width / 2f, Height / 2f));
        float dx = playerPos.X - (Position.X + Width / 2f);

        // Return to nest before nightfall
        if (_hasNest && _nestPosition.HasValue && ctx.WorldTime > 18f)
        {
            float distToNest = Vector2.Distance(Position, _nestPosition.Value);
            if (distToNest > 20f)
            {
                var toNest = _nestPosition.Value - Position;
                toNest.Normalize();
                Position += toNest * 80f * dt;
                _dir = _nestPosition.Value.X > Position.X ? 1 : -1;
                _state = State.Hovering;
            }
            else
            {
                _state = State.Nesting;
            }
            _prevGoal = CurrentGoal;
            return;
        }

        // Level boundary check for cross-screen movement
        WantsToLeaveLevel = false;
        if (CurrentGoal == CreatureGoal.Eat && !_hasNest)
        {
            if (Position.X < ctx.BoundsLeft + 30)
            {
                PreferredExitDirection = "left";
                WantsToLeaveLevel = true;
            }
            else if (Position.X > ctx.BoundsRight - 30)
            {
                PreferredExitDirection = "right";
                WantsToLeaveLevel = true;
            }
        }

        switch (_state)
        {
            case State.Hovering:
                _hoverPhase += dt * 2f;
                
                // Active food/prey seeking when hungry
                if (CurrentGoal == CreatureGoal.Eat)
                {
                    // Gentle vertical bob while hunting (don't pin to SpawnPos)
                    Position.Y += MathF.Cos(_hoverPhase) * 0.5f;
                    
                    if (_huntTarget != null && _huntTarget.Alive)
                    {
                        float preyDist = Vector2.Distance(Position, _huntTarget.Position);
                        if (preyDist < DiveRange && _huntCooldown <= 0)
                        {
                            _currentTargetType = TargetType.Prey;
                            _state = State.Tracking;
                            _stateTimer = TrackTime;
                        }
                        else
                        {
                            float moveSpeed = 80f;
                            var toTarget = _huntTarget.Position - Position;
                            if (toTarget.LengthSquared() > 0)
                            {
                                toTarget.Normalize();
                                Position += toTarget * moveSpeed * dt;
                            }
                            _dir = _huntTarget.Position.X > Position.X ? 1 : -1;
                        }
                    }
                    else
                    {
                        var food = FindFood(ctx.FoodSources, 600f);
                        if (food != null)
                        {
                            float foodDist = Vector2.Distance(Position, food.Position);
                            if (foodDist > 10f)
                            {
                                var toFood = food.Position - Position;
                                toFood.Normalize();
                                Position += toFood * 60f * dt;
                                _dir = food.Position.X > Position.X ? 1 : -1;
                            }
                        }
                        else
                        {
                            // Wander in current direction, expanding search
                            Position.X += _dir * 40f * dt;
                        }
                    }
                }
                else
                {
                    // Idle patrol / perch behavior
                    if (_isPerched)
                    {
                        // Gentle bob while perched
                        Position.Y = (_patrolTarget ?? SpawnPos).Y + MathF.Sin(_hoverPhase) * 2f;
                        _perchTimer -= dt;
                        if (_perchTimer <= 0)
                        {
                            _isPerched = false;
                            _patrolTarget = null;
                        }
                    }
                    else
                    {
                        // Pick a patrol target if we don't have one
                        if (!_patrolTarget.HasValue)
                        {
                            float offsetX = ((float)Random.Shared.NextDouble() * 2f - 1f) * 300f;
                            float targetX = MathHelper.Clamp(SpawnPos.X + offsetX, ctx.BoundsLeft + 20, ctx.BoundsRight - 20);
                            float targetY = SpawnPos.Y + ((float)Random.Shared.NextDouble() * 2f - 1f) * 40f;
                            _patrolTarget = new Vector2(targetX, targetY);
                        }

                        // Fly toward patrol target
                        var toTarget = _patrolTarget.Value - Position;
                        float distToTarget = toTarget.Length();
                        if (distToTarget < 6f)
                        {
                            // Arrived — perch or pick new target
                            if (Random.Shared.NextDouble() < 0.4)
                            {
                                _isPerched = true;
                                _perchTimer = 3f + (float)Random.Shared.NextDouble() * 5f;
                            }
                            else
                            {
                                _patrolTarget = null; // pick new target next frame
                            }
                        }
                        else
                        {
                            toTarget.Normalize();
                            Position += toTarget * 50f * dt;
                            // Gentle vertical bob while flying
                            Position.Y += MathF.Sin(_hoverPhase) * 0.5f;
                            _dir = _patrolTarget.Value.X > Position.X ? 1 : -1;
                        }
                    }
                }

                // Dive-bomb prey if hunting
                if (_huntTarget != null && _huntTarget.Alive && _huntCooldown <= 0 && _state == State.Hovering)
                {
                    _currentTargetType = TargetType.Prey;
                    _state = State.Tracking;
                    _stateTimer = TrackTime;
                }
                // Dive-bomb player if aggressive
                else if (dist < effectiveDetectRange && !Passive && _state == State.Hovering)
                {
                    _currentTargetType = TargetType.Player;
                    _state = State.Tracking;
                    _stateTimer = TrackTime;
                }
                break;

            case State.Tracking:
                _stateTimer -= dt;
                Position.X += MathF.Sin(_stateTimer * 30f) * 0.5f;
                if (_currentTargetType == TargetType.Prey && _huntTarget != null && _huntTarget.Alive)
                    _dir = _huntTarget.Position.X > Position.X ? 1 : -1;
                else
                    _dir = dx > 0 ? 1 : -1;

                if (_stateTimer <= 0)
                {
                    if (_currentTargetType == TargetType.Prey && _huntTarget != null && _huntTarget.Alive)
                        _diveTarget = _huntTarget.Position;
                    else
                        _diveTarget = playerPos;
                    var diveDir = _diveTarget - Position;
                    if (diveDir.LengthSquared() > 0)
                        diveDir.Normalize();
                    Velocity = diveDir * DiveSpeed * fleeSpeedBoost;
                    _state = State.DiveBomb;
                    _stateTimer = DiveMaxDist / DiveSpeed + 0.2f;
                    SetSquash(0.7f, 1.4f);
                }
                break;

            case State.DiveBomb:
                Position += Velocity * dt;
                _dir = Velocity.X > 0 ? 1 : -1;
                _stateTimer -= dt;

                if (Position.Y + Height >= floorY || _stateTimer <= 0)
                {
                    if (Position.Y + Height >= floorY)
                        Position.Y = floorY - Height;
                    Velocity = Vector2.Zero;
                    _state = State.Stunned;
                    _stateTimer = StunTime;
                    SetSquash(1.5f, 0.6f);
                }
                break;

            case State.Stunned:
                _stateTimer -= dt;
                // Apply gravity while stunned (fall to ground)
                Velocity.Y += 400f * dt;
                Position += Velocity * dt;
                if (Position.Y + Height >= floorY)
                {
                    Position.Y = floorY - Height;
                    Velocity = Vector2.Zero;
                }
                if (_stateTimer <= 0)
                    _state = State.Returning;
                break;

            case State.Returning:
                // If still hungry, don't return to spawn — continue hunting
                if (CurrentGoal == CreatureGoal.Eat)
                {
                    _state = State.Hovering;
                    break;
                }
                var toSpawn = SpawnPos - Position;
                float distToSpawn = toSpawn.Length();
                if (distToSpawn < 4f)
                {
                    Position = SpawnPos;
                    _state = State.Hovering;
                    _hoverPhase = 0;
                }
                else
                {
                    toSpawn.Normalize();
                    Position += toSpawn * ReturnSpeed * fleeSpeedBoost * dt;
                }
                break;

            case State.GatheringPlant:
                if (!_nestPosition.HasValue || _nestMaterial >= NestComplete)
                {
                    _hasNest = _nestMaterial >= NestComplete;
                    _state = State.Hovering;
                    break;
                }
                if (_carryingPlant)
                {
                    // Fly back to nest
                    var toNest = _nestPosition.Value - Position;
                    if (toNest.Length() < 8f)
                    {
                        _nestMaterial += 0.1f;
                        _carryingPlant = false;
                        if (_nestMaterial >= NestComplete)
                        {
                            _hasNest = true;
                            _state = State.Hovering;
                        }
                    }
                    else
                    {
                        toNest.Normalize();
                        Position += toNest * ReturnSpeed * dt;
                    }
                }
                else
                {
                    // Find nearest plant
                    if (_gatherTarget == null || _gatherTarget.Depleted)
                    {
                        _gatherTarget = null;
                        float bestDist = float.MaxValue;
                        foreach (var f in ctx.FoodSources)
                        {
                            if (f.Type != FoodType.Plant || f.Depleted) continue;
                            float d = Vector2.Distance(Position, f.Position);
                            if (d < bestDist) { bestDist = d; _gatherTarget = f; }
                        }
                    }
                    if (_gatherTarget != null)
                    {
                        var toPlant = _gatherTarget.Position - Position;
                        if (toPlant.Length() < 10f)
                        {
                            _carryingPlant = true;
                            _gatherTarget.Amount -= 0.05f;
                        }
                        else
                        {
                            toPlant.Normalize();
                            Position += toPlant * ReturnSpeed * dt;
                        }
                    }
                    else
                    {
                        _state = State.Hovering; // no plants available
                    }
                }
                break;

            case State.Nesting:
                if (!_hasNest || !_nestPosition.HasValue)
                {
                    _state = State.Hovering;
                    break;
                }
                var toNestRest = _nestPosition.Value - Position;
                if (toNestRest.Length() < 6f)
                {
                    // Resting at nest
                    Needs.Fatigue = MathHelper.Clamp(Needs.Fatigue - dt * 0.05f, 0f, 1f);
                    if (CurrentGoal != CreatureGoal.Rest)
                        _state = State.Hovering;
                }
                else
                {
                    toNestRest.Normalize();
                    Position += toNestRest * ReturnSpeed * dt;
                }
                break;
        }

        _prevGoal = CurrentGoal;
    }

    private Vector2? FindNestSpot(TileGrid tg, int ts, float boundsTop)
    {
        int cx = (int)(SpawnPos.X / ts);
        for (int y = (int)(SpawnPos.Y / ts) - 1; y >= (int)(boundsTop / ts) && y >= 0; y--)
        {
            var below = tg.GetTileAt(cx, y + 1);
            var here = tg.GetTileAt(cx, y);
            if (TileProperties.IsSolid(below) && !TileProperties.IsSolid(here))
                return new Vector2(cx * ts + ts / 2f, (y + 1) * ts - 4);
        }
        return null;
    }

    public override int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive) return 0;
        if (_state != State.DiveBomb && _state != State.Hovering) return 0;
        if (Rect.Intersects(playerRect))
            return ContactDamage;
        return 0;
    }

    public override bool TakeHit(int damage, float kbX = 0, float kbY = 0)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        MeleeHitCooldown = 0.15f;
        KnockbackVel = new Vector2(kbX, kbY);
        SetSquash(1.3f, 0.7f);
        _playerAggro = true; // player attacked us
        if (Hp <= 0)
        {
            Alive = false;
            return true;
        }
        // Getting hit stuns it — forced to fall briefly
        if (_state == State.DiveBomb || _state == State.Hovering || _state == State.Tracking)
        {
            Velocity = new Vector2(kbX, MathF.Max(kbY, 200f)); // ensure downward
            _state = State.Stunned;
            _stateTimer = StunTime * (kbY > 0 ? 2f : 1.5f); // longer stun if yanked down
        }
        return false;
    }

    public void SetSquash(float sx, float sy)
    {
        VisualScale = new Vector2(sx, sy);
        _squashHoldTimer = 0.05f;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;

        float drawX = Position.X;
        float drawY = Position.Y;

        int w = Width;
        int h = Height;
        int scaledW = (int)(w * VisualScale.X);
        int scaledH = (int)(h * VisualScale.Y);
        int offsetX = (w - scaledW) / 2;
        int offsetY = h - scaledH;

        // Body color — red-brown, flashes white when stunned
        Color bodyColor = _state == State.Stunned
            ? Color.Lerp(new Color(160, 60, 40), Color.White, MathF.Sin(_stateTimer * 20f) * 0.5f + 0.5f)
            : _state == State.Tracking
                ? Color.Lerp(new Color(160, 60, 40), Color.Red, 0.5f)
                : new Color(160, 60, 40);

        // Body
        sb.Draw(pixel, new Rectangle(
            (int)(drawX + offsetX), (int)(drawY + offsetY),
            scaledW, scaledH), bodyColor);

        // Wing animation
        int wingSpan = _wingFrame switch
        {
            0 => 6,
            1 => 3,
            2 => -1, // wings down
            3 => 3,
            _ => 6
        };

        // Faster wing flap during dive
        Color wingColor = new Color(120, 45, 30);
        int wingW = 5;
        int wingY = (int)(drawY + offsetY + 2);

        // Left wing
        sb.Draw(pixel, new Rectangle(
            (int)(drawX + offsetX - wingW), wingY - wingSpan,
            wingW, 4), wingColor);
        // Right wing
        sb.Draw(pixel, new Rectangle(
            (int)(drawX + offsetX + scaledW), wingY - wingSpan,
            wingW, 4), wingColor);

        // Eye (facing direction)
        int eyeX = _dir == 1
            ? (int)(drawX + offsetX + scaledW - 4)
            : (int)(drawX + offsetX + 2);
        sb.Draw(pixel, new Rectangle(eyeX, (int)(drawY + offsetY + 2), 2, 2), Color.Yellow);

        // Beak
        int beakX = _dir == 1
            ? (int)(drawX + offsetX + scaledW)
            : (int)(drawX + offsetX - 3);
        sb.Draw(pixel, new Rectangle(beakX, (int)(drawY + offsetY + 4), 3, 2), Color.Orange);
    }
}
