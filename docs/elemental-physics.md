# Elemental Physics System — Design Doc

## Architecture Overview

The cheapest path: a **tile reaction table** + a **tick-based propagation loop**. No new ECS, no pixel simulation. Just extend what we already have.

### Core Data Structure

```csharp
// TileGrid.cs — add to TileGrid class
private float[] _tileTimers;      // per-tile timer (for sustained reactions like heating metal)
private byte[] _tileState;        // per-tile state flags (wet, electrified, heated, corroded, burning, frozen, oiled)

[Flags]
public enum TileState : byte
{
    None        = 0,
    Wet         = 1,    // rained on, splashed
    Electrified = 2,    // conducting electricity
    Heated      = 4,    // near fire, accumulating heat
    Corroded    = 8,    // acid damage over time
    Burning     = 16,   // actively on fire (wood/oil)
    Frozen      = 32,   // ice variant
    Oiled       = 64,   // flammable surface
}
```

### How It Works

Every `N` frames (not every frame — 4-10 fps is fine for tile reactions), run `TileGrid.TickReactions(float dt)`:

```
for each tile in grid:
    if tile has neighbors or state flags that trigger a reaction:
        apply the reaction (change tile type, set state, spawn particle, start timer)
```

That's it. No pathfinding, no fluid sim, no entity system. Just a nested loop over the grid checking neighbor interactions.

### Why This Is Cheap

- Grid is already there. Adding `byte[]` state + `float[]` timers = ~2 arrays sized `width * height`.
- Reaction tick at 4fps on a 100×60 grid = 6000 checks × 4/sec = 24K checks/sec. Trivial.
- Each check is a switch statement on tile type + neighbor types. Branch predictor eats this alive.
- Visual effects (glow, steam, sparks) are just draw-time checks on `_tileState`, not new entities.

---

## Phase 1 — Tile-to-Tile Reactions (Cheap, High Impact)

### Implementation: ~200 lines in TileGrid.cs + ~50 lines draw code

**Add to TileGrid:**
- `byte[] _tileState` array (same size as grid)
- `float[] _tileTimers` array
- `void TickReactions(float dt)` — called from Game1.Update every 0.25s

**Reactions to implement:**

#### 1. Electricity + Water/Puddle → Electrified Pool
```
if tile is ElectricShock && neighbor is Water/Puddle:
    flood-fill connected water tiles, set Electrified state
    electrified water damages player (same as ElectricShock)
    
    // Visual: water tint shifts to yellow-white, small spark particles
    // Duration: while electricity source exists
    // Cost: one flood-fill when electricity activates (cache result, invalidate on tile change)
```
- Cheapest: on RetractExtended toggle, BFS from each ElectricShock tile into adjacent water. Store result in a `HashSet<(int,int)>` and reuse until grid changes.

#### 2. Fire + Adjacent Wood → Burns Away
```
if tile is Fire && neighbor is Wood (new tile type, or use Breakable):
    start timer on wood tile (3-5 seconds)
    set Burning state (visual: orange flicker overlay)
    when timer expires: tile becomes Empty
    fire spreads to adjacent wood tiles (chain reaction)
    
    // Rate limit: fire spreads once per second max
    // Cap: maximum 20 tiles burning simultaneously (prevent runaway)
```

#### 3. Rain → Extinguishes Fire, Creates Puddles
```
// Weather system writes into tile state, not tile types
when rain active:
    every 0.5s, for exposed tiles (no solid tile above):
        if tile is Fire: remove fire, set tile to Empty
        if tile is Empty && tile below is Solid: 30% chance → set Wet state
        if tile has Wet state for >3s: become Puddle tile
        
    // "Exposed" check: scan upward from tile until ceiling or grid top
    // Cache exposure map when weather changes (not every tick)
```

#### 4. Acid + Breakable → Dissolves Over Time
```
if tile is Acid && neighbor is Breakable/BreakableGlass/Wood:
    start timer on breakable (2-4 seconds)
    set Corroded state (visual: green drip particles, tile color shifts)
    when timer expires: tile becomes Empty (no item drop)
    
    // Acid is NOT consumed (persistent hazard)
```

#### 5. Water + Lava → Cooled Stone
```
if tile is Water && neighbor is Lava (or vice versa):
    both tiles become Dirt (solid platform)
    spawn steam particle burst
    
    // Immediate reaction, no timer needed
    // Player can use this to create bridges
```

#### 6. Fire + Metal → Red-Hot Metal
```
if tile is Fire && neighbor is Metal (Dirt/Wall variant?):
    increment heat timer on metal tile
    after 2s: set Heated state
    heated metal damages player on contact (same as fire)
    visual: tile color lerps toward red-orange
    
    // Cools down when fire removed (2s cooldown)
    // Heat conducts along connected metal tiles (1 tile per second, max 5 tiles)
```

