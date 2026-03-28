# GENESIS — Master Status Document
*Consolidated from all design docs, conversations, and codebase audit*
*Last updated: 2026-03-28*

---

## HOW TO READ THIS

- ✅ = **Done** — implemented, building, functional
- ⚠️ = **Partial** — exists but incomplete or buggy
- ❌ = **Not built** — design exists, code doesn't
- 🔵 = **Design only** — idea captured, no code attempted

Each section has the current state + what's needed to complete it.

---

## 1. PLAYER MOVEMENT & COMBAT

### Movement (Player.cs — 2400+ lines)
| Move | Status | Notes |
|------|--------|-------|
| Walk/Run | ✅ | Smooth, speed tiers |
| Jump | ✅ | Variable height |
| Double Jump | ✅ | Toggleable via `EnableDoubleJump` |
| Slide | ✅ | S+Space, i-frames, height reduction. Fixed floating bug 3/28. |
| Wall Cling | ❌ | **Not implemented.** No wall detection for player. Design says unlock in Living Forest from observing creatures. |
| Wall Jump | ❌ | Depends on Wall Cling |
| Crouch | ✅ | Height reduction, reduced speed |
| Vault Kick | ✅ | Toggleable |
| Uppercut | ✅ | Toggleable |
| Cartwheel | ✅ | Toggleable, i-frames |
| Flip/Backflip | ✅ | Toggleable |
| Blade Dash | ✅ | Toggleable |
| Dash | ❌ | Design says bio-unlock Area 4-5. Not coded. |
| Ground Pound | ✅ | Functional |
| Rope/Vine Swing | ✅ | Rope objects in levels, physics swing |

### Grapple System (Player.cs + Game1.cs)
| Feature | Status | Notes |
|---------|--------|-------|
| Grapple to terrain | ✅ | Fires hook, attaches, swings |
| Pull small enemies | ✅ | Pulls creature to player |
| Pull player to big enemies | ⚠️ | Code exists but wingbeater grapple was broken (fixed 3/28 — now yanks them down + stun + gravity) |
| Battery cost | ❌ | Grapple has no resource cost yet |
| Range limit | ⚠️ | Has max range but no upgrade path |

### Combat
| Feature | Status | Notes |
|---------|--------|-------|
| Melee combo (3-hit) | ✅ | Knife, hitstop, knockback |
| 16 weapon types | ✅ | WeaponType enum: Knife→Torch, stat tables in WeaponStats.cs |
| Weapon special moves | 🔵 | Design mentions 1-2 specials per weapon (sling air slam, gun radial spray). Not coded. |
| Sidearm/Gun | ✅ | Fires bullets, limited ammo, noise events |
| Shield blocking | ✅ | Directional, durability system |
| Tech Brace | ✅ | Hold to reduce damage, costs battery |
| Weapon durability/breaking | ❌ | Design mentions breakable weapons. Not implemented. |
| Knockback variation by weapon | 🔵 | Design wants big hammer = mutual knockback. All weapons use same KB currently. |
| Critical hit text (SotN style) | 🔵 | Not implemented |
| Damage types | ❌ | No slash/blunt/pierce differentiation |

### Suit & Battery System
| Feature | Status | Notes |
|---------|--------|-------|
| SuitIntegrity field | ✅ | `Player.SuitIntegrity` = 31f starting |
| Battery field | ✅ | `Player.Battery` = 80f starting |
| Battery drain (tech abilities) | ⚠️ | Only Tech Brace drains battery. Grapple/lantern/cipher should too. |
| Suit degradation over time | ❌ | Design says planet's energy field degrades suit. Not coded — SuitIntegrity is static. |
| Armor-movement tradeoff | ❌ | Design says removing suit pieces unlocks moves but reduces defense. Not implemented — all moves available in all tiers. |
| Suit piece slots | ❌ | No equip system for individual armor pieces (helmet, chest, gloves, legs) |
| Tech tier → Bio tier transition | 🔵 | Core identity feature. No code. |

