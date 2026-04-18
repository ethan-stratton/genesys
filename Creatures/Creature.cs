using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

/// <summary>
/// Goal-based AI: what a creature is currently trying to do,
/// driven by its needs (hunger, fatigue, safety).
/// </summary>
public enum CreatureGoal
{
    None,        // no pressing need
    Eat,         // hungry, seeking food
    Drink,       // thirsty, seeking water
    Rest,        // fatigued, seeking safe spot to rest
    Flee,        // safety is low, fleeing threats
    Wander,      // default exploration
    Investigate, // heard/saw something interesting
}

/// <summary>
/// Ecological role in the trophic system. Used by off-screen simulation
/// and creature-to-creature awareness to determine interactions.
/// </summary>
public enum EcologicalRole
{
    Herbivore,    // Eats plants/detritus (Forager crawler)
    Predator,     // Hunts other creatures (Leaper, Wingbeater)
    Scavenger,    // Eats corpses/leftovers (future)
    Decomposer,   // Breaks down dead matter (future)
    Apex,         // Top of food chain (Dragon influence)
    Prey,         // Primarily exists as food (small birds, batflies)
    Defensive,    // Doesn't hunt, defends territory (Bombardier)
    Flighty,      // Flees everything (Skitter)
}

/// <summary>
/// Creature needs that drive goal-based AI decisions.
/// All values 0–1. Behavior systems read these to decide what a creature does.
/// </summary>
public struct CreatureNeeds
{
    public float Hunger;   // 0 = full, 1 = starving
    public float Fatigue;  // 0 = rested, 1 = exhausted
    public float Safety;   // 0 = terrified, 1 = safe

    public static CreatureNeeds Default => new() { Hunger = 0.3f, Fatigue = 0f, Safety = 1f };
}

/// <summary>
/// Shared context passed to all creature Update calls.
/// Avoids each species needing different parameter lists.
/// </summary>
public struct CreatureUpdateContext
{
    public float Dt;
    public Vector2 PlayerCenter;
    public Vector2 PlayerVelocity;
    public TileGrid TileGrid;
    public int TileSize;
    public float LevelBottom;
    public float BoundsLeft;
    public float BoundsRight;
    public List<FoodSource> FoodSources;
    public List<Creature> NearbyCreatures;
    public float WorldTime;        // 0-24 hour clock
    public bool IsRaining;
    public bool IsStorming;
    public float Temperature;      // 0-1
    public float WindStrength;     // 0-1
    public List<NoiseEvent> NoiseEvents;
    public bool LanternActive;
    public Vector2 LanternPos;     // player center when lantern is on
    public float LanternRadius;    // 160px default
}

/// <summary>
/// Base class for all living creatures in the Genesis ecosystem.
/// Provides shared state, identity, needs, and ecological role.
/// Species-specific behavior is in subclasses via overrides.
/// </summary>
public abstract class Creature
{
    // --- Identity ---
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SpeciesName { get; set; } = "Unknown";

    // --- Ecology ---
    public EcologicalRole Role { get; set; } = EcologicalRole.Herbivore;
    public CreatureNeeds Needs;
    public CreatureGoal CurrentGoal { get; protected set; } = CreatureGoal.Wander;

    // Species-specific need rates (override in constructors)
    public float HungerRate = 0.002f;      // ~8 min to starve
    public float FatigueRate = 0.001f;     // ~16 min to exhaust
    public float FatigueRecoveryRate = 0.01f; // recovers 10x faster when resting

    // --- Spatial ---
    public Vector2 Position;
    public Vector2 Velocity;
    public int Dir = 1; // facing direction: 1 = right, -1 = left
    
    // Direction change cooldown — prevents spazzy flip-flopping
    private float _dirChangeCooldown;
    private const float DirChangeCooldownTime = 0.3f; // can't flip more than ~3x/sec
    // Ledge-stuck detection: if creature reverses at ledge edges too many times, allow drop
    protected int _ledgeReverseCount;
    protected float _ledgeReverseTimer;
    protected bool AllowLedgeDrop => _ledgeReverseCount >= 3; // after 3 reversals, just walk off
    /// <summary>Set Dir with anti-flip cooldown. Force=true bypasses cooldown (wall collision).</summary>
    protected bool TrySetDir(int dir, bool force = false)
    {
        if (dir == Dir) return true; // already facing this way
        if (!force && _dirChangeCooldown > 0) return false; // on cooldown
        Dir = dir;
        _dirChangeCooldown = DirChangeCooldownTime;
        return true;
    }
    public void TickDirCooldown(float dt)
    {
        if (_dirChangeCooldown > 0) _dirChangeCooldown -= dt;
        if (_ledgeReverseTimer > 0) { _ledgeReverseTimer -= dt; if (_ledgeReverseTimer <= 0) _ledgeReverseCount = 0; }
    }
    /// <summary>Call when creature reverses due to no floor ahead. Tracks stuck-on-ledge state.</summary>
    protected void NoteLedgeReversal() { _ledgeReverseCount++; _ledgeReverseTimer = 3f; }