### Phase 1 Draw Changes

In `DrawFallback` / tile draw loop, check `_tileState[idx]`:
- `Electrified`: overlay yellow-white flicker on water tiles, tiny spark particles
- `Burning`: orange-red flicker overlay, small flame particles rising
- `Corroded`: green tint, dripping particle
- `Heated`: color lerp toward `Color.OrangeRed` based on timer value
- `Frozen`: blue-white tint, crystalline highlight pixels
- `Wet`: slightly darker tint, occasional drip particle

These are just additive draw calls on top of existing tile rendering. No new sprite assets needed.

### New Tile Types Needed
- **Wood** (94): solid, burnable. Brown color. Burns away when fire-adjacent.
- **Metal** (95): solid, conducts electricity and heat. Grey/steel color.
- **Oil** (96): non-solid surface. Dark brown. Ignites into fire on contact with fire. Spreads flame rapidly.
- **Ice** (97): solid but slippery (reduced friction). Blue-white. Melts near fire → becomes Water.
- **Steam** (98): non-solid, temporary. Obscures vision (draw as white cloud). Dissipates after 3s.

---

## Phase 2 — Creature Synergy

### Implementation: ~100 lines per creature type

**Architecture:** Creatures already have Update loops. Add `ElementalEffect` to enemy base:

```csharp
public enum ElementalAffinity { None, Fire, Electric, Acid, Ice, Oil }

// In enemy base class or as component:
public ElementalAffinity Affinity;
public bool LeavesTrail;           // drops tiles behind it
public TileType TrailTile;         // what tile to leave
public float TrailInterval;        // seconds between drops
```

**Creatures:**

#### Fire Beetle
- Affinity: Fire. Immune to fire/lava damage.
- Behavior: walks along surfaces (use Crawler AI). Leaves Fire tiles behind it every 0.5s.
- Weakness: Water/puddle = instant kill. Rain = slowed + takes DOT.
- Player exploit: lure into water area, or toward gas leak for explosion.

#### Electric Jellyfish (floating)
- Affinity: Electric. Immune to electricity.
- Behavior: floats in water tiles. Electrifies all connected water while alive.
- Weakness: fire (2x damage). Pull out of water and it's weak.
- Player exploit: drain the water (break a pipe below) to strand it.

#### Acid Spitter
- Affinity: Acid. Immune to acid.
- Behavior: stationary or slow patrol. Projectile that creates Acid tile on impact.
- Weakness: electricity (2x damage).
- Player exploit: angle its shots to dissolve breakable walls blocking your path.

#### Oil Slug
- Affinity: None (just gross).
- Behavior: slow crawler. Leaves Oil tiles every 0.3s.
- Weakness: fire = instant death + chain ignition along oil trail.
- Player exploit: let it trail oil toward enemies, then ignite. Area denial tool.

#### Gas Bloat (flying)
- No trail. On death: creates 3×3 area of BrokenPipe-like gas.
- Fire near gas = explosion (destroy breakables in radius, heavy damage).
- Player exploit: kill near breakable walls for free demolition.

### Creature ↔ Element Damage Matrix

| | Fire | Electric | Acid | Water | Ice |
|---|---|---|---|---|---|
| Fire creature | immune | normal | 1.5x | **instant kill** | 2x |
| Electric creature | 2x | immune | normal | heals (in water) | normal |
| Acid creature | normal | 2x | immune | 0.5x (diluted) | normal |
| Oil slug | **chain death** | normal | normal | normal | slowed |
| Gas bloat | **explosion** | sparks (stun) | normal | normal | normal |

Implementation: in damage calculation, check `attacker.Affinity` vs `target.Affinity`. One switch statement.

---

## Phase 3 — Weather-Driven Gameplay

### Implementation: ~150 lines for weather system + per-weather effects

**Architecture:** Weather is a level-wide state, not per-tile.

```csharp
// Game1.cs or new WeatherSystem.cs
public enum WeatherType { Clear, Rain, Storm, HeatWave, ToxicWind, Freezing }

public class WeatherState
{
    public WeatherType Current;
    public float Duration;          // seconds remaining
    public float Intensity;         // 0-1, affects visual + mechanical severity
    public Vector2 WindDirection;   // for toxic wind, rain angle
    public float TransitionTimer;   // fade between weather states
}
```

**Weather can be:**
- Per-level (set in level data, always raining in this cave)
- Timed cycle (outdoor levels cycle through weather every 2-5 minutes)
- Triggered (boss phase change, switch activation, story event)

### Weather Effects

