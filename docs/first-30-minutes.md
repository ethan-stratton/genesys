# First 30 Minutes — Level Flow Design

## Setting

Adam crash-lands on an alien planet. His ship is destroyed. EVE (AI companion orb) boots up from the wreckage. Together they need to find shelter, survive, and figure out where they are.

The player knows nothing. The world teaches through environment + EVE's commentary.

---

## Pacing Map

```
[Prologue]  →  [Crash Site]  →  [Ship Interior]  →  [Ravine]  →  [Cave Mouth]
   1 min          5 min            4 min              6 min          3 min

→  [Underground Stream]  →  [Fungal Cavern]  →  [The Shelter]  →  [Deep Ruins]
        5 min                   4 min               2 min            ~open
```

**Total critical path: ~30 minutes**
**With exploration: ~45 minutes**

---

## Room-by-Room Breakdown

### 0. Prologue (existing) — 1 min
*Already implemented. Ship alarm, override, descent, crash.*

Transitions directly into the crash site. If skipped, player starts at crash site with EVE already active (existing `_prologueSkipped` flag).

---

### 1. CRASH SITE (existing room: `crashsite`) — 5 min

**Size:** ~40×15 tiles (wide, not tall)  
**Vibe:** Outdoor. Alien sky. Wreckage scattered. Small fires burning. Sparking wires.

**Layout:**
```
[sky / alien canopy]
                    ███ broken hull fragment ███
  ██fire██   ═══platform═══   ██electric██   ██pipe██
  ██████████████████████████████████████████████████████
        ↓ ship hatch                    ↑ rubble wall
        (exit-down)                (blocked — need dash)
  ████████████████████████████████████████████████████
```

**First 30 seconds — EVE boot-up:**
- Adam lies on ground (brief stun animation, or just start standing)
- EVE spark particles play (existing system)
- EVE: *"Systems online. Assessing damage... Adam, can you move? Try walking."*
- **Tutorial prompt: WASD/arrow keys to move**
- Player moves left/right on flat ground near wreckage

**Next 60 seconds — movement tutorial:**
- EVE: *"Hull fragment ahead. Jump over it."*
- **Tutorial: Space to jump**
- Small platforming over wreckage debris (2-3 simple jumps)
- EVE scans debris: *"Ship's reactor is offline. Navigation destroyed. We need to find shelter before nightfall."*

**Next 60 seconds — first hazards (passive):**
- Fire tiles block a direct path → player routes around them
- EVE: *"Careful. Fuel leak. Don't touch the flames."*
- Sparking wires (ElectricShock tiles, intermittent) → teaches timing
- BrokenPipe leaking → visual flavor, not dangerous yet

**Combat tutorial — minute 3-4:**
- A single **Forager Crawler** appears near the wreckage (attracted to sparks)
- EVE: *"Movement detected. Hostile fauna. Use your fists — it's all we have."*
- **Tutorial: J to attack**
- Forager is weak (2 hits). Non-threatening. Teaches attack timing.
- A second Forager appears after the first dies.

**Exploration reward:**
- Left side (optional): broken hull section with a **Heart pickup** behind a small jump puzzle
- EVE: *"Medical supply. Take it."*

**Exit down — Ship hatch:**
- Hatch on the ground (exit-down to ship-interior)
- EVE: *"The cargo bay hatch. Let's check if anything survived the crash."*

**Exit right — Rubble wall (BLOCKED):**
- Solid wall of debris. Player can see past it (background parallax shows ravine beyond).
- EVE scan: *"Structural weakness detected. We'd need more force to break through."*
- **Requires: Dash ability (unlocked in ship interior)**
- Player returns here after getting dash.

**Enemies:** 2 Forager Crawlers  
**Items:** 1 Heart pickup (optional)  
**Tiles used:** Fire, ElectricShock, BrokenPipe, platform, dirt, slope  
**New content needed:** EVE dialogue lines, tutorial prompt system, rubble wall (BreakableGlass or new DashBreakable)

