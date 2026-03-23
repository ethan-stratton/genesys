# Weather & Atmosphere System — Genesis

## Overview

Weather in Genesis is not random. The Dragon IS the planet's climate engine. Its corruption, emotional state, and proximity drive weather instability. Weather is a cascading chain reaction system, not RNG events.

This means: no dice rolls, no "random storm every 20 minutes." Every weather event has a cause the player can learn to read. The Dragon's biology, emotional state, and physical proximity are the inputs. The atmosphere is the output. Players who understand the Dragon understand the weather.

---

## The Four Global Variables

Four floats (0.0–1.0) tracked across the entire map. Each area has base values modified by Dragon proximity, time, and cascading events.

| Variable | What It Represents | Gameplay Effects |
|---|---|---|
| **Conductivity** | Static/electrical energy in atmosphere | Tech equipment degradation rate, arc-strikes between tall objects/metal, suit integrity drain |
| **Viscosity** | Air thickness/density | Movement speed, jump height, projectile physics, sound propagation distance |
| **Particulate** | Organic matter density in air | Visibility range, suit filter clogging, creature detection range (both player's and creatures') |
| **Resonance** | Dragon influence proximity | **Master variable.** Modifies all three others AND scales cipher abilities. High Resonance = powerful cipher + dangerous world |

### Variable Relationships

Resonance is the master. It doesn't directly cause weather — it amplifies the other three variables toward their trigger thresholds.

```
Resonance ─┬─► Conductivity modifier (+0.0 to +0.4 based on Resonance)
            ├─► Viscosity modifier (+0.0 to +0.3 based on Resonance)
            └─► Particulate modifier (+0.0 to +0.4 based on Resonance)
```

Each area has **base values** for the three atmospheric variables:
- **Forest/Jungle**: High Particulate base (0.4), moderate Viscosity (0.3), low Conductivity (0.1)
- **Mountains/Ridgelines**: High Conductivity base (0.5), low Viscosity (0.1), low Particulate (0.1)
- **Wetlands/Lowlands**: High Viscosity base (0.4), moderate Particulate (0.3), low Conductivity (0.1)
- **Open Plains**: Moderate all (0.2–0.3), most susceptible to rapid shifts
- **Near Dragon Nesting Sites**: Elevated base Resonance (0.3–0.5 even when Dragon is distant)

### Per-Frame Update Logic

```
For each variable V in [Conductivity, Viscosity, Particulate]:
    target = area.baseV + (Resonance * V.resonanceMultiplier) + eventModifiers
    current = lerp(current, target, deltaTime * transitionSpeed)
    clamp(current, 0.0, 1.0)

Resonance = calculateDragonProximity(playerPos, dragonPos) + area.baseResonance
           + cipherUseDecay  // recent cipher use leaves residual Resonance
```

Transition speed matters — variables don't snap. They drift. This gives players time to read the signs.

---

## Weather Events (Chain Reaction Model)

Weather events trigger when variable combinations cross thresholds. One event's effects push variables toward the next event's triggers. Players learn to read the chain.

### Event State Machine

Events have four phases:
1. **Building** (30–120 seconds) — Variables approaching threshold. EVE can warn here. Visual/audio hints.
2. **Active** (2–8 minutes depending on event) — Full gameplay effects.
3. **Waning** (30–60 seconds) — Effects diminish. Variables drift back.
4. **Aftermath** (variable) — Residual effects. Changed creature positions, damaged equipment, new paths revealed.

Events don't interrupt each other cleanly — one can trigger the next during its Active or Waning phase, creating compound weather.

---

### Static Monsoon

**Trigger**: Resonance > 0.7 AND Conductivity > 0.7

**What Happens**: Atmosphere ionizes. Constant low hum builds to a roar. Arc-strikes jump between tall plants, metal equipment, Adam's suit. No rain — just plasma and glass-sand fused by electrical discharge.

**Building Phase Signs**:
- Hair-raising static (controller vibration pulses)
- Faint crackling audio, increasing
- Small sparks between metal objects
- EVE: "Conductivity spiking. Atmospheric ionization in progress."

**Active Phase Gameplay**:
- Tech equipment degradation rate × 3.0
- Suit integrity drains passively near metal objects (2%/sec within 5m of metal)
- Standing near tall objects = arc-strike damage (randomized targeting, 1–3 sec intervals)
- Must stay grounded/low — prone or crouched reduces targeting priority
- Arc-strikes **illuminate hidden paths** — electrical discharge reveals conductive ore veins, hidden structures
- Projectile weapons with metal components arc unpredictably (reduced accuracy)