### Ability Unlock Tiers (from design doc)
| Tier | Unlocks | Status |
|------|---------|--------|
| Crash Landing | Walk, run, jump, knife, sidearm, EVE L1 | ⚠️ All moves exist but aren't gated. Player starts with everything enabled depending on `EvolutionStage`. |
| Area 1 Wreckage | Grapple, shield, flashlight, sprint boost | ⚠️ Grapple ✅, Shield ✅, Lantern ✅, Sprint boost ❌ |
| Area 2-3 Forest/Ruins | Wall cling, slide, enhanced lungs, grip pads | ⚠️ Slide ✅, rest ❌ |
| Area 4-5 Bone Reef/Deep | Double jump, dash, gravity shift, resonance pulse | ⚠️ Double jump ✅, rest ❌ |
| Area 6-7 Transformed/Sanctum | Full acrobatics, phase step, dragon sight | ❌ |

---

## 2. EVE (Companion AI)

### Currently Working
| Feature | Status | Notes |
|---------|--------|-------|
| Orbiting companion visual | ✅ | Drawn as orb near player |
| EveAlert text system | ✅ | 64 alert calls in Game1.cs |
| L key EVE dialogue log | ✅ | Scrollable log of past alerts |
| Passive scan (Level 1) | ⚠️ | Auto-scans creatures for bestiary. No visual scan effect. |
| Bestiary (B key) | ✅ | Species entries with sighting data, behavior, biome |
| EVE voice lines | ⚠️ | Text only, no audio. ~30 scripted lines in design doc, ~15 actually triggering in game. |

### Not Built
| Feature | Status | Notes |
|---------|--------|-------|
| Risk/Reward positioning (Safe/Active/Deep modes) | ❌ | Design doc has full spec. EVE is always in one mode currently. |
| EVE damage/offline | ❌ | Can't be hit or knocked offline |
| EVE personality degradation when damaged | ❌ | Design says glitchy/scared when hurt |
| Scan Level 2 (active, close-range detail) | ❌ | |
| Scan Level 3 (deep, lore, risk) | ❌ | |
| Scan result holographic display | ❌ | Design wants Metroid Prime-style popup with anatomy callouts |
| Weather prediction ability | 🔵 | Design: EVE observes clouds from cliffs, gains prediction confidence |
| Chemical/crafting knowledge | 🔵 | Design: EVE knows periodic table, makes crafting fun |
| NPC communication relay | 🔵 | Design: EVE talks to natives for you, relays summaries |
| EVE menu (settings submenu) | 🔵 | Quests, data, locations, dialogue threads, mysteries |
| Kill telegraph reveal (5 kills = show tells) | 🔵 | |

---

## 3. CREATURES & ECOSYSTEM

### Creature Types
| Creature | Status | Role | Notes |
|----------|--------|------|-------|
| Crawler (8 variants) | ✅ | Forager/Skitter/Leaper/Bombardier/Stalker/Spitter/Mimic/Resonant | Full AI, variant roles, procedural legs |
| Wingbeater | ✅ | Apex flyer | Dive-bomb, nesting, hunting, patrol (added 3/28), noise fear (3/28) |
| Bird | ✅ | Prey/flyer | Flee behavior, hunted by wingbeaters |
| Hopper | ✅ | Prey | Jump-based movement, herding |
| Thornback | ✅ | Defensive | Stationary tank, damage on contact |
| Scavenger | ✅ | Scavenger | Small, eats corpses, flees everything |
| Insect Swarm | ✅ | Environmental | Visual swarm, lantern attraction |
| Bristleback (porcupine) | 🔵 | Defensive herbivore | Design: curls into spiky ball, shoots spines. Not coded. |
| Box Jellyfish | 🔵 | Water predator | Design: harmless dome, deadly tentacles. Needs water physics. |
| Burrower | 🔵 | Ambush predator | Design doc creature. Hides underground, surfaces to attack. |
| Grazer/Herbivore | 🔵 | Primary consumer | Distinct from forager crawler — large, passive, herd animal |
| Alien Dog (predator) | 🔵 | Secondary consumer | Pack hunter for first-30 doc. Not same as crawlers. |
| Adapted (humanoid) | 🔵 | NPC faction | Semi-human descendants. Gesture communication. |
| Fireflies | 🔵 | Ambient | Design: swarm code + glow, attract mates |
| Rain Beetles | 🔵 | Weather-triggered | Emerge only when ground is soaked |
| Faulty Robots | 🔵 | Wreckage enemies | Ship drones gone haywire |

