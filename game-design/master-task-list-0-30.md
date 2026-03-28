# GENESIS — Master Task List: First 30 Minutes
# Last updated: 2026-03-28

*Every task needed to make minutes 0-30 playable, fun, and shippable.*
*Tasks are in dependency order. Build from top to bottom.*
*[EXISTING] = already built in current prototype. [MODIFY] = needs changes. [NEW] = from scratch.*

**Legend:** [x] = done, [~] = partial/placeholder, [ ] = not started

---

## PHASE 0: PROJECT RESTRUCTURE
*Before building anything new, restructure the prototype into Genesis.*

### 0.1 — Project Identity
- [ ] 001. Rename project from "arena-shooter" to "genesis" (solution, namespaces, folder)
- [x] 002. Create `GameState.Prologue` state (cutscene playback)
- [x] 003. Create `GameState.Gameplay` state (replaces current gameplay, keeps editor)
- [x] 004. Create `GameState.TitleCard` state (black screen + title text + fade)
- [x] 005. Add state transition system: Prologue → TitleCard → Gameplay (with fade effects)

### 0.2 — Save/Load Foundation
- [x] 006. Define `SaveData` class: player position, health, suit integrity, battery, inventory list, scavenger-killed flag, scan log, shelter-unlocked flags, world time, weather state
- [x] 007. JSON serialize/deserialize for SaveData
- [x] 008. Save trigger at shelter points (auto-save on rest)
- [x] 009. Load on game start (if save exists) or start fresh
- [x] 010. Death → reload last save (fade to black, respawn at shelter)