---

### 2. SHIP INTERIOR (existing room: `ship-interior`) — 4 min

**Size:** ~25×20 tiles (vertical, multi-floor)  
**Vibe:** Dark. Emergency lighting (red tint). Tilted/damaged geometry. Claustrophobic.

**Layout:**
```
  [hatch to crash site — exit-up]
  ████████████████████████
  ██   corridor         ██
  ██   ══platform══     ██
  ██        ↓ drop      ██
  ██   ══════════════   ██
  ██   cargo bay        ██
  ██   [EVE terminal]   ██
  ██   ══════════════   ██
  ██        ↓ drop      ██
  ██   engine room      ██
  ██   [DASH MODULE]    ██
  ██   puddle  electric ██
  ████████████████████████
```

**Corridor (top):**
- Short hallway. Broken pipes dripping (Puddle tiles on floor).
- EVE: *"Artificial gravity holding. Barely."*
- Player drops down through broken floor grating.

**Cargo Bay (middle):**
- Larger room. Crates (solid tiles as platforms). Some broken open (visual).
- **NPC: Damaged terminal** (not a person — a ship computer)
  - Interact: terminal displays corrupted text, EVE translates
  - EVE: *"Passenger manifest... one occupant. You. Destination... corrupted. But there's a nav module backup in engineering."*
  - This plants the long-term goal: figure out where you were going and why

**Engine Room (bottom):**
- Flooded with puddles (movement slow). Sparking wires (ElectricShock).
- **Puzzle:** Player must time movement through electrified puddle section
  - Electricity pulses on/off with RetractExtended timer
  - When off → puddle just slows you. When on → puddle + electric = damage
- **ABILITY UNLOCK: Dash Module**
  - Item pickup in the back of engine room
  - EVE: *"Kinetic accelerator. Ship-grade. Integrating with your suit."*
  - **Unlocks: Dash (Shift/double-tap direction)**
  - Tutorial prompt appears
  - Player can now dash through the puddle section quickly (skip the timing puzzle on return)

**Exit back up:**
- Player climbs back up through ship (platforms, no enemies)
- EVE: *"That rubble outside — try your new dash."*

**Enemies:** None (ship is empty — tension from environment only)  
**Items:** Dash Module (ability unlock), 1 Heart pickup in cargo bay corner  
**Tiles used:** Puddle, ElectricShock, BrokenPipe, BreakableGlass, platforms  
**New content needed:** Terminal NPC dialogue, Dash Module item/pickup system, EVE contextual lines

---

### 3. CRASH SITE REVISITED — 1 min

Player returns to crash site with Dash. Goes right.
- **Dash through rubble wall** → BreakableGlass/DashBreakable shatters
- EVE: *"That's one way through."*
- Opens path to the ravine.

**Backtracking reward (optional):** With dash, player can reach a previously inaccessible ledge on the left side of crash site → **Melee weapon pickup: Pipe Wrench** (or whatever the first real weapon is)
- EVE: *"Improvised, but effective."*

---

### 4. THE RAVINE (`ravine`) — 6 min

**Size:** ~20×40 tiles (TALL, vertical descent)  
**Vibe:** Outdoor but narrow. Rocky walls. Alien vegetation. First glimpse of the planet's ecosystem.

**Layout:**
```
  [exit from crash site — left]
  ██         alien sky         ██
  ██  ══platform══             ██
  ██         ══rope══          ██
  ██    crawler     ══plat══   ██
  ██  ══════════════           ██
  ██         ↓waterfall        ██
  ██    ══plat══   crawler     ██
  ██  rope         ══════════  ██
  ██              hopper       ██
  ██  ══════════════════════   ██
  ██  ↓ cave entrance          ██
  ████████████████████████████████
```

**This room does the heavy lifting for teaching:**