### Ecosystem Systems
| System | Status | Notes |
|--------|--------|-------|
| Goal-based AI (hunger/fatigue/safety) | ✅ | CreatureNeeds struct, TickNeeds(), SelectGoal() |
| Creature-to-creature interactions | ✅ | IsThreatTo(), ScanCreatures(), predator-prey collision |
| Food chain / trophic levels | ✅ | EcologicalRole enum, FoodSource.CanEat() matrix |
| FoodSource system | ✅ | Plants, debris, corpses, fertile ground, eating/decay |
| Corpse persistence | ✅ | Corpses stay until eaten, smell radius grows |
| Plant regrowth | ✅ | 1-2 new plants every 15-30s |
| Fertile ground from corpses | ✅ | Depleted corpses → faster plant growth |
| Noise detection + reaction | ✅ | NoiseEvent.cs, floating text, creature response |
| Burrowing | ✅ | Creatures sink into ground when scared/resting (fixed 3/28) |
| Herding/startle propagation | ✅ | Same-species panic spread, hopper herding |
| Predator selectivity (PreySize) | ✅ | WillHunt() per species |
| Wall-climbing (Stalker) | ✅ | GravityDir enum, wall-walk physics reworked 3/28 |
| Weather effects on creatures | ✅ | Rain: +hunger, -detection. Storm: -50% detection. Activity levels by time. |
| Time-of-day behavior | ✅ | Nocturnal/diurnal/crepuscular per species |
| Lantern creature reactions | ✅ | Nocturnal flee, bugs attracted |
| Debug overlay (F9) | ✅ | Species, goals, hunger/safety bars, food labels |
| Fleeing creature no-attack | ✅ | Fleeing creatures skip predator collision (fixed 3/28) |
| Forager flee intelligence | ✅ | Safe distance + calm-down (fixed 3/28) |
| Low-health flee | ✅ | ≤30% HP → flee with speed boost |
| Wingbeater passive by default | ✅ | Only aggros if provoked/nest/very close |
| **Pathfinding (A*)** | ❌ | **Highest priority missing system.** Creatures reverse at walls instead of navigating. |
| **Spatial memory** | ❌ | No remembered locations. Creatures are goldfish. |
| **Height awareness** | ❌ | Creatures don't know if they're on a ledge or in a valley |
| **Level topology map** | ❌ | No connectivity graph of platforms/passages |
| Cross-screen creature movement | ❌ | WantsToLeaveLevel signal exists on wingbeater but transit system not wired |
| Rain burrowing behavior | 🔵 | Design: bugs burrow when raining, some forced out by underground predators |
| Pack hunting AI | ❌ | |
| Swarm threat escalation | 🔵 | Design: 6+ crawlers become Level 3 threat |
| Rain beetle emergence | 🔵 | Design: spawn from mud when it rains |
| Abstract world simulation (Rain World) | 🔵 | WorldGraph.cs exists as stub. No off-screen population sim. |
| Creature learning | 🔵 | Design: burrowers learn player patterns |
| Creature response to armor tier | 🔵 | Design: bio enemies react differently to tech vs bio vs cipher Adam |

---

## 4. WORLD & LEVELS

### Level Infrastructure
| Feature | Status | Notes |
|---------|--------|-------|
| Tile system (30+ types) | ✅ | Dirt, stone, grass, wood, sand, platforms, hazards, liquids, breakables |
| Level editor | ✅ | Tile paint, entity placement, 11 tool types, exit wiring with search |
| Room transitions | ✅ | Fade transitions, smart exit fallback (fixed 3/28) |
| Level neighbors/connectivity | ✅ | JSON-based neighbor data |
| Level save/load | ✅ | JSON serialization |
| Overworld map | ⚠️ | OverworldData.cs + WorldMapData.cs exist. No visual map screen. |

### Actual Levels
| Level | Status | Size | Notes |
|-------|--------|------|-------|
| ship-interior | ✅ | Small | Starting room, basic |
| crashsite | ✅ | ~704px wide | Tiny test room. Design wants 8-10 screens. |
| surface-east | ✅ | 3200×1600px | Ecosystem test level, 18 creatures, multi-height terrain |
| Crash site (full) | ❌ | 8-10 screens | Wreckage, debris, cargo containers, ship hull |
| Living Forest | ❌ | 15-20 screens | Main progression area 1 |
| Native Ruins | ❌ | ? | Gated by wall climb |
| Bone Reef | ❌ | ? | Gated by breathing adaptation |
| Deep Ruins | ❌ | ? | Descent from Native Ruins |
| Transformed Lands | ❌ | ? | Late game |
| Dragon Sanctum | ❌ | ? | Final area |

