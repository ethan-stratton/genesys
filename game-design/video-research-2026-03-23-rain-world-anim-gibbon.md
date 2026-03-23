# Video Research: Rain World Animation + Gibbon Procedural Locomotion

**Date:** 2026-03-23
**Purpose:** Extract procedural animation techniques for Genesis (2D action RPG, MonoGame/C#, CPU rendering)

---

## 1. Source Material

### Video: The Rain World Animation Process
- **URL:** https://www.youtube.com/watch?v=sVntwsrjNe4
- **Channel:** Game Developers Conference
- **Speakers:** Joar Jakobsson (creature design/animation/AI/programming) and James Therrien (level design/audio/narrative)
- **Context:** GDC talk about how Rain World's creatures are entirely procedurally animated — no frame-by-frame or rigged playback

### Video: The Procedural Animation of Gibbon: Beyond the Trees
- **URL:** https://www.youtube.com/watch?v=KCKdGlpsdlo
- **Channel:** Wolfire Games
- **Speaker:** David Rosen
- **Context:** ~60-hour prototype for Broken Rules' game. Covers running, swinging, jumping — all procedural. Controls: A/D horizontal speed, W/S toggle run/swing, Space jump.

### GitHub Repo: ProjectGibbon
- **URL:** https://github.com/David20321/ProjectGibbon
- **Structure:** Unity project with two key files:
  - **`VerletSystem.cs`** — Verlet integration physics engine (particles + distance constraints). ~150 lines. Implements: gravity, implicit velocity (pos - old_pos), distance constraint enforcement with mass weighting, pinning.
  - **`GibbonControl.cs`** — ~1000+ lines. The entire animation system: simple 5-point rig (2 shoulders, 2 hands, 1 body base), full 14-point IK rig, two-bone IK solver, separate MovementSystems for walk/swing/jump that get blended into a display system.

---

## 2. Rain World's Animation Pipeline

### Core Architecture: Physics ↔ Cosmetics Separation
The single most important idea. Every creature has TWO layers:

1. **Physics/Logic Layer** (simple): Beads connected by sticks (distance-constrained points). This is what runs offscreen. Cheap. Drives AI, collision, interaction.
2. **Cosmetic Layer** (complex): Smooth curves, dangly bits, visual detail. One-way dependency on physics layer. Only runs when on-screen. "Paper doll pasted on top of logic."

**Why this matters for Genesis:** We can have dozens of creatures active offscreen with minimal cost. Only pay the cosmetic rendering cost when visible.

### AI IS Animation
No separation between locomotion AI and animation. The AI decides "where should I go" and the locomotion system figures out limb placement, which IS the animation. Example: vulture switching from climbing to flying isn't a triggered animation — the locomotion AI lost its grip points and organically switched to flight mode.

### The Grab System (Daddy Long Legs example)
For climbing creatures, the core loop is:
1. **Ideal grab position** (yellow dot): Where you'd grab if everywhere were valid
2. **Temporary goal** (green dot): Monte Carlo search — each frame, pick a random nearby coordinate, score it (terrain = good, air = bad, closer to ideal = better). If better than current temp goal, swap. Over frames, gravitates toward good positions.
3. **Actual grab target** (blue dot): Where the tentacle is currently heading

This stochastic approach is **cheaper than exhaustive search** and the randomness adds organic character.

### Weight Illusion (Brilliant Fake)
Instead of real rope/tension physics:
- Count tentacles currently touching terrain
- More touching → faster movement toward goal, less gravity
- Fewer touching → more gravity, slower forward progress
- Creates convincing illusion of weight support without any actual load-bearing simulation

### Dead Cosmetic Elements
Add non-functional visual elements (extra tentacles that don't interact with anything) that look identical to functional ones. Player can't tell the difference. Cheap visual complexity.

### Fantasy Creatures = Forgiveness
No one knows what a tentacle monster "should" move like. Wonky movement gets benefit of the doubt. Humans/horses would immediately look wrong.

---

## 3. Gibbon's Swing/Locomotion System

### Verlet Physics Core
From `VerletSystem.cs` — dead simple and perfect for our needs:
```
position += (position - old_position) + acceleration * dt²
```
- Velocity is implicit (current pos minus previous pos)
- Distance constraints enforced iteratively with mass weighting
- Pinning system for anchored points
- Based on Thomas Jakobson's 2001 Hitman ragdoll paper

### The Simple Rig (5 points, 5 constraints)
The body is a **triangle with two arms**:
- Points: `shoulder_r(0)`, `hand_r(1)`, `shoulder_l(2)`, `hand_l(3)`, `body_base(4)`
- Mass: shoulders=2, body=4 (heavier body = more inertia at core)
- Arms have min/max length constraints (min = 40% of max, allowing flex)
- Triangle constraints (top, right side, left side) maintain torso shape

This 5-point rig drives EVERYTHING — walk, swing, jump each have their own copy, then blend to display.

### Three Movement Systems → One Display
Separate `MovementSystem` instances for walk, swing, and jump. Each independently updates its own simple_rig + limb_targets. Results blend into the `display` system. This is the same separation-of-concerns Rain World uses.

### Center of Mass Control (Key Insight)
David's #1 rule: **Get the center of mass right or everything looks wrong.**

- Running: Average branch height in front/behind based on speed → smooth COM over rough terrain
- Swinging: Only pay attention to handhold positions, ignore geometry between them
- Both modes: COM follows sine wave variations, with amplitude/frequency changing based on horizontal speed

### Swing Animation Specifics
- Hands are pulled toward branch grab points with forces
- Stick constraints automatically orient the torso correctly
- Arms are entirely physics-driven — no keyframes
- Simple forces achieve effects: arms raise during run, spread during fast slides for "balance"
- Legs during swing: just contract/extend in sync with each swing arc (not physics-driven)

### Legs as Wheels
For running, legs are conceptually **circles that change size based on speed**:
- Slow = small circles
- Fast = large circles
- Gallop changes circle timing so feet land in different rhythm
- Slide: feet just stick to ground (simplest case)

### Magic Numbers + Fast Iteration
The code is FULL of tunable constants (gallop_offset, gallop_stride, gallop_hip_rotate, etc.). David's approach:
- Dear ImGui sliders for every parameter
- Minimize iteration time — try values fast, compare to reference footage
- Reference: Broken Rules' target animation, YouTube gibbon footage, academic papers (for pictures, not math)

### Anticipation / Look Direction
Procedural animation often feels "puppet-like" (purely reactive). Fix:
- Character looks where it's going (next handhold when swinging)
- For jumps: predict landing time + position, use that to drive look direction, pose angle, and timing
- Head look IK in the code: transforms look_target into head-local space, applies rotation

### Jump System
- On space: copy current display rig pose into jump rig
- Predict landing point via trajectory (simple ballistic)
- `jump_com_offset` preserves COM continuity so character doesn't warp
- Landing target drives anticipatory posing

---

## 4. What We Can Steal for Genesis

### From Rain World

| Technique | Genesis Application |
|---|---|
| **Physics/Cosmetics separation** | ALL creatures: simple collision body (runs always) + visual detail layer (runs when on-screen). Critical for CPU budget. |
| **Monte Carlo grab search** | Crawler leg placement, any climbing creature. Random sample positions each frame, score them, converge. Cheap + organic. |
| **Weight illusion via contact count** | Grapple swing: more contact points = more control. Creatures clinging to walls: count grip points → adjust gravity effect. |
| **Dead cosmetic elements** | Capes, hair, trailing bits on creatures that don't affect gameplay but add visual richness. Just slave them to physics points. |
| **AI = Animation** | Creature locomotion decisions directly produce animation. No animation state machine — the movement IS the visual. |

### From Gibbon

| Technique | Genesis Application |
|---|---|
| **Verlet particle system** | Universal animation backbone. 5 points + constraints = full body. Works in 2D, CPU-friendly, ~150 lines of code. |
| **COM sine wave** | All locomotion: running, swinging, flying. Smooth sine-based COM with speed-dependent amplitude/frequency. |
| **Legs as variable-radius circles** | Bipeds, quadrupeds — circle-based leg animation with speed-scaling. Gallop = offset timing between circles. |
| **Separate rigs per movement mode** | Walk rig, swing rig, jump rig, combat rig — blend between them for transitions. |
| **Anticipatory look direction** | All creatures look where they're going. Player character looks at grapple target. Predicted landing drives pose. |
| **Magic number sliders** | Build an ImGui (or equivalent) debug panel for tuning ALL animation parameters in real-time. Non-negotiable for procedural animation. |

---

## 5. Specific Grapple Swing Improvements (from Gibbon)

### Current System
Pendulum physics: angular acceleration from gravity, damping, A/D pump swing, W/S reel.

### Proposed Improvements

#### 5a. Verlet Body Rig for Player While Swinging
Replace (or augment) simple angle-based rendering with a 5-point Verlet rig:
- 2 hand points (pinned to rope endpoint or grapple point)
- 2 shoulder points
- 1 hip/body point (heaviest mass)
- Distance constraints maintain body shape
- Physics automatically generates secondary motion (torso tilting, arm stretching)
- **Result:** Body reacts to swing forces naturally instead of being a static sprite on a pendulum

#### 5b. COM Smoothing
- Track the character's center of mass as a smooth curve
- On grapple attach: preserve COM continuity (Gibbon's `jump_com_offset` trick)
- On release: smooth transition, don't snap
- COM follows sine-like arcs during swing, amplitude scales with swing speed

#### 5c. Anticipatory Posing
- When swinging toward a surface: character looks at it, legs extend toward predicted landing point
- When releasing grapple: predict landing trajectory, orient body toward landing
- When attaching: hands reach toward grapple point BEFORE connection (lead by a few frames)

#### 5d. Arm Physics During Swing
- Arms driven by forces pulling hands to grapple point
- Verlet constraints handle the rest — arms stretch, shoulders tilt
- On fast swings, arms should appear taut; on slow swings, more relaxed (adjust constraint stiffness or iterations)

#### 5e. Leg Animation During Swing
Steal Gibbon's simple approach:
- Contract and extend legs in sync with swing arc
- On upswing: legs tuck (shorter moment arm feels right)
- On downswing: legs extend
- Near release: legs reach toward predicted trajectory

#### 5f. Contact-Based Weight Feel (Rain World)
- When grappled: full control, low gravity feel
- When rope is at max length and taut: heavier feel, more pendulum
- Reeling in: lighter, more agile
- This maps to the "count contacts → adjust gravity" trick

---

## 6. Implementation Priority

### Phase 1: Foundation (Do First)
1. **Verlet particle system** — Port `VerletSystem.cs` to C#/MonoGame. ~150 lines. Points, distance constraints, mass, pinning. This is the backbone for everything.
2. **Debug visualization panel** — ImGui.NET or custom sliders. You CANNOT tune procedural animation without real-time parameter adjustment. Every magic number gets a slider.
3. **Player swing body rig** — 5-point Verlet rig for player during grapple swing. Hands pinned to rope end, body hangs naturally.

### Phase 2: Feel Good (Do Second)
4. **COM smoothing** — Sine-wave COM tracking for all locomotion modes. Smooth transitions between grapple attach/detach.
5. **Anticipatory look/pose** — Player looks at grapple target, legs orient toward predicted landing. Predicted trajectory calculation.
6. **Leg animation during swing** — Simple contract/extend synced to swing phase.

### Phase 3: All Creatures (Do Third)
7. **Physics/Cosmetics separation for all creatures** — Every creature gets a cheap physics body + optional visual layer.
8. **Monte Carlo limb placement** — For crawlers and any climbing creature. Stochastic grab-point search.
9. **Creature body rigs** — Verlet rigs per creature type. Tentacle creatures, flyers, bipeds each get their own point layout.

### Phase 4: Polish
10. **Dead cosmetic elements** — Hair, capes, trailing effects slaved to physics points.
11. **Gallop/gait variations** — Speed-dependent leg circle sizing for running creatures.
12. **Weight illusion system** — Contact counting → gravity/control adjustment for all climbing/swinging creatures.

---

## Key Takeaways

1. **Verlet integration is the answer.** Both Rain World and Gibbon use particles + distance constraints. It's simple, stable, cheap, and gives you secondary motion for free. Port it first.
2. **Separate physics from cosmetics.** Run cheap sim always, expensive visuals only when visible. This is how you get dozens of creatures on CPU.
3. **COM is king.** If the center of mass moves naturally, everything else can be approximate and still look good.
4. **Fake it.** Don't simulate rope tension — count contacts and scale gravity. Don't simulate muscle — use forces pulling to targets. The illusion is the product.
5. **Magic numbers need sliders.** Procedural animation is 30% architecture, 70% tuning. Without real-time parameter editing, you'll never get it to feel right.