    // --- Health ---
    public bool Alive = true;
    public int Hp;
    public int MaxHp;
    public float HitFlash;

    // --- Combat ---
    /// <summary>Contact damage to player (0 = no contact damage). Override per species.</summary>
    public virtual int ContactDamage => 0;
    /// <summary>Damage type multiplier. Override per species for resistances/weaknesses.</summary>
    public virtual float GetDamageMultiplier(DamageType type) => 1f;
    /// <summary>Whether this creature does contact damage to player on touch</summary>
    public virtual bool DoesContactDamage => ContactDamage > 0;
    /// <summary>Damage cooldown tracking for contact damage</summary>
    public float DamageCooldown;
    /// <summary>Melee hit cooldown (prevents multi-hit per swing)</summary>
    public float MeleeHitCooldown;
    /// <summary>Death particle color for this species</summary>
    public Color DeathParticleColor { get; set; } = new Color(100, 80, 60);
    /// <summary>Hit spray color for this species</summary>
    public Color HitColor { get; set; } = new Color(100, 80, 60);

    // --- Eating state ---
    public bool IsEating;
    public FoodSource EatingTarget;
    public float EatTimer;

    // --- Visual ---
    public Vector2 VisualScale = Vector2.One;

    // --- World graph (for off-screen simulation) ---
    public string CurrentNodeId;  // which room/area this creature is in
    public string GoalNodeId;     // where it's trying to get to
    public string HomeNodeId;     // where it lives/nests

    // --- Activity schedule ---
    // Activity schedule — override per species
    public virtual bool IsNocturnal => false;
    public virtual bool IsCrepuscular => false; // active at dawn/dusk

    /// <summary>
    /// Activity level based on time of day (0-1). 
    /// Diurnal creatures are active during day, nocturnal at night.
    /// </summary>
    public virtual float GetActivityLevel(float worldTime)
    {
        bool isNight = worldTime < 5f || worldTime >= 21f;
        bool isDawn = worldTime >= 5f && worldTime < 7f;
        bool isDusk = worldTime >= 17f && worldTime < 21f;
        bool isDay = worldTime >= 7f && worldTime < 17f;
        
        if (IsCrepuscular)
            return (isDawn || isDusk) ? 1f : 0.3f;
        if (IsNocturnal)
            return isNight ? 1f : isDawn || isDusk ? 0.5f : 0.15f;
        // Diurnal (default)
        return isDay ? 1f : isDawn || isDusk ? 0.6f : 0.1f;
    }

    // --- Abstract interface ---
    public abstract int CreatureWidth { get; }
    public abstract int CreatureHeight { get; }
    public abstract Rectangle Rect { get; }
    public abstract void Draw(SpriteBatch sb, Texture2D pixel);

    /// <summary>Unified update. Subclasses implement species-specific AI.</summary>
    public abstract void Update(float dt, CreatureUpdateContext ctx);