### Map Connectivity (Dark Souls 1 model)
| Connection | Gate | Status |
|-----------|------|--------|
| Wreckage → Living Forest | Open | ❌ Not built |
| Wreckage → Native Ruins | Wall climb | ❌ |
| Wreckage → Bone Reef | Breathing adaptation OR hazmat helmet | ❌ |
| Living Forest → Native Ruins | Deep scan tunnel | ❌ |
| Native Ruins → Deep Ruins | One-way drop, gravity cipher to return | ❌ |
| Bone Reef → Transformed | Tidal knowledge | ❌ |
| Deep Ruins → Transformed | Resonance pulse cipher | ❌ |
| Transformed → Dragon Sanctum | Understanding test | ❌ |
| Shortcuts (elevator, underwater tunnel, rope bridges) | Various | ❌ |

---

## 5. WEATHER & ATMOSPHERE

| Feature | Status | Notes |
|---------|--------|-------|
| WeatherSystem.cs | ✅ | Moisture/temp/wind/storm atmospheric sim |
| Rain particles | ✅ | Directional particles |
| Wind streaks | ✅ | Visual effect |
| Day-night cycle | ✅ | 24-min real-time cycle, 0-24h clock |
| Ambient light changes | ⚠️ | Time-based tinting exists but subtle |
| Weather affects creatures | ✅ | Rain: +hunger, -detection. Storm: halved detection. |
| Fog | ❌ | |
| Weather affects fire | ❌ | Rain should extinguish torches |
| Screen rain droplets | 🔵 | Design: realistic water drops on screen |
| Mud/puddle spawning from rain | 🔵 | |
| EVE weather prediction | 🔵 | Cliff observation, confidence level |
| Storm intensification over time | ✅ | WeatherSystem handles escalation |

---

## 6. VISUAL & GRAPHICS

| Feature | Status | Notes |
|---------|--------|-------|
| Fallback rectangles (all entities) | ✅ | **No sprite sheet.** Everything is colored rectangles + procedural. |
| Procedural legs (crawlers) | ✅ | Animated leg segments |
| Death particles | ✅ | Color-coded per species |
| Screen shake | ✅ | Hit reactions, environmental |
| Hit stop | ✅ | Frame freeze on melee contact |
| Fade transitions | ✅ | Level transitions |
| Squash & stretch | ✅ | On creatures for juice |
| Camera system | ✅ | SecondOrderDynamics spring, zoom |
| Pixel-perfect parallax | ❌ | From Silksong research. Not implemented. |
| Particles everywhere (dust, spores) | ❌ | |
| IGN debanding shader | ❌ | One line of shader code. Not done. |
| Subtle bloom | ❌ | |
| Depth of field | ❌ | |
| Vignette (player-centered) | ❌ | |
| LUT color grading per biome | ❌ | Full spec in MEMORY.md |
| Sprite sheet / actual art | ❌ | **Major gap.** Everything is programmer art. |
| HUD glitch effect | ❌ | |
| Scan holographic display | ❌ | |
| Death animation (backflip, slow-mo, chord) | 🔵 | Design: fly in air, darken, beautiful chord |
| Item pickup animation | 🔵 | |
| Critical hit text popup | 🔵 | |

---

## 7. AUDIO

| Feature | Status | Notes |
|---------|--------|-------|
| **Everything** | ❌ | **Zero audio in the entire game.** No music, no SFX, no ambient. |
| Floating text noise (SWSH/BANG) | ✅ | Visual-only noise representation |
| Music system (ambient reactive) | 🔵 | Design: weather-reactive, danger-level, dragon proximity |
| Death chord progression | 🔵 | YouTube reference captured |
| Dragon sound (resonant call) | 🔵 | Design: low harmonic, screen vibrate |

---

## 8. STORY & NARRATIVE