**Vertical traversal:**
- Ropes! First rope encounter.
- EVE: *"Organic fiber. Strong enough to climb."*
- **Tutorial: Up/Down to climb ropes, jump to dismount**
- Mix of rope → platform → rope descent

**Real combat:**
- 3-4 Forager Crawlers on platforms (easy, but more than before)
- 1 **Skitter Crawler** (faster, dodges — teaches player to time attacks)
- 1 **Hopper** near the bottom (new enemy type, jumps unpredictably)
- This is where the player starts to feel competent at combat

**Environmental storytelling:**
- Alien plants growing from walls (decorative tiles — new?)
- Strange markings on rock (EVE scans: *"Tool marks. Something intelligent made these."*)
- Hints that this planet isn't uninhabited

**Waterfall section (middle):**
- Water tiles flowing down one side (visual, maybe light current push)
- Creates puddles at the bottom
- Teaches: water is a feature of this world, not just a hazard

**Optional side cave (halfway down):**
- Small alcove behind a breakable wall
- Contains: **Ranged weapon pickup** (energy shard? thrown rock? something basic)
- EVE: *"Low-yield energy cell. Repurposed as a projectile emitter. Crude, but ranged."*
- **Unlocks ranged attack: K to fire**

**Exit down → Cave Mouth:**
- Bottom of ravine opens into darkness
- EVE: *"No sunlight ahead. Switching to thermal scan."*
- EVE orb glows brighter (visual change for dark areas)

**Enemies:** 3-4 Forager, 1 Skitter, 1 Hopper  
**Items:** Ranged weapon (optional side cave), Heart pickup  
**Tiles used:** Rope, platform, water, breakable, slopes  
**New content needed:** Waterfall visual, alien plant decorative tiles, EVE scan dialogue

---

### 5. CAVE MOUTH (`cave-mouth`) — 3 min

**Size:** ~30×12 tiles (wide, low ceiling)  
**Vibe:** Transition zone. Twilight. Half outdoor, half underground. Bioluminescent moss on walls.

**Layout:**
```
  [exit from ravine — up-right]
  ██████████████         open sky ██
  ██  biolum moss  ══plat══      ██
  ██     ══════platform══════    ██
  ██  crawler   crawler   ██████████
  ██  ════════════════  ↓puddle  ██
  ██  SHELTER (!)       stream→  ██
  ████████████████████████████████████
```

**Breathing room:**
- After the intense ravine descent, this room is calmer
- Wide platforms, fewer enemies (2 Foragers, passive until provoked)
- Bioluminescent moss on ceiling/walls (new decorative tile — blue-green glow)

**SHELTER — First save point!**
- Alien structure that EVE identifies as useful
- EVE: *"Geothermal vent. Warm. Defensible. A good place to rest."*
- **Tutorial: Walk into shelter to rest and save**
- Full heal + save point
- This is the player's "bonfire" — if they die after this, they respawn here

**EVE story beat:**
- While resting, EVE processes data from the ship terminal
- EVE: *"I've been analyzing the nav data fragments. Adam... our ship didn't crash by accident. The descent was controlled. Someone programmed it."*
- Player can ask follow-up (dialogue choices?) or just continue
- Plants the mystery: who sent you here, and why?

**Exit right → Underground Stream:**
- Stream of water flowing right (visual + audio cue)
- EVE: *"Water source. Following it should lead deeper underground."*

**Enemies:** 2 Forager Crawlers (passive)  
**Items:** Shelter (save/heal), 1 Heart pickup  
**Tiles used:** Bioluminescent decorative, shelter, puddle, water  
**New content needed:** Shelter tutorial, EVE story dialogue, bioluminescent tile

---

### 6. UNDERGROUND STREAM (`underground-stream`) — 5 min

**Size:** ~45×15 tiles (long horizontal, water throughout)  
**Vibe:** Dark. Water everywhere. First truly alien environment. Strange sounds.