#### Rain
- **Mechanical:** Exposed tiles get Wet state → puddles form. Fire extinguished. Acid diluted (slower dissolve). Creatures seek shelter (pathfind toward ceilings).
- **Visual:** Particle rain (angled lines falling). Screen-space, not world-space (cheap). Splash particles on solid surfaces. Slightly darker ambient light.
- **Gameplay:** Opens water-based paths (puddles fill gaps for electric puzzles). Closes fire-based paths (can't maintain flames). Forces creatures to move.

#### Lightning Storm (Rain + Electricity)
- **Mechanical:** Every 5-10s, lightning strikes a random exposed tile. If it hits water/metal: electrifies connected tiles for 3s. If it hits a creature: heavy damage. Player gets warning (flash + 0.5s delay).
- **Visual:** Screen flash (white overlay, 2 frames). Lightning bolt particle (zigzag line from top to strike point). Thunder screen shake.
- **Gameplay:** Outdoor areas become dangerous. Metal and water tiles are death traps. Forces player to time movement between strikes. Standing under ceiling = safe.

#### Heat Wave
- **Mechanical:** Puddles/shallow water evaporate (timer → empty). Fire spreads 2x faster. Heated metal threshold reduced. Ice melts. Player takes slow DOT if exposed too long (sunstroke — screen tint reddish).
- **Visual:** Heat shimmer (slight vertical wave distortion on background). Orange-tinted ambient. Particles rising from hot surfaces.
- **Gameplay:** Water puzzles become harder (evaporating). Fire puzzles become easier (spreads further). New paths open as ice melts.

#### Freezing
- **Mechanical:** Water → Ice tiles (walkable platforms!). Puddles → frozen (no slow effect). Lava surface → cooled stone. Creatures slowed 50%. Fire duration halved.
- **Visual:** Blue-white ambient tint. Frost particles. Ice crystal overlay on affected tiles.
- **Gameplay:** Water bodies become traversable. New platforming paths. But ice is slippery (reduced friction). Fire becomes precious resource.

#### Toxic Wind
- **Mechanical:** Wind pushes gas/acid clouds in wind direction. BrokenPipe spray changes direction. Poison DOT in exposed areas (weaker than acid). Blows out small fires.
- **Visual:** Green-tinted fog scrolling in wind direction. Particle debris.
- **Gameplay:** Directional hazard. Shelter matters. Gas leak puzzles change based on wind.

### Weather Scheduling

```json
// In level JSON:
"weather": {
    "default": "clear",
    "cycle": [
        { "type": "clear", "duration": 120, "weight": 3 },
        { "type": "rain", "duration": 60, "weight": 2 },
        { "type": "storm", "duration": 30, "weight": 1 }
    ],
    "triggers": [
        { "switch": "boss-phase-2", "type": "storm", "duration": 0 }
    ]
}
```

Duration 0 = lasts until trigger is deactivated. Weights control random selection probability.

---

## Implementation Order

```
Phase 1a: TileState array + TickReactions skeleton          (~2 hours)
Phase 1b: Electricity+water, fire+wood, water+lava          (~3 hours)  
Phase 1c: Acid+breakable, fire+metal, rain basics           (~3 hours)
Phase 1d: Visual overlays for all states                    (~2 hours)
Phase 1e: New tile types (Wood, Metal, Oil, Ice, Steam)     (~1 hour)

Phase 2a: ElementalAffinity on enemy base                   (~1 hour)
Phase 2b: Fire beetle + oil slug                            (~3 hours)
Phase 2c: Electric jellyfish + acid spitter                 (~3 hours)
Phase 2d: Gas bloat + explosion system                      (~2 hours)
Phase 2e: Damage matrix                                     (~1 hour)

Phase 3a: WeatherState + weather data in levels             (~2 hours)
Phase 3b: Rain (particles + puddle formation + fire ext.)   (~3 hours)
Phase 3c: Storm (lightning strikes)                         (~2 hours)
Phase 3d: Heat wave + freezing                              (~3 hours)
Phase 3e: Toxic wind                                        (~2 hours)
```

**Total estimate: ~32 hours across all phases.**
Phase 1 alone: ~11 hours. Gets you 80% of the "wow" factor.

---

## Key Design Principle

**Reactions happen at the tile level, not the entity level.** 

Fire doesn't know about wood. The reaction system checks "fire tile adjacent to wood tile" and transforms the wood. This means:
- Player-placed fire (if we add fire weapons) triggers the same reactions
- Enemy fire triggers the same reactions  
- Environmental fire triggers the same reactions
- Zero special-casing per source

This is exactly what Divinity does right — surfaces don't care WHO created them. The simulation just runs.

---

## References

- **Divinity: Original Sin 2** — surface system, chain reactions, blessed/cursed variants
- **Breath of the Wild / TotK** — intuitive element interactions (fire updraft, metal conducts lightning, wood burns)
- **Noita** — pixel-level simulation (inspiration for depth, not implementation)
- **Spelunky 2** — liquid flow in tile grid, lava+water=obsidian
- **Rain World** — creature ecology reacting to weather cycles
- **Terraria** — biome-specific liquid interactions, lava+water=obsidian, honey combinations