### Prologue
| Feature | Status | Notes |
|---------|--------|-------|
| 4-phase prologue sequence | ✅ | Ship, Override, Descent, Eye |
| Skip option (hold ESC) | ✅ | |
| Title card "GENESIS" | ✅ | |
| Audio | ❌ | Silent prologue |
| Art | ⚠️ | Placeholder |

### First 30 Minutes (fully designed, mostly not built)
| Beat | Status | Notes |
|------|--------|-------|
| 0:00 Black screen audio intro | ❌ | Scripted, no audio system |
| 0:45 Title card | ✅ | |
| 1:00 Wake up, injured walk | ❌ | No injured animation, no tight camera |
| 1:30 EVE reboots | ⚠️ | EVE exists but no reboot sequence |
| 2:00 Crash site exploration | ❌ | Level not designed (8-10 screens) |
| 3:30 First scavenger encounter | ⚠️ | Scavenger exists but not placed in scripted encounter |
| 4:30 Suit sparks / integrity drops | ❌ | SuitIntegrity exists but doesn't auto-degrade |
| 5:00 Toxic spore hazard | ❌ | |
| 6:00 Ship cockpit / mission log | ❌ | No readable terminals |
| 8:00 First hostile combat | ⚠️ | Combat works, scripted encounter not designed |
| 9:30 Edge of wreckage / forest vista | ❌ | |
| 10:00 Forest entry | ❌ | Forest level not built |
| 11:00 Observation lesson (creature eating) | ❌ | |
| 12:00 Environmental puzzle (split path) | ❌ | |
| 13:30 Weather shift | ⚠️ | Weather exists but not scripted |
| 15:00 First Adapted sign (cairn) | ❌ | |
| 16:30 Predator pack encounter | ❌ | |
| 18:00 Grapple found | ⚠️ | Grapple works but no scripted discovery |
| 20:00 Grapple teaches exploration | ❌ | |
| 22:00 THE SCREAM (Dragon) | ❌ | |
| 24:00 First Adapted encounter | ❌ | |
| 26:00 Native Ruins visible (gated) | ❌ | |
| 27:00 First shelter/save point | ❌ | No save point system |
| 28:00 EVE's signal revelation | ❌ | |
| 30:00 Forest opens / player choice | ❌ | |

### Story Beats & Quests
| Feature | Status | Notes |
|---------|--------|-------|
| 25 story beats | 🔵 | Draft in story-beats.md, needs gut-check |
| Quest system | ❌ | No quest tracking, no objectives |
| Scannable objects in levels | ❌ | |
| Invisible interactive tiles (glint) | 🔵 | |
| Behavior tracking (ScavengerKilled, etc.) | ❌ | |
| Adapted encounter logic | ❌ | |
| Dragon encounters (5 planned) | ❌ | |

---

## 9. SAVE/DEATH/PROGRESSION

| Feature | Status | Notes |
|---------|--------|-------|
| SaveData persistence | ✅ | JSON save with inventory, position, flags |
| Shelter save points | ❌ | Design: natural alcoves, rest spots. Not implemented. |
| Death respawn at shelter | ❌ | Currently no death respawn system |
| Death animation | ❌ | Design: fly up, slow-mo, darken, chord |
| Scavengers picking over death location | 🔵 | Design: failure is visible in world |
| Sleep mechanic (time advance) | 🔵 | Design: sleep at shelter → weather/patrol changes |
| Crafting system | ❌ | Design mentions EVE + chemistry. No code. |
| Item/equipment management | ⚠️ | Weapons exist, armor slots exist, no full inventory screen |

---

## 10. UI/HUD

| Feature | Status | Notes |
|---------|--------|-------|
| HP display | ✅ | |
| Weapon indicator | ✅ | |
| Lantern/Cipher/Torch HUD | ✅ | Yellow/purple/orange indicators |
| EVE dialogue log (L key) | ✅ | Scrollable |
| Bestiary (B key) | ✅ | Species entries |
| Suit integrity bar | ❌ | Field exists, no HUD display |
| Battery bar | ❌ | Field exists, no HUD display |
| Pause menu with scan log | ❌ | |
| Map screen | ❌ | |
| Quest/objective tracker | ❌ | |
| EVE full menu (settings submenu) | 🔵 | |

---

## PRIORITY TIERS

