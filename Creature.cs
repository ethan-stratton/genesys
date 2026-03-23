using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

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

    // --- Spatial ---
    public Vector2 Position;
    public Vector2 Velocity;
    public int Dir = 1; // facing direction: 1 = right, -1 = left

    // --- Health ---
    public bool Alive = true;
    public int Hp;
    public int MaxHp;
    public float HitFlash;

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
    /// Tick needs over time. Call from Update.
    /// </summary>
    public void TickNeeds(float dt)
    {
        Needs.Hunger = MathHelper.Clamp(Needs.Hunger + dt * 0.002f, 0f, 1f); // ~8 min to starve
        Needs.Fatigue = MathHelper.Clamp(Needs.Fatigue + dt * 0.001f, 0f, 1f);
        // Safety is set externally based on threats
    }
}
