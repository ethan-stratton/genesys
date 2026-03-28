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

    // --- Health ---
    public bool Alive = true;
    public int Hp;
    public int MaxHp;
    public float HitFlash;

    // --- Combat ---
    /// <summary>Contact damage to player (0 = no contact damage). Override per species.</summary>
    public virtual int ContactDamage => 0;
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
            if (d < bestDist) { bestDist = d; best = f; }
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

            if (d < preyDist && IsThreatTo(this, other) && !IsSameFaction(this, other))
            {
                nearestPrey = other;
                preyDist = d;
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
}
