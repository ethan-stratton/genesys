# GENESIS — Master Task List: First 30 Minutes
# Last updated: 2026-03-22

*Every task needed to make minutes 0-30 playable, fun, and shippable.*
*Tasks are in dependency order. Build from top to bottom.*
*[EXISTING] = already built in current prototype. [MODIFY] = needs changes. [NEW] = from scratch.*

---

## PHASE 0: PROJECT RESTRUCTURE
*Before building anything new, restructure the prototype into Genesis.*

### 0.1 — Project Identity
- [ ] 001. Rename project from "arena-shooter" to "genesis" (solution, namespaces, folder)
- [ ] 002. Create `GameState.Prologue` state (cutscene playback)
- [ ] 003. Create `GameState.Gameplay` state (replaces current gameplay, keeps editor)
- [ ] 004. Create `GameState.TitleCard` state (black screen + title text + fade)
- [ ] 005. Add state transition system: Prologue → TitleCard → Gameplay (with fade effects)

### 0.2 — Save/Load Foundation
- [ ] 006. Define `SaveData` class: player position, health, suit integrity, battery, inventory list, scavenger-killed flag, scan log, shelter-unlocked flags, world time, weather state
- [ ] 007. JSON serialize/deserialize for SaveData
- [ ] 008. Save trigger at shelter points (auto-save on rest)
- [ ] 009. Load on game start (if save exists) or start fresh
- [ ] 010. Death → reload last save (fade to black, respawn at shelter)

