# Procedural Animation & Ecosystem AI — Implementation Plan

## Overview
Transition Genesis from sprite-based rectangle rendering to Rain World-style procedural animation with goal-based creature AI. This is a phased plan — each phase is playable and testable independently.

---

## Phase 1: Procedural Crawler Legs (CURRENT)
**Goal**: Replace crawler rectangles with segmented bodies + IK legs

### 1A — IK Leg System
- [ ] `LimbSegment` struct: origin, length, angle
- [ ] `IKLeg` class: 2 segments (thigh + shin), target foot position, solve with law of cosines
- [ ] `IKLeg.SolveIK(Vector2 hip, Vector2 target)` → computes joint angles
- [ ] Render each segment as a colored rectangle at the solved angle

### 1B — Crawler Body Rewrite
- [ ] Body = 2-3 connected oval segments (head, thorax, abdomen) with spring connections
- [ ] 6 legs (3 per side) attached to thorax segment
- [ ] Tripod gait: legs grouped (1,3,5) and (2,4,6) — one group steps while other holds
- [ ] Foot targets: raycast down from hip to find ground contact point
- [ ] Step trigger: when foot target moves too far from current foot position, initiate step
- [ ] Step animation: smooth arc from old position to new position (~0.1s)

### 1C — Variant Differentiation
- [ ] Forager: short legs, slow gait, wide body — scurrying
- [ ] Skitter: long legs, fast gait, narrow body — darting
- [ ] Leaper: powerful rear legs (longer), shorter front legs — grasshopper-like
- [ ] Bombardier: armored look, pulsing abdomen segment, shorter legs

### 1D — Visual Polish
- [ ] Antennae as 2 thin trailing segments off the head (sine wave)
- [ ] Body color gradient (darker at joints)
- [ ] Leg segments slightly thinner at the foot end
- [ ] Shadow blob under body

**Estimated effort**: 2-3 sessions
**Deliverable**: Crawlers that walk with visible legs, body flex, variant shapes

---

## Phase 2: Sound-Based Detection
**Goal**: Enemies react to sounds, not omniscient player tracking

### 2A — Sound Event System
- [ ] `SoundEvent` struct: position, radius, intensity, type (footstep/landing/combat/ambient)
- [ ] Player generates sounds: landing (radius scales with fall speed), running (small radius), combat (large radius), crouching (minimal)
- [ ] Sound events decay over time (0.5-2s)

### 2B — Creature Hearing
- [ ] Each creature has a `hearingRange` stat
- [ ] On hearing a sound: navigate to sound SOURCE position (not player position)
- [ ] Investigation behavior: move to location, sniff around for 3-5s, return to routine
- [ ] Creatures currently chasing ignore quiet sounds (focus on target)
- [ ] Bio tier Adam = quieter footsteps. Tech suit = louder.

### 2C — Sound Visualization (Debug)
- [ ] Debug mode: show sound event radii as expanding circles
- [ ] Show creature hearing state (investigating vs unaware)

**Estimated effort**: 1-2 sessions
**Deliverable**: Stealth matters. Walking quietly past enemies is viable.

---

## Phase 3: Goal-Based AI
**Goal**: Creatures have lives beyond "chase player"

### 3A — Needs System
- [ ] Each creature has: hunger, fatigue, safety
- [ ] Hunger increases over time → seek food
- [ ] Fatigue increases over time → return to nest
- [ ] Safety decreases near threats → flee or fight

### 3B — World Objects
- [ ] Food sources: plant tiles, detritus patches (placed in editor)
- [ ] Nest locations: per-creature home positions (set at spawn, or placed as nest entities)
- [ ] Creatures navigate to food when hungry, eat (destroy/deplete), navigate home when fed

### 3C — Day/Night Cycle
- [ ] Time-of-day system (visual + gameplay)
- [ ] Diurnal creatures: active during day, shelter at night
- [ ] Nocturnal creatures: emerge at night
- [ ] Transition periods: dusk/dawn = most dangerous (overlap)

### 3D — Creature-to-Creature Awareness
- [ ] Predator/prey relationships between species
- [ ] Crawlers flee from wingbeaters
- [ ] Wingbeaters hunt crawlers/birds
- [ ] Emergent: player can use predators to clear prey, or vice versa

**Estimated effort**: 3-4 sessions
**Deliverable**: Creatures that live, not just patrol. Ecosystem emergent behavior.

---

## Phase 4: Dynamic Relationships
**Goal**: Creatures remember encounters

### 4A — Social Memory (Rain World model)
- [ ] Per-creature memory: `like`, `fear`, `tempLike`, `tempFear`, `know` (toward player and other creatures)
- [ ] Helping a creature (killing its predator) → increases `like`
- [ ] Attacking a creature → increases `fear`/decreases `like`
- [ ] Memory persists across rooms/sessions (saved)