### Tier 0 — Foundation (blocks everything else)
1. **Level design tool improvements** — need efficient way to build 8-10 screen crash site
2. **Ability gating system** — moves must be locked/unlocked by story progression, not EvolutionStage
3. **Suit degradation + battery drain** — the core identity mechanic
4. **Save/shelter system** — can't test progression without saves

### Tier 1 — Playable First 30 Minutes
5. **Crash site level (8-10 screens)** — the actual content
6. **EVE reboot sequence + scripted dialogue triggers**
7. **First scripted encounters** (scavenger discovery, first hostile)
8. **Grapple discovery moment** (find in debris, EVE comments)
9. **Forest edge transition** (vista, atmosphere change)
10. **Suit sparks / integrity auto-degradation**

### Tier 2 — Creature Intelligence
11. **A* pathfinding on tile grid** — creatures navigate terrain
12. **Spatial memory** — remember food/threats/safe spots
13. **Height awareness** — don't walk off cliffs
14. **Cross-screen creature transit**

### Tier 3 — World Systems
15. **Rain behavior** (bugs burrow, mud spawns creatures, fire extinguished)
16. **Crafting foundation** (EVE chemistry, basic recipes)
17. **Scan result display** (Metroid Prime style)
18. **EVE positioning modes** (Safe/Active/Deep)

### Tier 4 — Content
19. **Living Forest level (15-20 screens)**
20. **Wall cling unlock (bio observation)**
21. **Alien dog predator type**
22. **Adapted encounters (gesture communication)**
23. **The Scream (Dragon event)**

### Tier 5 — Polish
24. **Audio** (even placeholder SFX)
25. **Sprite art** (replace rectangles)
26. **Shader effects** (bloom, vignette, IGN, LUT)
27. **Parallax backgrounds**
28. **Death animation + respawn**
29. **HUD polish** (suit bar, battery bar, glitch effects)

---

## LOOSE IDEAS (Captured, Not Prioritized)
- Plant Wave alternative for music (can't use commercially)
- Dragon Mistress mecha dragon theme
- Genesis album by Talisman for inspiration
- Tree music concept
- Story where player sacrifices themselves?
- Alternative timeline setting (spaceships built sooner)
- Eve translates native language eventually
- Eve can talk to natives for you (relay summaries)
- Getting soaked → cold → craft wax jacket (real-world chemistry)
- Sling charged attack straight down in air (mini-pause)
- Gun radial spray in air (high ammo cost)
- Swarm: EVE tells you to walk slowly through
- Fireflies: swarm code + glow + mate attraction
- Bottom-up ecosystem control (weather → plants → herbivores → predators)
- Fixing game problems video: https://www.youtube.com/watch?v=rJZyPdYIbZI
- Improving jumps video: https://www.youtube.com/shorts/rjheFMXtISo
- Video game writing maturity: YouTube reference
- Movement like Zero from Mega Man X
- In-game design docs (so game and docs don't diverge)
- Comment from video: plan every quest/story beat before implementation
- SotN vs Bloodstained quest design (collection fun vs chore grinding)
- Babel Hive isn't an enemy — it's a plant (scan level 4 reveals)

---

## DESIGN DOCS INDEX
| File | Content |
|------|---------|
| `genesis-core-design.md` | Core game identity, themes, pillars |
| `first-30-minutes.md` | Minute-by-minute opening sequence |
| `story-beats.md` | 25 planned story beats |
| `world-map.md` | Area descriptions, connectivity |
| `progression-map.md` | Ability unlock order |
| `weapons-design.md` | Weapon types, stats, specials |
| `tools-and-equipment.md` | Non-weapon gear |
| `eve-and-quests.md` | EVE mechanics, quest system |
| `creature-ideas.md` | New creature concepts |
| `weather-system.md` | Weather mechanics |
| `weather-atmosphere-system.md` | Atmosphere design |
| `design-philosophy.md` | Design principles |
| `reference-architecture.md` | Code architecture notes |
| `procedural-animation-plan.md` | Animation system plan |
| `language-barrier-communication.md` | Adapted communication |
| `master-task-list-0-30.md` | Old task list (partially outdated) |
| `video-research-*.md` | Research from YouTube videos |

---
*This is the single source of truth. Update this file, not scattered lists.*