### 0.3 — Camera Overhaul
- [x] 011. Basic camera follow player [EXISTING]
- [ ] 012. Dynamic camera zoom: tight (10-tile view) during crash site, pulls back when entering forest
- [ ] 013. Camera zoom transition system (smooth lerp between zoom levels)
- [ ] 014. Camera bounds per area (don't show void beyond level edges)

---

## PHASE 1: CORE SYSTEMS OVERHAUL
*Modify existing systems to match Genesis design.*

### 1.1 — Player Character (Adam)
- [x] 015. Basic movement: walk, run, jump [EXISTING]
- [ ] 016. Injured movement state: limping animation, reduced speed (active until minute ~3)
- [ ] 017. Recovery trigger: picking up knife or EVE rebooting restores normal movement
- [ ] 018. Suit system: integrity % (starts 31%, passive drain from tech suppression field)
- [ ] 019. Battery system: charge level, passive drain, consumption by tech items (grapple, shield, etc.)
- [ ] 020. Suit integrity affects: defense (higher = more damage absorbed), HUD glitchiness (lower = more static)
- [ ] 021. Battery affects: tech item availability (0 battery = tech items non-functional)
- [ ] 022. Remove/disable all advanced movement at game start (no slide, vault kick, blade dash, uppercut, cartwheel, flip — these are mid/late-game bio unlocks)
- [ ] 023. Basic jump only — no double jump, no wall cling, no wall climb at start
- [ ] 024. Crouch/duck under low obstacles (new, replaces slide for now)
- [ ] 025. Mouse-direction aiming system (replace 8-direction lock)
- [ ] 026. Aim-assist when suit integrity > 50% (subtle crosshair magnetism, fades as suit degrades)

### 1.2 — Combat Knife (Melee)
- [x] 027. Basic melee attack [EXISTING — modify]
- [ ] 028. Knife-specific animation: short range, fast, 3-hit combo
- [ ] 029. Knife interaction with environment: slash fungus, pry open containers
- [ ] 030. Knife damage: 8 per hit (balanced so predator takes 3-4 hits)

### 1.3 — Sidearm (Ranged)
- [ ] 031. Sidearm weapon type: mouse-aimed, hitscan or fast projectile
- [ ] 032. Ammo system: 12 rounds max, no reload mechanic (you have what you find)
- [ ] 033. Ammo HUD display (rounds remaining, visible when weapon equipped)
- [ ] 034. Weapon switching: knife ↔ sidearm (if found), key bind (e.g., 1/2 or scroll wheel)
- [ ] 035. Sidearm damage: 20 per shot (2-shot kill on predator)
- [ ] 036. Gunshot sound attracts nearby enemies (radius-based aggro trigger)
- [ ] 037. Muzzle flash + shell casing particles

### 1.4 — Grapple Module
- [ ] 038. Grapple as equippable tool (not weapon slot — separate key, e.g., E or right-click)
- [ ] 039. Grapple fires toward mouse cursor, attaches to terrain/anchor points
- [ ] 040. Grapple swing physics: pendulum motion, release at apex for momentum
- [ ] 041. Grapple pull: pull Adam to anchor point (for vertical traversal)
- [ ] 042. Grapple battery cost: each use drains X% battery
- [ ] 043. Grapple fails gracefully when battery = 0 (click sound, nothing happens, EVE comment first time)
- [ ] 044. Grapple anchor points: specific tiles/objects marked as grappleable (visual indicator — subtle hook/ring)
- [ ] 045. Grapple feel: mechanical click on fire, motor whirr during pull, cable weight on swing

### 1.5 — Inventory System
- [ ] 046. Simple inventory: list of held items (knife, sidearm, grapple, battery cells, chitin piece)
- [ ] 047. Battery cell item: consumable, restores X% suit integrity + Y% battery
- [ ] 048. Pickup interaction: walk over small items (auto-collect), interact key for large items
- [ ] 049. Inventory HUD: minimal, shows equipped weapon + battery + suit integrity
- [ ] 050. No crafting menu for first 30 minutes (bio-tier crafting comes later)

---

## PHASE 2: EVE SYSTEM
*The companion that makes the game feel alive.*

### 2.1 — EVE Core
- [x] 051. EVE orb follows player [EXISTING — orbiting behavior]
- [ ] 052. EVE visual states: healthy (bright, smooth orbit), damaged (flickering, erratic orbit), offline (dark, stationary)
- [ ] 053. EVE boot sequence: spawn glitchy at minute 1:15, stabilize over 5 seconds
- [ ] 054. EVE orb physics: shakes off water droplets in rain, recoils from explosions, dims when player kills passive creatures
- [ ] 055. EVE speech bubble system [EXISTING — modify for scan level colors]

### 2.2 — Scan Level 1 (Passive)
- [ ] 056. Auto-scan system: EVE detects scannable objects within radius (~6 tiles)
- [ ] 057. Scan trigger: proximity + line-of-sight + cooldown (don't spam)
- [ ] 058. Visual: EVE orb pulses soft BLUE, brief glow on scanned object
- [ ] 059. Speech: short text bubble (1 line, 3-4 seconds) — no player action required
- [ ] 060. Scannable object data: define `ScannableObject` class with id, scan-level-1 text, position, scan-cooldown
- [ ] 061. Place scannable markers in crash site: alien plants, debris types, fire sources
- [ ] 062. Place scannable markers in forest: root structures, canopy species, thermal signatures

### 2.3 — Scan Level 2 (Active)
- [ ] 063. Player-triggered scan: press scan key (e.g., Q) while near scannable object
- [ ] 064. Visual: EVE orb shifts to GREEN focused beam aimed at target, lasts ~3 seconds
- [ ] 065. Speech: 2-3 line text bubble (5-7 seconds), more detailed info
- [ ] 066. Level 2 scan unlocks after EVE repairs scanner module (minute ~2, first battery cell use)
- [ ] 067. HUD indicator: scan level icon in corner (blue dot = L1, green dot = L2 available)
- [ ] 068. Some objects only have L1 data. Others have L1 + L2. Visual cue on objects with L2 data available (subtle shimmer?)

### 2.4 — Scan Level 3 (Deep — Limited in First 30 Min)
- [ ] 069. Deep scan: hold scan key while stationary for 2 seconds near L3-capable object
- [ ] 070. Visual: EVE orb emits GOLD resonance rings, expands outward
- [ ] 071. Speech: 3-4 line text bubble (8-10 seconds), reveals secrets/mechanics/lore
- [ ] 072. Only 2-3 objects in first 30 minutes support L3 (predator alpha corpse, stone cairn, adapted chitin piece)
- [ ] 073. L3 scan can trigger gameplay changes: reveal hidden path, unlock knowledge entry

### 2.5 — Scan Log
- [ ] 074. Scan log data structure: list of all scanned objects with level reached
- [ ] 075. Accessible via pause menu: simple list view, organized by area
- [ ] 076. Scan count affects Adapted encounter trigger (more scans = higher "curiosity" score)

### 2.6 — EVE Dialogue System
- [ ] 077. Triggered dialogue: specific game events fire EVE lines (enter forest, find sidearm, etc.)
- [ ] 078. Dialogue queue: if multiple triggers fire close together, queue them with gaps
- [ ] 079. Dialogue priority: critical lines (suit failing, Dragon scream) override queued ambient lines
- [ ] 080. Dialogue variety: 2-3 variants per trigger, random selection (prevents repetition on replay)

---

## PHASE 3: CREATURES
*The planet's inhabitants.*

### 3.1 — Scavenger (Decomposer)
- [ ] 081. Scavenger sprite: small (12x8px?), alien rat-like silhouette
- [ ] 082. Scavenger AI: wander near debris/food sources, freeze when player approaches, flee if player gets too close
- [ ] 083. Scavenger interaction: can be killed (1 hit), drops nothing
- [ ] 084. Scavenger carrying behavior: picks up small shiny objects, carries them toward nest
- [ ] 085. Scavenger pack behavior: groups of 3-4 guard a found object, scatter when player approaches
- [ ] 086. Scavenger nest: static location near shelter, populated if scavengers alive, empty if first scavenger killed
- [ ] 087. Scavenger gift behavior (at shelter): if alive + spared, one brings debris bit and drops near player
- [ ] 088. Kill/spare tracking: global flag `_scavengerKilled` set on first scavenger death, persists in save

### 3.2 — Herbivore (Primary Consumer)
- [ ] 089. Herbivore sprite: medium (20x16px?), gentle silhouette, clearly non-threatening
- [ ] 090. Herbivore AI: graze near specific plants, move between grazing spots, flee from predators and loud sounds
- [ ] 091. Herbivore-plant interaction: eating from bioluminescent plant triggers spore release (particle effect)
- [ ] 092. Herbivore scannable: L1 "Passive herbivore." L2 (on spore plant) "Spores have mild regenerative property. Self-medicating behavior."
- [ ] 093. Herbivore flee behavior: runs from gunshots, predators, fast player approach

### 3.3 — Predator (Secondary Consumer) — Solo
- [ ] 094. Predator sprite: medium-large (24x16px?), alien dog silhouette, aggressive posture
- [ ] 095. Predator AI: patrol territory, detect player by proximity/sight, charge attack
- [ ] 096. Predator combat: charge → bite (contact damage), short cooldown, re-engage
- [ ] 097. Predator HP: 30 (3-4 knife hits at 8 dmg, 2 sidearm shots at 20 dmg)
- [ ] 098. Predator damage to player: 15 per hit (meaningful but not one-shot)
- [ ] 099. Predator drawn to fire/thermal signatures (wander toward crash fires)
- [ ] 100. Predator death: ragdoll or death animation, scannable corpse remains briefly
- [ ] 101. Solo predator at crash site exit (minute 7:30 encounter)

### 3.4 — Predator Pack (Dynamic Event)
- [ ] 102. Pack AI: 3 predators with coordinated behavior
- [ ] 103. Pack hunting pattern: 1 flusher, 2 flankers — pursue herbivore prey in the forest
- [ ] 104. Pack prey: herbivore AI that flees, can be caught and killed by pack
- [ ] 105. Pack detection: scent-based. Adam is masked by crash residue (invisible timer or proximity-based)
- [ ] 106. If scent mask expires or player gets too close: pack aggros
- [ ] 107. Pack flanking AI: when aggro'd on player, one circles behind while two approach front
- [ ] 108. Pack hunt success scenario: if player watches, prey is caught, pack eats, then disperses
- [ ] 109. Post-hunt: kill site scannable (bone fragments → planetary history data)
- [ ] 110. Post-fight (knife win): alpha corpse is L3 scannable → pack hierarchy + scent-masking chemical data
- [ ] 111. Sidearm use: kills predators but triggers "gunshot scatter" — nearby wildlife flees for 60 seconds
- [ ] 112. Sneak path: alternate route through dense foliage, requires slow movement (no running)
- [ ] 113. Sneak scan reward: L2 on pack from hiding → subsonic communication data
- [ ] 114. Track which approach player took: `_predatorPackOutcome` enum (Fought/Shot/Sneaked/Watched) — saved, affects scan log content

### 3.5 — Creature Response to Weather
- [ ] 115. All creatures seek shelter during heavy rain (move toward trees/overhangs)
- [ ] 116. Bioluminescence increases in darkness/rain (creatures + plants)
- [ ] 117. Predator activity increases at dusk/dawn, decreases in heavy rain
- [ ] 118. Nocturnal creature variants active after shelter rest (different silhouettes, behaviors)

---

## PHASE 4: ENVIRONMENT & LEVELS
*The world the player moves through.*

### 4.1 — Crash Site (Area 1a — The Wreckage)
- [ ] 119. Design crash site layout: 8-10 screens, linear-ish with branches
- [ ] 120. Ship hull tileset: torn metal, sparking wires, broken screens, scattered cargo
- [ ] 121. Fire particles: small fires on debris, ambient light source, attract predators
- [ ] 122. Alien plant intrusions: native vegetation already growing on/around wreckage
- [ ] 123. Collapsed bulkhead: low obstacle, teaches crouching
- [ ] 124. Cargo container: interactable, contains knife
- [ ] 125. Hidden storage locker: requires knife interaction, contains sidearm
- [ ] 126. Crushed terminal: requires battery cell to power, contains CO's message
- [ ] 127. Toxic fungus hazard: blocks one path, 3 solution options
- [ ] 128. Ship cockpit area: dead screens, EVE's beacon dialogue trigger, sidearm locker
- [ ] 129. Exit to forest: clear transition zone, visual shift from wreckage to vegetation

### 4.2 — Living Forest (Area 2 — First 30 Min Portion)
- [ ] 130. Design forest layout: 15-20 screens, more open, multiple paths
- [ ] 131. Forest tileset: massive alien trees, bioluminescent undergrowth, root platforms
- [ ] 132. Canopy layer: parallax background, creatures visible in upper layer (unreachable for now)
- [ ] 133. Bioluminescent plants: glow in response to proximity, weather, creature interaction
- [ ] 134. Dense undergrowth zones: slow movement, provides stealth cover (for predator pack sneak)
- [ ] 135. Ravine: gap crossable only with grapple (progression gate)
- [ ] 136. Canopy platform: reachable via grapple, provides vista view
- [ ] 137. Stone cairn clearing: Adapted sign, scannable (L2 + L3)
- [ ] 138. Tree scratch marks: environmental detail near cairn, scannable (L1 only)
- [ ] 139. Grapple debris in tree: grapple module pickup location, requires navigation to reach
- [ ] 140. Path split: left (scavenger pack + battery cell) vs right (high ledge, locked)
- [ ] 141. Marshland transition: forest thins, ground gets wet, different vegetation
- [ ] 142. Cliff face: Native Ruins visible at top, too sheer to climb (wall-climb gate)
- [ ] 143. Shelter alcove: under massive tree root, save point

### 4.3 — Environmental Storytelling Objects
- [ ] 144. Scattered ship debris throughout forest (small pieces — bolts, panels, wiring), showing crash debris field spread
- [ ] 145. Scavenger nests built from ship debris (visual storytelling — they're using your stuff)
- [ ] 146. Adapted trail markers: subtle, easy to miss — arranged leaves, redirected water flow, marks on rocks
- [ ] 147. Previous visitor evidence: a very old, corroded piece of different ship tech half-buried in forest floor (scannable, L2: "This alloy is... centuries old. Different manufacture than ours.")

### 4.4 — Vista/Skybox
- [ ] 148. Distant mountains (Native Ruins direction) — parallax background layer
- [ ] 149. Stormcloud bank (Bone Reef direction) — parallax, animated slowly
- [ ] 150. The Transformed Lands: massive geometric shapes on far horizon, faintly glowing, unsettling
- [ ] 151. Sky color system: changes with time of day and weather
- [ ] 152. From canopy platform: all distant landmarks visible simultaneously (reward moment)

---

## PHASE 5: WEATHER SYSTEM
*The world breathes.*

### 5.1 — Weather State Machine
- [ ] 153. Weather states: Clear, LightRain, HeavyRain, Overcast, Fog, Storm (subset for first 30 min — no acid rain, hail, lightning yet)
- [ ] 154. Weather transition system: smooth blend between states over 30-60 seconds
- [ ] 155. Weather timer: random duration per state (2-8 minutes), weighted by area
- [ ] 156. Weather affects: visibility (fog/rain reduce sight range), creature behavior, plant response, sound

### 5.2 — Rain
- [x] 157. Rain particle system [EXISTING — modify]
- [ ] 158. Light rain: sparse particles, subtle darkening, ambient sound
- [ ] 159. Heavy rain: dense particles, reduced visibility, louder sound, creatures shelter
- [ ] 160. Rain interacts with surfaces: splash particles on platforms/ground
- [ ] 161. Rain interacts with fire: crash site fires dim/extinguish in heavy rain (changes predator behavior — no thermal draw)
- [ ] 162. EVE water droplet shake animation during rain

### 5.3 — Wind
- [x] 163. Wind particle streaks [EXISTING — modify]
- [ ] 164. Wind direction: variable, affects rain angle, particle drift
- [ ] 165. Wind visual: leaf/debris particles drift in wind direction
- [ ] 166. Wind affects player: slight push in wind direction during jumps (subtle, not frustrating)
- [ ] 167. Wind affects grapple swing: pendulum biased by wind

### 5.4 — Fog
- [ ] 168. Fog rendering: gradient overlay reducing visibility at distance
- [ ] 169. Fog density: variable, heavier in low areas (marshland transition)
- [ ] 170. Fog affects: scan range reduced, creatures harder to spot, atmospheric tension

### 5.5 — Time of Day
- [ ] 171. Day/night cycle: simplified — "shelter rest" advances time by ~6 hours
- [ ] 172. Sky color gradient: warm (day) → cool (evening) → dark (night) → grey (dawn)
- [ ] 173. Bioluminescence intensity: increases with darkness
- [ ] 174. Nocturnal/diurnal creature swap: different creatures active at different times

### 5.6 — Weather HUD
- [ ] 175. Temperature indicator: small icon, affects stamina regen rate
- [ ] 176. EVE weather commentary: "Barometric pressure dropping" etc., triggered on state transitions

---

## PHASE 6: HUD & UI
*Minimal, immersive, informative.*

### 6.1 — Gameplay HUD
- [ ] 177. Health bar: left side, simple, color-coded (green → yellow → red)
- [ ] 178. Suit integrity bar: below health, with % number, sparks when low
- [ ] 179. Battery bar: below suit, drains visibly when using tech
- [ ] 180. Equipped weapon indicator: bottom-left, icon + ammo count (sidearm) or ∞ (knife)
- [ ] 181. EVE scan level indicator: top-right corner, colored dot (blue/green/gold)
- [ ] 182. HUD glitch effect: as suit integrity drops, HUD elements flicker/distort
- [ ] 183. No HUD state: first 45 seconds (wake up to EVE reboot), HUD fades in when EVE activates
- [ ] 184. Temperature indicator: small, unobtrusive

### 6.2 — Pause Menu
- [ ] 185. Pause overlay: inventory list, scan log, current objectives (informal — EVE's notes)
- [ ] 186. Settings: volume, controls, display
- [ ] 187. Save indicator: "Last shelter: [name]"

### 6.3 — Dialogue Display
- [x] 188. Speech bubble system [EXISTING — modify]
- [ ] 189. EVE dialogue: speech bubble near orb with background box, color-coded border by scan level
- [ ] 190. Dialogue fade: text appears over 0.5s, holds, fades over 1s
- [ ] 191. Dialogue queue indicator: subtle "..." on EVE when she has queued lines

---

## PHASE 7: PROLOGUE CUTSCENE
*The 30-second hook.*

### 7.1 — Cutscene System
- [ ] 192. Cutscene state: disables player input, plays scripted sequence
- [ ] 193. Cutscene timeline: list of timed events (show image, play sound, display text, fade)
- [ ] 194. Skip option: hold [ESC] for 1 second to skip (don't let accident-skip)

### 7.2 — Prologue Scenes
- [ ] 195. Phase 1 art: Ship cockpit interior (pixel art, 320x180 or game resolution). Screens, stars, EVE orb
- [ ] 196. Phase 2 art: Adam's hands on controls. Signal display
- [ ] 197. Phase 3 art: Ship exterior descending into atmosphere, trailing smoke. Planet below
- [ ] 198. Phase 4 art: THE EYE. Single red mechanical eye on black. This needs to be ICONIC
- [ ] 199. Phase transitions: fade/cut between phases, white flash before the eye
- [ ] 200. Audio: ship alarm SFX, system failure SFX, EVE voice lines (text + SFX), impact SFX
- [ ] 201. The Dragon resonance sound: deep bass vibration, used during the Eye and again at minute 22

### 7.3 — Title Card
- [ ] 202. "GENESIS" text: large, centered, clean font. Appears on black
- [ ] 203. Ambient sound crossfade: silence → wind + wildlife during title hold
- [ ] 204. Fade to gameplay: title fades, camera fades in on Adam face-down

---

## PHASE 8: AUDIO (Placeholder → Polish)
*Sound makes or breaks atmosphere. Start with placeholders, polish later.*

### 8.1 — Sound Effects
- [ ] 205. Footsteps: metal (crash site), soft earth (forest), squelch (marshland)
- [ ] 206. Knife swing + impact
- [ ] 207. Sidearm fire + shell casing
- [ ] 208. Grapple fire + attach + motor whirr + release
- [ ] 209. Suit spark/crackle (periodic, increases as integrity drops)
- [ ] 210. EVE boot-up static
- [ ] 211. EVE scan sounds: blue hum (L1), green ping (L2), gold resonance (L3)
- [ ] 212. Scavenger: skittering, small chirps
- [ ] 213. Herbivore: gentle calls, feeding sounds
- [ ] 214. Predator: growl, charge snarl, bite snap
- [ ] 215. Predator pack: subsonic rumble (during coordinated hunt)
- [ ] 216. Fungus slash: organic squelch + spore release hiss
- [ ] 217. The Dragon SCREAM: low resonance + building harmonic. Must be visceral. Same frequency as prologue eye sound
- [ ] 218. Rain: ambient loop, varies with intensity
- [ ] 219. Wind: ambient loop, directional
- [ ] 220. Thunder: distant rumble (for storm weather state)
- [ ] 221. Shelter rest: brief wind-down, then quiet ambient transition

### 8.2 — Music
- [ ] 222. Crash site: near-silence. Just ambient sound + suit sparking. Desolation
- [ ] 223. Forest: minimal ambient melody. 3-4 notes on a pad/synth, barely there. Evolves with weather
- [ ] 224. Danger proximity: subtle heartbeat-like pulse when enemies are near
- [ ] 225. Discovery sting: brief melodic phrase when scanning something significant
- [ ] 226. The Scream moment: all ambient audio cuts → bass resonance → silence. Then forest sounds return slowly
- [ ] 227. Shelter: warm, safe ambient tone. Brief. Player should feel relief

---

## PHASE 9: BEHAVIOR TRACKING & CONSEQUENCES
*Invisible systems that make the world feel responsive.*

### 9.1 — Player Behavior Flags
- [ ] 228. `ScavengerKilled` (bool): set on first scavenger kill, affects shelter encounter
- [ ] 229. `PredatorPackOutcome` (enum: None/Fought/Shot/Sneaked/Watched): set during pack event
- [ ] 230. `ScanCount` (int): total L2+ scans performed, affects Adapted trigger
- [ ] 231. `SuitPiecesRemoved` (int): how many armor pieces removed (not in first 30 min, but track from start)
- [ ] 232. `SidearmFound` (bool): did they find the hidden sidearm
- [ ] 233. `COMessageRead` (bool): did they find and read the CO's message
- [ ] 234. `GrappleUseCount` (int): how often they use tech solutions
- [ ] 235. All flags saved in SaveData

### 9.2 — Adapted Encounter Logic
- [ ] 236. Adapted visibility score: calculated from flags (kill=-2, observe=+1, scanCount/5=+1, suitRemoved=+1)
- [ ] 237. Score >= 2: Adapted reveals itself + leaves chitin piece
- [ ] 238. Score < 2: Only chitin piece found, no visual encounter
- [ ] 239. Adapted entity: thin humanoid sprite, color-shifts to match background tiles
- [ ] 240. Adapted AI: observe player from distance, retreat if approached, disappear at set trigger distance

### 9.3 — Scavenger Consequence Chain
- [ ] 241. If scavenger killed → shelter nest empty, scratch marks (environmental detail)
- [ ] 242. If scavenger spared → shelter nest populated, gift-giving behavior active
- [ ] 243. Gift item: "Shiny Debris" — useless item but scannable (L1: "Ship alloy, partially corroded. They collect things that glint.")

---

## PHASE 10: LEVEL DESIGN & PLACEMENT
*Building the actual playable space.*

### 10.1 — Crash Site Level Design
- [ ] 244. Block out crash site in level editor: 8-10 screens, mostly horizontal
- [ ] 245. Place ship hull tiles: walls, platforms, overhead beams
- [ ] 246. Place fire particle emitters (3-4 small fires)
- [ ] 247. Place alien plant scannables (2-3 at crash edges)
- [ ] 248. Place cargo container (knife pickup) in natural path
- [ ] 249. Place hidden storage locker (sidearm) — off the main path, behind breakable debris
- [ ] 250. Place crushed terminal (CO message) — requires battery cell, very off-path
- [ ] 251. Place toxic fungus hazard with 3 route options
- [ ] 252. Place solo predator spawn near crash exit
- [ ] 253. Place scavenger spawn (first encounter creature)
- [ ] 254. Place EVE reboot trigger zone
- [ ] 255. Test: walk through entire crash site, verify pacing (~6 minutes exploration)

### 10.2 — Forest Level Design
- [ ] 256. Block out forest: 15-20 screens, branching paths, vertical sections
- [ ] 257. Place forest tileset: massive trees, roots as platforms, undergrowth zones
- [ ] 258. Place herbivore + bioluminescent plant (observation lesson area)
- [ ] 259. Place path split (left: scavengers + battery, right: high ledge lock)
- [ ] 260. Place stone cairn clearing (Adapted sign)
- [ ] 261. Place predator pack hunting ground (open area with cover options)
- [ ] 262. Place grapple module in tree debris (requires small traversal puzzle to reach)
- [ ] 263. Place ravine (grapple gate)
- [ ] 264. Place canopy platform (vista point — high up, grapple-accessible)
- [ ] 265. Place Adapted encounter zone (after scream, quiet area)
- [ ] 266. Place cliff face (Native Ruins visible above, wall-climb gate)
- [ ] 267. Place shelter alcove (save point, scavenger nest nearby)
- [ ] 268. Place forest→marshland transition zone
- [ ] 269. Place scattered ship debris throughout (environmental storytelling)
- [ ] 270. Place old corroded ship piece (previous visitor evidence, scannable)
- [ ] 271. Place Adapted trail markers (subtle environmental details, 3-4 throughout forest)
- [ ] 272. Test: run through forest, verify pacing (~20 minutes with exploration, ~12 minutes rushing)

### 10.3 — Vista Setup
- [ ] 273. Create parallax background layers: mountains, stormclouds, Transformed Lands geometry
- [ ] 274. Place vista trigger at canopy platform (camera holds briefly to let player look)
- [ ] 275. Verify all distant landmarks visible from canopy simultaneously

---

## PHASE 11: THE SCREAM (Dragon Encounter 1)
*The most important 15 seconds in the first 30 minutes.*

- [ ] 276. Trigger zone in forest (after grapple section, before Adapted encounter)
- [ ] 277. On enter: all ambient sound fades over 1 second
- [ ] 278. Dragon resonance sound plays (same as prologue eye — player connects them)
- [ ] 279. Screen vibration: subtle, 3-4 seconds
- [ ] 280. All creatures in loaded area: freeze → flee animation (same direction, away from sound source)
- [ ] 281. EVE: orb flares red, readings spike. Voice line: "Energy signature — massive — bearing—" cuts off
- [ ] 282. 3 seconds of absolute silence (forest empty, no ambient, nothing)
- [ ] 283. EVE: "...I think we should keep moving."
- [ ] 284. Ambient sounds return slowly over 10 seconds (but fewer creatures — most fled)
- [ ] 285. No creature respawns in this zone until player leaves and returns

---

## PHASE 12: SCRIPTED EVE DIALOGUE (All Lines for 0-30 Min)

### 12.1 — Critical Path Lines
- [ ] 286. Boot: "Systems... partial. I—" [static] "—damage assessment in progress. Adam, don't move too fast. Your suit integrity is at 31%."
- [ ] 287. Knife found: "Standard issue. At least something survived."
- [ ] 288. Suit spark: "Power cell rupture. The planet's energy field is interfering with organized electrical systems. Anything with circuitry is... degrading."
- [ ] 289. Fungus: "Atmospheric contaminant. Avoid prolonged exposure."
- [ ] 290. Beacon: "The distress beacon activated on impact. Automated. I couldn't stop it. The Federation will have received it by now."
- [ ] 291. Sidearm: "I wouldn't waste them."
- [ ] 292. First predator kill: "Predatory species. Drawn to thermal signatures." [pause] "There will be more."
- [ ] 293. Sidearm kill variant: "...That works too."
- [ ] 294. Forest edge: "Scanning perimeter. The forest canopy is... extensive. I can't determine the boundaries." [beat] "Adam, I want it on record that I recommended we not come here."
- [ ] 295. Weather shift: "Barometric pressure dropping. This could intensify."
- [ ] 296. Stone cairn: "These stones were placed deliberately. Recently." [quiet] "Adam, we're not the first ones here."
- [ ] 297. Grapple found: "Partially functional. Battery drain, but better than nothing."
- [ ] 298. Vista: "What IS that?" (if player looks at Transformed Lands)
- [ ] 299. Scream reaction: "Energy signature — massive — bearing—" [cut off] "...I think we should keep moving."
- [ ] 300. Adapted sighting: "Adam... that's not native fauna. The biosignature is... partially human." [shaken] "How is that possible?"
- [ ] 301. Adapted chitin only: "This was crafted. By hands. Recently."
- [ ] 302. Cliff face: "The surface is smooth but there are handholds near the top. If you could get higher, or..."
- [ ] 303. Shelter: "This is defensible. Your suit integrity is at [X]%. I'd recommend we stop."
- [ ] 304. Scavenger gift: "I think... it's sharing."
- [ ] 305. Weather activate: "Local atmospheric patterns are... complex. I'll track what I can."
- [ ] 306. Signal: "Adam... the signal you followed. I've been analyzing it since we landed." [pause] "It's not a standard distress beacon. The encoding is... old. Centuries old." "And it's not broadcasting FROM the planet." [longer pause] "It's broadcasting TO it."

### 12.2 — Ambient / Scan Lines (Randomized Pool)
- [ ] 307. Forest L1 lines (8-10 variants): "Unusual root structure." / "That canopy species is filtering the rain." / "Thermal signature, 40 meters, moving away." / "Soil composition unlike anything in our database." / etc.
- [ ] 308. Crash site L1 lines (4-5 variants): "Unknown flora. Bioluminescent." / "Hull stress fractures consistent with atmospheric entry." / "Power residue. Something's draining the cells." / etc.
- [ ] 309. Observation lesson L2: "The spores have a mild regenerative property. The creature knows — it's self-medicating." [pause] "Adam, if we collected those spores..."
- [ ] 310. Pack hunt L2 variants: fight/sneak/watch each have distinct line sets (3-4 lines each)
- [ ] 311. Old ship debris L2: "This alloy is... centuries old. Different manufacture than ours."
- [ ] 312. Predator alpha L3: pack hierarchy + scent-masking chemical data
- [ ] 313. Stone cairn L3: deeper analysis of stone arrangement, cultural significance

### 12.3 — Conditional Lines
- [ ] 314. Grapple fails (no battery): "The grapple needs power, Adam." (first time only)
- [ ] 315. Low suit integrity (<15%): "Adam, we need to find a power source. Soon."
- [ ] 316. First sidearm use: ammo count callout "Seven remaining." / "Ten remaining." etc.
- [ ] 317. Player near gun for predator pack (if sidearm held): "That will be loud." (subtle discouragement)

---

## PHASE 13: POLISH & FEEL
*What separates "functional" from "fun."*

### 13.1 — Screen Effects
- [ ] 318. Fade to black / fade from black (transitions, death, shelter rest)
- [ ] 319. Screen shake: on explosions, predator charge impact, Dragon scream
- [ ] 320. HUD glitch: random pixel displacement on HUD elements, intensity scales with suit damage
- [ ] 321. Vignette: subtle edge darkening, increases in dark/foggy conditions
- [ ] 322. White flash: prologue Phase 4 (before the Eye)

### 13.2 — Particle Effects
- [x] 323. Rain particles [EXISTING — modify for angle/intensity variation]
- [x] 324. Wind streaks [EXISTING — modify for direction system]
- [ ] 325. Fire particles: at crash debris, affected by rain/wind
- [ ] 326. Suit spark particles: periodic crackle from Adam's suit
- [ ] 327. Spore particles: released from bioluminescent plants, drift upward, glow
- [ ] 328. EVE scan particles: blue/green/gold particle burst matching scan level
- [ ] 329. Grapple cable: rendered line from Adam to anchor point, slight sway
- [ ] 330. Water droplets on EVE: during rain, shake off animation
- [ ] 331. Dust/debris: ambient particles in wind, drift in wind direction
- [ ] 332. Predator charge dust: kicked-up dirt particles during rush attack
- [ ] 333. Creature flee particles: scattered leaves/dust when creatures scatter (Dragon scream)
- [ ] 334. Adapted shimmer: subtle color-shift particles where Adapted was standing

### 13.3 — Animation Polish
- [ ] 335. Adam injured walk cycle (first 2 minutes)
- [ ] 336. Adam normal walk/run cycle
- [ ] 337. Adam crouch animation
- [ ] 338. Adam knife swing (3-hit combo)
- [ ] 339. Adam sidearm aim + fire
- [ ] 340. Adam grapple fire + swing + pull
- [ ] 341. Adam pickup item animation
- [ ] 342. EVE boot flicker sequence
- [ ] 343. EVE orbit variations: healthy (smooth circle), damaged (erratic), scared (tight orbit, close to Adam)
- [ ] 344. Scavenger: skitter, freeze, flee, carry-item, gift-drop
- [ ] 345. Herbivore: graze, walk, flee
- [ ] 346. Predator: patrol, charge, bite, death
- [ ] 347. Predator pack: hunt formation, flank movement, eating
- [ ] 348. Adapted: stand, observe, retreat, disappear (color-shift fade)
- [ ] 349. Bioluminescent plant: idle glow, brightens when eaten/approached, spore release

### 13.4 — Juice
- [ ] 350. Hit stop on knife impact (2-3 frames)
- [ ] 351. Hit stop on sidearm impact (1-2 frames)
- [ ] 352. Camera nudge on sidearm fire (small recoil)
- [ ] 353. Squash/stretch on creature death
- [ ] 354. Battery cell pickup: brief glow + HUD bar flash
- [ ] 355. Grapple attachment: camera snap + slight zoom
- [ ] 356. Shelter rest: screen dims warmly (not just fade — color shift to warm tones)
- [ ] 357. Weather transition: smooth 30-second blend, not instant switch

---

## PHASE 14: TESTING & ITERATION
*Make it fun.*

- [ ] 358. Playtest crash site pacing: should feel urgent but explorable (~6 min)
- [ ] 359. Playtest first combat: should feel dangerous but learnable
- [ ] 360. Playtest forest pacing: should feel vast and alive (~20 min with exploration)
- [ ] 361. Playtest grapple feel: should feel industrial and satisfying
- [ ] 362. Playtest weather: should feel atmospheric, not annoying or distracting
- [ ] 363. Playtest predator pack: all 4 paths should feel viable and differently rewarding
- [ ] 364. Playtest The Scream: should make the player stop breathing for a second
- [ ] 365. Playtest Adapted encounter: favorable vs unfavorable should both feel meaningful
- [ ] 366. Playtest shelter: should feel like genuine relief
- [ ] 367. Playtest EVE signal revelation: should land with weight, not confusion
- [ ] 368. Verify scan level visual distinction is clear at a glance
- [ ] 369. Verify HUD is readable but not distracting
- [ ] 370. Verify save/load works correctly with all behavior flags
- [ ] 371. Full 30-minute playthrough: time it, note dead spots, note confusion points
- [ ] 372. Second playthrough (rush): verify it's completable in ~12-15 min without exploration
- [ ] 373. Third playthrough (different choices): verify kill path vs spare path both feel complete

---

## TOTAL: 373 tasks
## Estimated phases of work:
- Phase 0 (Restructure): 1-2 sessions
- Phase 1 (Core systems): 3-5 sessions
- Phase 2 (EVE): 2-3 sessions
- Phase 3 (Creatures): 3-4 sessions
- Phase 4 (Environment): 4-6 sessions
- Phase 5 (Weather): 2-3 sessions
- Phase 6 (HUD/UI): 1-2 sessions
- Phase 7 (Prologue): 2-3 sessions
- Phase 8 (Audio): 2-4 sessions (placeholder pass + polish pass)
- Phase 9 (Behavior tracking): 1 session
- Phase 10 (Level design): 4-6 sessions
- Phase 11 (Dragon scream): 1 session
- Phase 12 (Dialogue): 1-2 sessions
- Phase 13 (Polish): 3-5 sessions
- Phase 14 (Testing): 2-3 sessions

**Total: ~30-50 working sessions**

---

*Check off tasks as completed. Update this file as design evolves.*
*When all 373 are done, minutes 0-30 are shippable. Then we do 30-60.*