### 4B — Personality Stats
- [ ] Per-creature random stats at spawn: sympathy, energy, bravery
- [ ] Derived: nervousness, aggression, dominance
- [ ] Same species, different individuals behave differently

### 4C — Lineage
- [ ] Killed creature → replaced with slightly stronger variant next cycle
- [ ] Natural selection: ecosystem gets harder if you kill aggressively
- [ ] Pacifist play → ecosystem stays stable/friendly

**Estimated effort**: 3-4 sessions
**Deliverable**: A living world that responds to playstyle

---

## Phase 5: Player Procedural Animation
**Goal**: Adam's limbs move procedurally

### 5A — Leg System
- [ ] 2 legs with IK, foot plants on ground
- [ ] Running = procedural stride cycle
- [ ] Jumping = legs tuck, extend on landing
- [ ] Crouching = legs bend deeper

### 5B — Arm System
- [ ] Arms with 2 segments each
- [ ] Climbing = hand-over-hand (each hand targets next rope/wall point)
- [ ] Melee = arm follows weapon swing arc
- [ ] Idle = arms hang or sway slightly

### 5C — Body Response
- [ ] Torso leans into movement direction
- [ ] Landing impact compresses body momentarily
- [ ] Wind affects hair/loose elements

**Estimated effort**: 4-5 sessions
**Deliverable**: Adam that moves like a person, not a sprite sheet

---

## Phase 6: All Creatures Procedural
- [ ] Wingbeater: wing flap physics, body bob, talons tuck/extend
- [ ] Birds: wing tuck in flight, legs dangle, perch animation
- [ ] Future creatures: built from limb primitives (define limb count/length/behavior = new creature)
- [ ] Dragon: massive scale procedural — each segment of body independent

**Estimated effort**: Ongoing as creatures are added

---

## Success Criteria
- Crawlers look like living insects, not colored rectangles
- Player can sneak past enemies using sound awareness
- Creatures have visible daily routines (food → nest)
- Killing aggressively makes the world harder
- Helping creatures changes their behavior toward you
- Adam's movement feels physical and connected to the world

---

## References
- Rain World GDC talks and devlog
- Research notes: `video-research-2026-03-23-ecosystems.md`
- Enemy design principles: `video-research-2026-03-23-enemies.md`

---

## Architecture Foundation (DO FIRST — prevents refactoring)

These are structural decisions that become exponentially harder to change later. Lay them down before building more creatures.

### 1. `Creature` Base Class
- All enemies inherit: Crawler, Bird, Wingbeater, and all future types
- Shared: Position, Velocity, Hp, MaxHp, Alive, HitFlash, VisualScale, Dir
- Shared: CreatureNeeds (hunger, fatigue, safety)
- Shared: CreatureId (persistent Guid, survives room transitions and save/load)
- Shared: HomeNode, GoalNode (WorldNode references for long-range goals)
- Shared: Update(dt), Draw(sb, pixel), TakeDamage(), Die()
- Variant-specific behavior via override or composition

### 2. `CreatureNeeds` Struct
```
float Hunger;    // 0 = full, 1 = starving. Increases over time.
float Fatigue;   // 0 = rested, 1 = exhausted. Increases with activity.
float Safety;    // 0 = terrified, 1 = safe. Decreases near threats.
```
Every creature carries this from spawn. Behavior systems read it later.

### 3. `WorldNode` Graph
- Each room/area = a node with Id, name, entry/exit points
- Edges = connections between rooms (which exit leads where)
- Creatures store CurrentNode + GoalNode
- Off-screen creatures pathfind on this graph abstractly
- On-screen creatures use full physics

### 4. Persistent Creature IDs
- Each creature gets a Guid at spawn
- Save/load serializes creature state by Id
- Room transitions preserve creature identity
- Enables: reputation tracking, lineage, "this specific lizard remembers you"

### Priority Order
1. Creature base class (NOW — every new enemy type makes this harder)
2. CreatureNeeds fields (NOW — just data, zero behavior change)
3. Persistent IDs (NOW — trivial to add, painful to retrofit)
4. WorldNode graph (SOON — before second area is built)
5. Everything else (LATER — builds on top of these)

## Off-Screen Simulation Design

### How It Works
- Player is in Room A. Creatures in Room B, C, D still exist.
- Off-screen creatures run simplified tick every ~2 seconds:
  - Advance along WorldNode graph toward GoalNode
  - Consume needs (hunger++, fatigue++)
  - Make decisions (find food, return home, flee predator)
  - NO physics, NO collision, NO rendering
- When creature's CurrentNode == player's room → materialize with full simulation
- When player leaves a room → creatures in that room switch to off-screen mode

### What This Enables
- Enter a room and find two species already fighting
- Creatures migrate between areas seeking food
- Predators follow prey across rooms
- Kill all crawlers in an area → they're actually gone (until lineage respawns)
- The world has a pulse whether you're watching or not

### Rain World's Key Insight
"The player is just another creature. There are no special cases for the player."