### 0.3 — Camera Overhaul
- [x] 011. Basic camera follow player [EXISTING]
- [ ] 012. Dynamic camera zoom: tight (10-tile view) during crash site, pulls back when entering forest
- [x] 013. Camera zoom transition system (smooth lerp between zoom levels)
- [ ] 014. Camera bounds per area (don't show void beyond level edges)

---

## PHASE 1: CORE SYSTEMS OVERHAUL
*Modify existing systems to match Genesis design.*

### 1.1 — Player Character (Adam)
- [x] 015. Basic movement: walk, run, jump [EXISTING]
- [x] 016. Injured movement state: limping animation, reduced speed (active until minute ~3) — IsInjured + InjuredSpeedMult (0.45x) + limp timer implemented
- [ ] 017. Recovery trigger: picking up knife or EVE rebooting restores normal movement
- [x] 018. Suit system: integrity % (starts 31%, passive drain from tech suppression field)
- [x] 019. Battery system: charge level, passive drain, consumption by tech items (grapple, lantern, sprint)
- [x] 020. Suit integrity affects: defense (higher = more damage absorbed), HUD glitchiness (lower = more static) — HUD glitch timer implemented
- [x] 021. Battery affects: tech item availability (0 battery = lantern/sprint/cipher helmet non-functional)
- [x] 022. Remove/disable all advanced movement at game start (tier system gates abilities per tier)
- [x] 023. Basic jump only — tier 0 (Tech) has no double jump, no wall climb at start
- [x] 024. Crouch/duck under low obstacles
- [x] 025. Mouse-direction aiming system (replace 8-direction lock)
- [ ] 026. Aim-assist when suit integrity > 50% (subtle crosshair magnetism, fades as suit degrades)

### 1.2 — Combat Knife (Melee)
- [x] 027. Basic melee attack [EXISTING — full combo system with multiple weapons]
- [ ] 028. Knife-specific animation: short range, fast, 3-hit combo
- [ ] 029. Knife interaction with environment: slash fungus, pry open containers
- [x] 030. Knife damage: configurable via WeaponStats (damage, range, knockback per weapon type)

### 1.3 — Sidearm (Ranged)
- [x] 031. Sidearm weapon type: mouse-aimed, hitscan bullets via Bullet class
- [x] 032. Ammo system: 12 rounds per clip, reload mechanic (1.2s reload)
- [x] 033. Ammo HUD display (rounds remaining visible in HUD)
- [x] 034. Weapon switching: dual 4-slot system (L1/L2/R1/R2), any weapon in any hand
- [x] 035. Sidearm damage: configurable via WeaponStats
- [x] 036. Gunshot sound attracts nearby enemies (NoiseEvent BANG, 400px radius, 1.0 intensity)
- [x] 037. Muzzle flash particles (flash + core glow, 0.06s duration)

### 1.4 — Grapple Module
- [x] 038. Grapple as equippable tool (separate from weapon slots, E key or right-click)
- [x] 039. Grapple fires toward mouse cursor, attaches to terrain/anchor points
- [x] 040. Grapple swing physics: pendulum motion with gravity (GrappleGravityBase = 1300f), release at apex for momentum
- [x] 041. Grapple pull: pull Adam to anchor point + pull enemies toward player
- [x] 042. Grapple battery cost: each use drains battery
- [x] 043. Grapple fails gracefully when battery = 0 (EVE comment)
- [~] 044. Grapple anchor points: attaches to solid terrain. No visual indicator/ring for anchor points yet
- [x] 045. Grapple feel: hook speed 1000px/s, max length ~6 tiles (200px), reel speed 200px/s

### 1.5 — Inventory System
- [x] 046. Full inventory screen: Equipment (body silhouette + weapon slots), Suit stats, Tools, Log/Bestiary tabs
- [x] 047. Battery cell item: consumable, restores battery/suit
- [x] 048. Pickup interaction: walk over items (auto-collect), editor can spawn items
- [x] 049. Inventory HUD: equipped weapon + battery + suit integrity + durability bars + armor/lantern/cipher/torch indicators
- [x] 050. Equipment system: helmet slot (cipher helmet), chest slot (tech chest plate), legs slot (rocket boots), 4 hand slots (L1/L2/R1/R2)

---

## PHASE 2: EVE SYSTEM
*The companion that makes the game feel alive.*

### 2.1 — EVE Core
- [x] 051. EVE orb follows player [EXISTING — orbiting behavior]
- [~] 052. EVE visual states: healthy orbit exists. Damaged/offline visual states not implemented
- [ ] 053. EVE boot sequence: spawn glitchy at minute 1:15, stabilize over 5 seconds
- [ ] 054. EVE orb physics: shakes off water droplets in rain, recoils from explosions, dims when player kills passive creatures
- [x] 055. EVE speech bubble system [EXISTING — 75+ unique EVE alert lines in codebase]

### 2.2 — Scan Level 1 (Passive)
- [~] 056. Auto-scan system: cipher scan (Q hold) exists, passive auto-scan not implemented
- [ ] 057. Scan trigger: proximity + line-of-sight + cooldown (don't spam)
- [~] 058. Visual: cipher scan has visual overlay. No per-scan-level color distinction yet
- [x] 059. Speech: EVE alert text bubbles (1 line, configurable duration)
- [~] 060. Scannable object data: Bestiary tracks creature scan data. No generic ScannableObject for env items
- [ ] 061. Place scannable markers in crash site: alien plants, debris types, fire sources
- [ ] 062. Place scannable markers in forest: root structures, canopy species, thermal signatures

### 2.3 — Scan Level 2 (Active)
- [~] 063. Player-triggered scan: Q hold cipher scan exists as toggle, not per-object targeting
- [ ] 064. Visual: EVE orb shifts to GREEN focused beam aimed at target, lasts ~3 seconds
- [~] 065. Speech: bestiary has species descriptions. Per-object L2 scan text not implemented
- [x] 066. Scan level 2 unlock: _cipherScanUnlocked gate exists, toggleable
- [ ] 067. HUD indicator: scan level icon in corner (blue dot = L1, green dot = L2 available)
- [ ] 068. Some objects only have L1 data. Visual cue on objects with L2 data available

### 2.4 — Scan Level 3 (Deep — Limited in First 30 Min)
- [~] 069. Deep scan: L3 scan flag exists in bestiary. Hold mechanic not implemented
- [ ] 070. Visual: EVE orb emits GOLD resonance rings, expands outward
- [ ] 071. Speech: 3-4 line detailed text not written
- [ ] 072. Only 2-3 objects in first 30 minutes support L3
- [ ] 073. L3 scan can trigger gameplay changes: reveal hidden path, unlock knowledge entry

### 2.5 — Scan Log
- [x] 074. Scan log data structure: _scanLog HashSet + Bestiary with per-species tracking
- [x] 075. Accessible via inventory: Log/Bestiary tab with species list, scrollable detail panel
- [ ] 076. Scan count affects Adapted encounter trigger (more scans = higher "curiosity" score)

### 2.6 — EVE Dialogue System
- [x] 077. Triggered dialogue: EveAlert/EveAlertOnce system with 75+ contextual lines
- [~] 078. Dialogue queue: single alert at a time. No proper queue with gaps
- [ ] 079. Dialogue priority: critical lines override queued ambient lines
- [ ] 080. Dialogue variety: 2-3 variants per trigger, random selection

---

## PHASE 3: CREATURES
*The planet's inhabitants.*

### 3.1 — Scavenger (Decomposer)
- [x] 081. Scavenger sprite: 12x8px alien rat silhouette (fallback rectangles)
- [x] 082. Scavenger AI: wander near debris/food sources, freeze when player approaches, flee if too close
- [x] 083. Scavenger interaction: can be killed (1 HP), drops corpse as food source
- [ ] 084. Scavenger carrying behavior: picks up small shiny objects, carries toward nest
- [ ] 085. Scavenger pack behavior: groups of 3-4 guard a found object, scatter when approached
- [ ] 086. Scavenger nest: static location near shelter
- [ ] 087. Scavenger gift behavior: if alive + spared, one brings debris and drops near player
- [ ] 088. Kill/spare tracking: global flag system exists (SaveData.Flags dict)

### 3.2 — Herbivore (Primary Consumer)
- [x] 089. Herbivore sprites: Crawler-Forager (16x10), Hopper (12x10), Bird (10x8) — all fallback rectangles
- [x] 090. Herbivore AI: graze near food sources (FoodSource system), move between patches, flee from predators and loud sounds (NoiseEvent)
- [x] 091. Herbivore-plant interaction: eating from food sources with EatTimer, food depletes
- [~] 092. Herbivore scannable: bestiary tracks species. L1/L2 scan text not written
- [x] 093. Herbivore flee behavior: runs from NoiseEvent BANG, predators (IsThreatTo), startles propagate to herd (PropagateStartle)

### 3.3 — Predator (Secondary Consumer) — Solo
- [x] 094. Predator sprites: Crawler variants (Leaper/Stalker/Spitter), Wingbeater — fallback rectangles
- [x] 095. Predator AI: patrol territory, detect player by proximity/sight, hunt prey creatures
- [x] 096. Predator combat: charge → contact damage, cooldown, re-engage. Wingbeater dive-bomb mechanic
- [x] 097. Predator HP: configurable per species (Crawler HP varies by variant)
- [x] 098. Predator damage to player: configurable contact damage per creature type
- [~] 099. Predator drawn to fire/thermal signatures — drawn to NoiseEvents, not specifically fire
- [x] 100. Predator death: death particles, scannable corpse remains as FoodSource
- [ ] 101. Solo predator at crash site exit (minute 7:30 encounter) — level design not done

### 3.4 — Predator Pack (Dynamic Event)
- [ ] 102. Pack AI: coordinated behavior for multiple predators
- [ ] 103. Pack hunting pattern: flusher + flankers
- [ ] 104. Pack prey: herbivore that flees, can be caught
- [ ] 105. Pack detection: scent-based, Adam masked
- [ ] 106. If scent mask expires: pack aggros
- [ ] 107. Pack flanking AI
- [ ] 108. Pack hunt success scenario: if player watches, prey caught, pack eats, disperses
- [ ] 109. Post-hunt: kill site scannable
- [ ] 110. Post-fight: alpha corpse L3 scannable
- [ ] 111. Sidearm use: kills predators but gunshot scatter already works (NoiseEvent BANG causes flee)
- [ ] 112. Sneak path: alternate route
- [ ] 113. Sneak scan reward
- [ ] 114. Track approach: `_predatorPackOutcome` enum

### 3.5 — Creature Response to Weather
- [x] 115. Creature activity schedule: nocturnal/diurnal/crepuscular species, activity multiplier based on time of day
- [~] 116. Bioluminescence increases in darkness/rain — no bioluminescence system yet, but rain/darkness tracked
- [x] 117. Predator activity: weather detection multiplier (rain reduces detection, storms more)
- [x] 118. Nocturnal creature variants: Stalker/Leaper/Scavenger nocturnal, Bird/Hopper/Forager diurnal

### 3.6 — Ecosystem (Not in original list — built during development)
- [x] Food chain: IsThreatTo() threat table, WillHunt() with PreySize selectivity
- [x] FoodSource system: FoodType enum, eating/decay, plant regrowth, corpse→fertile ground cycle
- [x] CreatureNeeds: Hunger/Fatigue/Safety drives with tick rates, EvaluateGoal() priority system
- [x] Creature awareness: ScanCreatures() with weather-modified detection ranges
- [x] Noise reaction: NoiseEvent system with floating comic text (SWSH/BANG/SKREE/THUD)
- [x] Burrowing: resting creatures sink into ground, harder to detect
- [x] Herding: panic propagation (PropagateStartle), hopper herd drift
- [x] Wingbeater nesting: finds ledge, gathers plants, builds nest, territorial near nest
- [x] Wall-walking: Stalker crawlers walk on walls/ceilings (GravityDir system)
- [x] Low-health flee: ≤30% HP creatures flee (Thornback hunkers instead)
- [x] Predator selectivity: PreySize ranges per species
- [x] Lantern creature reactions: nocturnal creatures flee lantern light, bugs attracted
- [x] Damage types: Slash/Blunt/Pierce/Fire/Electric with per-creature resistance multipliers

---

## PHASE 4: ENVIRONMENT & LEVELS
*The world the player moves through.*

### 4.1 — Crash Site (Area 1a — The Wreckage)
- [~] 119. Crash site layout: crashsite.json exists (~704px wide, very small). Needs full redesign
- [ ] 120. Ship hull tileset: torn metal, sparking wires, broken screens, scattered cargo
- [ ] 121. Fire particles: at crash debris, ambient light source, attract predators
- [ ] 122. Alien plant intrusions: native vegetation growing on wreckage
- [ ] 123. Collapsed bulkhead: teaches crouching
- [ ] 124. Cargo container: interactable, contains knife
- [ ] 125. Hidden storage locker: requires knife interaction, contains sidearm
- [ ] 126. Crushed terminal: requires battery cell to power, contains CO's message
- [ ] 127. Toxic fungus hazard: blocks one path, 3 solution options
- [ ] 128. Ship cockpit area: dead screens, EVE's beacon dialogue trigger, sidearm locker
- [ ] 129. Exit to forest: clear transition zone

### 4.2 — Living Forest (Area 2 — First 30 Min Portion)
- [~] 130. Forest layout: surface-east.json exists (3200×1600px, 18 creatures). Needs expansion
- [ ] 131. Forest tileset: massive alien trees, bioluminescent undergrowth, root platforms
- [ ] 132. Canopy layer: parallax background (system exists but disabled — layers too small)
- [ ] 133. Bioluminescent plants: glow in response to proximity, weather, creature interaction
- [ ] 134. Dense undergrowth zones: slow movement, stealth cover
- [ ] 135. Ravine: gap crossable only with grapple (progression gate)
- [ ] 136. Canopy platform: reachable via grapple, provides vista view
- [ ] 137. Stone cairn clearing: Adapted sign, scannable
- [ ] 138. Tree scratch marks: environmental detail near cairn, scannable
- [ ] 139. Grapple debris in tree: grapple module pickup location
- [ ] 140. Path split: left (scavengers + battery cell) vs right (high ledge, locked)
- [ ] 141. Marshland transition: forest thins, ground gets wet
- [ ] 142. Cliff face: Native Ruins visible at top, too sheer to climb (wall-climb gate)
- [ ] 143. Shelter alcove: under massive tree root, save point

### 4.3 — Environmental Storytelling Objects
- [ ] 144. Scattered ship debris throughout forest
- [ ] 145. Scavenger nests built from ship debris
- [ ] 146. Adapted trail markers: subtle
- [ ] 147. Previous visitor evidence: corroded piece of different ship tech

### 4.4 — Vista/Skybox
- [~] 148. Distant mountains — parallax system exists but no art/layers loaded
- [ ] 149. Stormcloud bank — parallax
- [ ] 150. The Transformed Lands: massive geometric shapes on far horizon
- [ ] 151. Sky color system: changes with time of day and weather
- [ ] 152. From canopy platform: all distant landmarks visible simultaneously

### 4.5 — Existing Levels (Not in original list)
- [x] Ship interior level (ship-interior.json) — small interior area
- [x] Surface-east ecosystem level (3200×1600px, 100×50 tiles, 18 creatures, mixed ecosystem)
- [x] Training hall level (training-hall.json)
- [x] Debug rooms (debug-room.json, debug-room-2.json)
- [x] Level transition system with exit IDs + smart fallback matching
- [x] Overworld/world map system with biome nodes

---

## PHASE 5: WEATHER SYSTEM
*The world breathes.*

### 5.1 — Weather State Machine
- [x] 153. Weather states: WeatherSystem.cs with moisture/temperature/wind/storm atmospheric simulation
- [x] 154. Weather transition system: smooth organic transitions driven by atmospheric sim
- [x] 155. Weather timer: organic — moisture builds near water, rain at 0.7 threshold, storms build during rain
- [x] 156. Weather affects: creature detection ranges (rain 70%, storm 50%), creature behavior, hunger rates (+50% in rain)

### 5.2 — Rain
- [x] 157. Rain particle system [EXISTING]
- [~] 158. Light rain: particles exist. Ambient sound not implemented
- [~] 159. Heavy rain: dense particles exist. Audio not implemented. Creatures flee to shelter not implemented
- [ ] 160. Rain interacts with surfaces: splash particles on ground
- [ ] 161. Rain interacts with fire: extinguish fires in heavy rain
- [ ] 162. EVE water droplet shake animation during rain

### 5.3 — Wind
- [x] 163. Wind particle streaks [EXISTING]
- [~] 164. Wind direction: variable. Doesn't visually affect rain angle
- [ ] 165. Wind visual: leaf/debris particles drift in wind direction
- [ ] 166. Wind affects player: slight push during jumps
- [ ] 167. Wind affects grapple swing: pendulum biased by wind

### 5.4 — Fog
- [ ] 168. Fog rendering: gradient overlay reducing visibility
- [ ] 169. Fog density: variable, heavier in low areas
- [ ] 170. Fog affects: scan range reduced, creatures harder to spot

### 5.5 — Time of Day
- [x] 171. Day/night cycle: worldTime 0-24 float, shelter rest advances time
- [x] 172. Sky color gradient: warm day → cool night (implemented in draw code)
- [~] 173. Bioluminescence intensity: no bioluminescence system, but darkness tracked
- [x] 174. Nocturnal/diurnal creature swap: species have IsNocturnal/IsCrepuscular, activity multiplier

### 5.6 — Weather HUD
- [ ] 175. Temperature indicator: small icon
- [~] 176. EVE weather commentary: atmospheric sim running, no EVE lines hooked to transitions

---

## PHASE 6: HUD & UI
*Minimal, immersive, informative.*

### 6.1 — Gameplay HUD
- [x] 177. Health bar: left side, color-coded (green → yellow → red)
- [x] 178. Suit integrity bar: below health, with % number
- [x] 179. Battery bar: below suit, drains visibly when using tech
- [x] 180. Equipped weapon indicator: weapon name + durability bar in HUD
- [ ] 181. EVE scan level indicator: not implemented
- [x] 182. HUD glitch effect: _hudGlitchTimer + _hudGlitchRng when suit integrity low
- [ ] 183. No HUD state: first 45 seconds, HUD fades in with EVE
- [ ] 184. Temperature indicator: not implemented

### 6.2 — Pause Menu
- [x] 185. Inventory overlay: Equipment, Suit, Tools, Log/Bestiary tabs. Full screen
- [~] 186. Settings: CRT filter toggle, hit stop toggle, screen shake toggle exist. No full settings screen
- [ ] 187. Save indicator: "Last shelter: [name]"

### 6.3 — Dialogue Display
- [x] 188. Speech bubble system [EXISTING]
- [x] 189. EVE dialogue: speech bubble near orb with background, text display
- [x] 190. Dialogue fade: text appears with duration, fades
- [ ] 191. Dialogue queue indicator: "..." on EVE when queued lines

---

## PHASE 7: PROLOGUE CUTSCENE
*The 30-second hook.*

### 7.1 — Cutscene System
- [x] 192. Cutscene state: disables player input, plays scripted sequence
- [x] 193. Cutscene timeline: timed events (show image, play sound, display text, fade)
- [x] 194. Skip option: hold [ESC] to skip

### 7.2 — Prologue Scenes
- [~] 195. Phase 1 art: placeholder/basic
- [~] 196. Phase 2 art: placeholder/basic
- [~] 197. Phase 3 art: placeholder/basic
- [~] 198. Phase 4 art: THE EYE — placeholder
- [x] 199. Phase transitions: fade/cut between phases, white flash
- [~] 200. Audio: SFX not implemented
- [~] 201. The Dragon resonance sound: not implemented

### 7.3 — Title Card
- [x] 202. "GENESIS" text: large, centered, clean font
- [~] 203. Ambient sound crossfade: no audio
- [x] 204. Fade to gameplay: title fades, camera fades in

---

## PHASE 8: AUDIO (Placeholder → Polish)
*Sound makes or breaks atmosphere. Start with placeholders, polish later.*

### 8.1 — Sound Effects
- [ ] 205-221. ALL AUDIO NOT IMPLEMENTED — zero sound files in project
- Note: NoiseEvent system creates visual floating text as placeholder for audio (SWSH, BANG, SKREE, THUD)

### 8.2 — Music
- [ ] 222-227. NO MUSIC IMPLEMENTED

---

## PHASE 9: BEHAVIOR TRACKING & CONSEQUENCES
*Invisible systems that make the world feel responsive.*

### 9.1 — Player Behavior Flags
- [x] 228. SaveData.Flags dictionary exists for arbitrary bool flags
- [ ] 229. PredatorPackOutcome enum: not implemented
- [~] 230. ScanCount: bestiary tracks species encounters. No total scan counter
- [x] 231. SuitPiecesRemoved: chest plate + rocket boots + helmet all toggleable, states saved
- [ ] 232. SidearmFound: no pickup event tracking
- [ ] 233. COMessageRead: not implemented
- [ ] 234. GrappleUseCount: not tracked
- [x] 235. All equipment/flag state saved in SaveData

### 9.2 — Adapted Encounter Logic
- [ ] 236-240. NOT IMPLEMENTED

### 9.3 — Scavenger Consequence Chain
- [ ] 241-243. NOT IMPLEMENTED (scavenger creature exists but no consequence system)

---

## PHASE 10: LEVEL DESIGN & PLACEMENT
*Building the actual playable space.*

### 10.1 — Crash Site Level Design
- [~] 244. Crash site exists but is ~704px wide — needs full redesign to 8-10 screens
- [ ] 245-255. NOT PLACED — level is too small for proper placement

### 10.2 — Forest Level Design
- [~] 256. Surface-east exists (3200×1600px) — needs expansion and design pass
- [ ] 257-272. NOT PLACED — basic terrain only, no designed encounters/paths

### 10.3 — Vista Setup
- [ ] 273-275. NOT IMPLEMENTED

---

## PHASE 11: THE SCREAM (Dragon Encounter 1)
- [ ] 276-285. NOT IMPLEMENTED

---

## PHASE 12: SCRIPTED EVE DIALOGUE (All Lines for 0-30 Min)

### 12.1 — Critical Path Lines
- [~] 286-306. 75+ EveAlert/EveAlertOnce calls exist in code, but these are contextual system alerts (equipment, pickups, deaths), NOT the scripted narrative dialogue. Narrative lines not written/placed

### 12.2 — Ambient / Scan Lines
- [ ] 307-313. NOT WRITTEN

### 12.3 — Conditional Lines
- [~] 314. Grapple battery fail: EVE alert exists
- [~] 315. Low suit integrity: HUD glitch exists, no EVE warning line
- [ ] 316. First sidearm use ammo callout
- [ ] 317. Player near gun for predator pack

---

## PHASE 13: POLISH & FEEL
*What separates "functional" from "fun."*

### 13.1 — Screen Effects
- [x] 318. Fade to black / fade from black (transitions, death, shelter rest)
- [x] 319. Screen shake: configurable per weapon, on kills (ShakeDuration, ShakeIntensity)
- [x] 320. HUD glitch: _hudGlitchTimer with random pixel displacement, scales with suit damage
- [x] 321. Vignette: implemented (CRT shader VignetteStrength + purple vignette overlay)
- [x] 322. White flash: prologue Phase 4

### 13.2 — Particle Effects
- [x] 323. Rain particles [EXISTING]
- [x] 324. Wind streaks [EXISTING]
- [ ] 325. Fire particles: at crash debris
- [ ] 326. Suit spark particles: periodic crackle
- [ ] 327. Spore particles: from bioluminescent plants
- [ ] 328. EVE scan particles: per scan level
- [x] 329. Grapple cable: rendered rope/line from player to anchor
- [ ] 330. Water droplets on EVE: during rain
- [~] 331. Dust/debris: sprint particles (thruster exhaust) exist. Ambient wind particles not done
- [ ] 332. Predator charge dust
- [ ] 333. Creature flee particles: scattered leaves/dust
- [ ] 334. Adapted shimmer: color-shift particles

### 13.3 — Animation Polish
- [ ] 335-349. ALL CREATURES AND PLAYER ARE FALLBACK RECTANGLES — no sprite animations

### 13.4 — Juice
- [x] 350. Hit stop on melee impact (configurable per weapon: HitStopFinisher, HitStopKill)
- [x] 351. Hit stop on ranged impact
- [x] 352. Camera nudge on weapon hits (configurable ShakeDuration/ShakeIntensity per weapon)
- [x] 353. Squash/stretch on player (land squash, jump stretch, dash stretch)
- [ ] 354. Battery cell pickup: glow + HUD flash
- [ ] 355. Grapple attachment: camera snap + slight zoom
- [ ] 356. Shelter rest: warm color shift
- [~] 357. Weather transition: atmospheric sim provides smooth transitions organically

---

## PHASE 14: TESTING & ITERATION
*Make it fun.*

- [ ] 358-373. NOT DONE — need designed levels first

---

## SCORE SUMMARY

| Category | Done | Partial | Not Started | Total |
|----------|------|---------|-------------|-------|
| Phase 0: Restructure | 4 | 0 | 1 | 5 |
| Phase 1: Core Systems | 29 | 1 | 6 | 36 |
| Phase 2: EVE | 10 | 8 | 12 | 30 |
| Phase 3: Creatures | 22 | 3 | 17 | 42 |
| Phase 4: Environment | 6 | 4 | 24 | 34 |
| Phase 5: Weather | 10 | 5 | 7 | 22 |
| Phase 6: HUD/UI | 8 | 1 | 4 | 13 |
| Phase 7: Prologue | 5 | 6 | 0 | 11 |
| Phase 8: Audio | 0 | 0 | 23 | 23 |
| Phase 9: Behavior | 3 | 1 | 9 | 13 |
| Phase 10: Level Design | 0 | 2 | 27 | 29 |
| Phase 11: Dragon | 0 | 0 | 10 | 10 |
| Phase 12: Dialogue | 0 | 4 | 28 | 32 |
| Phase 13: Polish | 10 | 3 | 12 | 25 |
| Phase 14: Testing | 0 | 0 | 16 | 16 |
| **TOTAL** | **107** | **38** | **196** | **341** |

**Progress: ~37% complete (107 done + 38 partial out of 341 unique tasks)**

### Biggest gaps:
1. **Level Design (Phase 10)** — 2/29 tasks. Need actual designed spaces
2. **Audio (Phase 8)** — 0/23. Zero sound
3. **Dialogue (Phase 12)** — 0/32. Narrative text not written
4. **Environment Art (Phase 4)** — 6/34. No tilesets, parallax, vista
5. **Predator Pack (Phase 3.4)** — 0/13. Coordinated AI not built

### Strongest areas:
1. **Core Systems (Phase 1)** — 29/36 done. Movement, combat, inventory, grapple, weapons all functional
2. **Creatures (Phase 3)** — 22/42 done. Full ecosystem with food chain, 8 species, weather response
3. **Polish/Juice (Phase 13)** — 10/25 done. Hit stop, screen shake, squash/stretch, vignette, HUD glitch

---

*Check off tasks as completed. Update this file as design evolves.*
*When all tasks are done, minutes 0-30 are shippable. Then we do 30-60.*