**Visual**:
- Faint violet corona discharge (St. Elmo's Fire) on every surface
- Sky shifts to deep violet-white
- Glass-sand particles glow where arcs hit ground
- Persistent afterimage trails on arc paths

**Audio**:
- Constant low-frequency hum (60–80 Hz)
- Crackling percussion of arc-strikes
- Metal objects ring/sing

**Creature Effects**:
- Insects burrow underground (unavailable for harvest/study)
- Predators become more aggressive — disoriented, lashing out
- **Static-adapted creatures emerge** — feed on electrical energy, only visible during monsoons (design opportunity: unique harvests, study targets)
- Herbivores stampede toward low ground

**Aftermath**:
- Glass-sand formations where arcs hit (temporary terrain features)
- Damaged flora (exposed resources?)
- Creature corpses from arc-strikes (harvestable)

---

### Seed Gale

**Trigger**: Resonance > 0.6 AND Particulate > 0.7 (Dragon's proximity triggers synchronized flora reproduction)

**What Happens**: The planet's massive flora simultaneously release billions of pressurized seeds. Sky turns opaque green/orange. A biological blizzard.

**Building Phase Signs**:
- Flora swell visibly — seed pods enlarge
- Pollen haze thickens (yellow-green tint)
- Creature behavior shifts — herbivores stop grazing, look up
- EVE: "Particulate rising fast. Seed dispersal imminent. Recommend sealing suit filters."

**Active Phase Gameplay**:
- Visibility range drops to 10–15m (from normal 100m+)
- Suit filters clog — integrity drops 1%/sec unless player manually clears filters (interaction prompt, 3 sec animation, buys 30 sec)
- Navigation by landmarks impossible — must use EVE's compass/waypoint or Resonance sense (cipher ability)
- Seeds are sticky/parasitic — attach to Adam's suit over time
  - Cosmetic at first, then gameplay: attached seeds add weight (movement slow), some are mildly toxic (suit integrity), some attract creatures
  - Must be manually removed (interaction) or burned off near heat source
- Bio-tier equipment **benefits** — some bio upgrades feed on particulate (passive healing?)

**Visual**:
- Organic blizzard — thick clouds of green/amber particles
- Seeds range from dust-size to fist-size
- Larger seeds impact with visible force (bounce off surfaces)
- World takes on amber/green color cast

**Audio**:
- Rushing wind with organic texture (not clean — wet, heavy)
- Seeds impacting suit/surfaces (patter → hammering as intensity builds)
- Muffled creature calls

**Creature Effects**:
- Herbivores enter feeding frenzy — gorging on seeds. Ignore player unless threatened. **Trampling hazard** if player is between herbivore and seed concentration.
- Predators use reduced visibility to hunt — **more dangerous, harder to detect**
- Some creatures use seed cover for ambush tactics (emerge from particulate cloud)
- Flying creatures grounded by particulate density

**Aftermath**:
- Seed deposits on ground (new flora growth over time? Future system hook)
- Herbivores sluggish and satiated (less aggressive, easier to study)
- Visibility slowly returns as seeds settle

---

### Atmospheric Liquefaction ("Heavy Fog")

**Trigger**: Resonance drops rapidly (delta < -0.3 within 60 seconds — Dragon withdraws, planet "exhales")

This is the **withdrawal event** — it happens when the Dragon leaves, not when it arrives. The atmosphere can't adjust fast enough.

**Building Phase Signs**:
- Sudden silence — ambient creature sounds stop
- Temperature shift visible (breath becomes visible, then surfaces fog)
- Air feels "heavy" — Viscosity climbing fast
- EVE: "Resonance dropping rapidly. Atmospheric phase transition imminent. Find high ground."

**Active Phase Gameplay**:
- Bottom ~6 meters (20 feet) becomes thick translucent atmospheric slush
- **Below fog line**:
  - Movement speed × 0.3 (swimming-like)
  - Jump height × 0.2
  - Projectile speed × 0.4 (arcing, unreliable)
  - Sound propagation halved (can't hear threats until close)
  - Melee range effectively unchanged (advantage for melee builds)
- **Above fog line**:
  - Normal movement
  - Crystal clear visibility — can see across entire area
  - Stars visible even during "day"
  - Can see creatures struggling below (strategic advantage)
- Must find high ground, climb, or wait it out
- Suit integrity stable (no chemical damage, just physical impediment)

**Visual**:
- Below fog line: refracted, dreamlike. Light bends. Shapes distort. Beautiful and disorienting.
- Above fog line: impossibly clear. Stars visible. Distant landmarks sharp.
- The boundary between zones is visible — a shimmering horizontal plane
- Creatures below appear as warped silhouettes

**Audio**:
- Below: muffled, underwater quality. Own breathing amplified.
- Above: dead silence except wind. Eerie clarity.
- The boundary acts as a sound barrier

**Creature Effects**:
- Ground creatures trapped/slowed (same movement penalties)
- Flying creatures (wingbeaters) have **massive advantage** — hunt freely above fog, dive in for kills
- Aquatic/adapted creatures thrive — this is their element
- Creatures that live above fog line (canopy dwellers) are unaffected
- **Predator/prey dynamics invert** — normally dangerous ground predators become vulnerable to aerial hunters

**Aftermath**:
- Fog dissipates bottom-up over 60–90 seconds
- Wet surfaces (residual condensation) — affects traction, visual
- Creatures that sheltered in place remain still briefly (window for approach/study)

---

### Dragon Chill

**Trigger**: Resonance spikes above 0.9 then drops below 0.3 within 30 seconds (Dragon is distressed/in pain)

This replaces the earlier "Cryo-Flash" concept. It's not geological — it's the Dragon's emotional state bleeding into the environment. The planet grieves with the Dragon.

**Building Phase Signs**:
- Resonance spike is itself the warning — EVE registers anomaly
- Brief moment of intense warmth (the spike), then sudden stillness
- All creatures freeze in place for 1–2 seconds (they feel it before Adam does)
- EVE: "Temperature anomaly — the signal is... grieving?"

**Active Phase Gameplay**:
- Temperature plummets continuously from epicenter outward
- Frost spreads visibly from the Dragon's direction
- Must find heat source immediately:
  - Thermal vents (environmental)
  - Fire (craftable)
  - Adapted shelters (have heat sources — reward for knowing the world)
  - **Movement generates warmth** — standing still = freezing. Creates urgency to keep moving.
- Suit heating system drains power rapidly (tech-tier vulnerability)
- Cold damage ticks increase over time (not instant death — pressure that builds)
- Bio-tier: some bio upgrades provide insulation (resistance, not immunity)
- Cipher-tier: Resonance is now low (the drop caused the event) — cipher abilities weakened during Dragon Chill. **You're at your weakest when you need power most.**

**Visual**:
- Frost crystallizes on surfaces in real-time, spreading outward from Dragon's direction (directional cue — tells player where Dragon is)
- Breath visible
- Colors desaturate progressively — world becomes monochrome at peak
- Ice crystal particle effects (beautiful but deadly)
- Light sources become warm beacons (contrast against blue-white world)

**Audio**:
- Creaking, cracking of rapid frost formation
- Wind drops to nothing — oppressive silence
- Crystalline tinkling of ice forming
- Adam's breathing becomes strained, visible

**Creature Effects**:
- Most creatures flee toward shelter/nests — following them can lead to shelter
- Cold-adapted creatures emerge (unique encounters only available during Dragon Chill)
- The Adapted know to hide — their shelters have heat sources (player can seek them out)
- Some creatures huddle together for warmth (emergent behavior — exploit or protect?)

**Aftermath**:
- Frost lingers on surfaces (visual + slippery terrain)
- Dead vegetation where frost was heaviest (changed landscape)
- Dragon's direction revealed by frost pattern (environmental storytelling)

---

## Compound Weather & Chain Reactions

Events push variables that can trigger subsequent events. This is the chain reaction system.

**Example Chain**:
1. Dragon approaches area → Resonance rises
2. High Resonance + forest's high base Particulate → **Seed Gale** triggers
3. Seed Gale increases Particulate further, organic matter generates static → Conductivity rises
4. If Conductivity crosses 0.7 while Resonance still high → **Static Monsoon** follows
5. Static Monsoon's energy disperses → Conductivity drops, but heat generation raises Viscosity
6. Dragon departs (scared off by its own storm?) → Resonance drops rapidly → **Atmospheric Liquefaction**

The player who reads the chain can prepare: "Seed Gale in a forest near the Dragon? Static Monsoon is coming next. I need to get away from metal and find low ground — but not TOO low, because if the Dragon leaves, the fog comes."

**Design Rule**: Never more than two simultaneous active events. If a third would trigger, queue it for when the first ends. Compound weather should be challenging, not incomprehensible.

---

## Dragon as Climate Engine

The Dragon doesn't consciously control weather. Its existence IS the climate.

| Dragon State | Resonance Effect | Weather Tendency |
|---|---|---|
| Flying overhead | Resonance spike (0.7–1.0) in area | High-Resonance events likely (Static Monsoon, Seed Gale) |
| In pain/distressed | Spike then crash | Dragon Chill |
| Calm/dormant | Low Resonance (0.1–0.3) | Stable weather, safe exploration |
| Corruption spreading | Base Resonance increases in affected areas permanently | Previously stable areas become weather-prone |
| Feeding/hunting | Moderate Resonance, fluctuating | Unpredictable minor events |

### EVE Learns Dragon-Weather Patterns

EVE's weather prediction improves over the game:

- **Early game**: "Atmospheric disturbance detected." (Vague, after building phase starts)
- **Mid game**: "Resonance climbing. Conductivity approaching threshold. Storm likely in four minutes." (Specific, during building phase)
- **Late game**: "Dragon's flight path suggests it will pass over this region in twelve minutes. Based on current atmospheric base values, expect Seed Gale transitioning to Static Monsoon. Recommend relocating to the ridge — above fog line and away from tall metal." (Predictive, before building phase)
- **Endgame**: "The signal is agitated. Resonance spike imminent — prepare for Dragon Chill." (Reads Dragon's emotional state)

This means weather is **PREDICTABLE** if you understand the Dragon. Veteran players read the signs before EVE does.

---

## Gameplay Implications

### Tech Tier Vulnerability

| Weather Event | Tech Tier | Bio Tier | Cipher Tier |
|---|---|---|---|
| Static Monsoon | **Devastating** — 3× degradation, arc targeting | Resistant — organic components don't conduct | Thrives — high Resonance powers abilities (but arc-strikes still hurt) |
| Seed Gale | Moderate — filters clog | **Benefits** — some bio upgrades feed on particulate | Neutral — Resonance high but no cipher-specific interaction |
| Atmospheric Liquefaction | Moderate — suit power drain for mobility | Moderate — same movement penalties | **Weakened** — low Resonance means weak cipher |
| Dragon Chill | **Severe** — heating drains power fast | Moderate — some insulation | **Weakened** — Resonance crashed, cipher near-useless |

This forces meaningful equipment choices based on weather forecasting. "I'm heading into Dragon territory in a forest. Seed Gale → Static Monsoon chain is likely. I should swap to bio equipment."

### Creature Behavior Changes

Weather modifies the `CreatureNeeds.Safety` value for all creatures in the affected area:

- **Static Monsoon**: Safety need spikes for most creatures (flee/shelter). Static-adapted creatures: Safety need drops (they thrive).
- **Seed Gale**: Herbivore Hunger need drops to 0 (gorging). Predator Safety need drops (hunting in cover). Prey Safety need spikes.
- **Atmospheric Liquefaction**: Ground creature Safety need spikes. Aerial creature Safety need drops. Movement-dependent creatures (chasers) become ineffective.
- **Dragon Chill**: Universal Safety need spike except cold-adapted creatures.

Some creatures **only appear** during specific weather states:
- Static feeders during Static Monsoon
- Cold-adapted apex predators during Dragon Chill
- Fog swimmers during Atmospheric Liquefaction

This creates weather-gated content without artificial locks.

### Environmental Storytelling

Weather teaches the player about the Dragon before they ever see it:
- "Why does it get cold here?" → later: "Oh. The Dragon passed overhead and was in pain."
- "This area always has seed storms" → "The Dragon nests nearby."
- "The Adapted built their village on a ridge" → "Above the fog line. They've learned."
- "Why is this ruin covered in glass-sand?" → "Static Monsoons. This was near the Dragon's old flight path."

Adapted settlements encode weather knowledge in their architecture:
- Built on high ground (above fog line)
- No metal in construction (static resistance)
- Sealed structures with filter systems (seed protection)
- Heat sources in every shelter (Dragon Chill readiness)

The player who studies Adapted architecture learns weather survival before experiencing the events.

---

## Resonance and Cipher Abilities

```
cipherPower = basePower * resonanceMultiplier(currentResonance)

resonanceMultiplier(r):
    if r < 0.1: return 0.2   // Near-useless
    if r < 0.3: return 0.5   // Weak
    if r < 0.5: return 0.8   // Functional
    if r < 0.7: return 1.0   // Full power
    if r < 0.9: return 1.3   // Amplified
    return 1.6               // Dangerous power (but weather is trying to kill you)
```

### Risk/Reward Loop

High Resonance = stronger cipher BUT more dangerous weather. The player must decide:
- Do you seek out Dragon influence for power at the cost of survival difficulty?
- Do you stay in low-Resonance areas where cipher is weak but weather is calm?
- Do you use cipher abilities during high-Resonance weather, knowing the Dragon **notices** heavy cipher use?

### Cipher Use Draws Attention

Heavy cipher use during high Resonance leaves a "residual Resonance" signature:

```
cipherUseDecay += cipherPowerUsed * 0.1  // Accumulates
cipherUseDecay = max(0, cipherUseDecay - deltaTime * decayRate)

// If cipherUseDecay > threshold, Dragon attention increases
// Dragon may change flight path toward player
// This feeds into: "combat identity — cipher draws Dragon attention"
```

This creates a feedback loop: cipher use in storms → Dragon notices → Dragon approaches → Resonance increases → weather intensifies → cipher gets stronger → more tempting to use → Dragon notices more.

The player must choose when to stop.

### Minimum Resonance Requirements

Some cipher abilities require minimum Resonance to activate:

| Ability Tier | Min Resonance | Context |
|---|---|---|
| Basic (sense, minor manipulation) | 0.1 | Available almost everywhere |
| Intermediate (shielding, communication) | 0.3 | Need some Dragon influence |
| Advanced (major manipulation, area effects) | 0.6 | Only near Dragon activity |
| Transcendent (reality-bending) | 0.8 | Deep in Dragon territory, weather is actively hostile |

---

## Implementation Notes

### Architecture

```
WeatherSystem
├── GlobalVariables (4 floats, updated per-frame)
│   ├── Conductivity
│   ├── Viscosity
│   ├── Particulate
│   └── Resonance
├── AreaBaseValues (per-area config)
│   └── Map<AreaId, {baseConductivity, baseViscosity, baseParticulate, baseResonance}>
├── DragonProximityCalculator
│   └── Uses Dragon's position on world graph → distance to player → Resonance contribution
├── WeatherStateMachine
│   ├── States: Clear, Building, Active, Waning, Aftermath
│   ├── Transitions driven by variable thresholds
│   └── Max 2 simultaneous active events (queue overflow)
├── VisualEffectsDriver
│   └── Reads variables → drives shader params, particle systems, post-processing
├── AudioDriver
│   └── Reads variables → drives ambient layers, spatial audio modifications
└── GameplayModifierApplicator
    ├── Movement speed/jump height modifiers (from Viscosity)
    ├── Equipment degradation rate modifiers (from Conductivity, Particulate)
    ├── Visibility range modifier (from Particulate)
    └── Cipher power multiplier (from Resonance)
```

### Variable Update Pipeline (Per Frame)

1. Calculate Dragon proximity → set Resonance
2. Add area base values to each variable
3. Apply Resonance modifiers to other three variables
4. Apply active event modifiers (events can push variables)
5. Lerp current values toward targets (smooth transitions)
6. Clamp all to 0.0–1.0
7. Check threshold crossings → trigger state machine transitions
8. Push current values to visual/audio/gameplay systems

### Visual Effect Thresholds

| Variable | Threshold | Effect |
|---|---|---|
| Conductivity > 0.3 | Minor sparks on metal objects |
| Conductivity > 0.5 | Visible static on Adam's suit, audio crackle |
| Conductivity > 0.7 | **Static Monsoon trigger** (if Resonance > 0.7) |
| Viscosity > 0.3 | Slight movement slow, air has visible haze |
| Viscosity > 0.5 | Noticeable movement penalty, sound muffling begins |
| Viscosity > 0.7 | Heavy fog territory (if triggered by Resonance drop) |
| Particulate > 0.3 | Pollen haze, reduced draw distance |
| Particulate > 0.5 | Visibility noticeably reduced, filter warnings |
| Particulate > 0.7 | **Seed Gale trigger** (if Resonance > 0.6) |
| Resonance > 0.3 | Cipher abilities at functional power |
| Resonance > 0.5 | Faint ambient hum, subtle visual distortion |
| Resonance > 0.7 | Strong hum, visual shimmer, weather events likely |
| Resonance > 0.9 | Overwhelming sensory input, screen effects intensify |

### Creature AI Integration

Weather variables feed into the existing `CreatureNeeds` system:

```
creature.needs.safety += weatherSafetyModifier(currentWeatherEvent, creature.type)
creature.needs.hunger += weatherHungerModifier(currentWeatherEvent, creature.type)

// Creature type tags: [static-adapted, cold-adapted, fog-swimmer, aerial, ground, burrowing]
// Each tag has per-event modifiers
```

Creatures don't need to "know" about weather events — they respond to their needs, which weather modifies. Emergent behavior from simple inputs.

### Performance Considerations

- Four floats are cheap. Update every frame, no concern.
- Visual effects are the cost — particle systems for Seed Gale and fog are expensive.
  - LOD particle density based on distance from player
  - Fog uses volumetric shader with distance falloff (not full-scene volumetrics)
  - Static Monsoon arcs are instanced with pooled VFX
- Weather transitions use lerp, not sudden switches — no frame spikes from instant state changes
- Creature behavior changes are need-value modifications, not new AI states — no additional pathfinding cost

---

## Adam's Animation Progression (Federation → Bio → Cipher)

This is tied to the weather/atmosphere system because the planet is literally changing how Adam exists in space. His movement evolves as his relationship with the planet deepens.

### Tech Tier (Federation Standard)
- Tight breath animation, rigid upright posture, military bearing
- Short, precise strides — efficient but mechanical
- Arms close to body, minimal sway
- Quick, snappy direction changes (trained reflexes)
- **The "soldier"** — trained, constrained, artificial
- *Implementation*: High damping on SecondOrderDynamics, short animation blend times, restricted rotation ranges

### Bio Tier
- Slouches forward slightly, more predatory stance
- Lower center of gravity, longer strides
- More fluid transitions between states (SecondOrderDynamics with lower damping)
- Arms swing wider, body leans into turns
- Head tracks threats more naturally (less "scanning," more "sensing")
- **The "hunter"** — the planet is teaching him to move like its creatures
- *Implementation*: Reduce SecondOrderDynamics damping by 30%, increase blend times, add subtle procedural lean on direction changes

### Cipher Tier
- Movement becomes almost weightless, floaty quality
- Subtle defiance of physics — slightly too smooth, slightly too fast
- Occasional "glitch" frames where position shifts without visible transition (1–2 pixel micro-teleports)
- Turns happen before the animation catches up (intent precedes body)
- **The "transcendent"** — no longer fully bound by the planet's rules OR the Federation's training
- *Implementation*: Very low SecondOrderDynamics damping, slight movement speed increase (2–5%), occasional frame-skip on rotation animations, subtle position smoothing that "cheats" collision by 1–2 frames

### Progression Rules

- **SUBTLE.** No cutscene shows the change. No UI notification.
- Blend between tiers based on current upgrade loadout percentage (not binary switch)
- If player mixes tiers, animation blends proportionally
- Players notice over time: "Wait, is he moving differently?"
- The planet is literally changing how Adam exists in space

### Weather Interaction with Animation

- During Static Monsoon: Tech-tier Adam flinches/braces. Bio-tier Adam hunches lower. Cipher-tier Adam barely reacts (electricity flows around him).
- During Seed Gale: All tiers raise arm to shield face. Cipher-tier arm raise is slower, more casual.
- During Atmospheric Liquefaction: Tech-tier struggles mechanically. Bio-tier moves like swimming. Cipher-tier moves at near-normal speed (reality-bending).
- During Dragon Chill: Tech-tier shivers rigidly. Bio-tier curls inward (animal instinct). Cipher-tier becomes brittle, uncertain — the one event where cipher loses its grace (Resonance crashed).

---

## Open Questions

1. **Weather duration tuning** — 2–8 minute active phases are a starting point. Needs playtesting. Too short = trivial. Too long = tedious.
2. **Indoor/shelter behavior** — Do weather variables affect interiors? Probably reduced but not zero (sealed Adapted shelters = near-zero, caves = partial, open ruins = full).
3. **Multiplayer implications** — If Genesis ever goes co-op, weather is global. Both players experience it. Cipher use by either player draws Dragon attention.
4. **Dragon Chill frequency** — Tied to story beats? Or can it happen organically? Probably both — scripted for key moments, organic when Dragon is actually distressed.
5. **Seed parasites** — Seeds attaching to Adam: cosmetic progression or mechanical threat? Current design says mechanical (weight, toxicity, creature attraction). Needs prototyping.
6. **Weather-gated areas** — Some areas only accessible during specific weather? e.g., fog reveals underwater passages, static monsoon powers ancient tech. High potential but scope risk.