**Layout:**
```
  ██████████████████████████████████████████████████
  ██  [from cave]   ════plat════   acid pool    ██
  ██  ~~water~~ ██  ██  ~~water~~  ████  ██     ██
  ██  ~~water~~ ██      ~~water~~     breakable ██
  ██  ══plat══  ██  ══rope══  ██  ══plat══      ██
  ██  ~~water~~   ~~water~~water~~  ↓exit       ██
  ██████████████████████████████████████████████████
```

**Water traversal puzzle:**
- Large sections of water (slows movement significantly with new transparent rendering)
- Player must hop between platforms, use ropes, and dash across water gaps
- Teaches: dash over water = fast transit (momentum carries through puddles if dashing)

**Acid introduction:**
- One pool of acid (green, visually distinct from water)
- EVE: *"Corrosive compound. Avoid contact."*
- Acid has breakable wall next to it → acid dissolves it slowly (Phase 1 physics!)
  - If physics not implemented yet: just place the path as open, add acid as flavor

**New enemy — Bombardier Crawler:**
- Shoots projectile from distance
- Positioned on far platform across water gap
- Forces player to close distance (dash across water) or use ranged weapon
- 1-2 of these mixed with Foragers

**Environmental puzzle:**
- Path forward is blocked by breakable wall
- Acid pool is adjacent → acid eats through it over time (or player breaks it with melee)
- Optional faster path: hit breakable glass from above (drop attack)

**Optional area:**
- Underwater alcove (swim down? or wade through deep water at slow speed)
- Contains: **Upgrade chip** — first upgrade item
  - EVE: *"Processing enhancement. I can integrate this. Your suit's response time just improved."*
  - Effect: faster attack speed, or dash cooldown reduction, or similar small buff

**Exit down → Fungal Cavern**

**Enemies:** 2 Forager, 2 Bombardier  
**Items:** Upgrade chip (optional), Heart pickup  
**Tiles used:** Water (lots), Acid, Breakable, platforms, ropes  
**New content needed:** Bombardier enemy behavior (exists as variant), upgrade system hookup

---

### 7. FUNGAL CAVERN (`fungal-cavern`) — 4 min

**Size:** ~30×25 tiles (open, organic shapes)  
**Vibe:** Alien. Bioluminescent mushrooms. Spore particles in air. Strange beauty. This is the "wow" room.

**Layout:**
```
  ████████████████████████████████████
  ██  [from stream]                 ██
  ██       🍄  ══mushroom plat══   ██
  ██   🍄      🍄                  ██
  ██  ══plat══     leaper!         ██
  ██       🍄  ══════plat══════    ██
  ██   hopper   hopper             ██
  ██  ══════════════════════       ██
  ██           [NPC?]    → exit    ██
  ████████████████████████████████████
```

**Visual showcase:**
- This room should look GOOD. Giant mushroom platforms. Spore particles drifting.
- Color palette shifts — warm oranges/purples instead of the cool blues of water caves
- If we have the particle system: floating spores, pollen, dust (from MEMORY.md Silksong notes)

**Hardest combat yet:**
- 2 **Hoppers** on the main floor (unpredictable jumping)
- 1 **Leaper Crawler** on upper platform (fast, aggressive, drops down on you)
- This is the first time the player might die if they're not careful
- Fair but intense — shelter is only one room back

**Mushroom platforming:**
- Bouncy mushroom tops? (new tile type: Bouncy — launches player upward on contact)
- Or just use them as organic-shaped platforms with slopes

**NPC encounter (optional but powerful):**
- Strange alien creature — not hostile. Sitting among mushrooms.
- Can't speak (no shared language) but gestures
- EVE translates body language: *"It's... offering something. A gift? It seems to recognize technology."*
- Gives player: **Wall Jump Module**
  - EVE: *"Magnetic grip enhancement. You can push off walls now."*
  - **Unlocks: Wall Jump**
  - Tutorial prompt