    /// <summary>Check contact damage against player rect. Returns damage dealt (0 if none).</summary>
    public virtual int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive || ContactDamage <= 0 || DamageCooldown > 0) return 0;
        if (Rect.Intersects(playerRect))
        {
            DamageCooldown = 1.0f;
            return ContactDamage;
        }
        return 0;
    }

    /// <summary>
    /// Apply damage. Returns true if creature died from this hit.
    /// Override for special death behavior.
    /// </summary>
    public virtual bool TakeDamage(int amount)
    {
        if (!Alive) return false;
        Hp -= amount;
        HitFlash = 0.15f;
        if (Hp <= 0)
        {
            Hp = 0;
            Alive = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Take damage with knockback. Default delegates to TakeDamage + applies velocity.
    /// Subclasses may override for custom behavior.
    /// </summary>
    public virtual bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        bool killed = TakeDamage(damage);
        if (knockbackX != 0 || knockbackY != 0)
            Velocity = new Vector2(knockbackX, knockbackY);
        return killed;
    }

    /// <summary>
    /// Select the most urgent goal based on current needs.
    /// Override for species-specific priority logic.
    /// </summary>
    public virtual CreatureGoal SelectGoal()
    {
        // Low health = flee (most creatures run when hurt)
        if (Hp > 0 && Hp <= MaxHp * 0.3f) return CreatureGoal.Flee;
        if (Needs.Safety < 0.3f) return CreatureGoal.Flee;
        if (Needs.Hunger > 0.7f) return CreatureGoal.Eat;
        if (Needs.Fatigue > 0.7f) return CreatureGoal.Rest;
        return CreatureGoal.Wander;
    }

    /// <summary>
    /// Tick needs over time. Call from Update.
    /// </summary>
    public void TickNeeds(float dt)
    {
        Needs.Hunger = MathHelper.Clamp(Needs.Hunger + dt * HungerRate, 0f, 1f);

        // Fatigue builds when active, recovers when resting
        if (CurrentGoal == CreatureGoal.Rest)
            Needs.Fatigue = MathHelper.Clamp(Needs.Fatigue - dt * FatigueRecoveryRate, 0f, 1f);
        else
            Needs.Fatigue = MathHelper.Clamp(Needs.Fatigue + dt * FatigueRate, 0f, 1f);

        // Safety recovers toward 1.0 when no threats present
        Needs.Safety = MathHelper.Clamp(Needs.Safety + dt * 0.1f, 0f, 1f);

        CurrentGoal = SelectGoal();
    }

    /// <summary>
    /// Apply weather modifiers to velocity and needs. Call from subclass Update after movement calc.
    /// </summary>
    public void ApplyWeatherEffects(float dt, CreatureUpdateContext ctx)
    {
        // Rain: slower movement, increased hunger
        if (ctx.IsRaining)
        {
            Velocity.X *= 0.7f;
            Needs.Hunger = MathHelper.Clamp(Needs.Hunger + 0.002f * dt, 0f, 1f);
        }
        // Storm: even slower
        if (ctx.IsStorming)
        {
            Velocity.X *= 0.5f;
        }
        // Cold: slower + more fatigue
        if (ctx.Temperature < 0.3f)
        {
            Velocity.X *= 0.8f;
            Needs.Fatigue = MathHelper.Clamp(Needs.Fatigue + 0.001f * dt, 0f, 1f);
        }
        // Rain burrowing for prey creatures
        if (ctx.IsRaining && CanBurrow && BurrowProgress < 1f
            && (Role == EcologicalRole.Herbivore || Role == EcologicalRole.Prey || Role == EcologicalRole.Flighty)
            && Needs.Safety >= 0.5f) // only burrow if not in danger
        {
            // Encourage resting/burrowing
            Needs.Fatigue = MathHelper.Clamp(Needs.Fatigue + 0.005f * dt, 0f, 1f);
        }
    }

    // TODO: Slug slime trail slow effect — check in creature update loops:
    // foreach slug in _slugs, if slug.IsOnSlimeTrail(creature.Position), creature.Velocity.X *= 0.7f;

    /// <summary>Find the nearest edible food source within range.</summary>
    public FoodSource FindFood(List<FoodSource> foods, float maxRange = 9999f)
    {
        FoodSource best = null;
        float bestDist = maxRange;
        foreach (var f in foods)
        {
            if (f.Depleted) continue;
            if (!FoodSource.CanEat(Role, f.Type)) continue;
            float d = Vector2.Distance(Position, f.Position);
            // Use SmellRadius for corpses — they're detectable from farther away as they age
            float detectRange = Math.Min(maxRange, f.SmellRadius > 0 ? f.SmellRadius : maxRange);
            if (d < detectRange && d < bestDist) { bestDist = d; best = f; }
        }
        return best;
    }

    /// <summary>
    /// Returns true if 'predator' naturally hunts/threatens 'prey'.
    /// This defines the food chain.
    /// </summary>
    public static bool IsThreatTo(Creature predator, Creature prey)
    {
        if (predator.Role is EcologicalRole.Predator or EcologicalRole.Apex)
        {
            if (prey.Role is EcologicalRole.Herbivore or EcologicalRole.Prey
                or EcologicalRole.Scavenger or EcologicalRole.Flighty)
                return true;
        }
        if (predator.Role == EcologicalRole.Predator && prey.Role == EcologicalRole.Predator)
        {
            int predArea = predator.CreatureWidth * predator.CreatureHeight;
            int preyArea = prey.CreatureWidth * prey.CreatureHeight;
            if (predArea > preyArea * 1.3f) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if these two creatures are the same faction/species and should ignore each other.
    /// </summary>
    public static bool IsSameFaction(Creature a, Creature b)
    {
        if (a.GetType() == b.GetType())
        {
            if (a is Crawler ca && b is Crawler cb)
                return ca.Variant == cb.Variant;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Scan nearby creatures for threats and prey. Updates Safety and returns nearest threat/prey.
    /// </summary>
    protected (Creature nearestThreat, float threatDist, Creature nearestPrey, float preyDist)
        ScanCreatures(List<Creature> creatures, float threatRange, float preyRange)
    {
        Creature nearestThreat = null;
        float threatDist = threatRange;
        Creature nearestPrey = null;
        float preyDist = preyRange;

        foreach (var other in creatures)
        {
            if (other == this || !other.Alive) continue;
            float d = Vector2.Distance(Position, other.Position);

            if (d < threatDist && IsThreatTo(other, this))
            {
                nearestThreat = other;
                threatDist = d;
            }

            if (d < preyDist && WillHunt(other) && !IsSameFaction(this, other))
            {
                // Burrowed creatures are harder to detect
                float effectiveD = other.IsBurrowed ? d * 2f : d;
                if (effectiveD < preyDist)
                {
                    nearestPrey = other;
                    preyDist = effectiveD;
                }
            }
        }

        if (nearestThreat != null)
        {
            float safetyFromCreature = MathHelper.Clamp(threatDist / threatRange, 0f, 1f);
            Needs.Safety = Math.Min(Needs.Safety, safetyFromCreature);
        }

        return (nearestThreat, threatDist, nearestPrey, preyDist);
    }

    /// <summary>Find the nearest creature of a specific role within range.</summary>
    public Creature FindNearestCreature(List<Creature> creatures, EcologicalRole role, float maxRange)
    {
        Creature best = null;
        float bestDist = maxRange;
        foreach (var c in creatures)
        {
            if (c == this || !c.Alive || c.Role != role) continue;
            float d = Vector2.Distance(Position, c.Position);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    // --- Burrowing (Feature 4) ---
    public bool IsBurrowed;
    public float BurrowProgress; // 0 = surface, 1 = fully burrowed
    public virtual bool CanBurrow => true;

    // --- Grazing (Feature 5) ---
    public Vector2? LastFoodPosition;
    public float GrazeMoveTimer;
    protected CreatureGoal _prevGoal;

    /// <summary>Propagate panic to nearby same-species creatures.</summary>
    public static void PropagateStartle(Creature source, List<Creature> creatures, float range = 100f)
    {
        foreach (var c in creatures)
        {
            if (c == source || !c.Alive) continue;
            if (!IsSameFaction(source, c)) continue;
            float d = Vector2.Distance(source.Position, c.Position);
            if (d < range)
            {
                float panicAmount = (1f - d / range) * 0.5f;
                c.Needs.Safety = Math.Min(c.Needs.Safety, source.Needs.Safety + panicAmount);
            }
        }
    }

    // --- Predator Selectivity (Feature 7) ---
    /// <summary>Minimum and maximum prey size (area in pixels²) this creature will hunt.</summary>
    public virtual (int min, int max) PreySize => (0, int.MaxValue);

    /// <summary>Whether this creature considers the target worth hunting.</summary>
    public virtual bool WillHunt(Creature target)
    {
        if (!IsThreatTo(this, target)) return false;
        int area = target.CreatureWidth * target.CreatureHeight;
        var (min, max) = PreySize;
        return area >= min && area <= max;
    }

    // --- Tile Raycasting ---
    /// <summary>Simple tile raycast: returns distance to first solid tile, or maxDist if clear.</summary>
    public static float TileRaycast(TileGrid tg, int ts, Vector2 from, int dirX, int dirY, float maxDist = 200f)
    {
        if (tg == null) return maxDist;
        float step = ts * 0.5f;
        int steps = (int)(maxDist / step);
        for (int i = 1; i <= steps; i++)
        {
            float x = from.X + dirX * step * i;
            float y = from.Y + dirY * step * i;
            int tx = (int)(x / ts);
            int ty = (int)(y / ts);
            if (TileProperties.IsSolid(tg.GetTileAt(tx, ty)))
                return step * i;
        }
        return maxDist;
    }

    /// <summary>Check if there's a floor ahead for ledge detection.</summary>
    public static bool HasFloorAhead(TileGrid tg, int ts, Vector2 pos, int dir, float checkAhead = 16f, float checkDepth = 48f)
    {
        if (tg == null) return true;
        float checkX = pos.X + dir * checkAhead;
        float checkY = pos.Y;
        for (float dy = 0; dy < checkDepth; dy += ts * 0.5f)
        {
            int tx = (int)(checkX / ts);
            int ty = (int)((checkY + dy) / ts);
            if (TileProperties.IsSolid(tg.GetTileAt(tx, ty)))
                return true;
        }
        return false;
    }

    /// <summary>Check if there's a wall immediately ahead.</summary>
    public static bool HasWallAhead(TileGrid tg, int ts, Vector2 pos, int dir, float checkDist = 8f)
    {
        if (tg == null) return false;
        int tx = (int)((pos.X + dir * checkDist) / ts);
        int ty = (int)(pos.Y / ts);
        return TileProperties.IsSolid(tg.GetTileAt(tx, ty));
    }

    // --- Pathfinding ---
    protected List<Vector2> _path;
    protected int _pathIndex;

    /// <summary>Navigate to target using ground pathfinding. Returns true if path found.</summary>
    protected bool NavigateTo(Vector2 target, CreatureUpdateContext ctx)
    {
        if (ctx.TileGrid == null) return false;
        _path = ctx.TileGrid.FindGroundPath(Position, target, CreatureWidth);
        _pathIndex = 0;
        return _path != null;
    }

    /// <summary>Follow the current path. Moves toward each waypoint in sequence.</summary>
    protected void UpdatePathFollow(float dt, float speed)
    {
        if (_path == null || _pathIndex >= _path.Count) { _path = null; return; }

        Vector2 target = _path[_pathIndex];
        Vector2 diff = target - Position;
        float dist = diff.Length();

        if (dist < 4f)
        {
            _pathIndex++;
            if (_pathIndex >= _path.Count) { _path = null; return; }
            target = _path[_pathIndex];
            diff = target - Position;
            dist = diff.Length();
        }

        if (dist > 0)
        {
            Vector2 dir = diff / dist;
            float move = speed * dt;
            if (move > dist) move = dist;
            Position += dir * move;
            if (dir.X > 0.1f) TrySetDir(1);
            else if (dir.X < -0.1f) TrySetDir(-1);
        }
    }

    // --- Wander Range ---
    public Vector2 SpawnOrigin;
    public float WanderRadius = 100f;
    public float MaxWanderRadius = 500f;

    // --- Noise Reaction (Feature 3) ---
    /// <summary>Check for nearby noise events and react. Returns the loudest noise within hearing range.</summary>
    public NoiseEvent CheckNoise(List<NoiseEvent> events, float hearingRange = 300f)
    {
        if (events == null) return null;
        NoiseEvent loudest = null;
        float loudestIntensity = 0f;
        foreach (var e in events)
        {
            if (e.Expired) continue;
            if (e.Timer < e.MaxTimer - 0.3f) continue;
            float d = Vector2.Distance(Position, e.Position);
            if (d < hearingRange && d < e.Radius)
            {
                float effective = e.Intensity * (1f - d / e.Radius);
                if (effective > loudestIntensity)
                {
                    loudest = e;
                    loudestIntensity = effective;
                }
            }
        }
        return loudest;
    }

    /// <summary>
    /// React to lantern light. Nocturnal creatures flee, some bugs are attracted.
    /// Call from creature Update. Returns: -1 = flee, +1 = attracted, 0 = ignore.
    /// </summary>
    public int ReactToLantern(CreatureUpdateContext ctx)
    {
        if (!ctx.LanternActive) return 0;
        float dist = Vector2.Distance(Position + new Vector2(CreatureWidth / 2f, CreatureHeight / 2f), ctx.LanternPos);
        if (dist > ctx.LanternRadius * 1.5f) return 0;

        if (IsNocturnal)
        {
            Needs.Safety = MathHelper.Clamp(Needs.Safety - 0.3f, 0f, 1f);
            return -1; // flee
        }
        if (this is Crawler cr && (cr.Variant == CrawlerVariant.Skitter || cr.Variant == CrawlerVariant.Forager))
            return 1; // attracted to light
        return 0;
    }
}