**OR** — wall jump was already in MoveTier 0. In that case, the NPC gives a different upgrade:
- **Grapple Module** (if that's a thing)
- **Health upgrade** (+25 max HP)
- **EVE upgrade** (longer scan range, or new scan ability)

**Exit right → The Shelter (deep)**

**Enemies:** 2 Hopper, 1 Leaper  
**Items:** Ability/upgrade from NPC, Heart pickup  
**Tiles used:** New mushroom decorative/bouncy, bioluminescent, platforms, slopes  
**New content needed:** NPC interaction, spore particles, mushroom tiles, color palette

---

### 8. THE DEEP SHELTER (`deep-shelter`) — 2 min

**Size:** ~15×10 tiles (small, intimate)  
**Vibe:** Safe haven. Warm. Ancient but welcoming. Clearly built by someone.

**Layout:**
```
  ████████████████████████
  ██  [from cavern]     ██
  ██  ══════════════    ██
  ██    SHELTER         ██
  ██    [ancient mural] ██
  ██  ══════════════    ██
  ██         → exit     ██
  ████████████████████████
```

**Second shelter — checkpoint before the unknown:**
- Full heal + save
- Small room, clearly artificial (straight walls, carved stone)
- First sign of advanced civilization on this planet

**Story climax of the first 30 minutes:**
- Ancient mural on the wall (NPC-like interaction — player examines it)
- EVE scans it: *"These markings... Adam, this isn't alien. This is human. Old human. Thousands of years old."*
- Beat.
- EVE: *"We weren't sent to an alien planet. We were sent home."*
- **TITLE CARD: GENESIS** (appears for the first time — the title screen title was maybe just "???")
- This reframes everything. The "alien" planet is Earth. Far future. Humanity fell. You're a return mission.

**Exit right → Deep Ruins (open-ended, post-30-min content)**

**Enemies:** None  
**Items:** Shelter  
**Tiles used:** Carved stone (new decorative?), existing  
**New content needed:** Mural interaction, EVE revelation dialogue, title card trigger

---

### 9. DEEP RUINS (`deep-ruins`) — Open-ended

This is where the 30-minute vertical slice ENDS and the full game begins.

- Ancient human ruins. Overgrown. Dangerous.
- New enemy types. Bigger rooms. Harder platforming.
- The player now has: basic melee, ranged, dash, wall jump (or equivalent)
- They have a goal: explore the ruins, find out what happened to humanity
- They have a mystery: who programmed the ship to come here?

This room can be a placeholder for now — just needs to exist as a "to be continued" boundary.

---

## Ability Unlock Sequence

| When | What | Where | Opens |
|---|---|---|---|
| Start | Basic melee (fists) | Crash site | Combat |
| ~8 min | **Dash** | Ship interior (engine room) | Rubble walls, water-dash, dash attacks |
| ~15 min | **Ranged attack** (optional) | Ravine side cave | Distant enemies, switches(?) |
| ~25 min | **Wall jump** or health upgrade | Fungal cavern NPC | Vertical exploration, shortcuts |

This gives one new ability every ~8 minutes. Each one changes how you move through the world and lets you find new things in old rooms.

---

## Enemy Ramp

| Room | Enemies | Difficulty |
|---|---|---|
| Crash site | 2 Forager | Tutorial (can't really die) |
| Ship interior | 0 | Environmental tension only |
| Ravine | 4 Forager + 1 Skitter + 1 Hopper | First real combat |
| Cave mouth | 2 Forager (passive) | Breather |
| Underground stream | 2 Forager + 2 Bombardier | Ranged pressure |
| Fungal cavern | 2 Hopper + 1 Leaper | Peak difficulty |
| Deep shelter | 0 | Story moment |

**Total enemies in critical path: ~15**  
**First death likely in: Fungal Cavern (room 7)**

---

## Health Economy

- Start: 100 HP
- Forager deals: 10 damage per hit
- Skitter deals: 15 damage
- Hopper deals: 20 damage  
- Leaper deals: 25 damage
- Bombardier projectile: 15 damage
- Fire/Electric/Acid: 10 damage per tick (0.5s interval)
- Puddle: 0 damage (slow only)

- Heart pickup: heals 25 HP
- Shelter: full heal

- Hearts placed: ~6 across all rooms (150 total healing available)
- Shelters: 2 (cave mouth + deep shelter)

Player should arrive at fungal cavern with 60-80 HP if they've been taking some hits and collecting hearts. The shelter before it guarantees a full-health attempt.

---

## Room Connections (Exit Wiring)

```
crashsite
  exit-down    → ship-interior / exit-to-crashsite
  exit-right   → ravine / exit-from-crashsite       [requires dash]

ship-interior
  exit-up      → crashsite / exit-down

ravine
  exit-left    → crashsite / exit-right
  exit-down    → cave-mouth / exit-from-ravine
  exit-side    → ravine-alcove / exit-to-ravine      [optional, breakable wall]

ravine-alcove  [optional room]
  exit-back    → ravine / exit-side

cave-mouth
  exit-up      → ravine / exit-down
  exit-right   → underground-stream / exit-from-cave

underground-stream
  exit-left    → cave-mouth / exit-right
  exit-down    → fungal-cavern / exit-from-stream

fungal-cavern
  exit-up      → underground-stream / exit-down
  exit-right   → deep-shelter / exit-from-cavern

deep-shelter
  exit-left    → fungal-cavern / exit-right
  exit-right   → deep-ruins / exit-from-shelter
```

**Total rooms: 9 (7 critical path + 1 optional alcove + 1 end placeholder)**

---

## New Systems Needed

### Must-have for playable 30 min:
1. **Tutorial prompt system** — contextual "Press X to Y" text that appears once and fades
2. **Ability unlock pickups** — item that grants ability + sets SaveData flag
3. **Dash-breakable tiles** — solid tile destroyed by dashing into it (or reuse BreakableGlass)
4. **EVE contextual dialogue triggers** — EVE speaks when entering a zone / scanning an object (tie to exit zones or invisible trigger rects)
5. **Title card trigger** — mid-game title reveal (screen overlay, dramatic)

### Nice-to-have:
6. **Bouncy mushroom tile** — launches player upward on land
7. **Bioluminescent decorative tiles** — glow effect
8. **Spore/dust particles** — ambient floating particles per room
9. **Dark rooms + EVE glow** — EVE lights nearby area in dark rooms
10. **NPC gesture/trade interaction** — non-verbal NPC gives item

### Already exists (just needs content):
- Dialogue system (needs written lines)
- NPC placement (editor tool exists)
- Enemy placement (editor tool exists)
- Exit wiring (editor + search overlay)
- Save/load (shelter system works)
- Item pickups (system exists)

---

## Production Estimate

| Task | Hours | Priority |
|---|---|---|
| Build 8 rooms in editor (layout only) | 6-8 | P0 |
| Write EVE dialogue (all rooms) | 3-4 | P0 |
| Tutorial prompt system | 2 | P0 |
| Ability unlock pickups | 2 | P0 |
| EVE trigger zones | 2 | P0 |
| Place enemies + balance | 3 | P0 |
| Place items + hearts | 1 | P0 |
| Dash-breakable gate | 1 | P0 |
| Title card reveal | 1 | P1 |
| Bouncy mushroom tile | 1 | P1 |
| Bioluminescent tiles | 1 | P1 |
| Ambient particles | 2 | P1 |
| Dark room + EVE light | 2 | P1 |
| NPC interaction (fungal) | 2 | P1 |
| Sound effects | 4-6 | P2 |
| Music | 4-6 | P2 |

**P0 total: ~20 hours** (playable but bare)  
**P0 + P1: ~29 hours** (feels good)  
**Full with audio: ~40 hours**

---

## The One-Sentence Pitch

*You wake up in the wreckage of your ship on what you think is an alien world, and 30 minutes later you discover it's Earth — and you were sent here on purpose.*
