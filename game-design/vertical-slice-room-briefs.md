# Vertical Slice — Room Briefs & Progression
# The First 20 Minutes: Crash → Wreckage → Forest Edge

*Every room has ONE job. Mood first, mechanics second.*

---

## Resizing Rooms

Rooms are easy to resize. In the level JSON:
- `bounds` → `left/right/top/bottom` (pixel dimensions)
- `tileGrid` → `width/height` (tile count, 32px each)
- Change both to match: `width = (right - left) / 32`, `height = (bottom - top) / 32`
- Tile grid auto-creates from bounds if missing
- **Expanding**: safe, new tiles default to Empty
- **Shrinking**: tiles at cut edges are lost
- Camera, player bounds, enemy snapping all read from bounds dynamically

No code changes needed. Just edit the JSON (or we can add +/- buttons to the editor later).

---

## The Flow

```
[1] Ship Interior ──→ [2] Crash Perimeter ──→ [3] Debris Field
                                                      │
[6] Observation Clearing ←── [5] Forest Edge ←── [4] Upper Hull
```

Total play time: ~15-20 minutes for a first-time player.
Each room: 2-4 minutes.

---

## Room 1: Ship Interior
**File:** `Content/levels/ship-interior.json` (exists, needs redesign)

### Brief
| | |
|---|---|
| **Job** | Teaching + Rest (tutorial hub) |
| **Mood** | Claustrophobic, damaged, intimate. You wake up alone. |
| **Teaches** | Walk, jump, crouch (collapsed beam), interact (terminals), equip (knife) |
| **Size** | 120×40 tiles (3840×1280px) — 2 decks, ~12 screens wide |

### Kishōtenketsu
1. **Introduce:** Wake up in medical bay. Tight space. Walk left. EVE reboots (glitchy, flickering). HUD fades in. "Systems... partial recovery. Adam. You're alive."
2. **Develop:** Upper corridor — collapsed beam blocks the path. Crouch to pass. First lesson: you can crouch. Sparking wires overhead (atmosphere, not damage).
3. **Twist:** Cargo bay — scavenger runs across the floor carrying a piece of your ship. It's fast, small, harmless. The planet is already inside your ship. Find the knife on a crate.
4. **Conclude:** Cockpit — EVE interfaces with the ship's log. "The distress signal we followed... it's centuries old. And it was broadcasting TO this planet, not from it." Exit hatch leads outside.

### Key Items
- **Knife** (cargo bay, on a crate — first weapon)
- **CO's Message** (engine room terminal — optional lore, needs battery cell from crew quarters)
- **Battery Cell** (crew quarters locker — optional, teaches "lock before key" if player finds terminal first)

### Creature Placement
- 2-3 Scavengers (non-hostile, flee on sight, carrying debris)
- They establish: this planet has life, it's already scavenging your stuff

### EVE Lines
- Boot: "Systems... partial recovery. Running on backup."
- First scan (sparking wire): "Hull integrity at 12%. The atmosphere is degrading our alloys."
- Scavenger: "Local fauna. Already adapted to metabolize our materials. Resourceful."
- Cockpit reveal: "Adam... that signal. It's not a distress call. It's an invitation."
- Exit: "I'm reading open atmosphere ahead. Atmospheric composition is... breathable. Barely."

### Connections
- **Exit right** → Crash Perimeter (hatch leads to cave exterior)
- **Exit down** (locked) → Engine room leads to hull breach (surface-east, for later)

### Design Notes
- Camera stays tight (zoomed in slightly) for first 30 seconds, then eases out
- No enemies that can hurt you. Ship is safe. This is the womb.
- Lighting: emergency red in corridors, blue-white in medical bay, dark in cargo
- Player should feel: "My ship is dead. I need to get out."

---

## Room 2: Crash Perimeter
**File:** `Content/levels/crashsite.json` (exists, needs redesign)

### Brief
| | |
|---|---|
| **Job** | Teaching + Discovery |
| **Mood** | Disorientation. Bright light after dark ship. Alien beauty mixed with wreckage. |
| **Teaches** | Environmental hazards (spore cloud), scanning, multiple-solution problems |
| **Size** | 80×60 tiles (2560×1920px) — vertical cave/ravine, ship embedded in cliff wall |

### Kishōtenketsu
1. **Introduce:** Exit the ship hatch onto a rocky ledge. Bright sky above, deep cave below. Ship hull visible embedded in the cliff face. Alien plants growing on the wreckage. EVE: "We're... outside." First passive scan triggers on a glowing plant — "Bioluminescent. No Earth analogue."
2. **Develop:** Descend the ledge (easy platforming, wide platforms). Toxic spore cloud blocks the main path down. EVE: "Airborne particulates. Toxic at this concentration." Three solutions: (a) find the gap in the cloud where wind blows, (b) crouch-walk under it (it floats at head height), (c) sprint through and take minor suit damage. Player discovers their preferred approach.
3. **Twist:** Below the spore cloud — a flat area with scattered ship debris. A dead creature lies here (crashed into the ship wreckage, killed on impact). EVE can scan it: first bestiary entry. "Exoskeletal arthropod. Herbivorous mandible structure. This one died on impact — our impact." Your crash killed something. The planet was here first.
4. **Conclude:** Cave opens up. You can see DOWN into a deep chasm (beautiful, bioluminescent, clearly explorable later — grapple gate). You can see RIGHT toward the debris field outside the cave. The path forward is right. The mystery is down.

### Key Items
- None required. Optional: **Data fragment** on a piece of ship hull (lore about Adam's mission, foreshadows why he followed the signal)

### Creature Placement
- 1 dead creature (scannable, not alive) — the impact casualty
- 2 Foragers on the walls (if wall-walking works well) — alive, harmless, crawling on the cave walls. Establishes bugs are everywhere.

### EVE Lines
- Exit ship: "External atmosphere confirmed. Gravity 1.03 standard. We're... somewhere."
- Spore cloud: "I wouldn't breathe that. Your suit's filtration is compromised."
- Dead creature scan: "Herbivorous. Exoskeletal. Killed by blunt force trauma — our landing. We crashed into its world."
- Chasm look-down: "Significant depth. I'm reading bioluminescent organisms. We'd need proper equipment to descend."
- Path right: "Open terrain ahead. I'm detecting debris scatter patterns — more of our ship."

### Connections
- **Exit left** → Ship Interior (back through the hatch)
- **Exit right** → Debris Field (cave mouth opens to the surface)
- **Visual-only** → Deep cave below (grapple gate — player can see but not reach)

### Design Notes
- Vertical layout. The cave is a wound in the cliff — ship is lodged in it.
- "Lock before key": the deep chasm is visible and beautiful. Player WANTS to go there. Can't yet. This is the first locked door.
- The dead creature is emotional, not mechanical. You killed something by arriving. The planet was alive before you.
- First time the player sees alien life (foragers on walls). They should feel: "This place is alive and I'm the intruder."

---

## Room 3: Debris Field
**File:** `Content/levels/debris-field.json` (NEW)

### Brief
| | |
|---|---|
| **Job** | Challenge (first real combat) |
| **Mood** | Exposed. Vulnerable. Beautiful but dangerous. |
| **Teaches** | Combat, weapon switching, enemy behavior observation, EVE tactical scanning |
| **Size** | 100×35 tiles (3200×1120px) — wide, relatively flat. Open ground with scattered wreckage. |

### Kishōtenketsu
1. **Introduce:** Emerge from the cave onto the planet's surface. Wide open. Sky visible. Alien vegetation everywhere — tall grass-analogue, bulbous plants, things that pulse with light. Ship debris scattered across the ground (hull plates, cargo containers, a snapped wing section). It's beautiful and lonely. Walk forward. EVE scans ambient life: "Microbial density is extraordinary. This soil is more alive than any ecosystem on record."
2. **Develop:** Halfway across, a Hopper spots you and hops away nervously. Non-hostile but startled. EVE: "Skittish. Herbivore. It's more afraid of us than—" Then a Leaper appears on a rock outcrop ahead. It's watching you. Not attacking. Sizing you up. EVE: "That one's different. Predatory posture. Mandibles designed for—" It charges.
3. **Twist:** First real fight. The Leaper is alone, manageable with the knife. But when you kill it (or if you take too long), two more emerge from behind wreckage. Now it's a real encounter. If the player is observant, they notice the Hopper fleeing the same direction the Leapers came from — the ecosystem reacts coherently. EVE mid-fight: "Weak point — the joint between thorax segments!" (scanning reveals weakness during combat, teaching the scan-in-combat loop)
4. **Conclude:** After the fight, scan the Leaper corpse. EVE gives full readout. Ahead: a cargo container with a **Battery Cell** inside (if you didn't find the one in the ship). The field ends at a cliff/ramp leading UP to the ship's upper hull section, which broke off and landed at a higher elevation.

### Key Items
- **Battery Cell** (in damaged cargo container — provides reason to search wreckage)
- Optional: **Ship Log Fragment** (on a cracked data pad near the wing section)

### Creature Placement
- 1 Hopper (ambient, flees toward the leaper area — foreshadowing)
- 3 Leapers (1 visible on rock, 2 hidden behind wreckage — ambush)
- 2-3 Foragers (on debris, harmless, crawling over ship hull — the planet is reclaiming your technology)
- 1 Scavenger (visible briefly, runs into a crevice with a piece of ship tech — callbacks to the ship interior scavengers)

### EVE Lines
- Surface emergence: "Open sky. Atmospheric shimmer suggests a dense ionosphere. This planet has weather."
- Hopper: "Herbivore. Low threat. It was here first — we're in its territory."
- Leaper (pre-fight): "Predatory. Fast. Watch the—" (interrupted by charge)
- Mid-fight scan: "Target the joints. The carapace is rigid but the segments have gaps."
- Post-fight: "Heart rate elevated. Yours, not mine. That was... educational."
- Battery find: "Standard EVE-compatible cell. 40% charge remaining. Better than nothing."

### Connections
- **Exit left** → Crash Perimeter (cave mouth)
- **Exit right/up** → Upper Hull (ramp/climb to elevated wreckage)

### Design Notes
- This is the WIDEST room so far. After the tight ship and vertical cave, the openness should feel like relief and danger simultaneously.
- Scattered wreckage serves as cover during the Leaper fight — player naturally uses the environment.
- The foragers crawling over your ship debris = the "moldy worldbuilding" principle. The planet is already digesting your technology.
- Combat should feel scrappy, not heroic. You have a knife. These things are fast. It should feel like survival.
- The Hopper fleeing toward the Leapers is kishōtenketsu WITHIN the room — the ecosystem tells you something is there before you see it.

---

## Room 4: Upper Hull
**File:** `Content/levels/upper-hull.json` (NEW)

### Brief
| | |
|---|---|
| **Job** | Landmark + Discovery (the vista moment) |
| **Mood** | Awe. Scale. "Oh my god, this world is enormous." |
| **Teaches** | World geography (where to go), the Transformed Lands exist (distant visual), lock-before-key (cliff to Ruins visible) |
| **Size** | 60×50 tiles (1920×1600px) — tall but narrow. Vertical climb up broken hull, wide platform at top. |

### Kishōtenketsu
1. **Introduce:** Climb the broken hull section (it's lodged at an angle against the cliff, forming a ramp). Metal platforms, some unstable (platform tiles that crumble — or just look precarious). EVE notes: "The upper hull section. Structural integrity 3%. Don't trust the metal."
2. **Develop:** Midway up, a small alcove in the cliff face. A Bird perches here. It watches you, then flies away toward the forest. First bird sighting. EVE: "Avian analogue. Observing us. Possibly territorial... or curious." Inside the alcove: optional scannable wall markings. Ancient. Not from your ship. Someone was here before.
3. **Twist:** Reach the top. The camera PULLS BACK. Wide shot. The player can now see: the Living Forest stretching west (canopy, massive trees, movement in the distance), the Transformed Lands on the far horizon (wrong colors, geometric patterns, something VAST moving), and above — the sky. Two moons. An alien sky. EVE goes quiet for a moment. Then: "Adam... this world has been inhabited for millennia. We're looking at an active biosphere with..."  She trails off. "We need to move."
4. **Conclude:** The path forward descends from the hull into the forest canopy. A gentle slope down. Before you go: look NORTH — a sheer cliff face. At the top, barely visible, structures. Native Ruins. Unreachable from here (wall-climb gate). The lock is shown. The forest is the only way forward.

### Key Items
- None required. Optional: **Ancient Wall Marking** (scannable, EVE can't fully translate — "Pre-technological inscription. I'll need more data points to decode this.")

### Creature Placement
- 1 Bird (ambient, perches then flies away — leads your eye toward the forest)
- 1 Wingbeater visible in the FAR DISTANCE (silhouette against the sky, massive, circling lazily — establishes apex predators exist without threatening the player)

### EVE Lines
- Climbing: "Careful. This alloy wasn't designed to be scaffolding."
- Alcove markings: "These aren't natural erosion patterns. Someone carved this. A long time ago."
- Vista moment: (pause) "...Adam. This isn't a dead world. This is an old one."
- Transformed Lands glimpse: "That region — the spectral signature is unlike anything in my database. The energy readings are..." (trails off)
- Ruins visible: "Structures on that cliff face. Artificial. But we can't reach them from here."
- Path to forest: "The forest canopy is dense. Limited visibility once we're under it. Stay alert."

### Connections
- **Exit down/left** → Debris Field (back down the hull)
- **Exit right/down** → Forest Edge (descend into the canopy)
- **Visual-only** → Native Ruins (visible on cliff, unreachable — wall-climb gate)
- **Visual-only** → Transformed Lands (distant horizon, ominous)

### Design Notes
- This room exists for ONE MOMENT: the vista. Everything builds to it.
- The camera pull-back should be dramatic. If we can widen the camera zoom for this room (or this trigger specifically), do it.
- The Wingbeater silhouette in the distance is crucial. The player sees something massive and thinks "I never want to fight that." They will, eventually.
- The Ruins on the cliff = lock before key. The player SEES Act 2 content and wonders.
- This is the emotional transition: from survival ("I'm stranded, everything is broken") to wonder ("This world is ancient and alive and I don't understand it").
- Keep it QUIET. Few enemies. Wind sounds. Let the view do the work.

---

## Room 5: Forest Edge
**File:** `Content/levels/forest-edge.json` (NEW)

### Brief
| | |
|---|---|
| **Job** | Mood + Teaching (atmosphere shift, observation as mechanic) |
| **Mood** | Wonder mixed with unease. Beautiful and you don't belong. |
| **Teaches** | Observation (watching creature behavior reveals useful info), ecosystem as system, path-splitting |
| **Size** | 120×40 tiles (3840×1280px) — wide, horizontal. Dense canopy above, dappled light. |

### Kishōtenketsu
1. **Introduce:** Descend from the hull into the canopy. The atmosphere TRANSFORMS. Dappled light filtering through massive leaves. Alien sounds. The color palette shifts — warm greens, bioluminescent blues, organic purples. Everything is alive. Vines move. Plants pulse. EVE: "Biodensity is... I need to recalibrate my sensors." The player walks through an alien forest for the first time. Let them just walk and look.
2. **Develop:** Come across a clearing. A group of Hoppers is grazing near a plant cluster. One picks a berry-analogue, eats it. Another does a little hop-dance (their idle behavior). If the player approaches slowly, they don't flee. If the player runs, they scatter. EVE observation: "They're selecting specific plants. Avoiding the red-tipped ones. Self-medicating? Or the red ones are toxic." **This teaches:** some plants are dangerous. Observation reveals information that mechanics don't.
3. **Twist:** Path splits. LEFT goes down into a darker area — fungal, bioluminescent, damp. A crawling sound. Something moves in the dark. EVE: "Low visibility. I'd recommend against—" The path is viable but intimidating. RIGHT continues along the surface — well-lit, more vegetation, clearly the "intended" path. But the player who goes LEFT finds a hidden **Grapple Module** earlier than expected (sequence break reward for bravery). Both paths converge at Room 6.
4. **Conclude:** Weather begins to shift. Clouds visible through canopy gaps. The light changes. EVE: "Barometric pressure dropping. Rain incoming." The forest reacts — creatures start moving to shelter, plants close up. The player feels: time is passing, the world has rhythms, I should find shelter too.

### Key Items
- **Grapple Module** (LEFT path only — hidden in fungal area, on a dead explorer's remains. Not FROM the player's ship. Someone else was here. EVE: "This isn't our technology. It's... similar, but the manufacturing signatures don't match.")
- Optional: **Medicinal Plant** (scannable near the Hoppers — EVE identifies it as a healing compound, teaches the player that scanning plants has practical value)

### Creature Placement
- 4-5 Hoppers (grazing group in clearing — behavioral teaching moment)
- 2-3 Foragers (on tree trunks, ambient, crawling)
- 1 Skitter (LEFT path, dark area — startles the player, harmless but scary)
- 1 Stalker (LEFT path ceiling — if the player is observant, they see it watching them. If not, it drops down as they grab the grapple. Fight or flee.)

### EVE Lines
- Enter forest: "The canopy is filtering 80% of UV. This ecosystem is layered — I'm counting at least four distinct biological strata."
- Hopper observation: "Watch. They're avoiding the red-tipped plants. Either learned behavior or instinct. Either way, we should too."
- Path split (left): "Fungal growth. Low visibility. Significant moisture. Something lives in there."
- Path split (right): "Open terrain continues. Moderate threat level. I'd recommend—" (player chooses)
- Grapple find: "Adam. This equipment isn't from our ship. Someone else was here."
- Dead explorer: "No biological remains. Just equipment. Either they left... or something removed the body."
- Weather shift: "Pressure's dropping. This planet has aggressive weather cycles. We should find cover."

### Connections
- **Exit left** → Upper Hull (back up to the vista)
- **Exit right (both paths)** → Observation Clearing
- **LEFT sub-path** → Fungal area (same room, loops back to main path near the right exit)

### Design Notes
- This room is about ATMOSPHERE, not challenge. The combat is optional (stalker on left path only).
- The Hopper grazing scene is the game's first "teaching through observation" moment. This is the Genesis scanning philosophy in action — you learn something useful by watching, not by being told.
- The path split is critical. LEFT = risk + reward + lore (the dead explorer foreshadows the cycle of visitors). RIGHT = safe + pleasant. Neither is wrong. The game respects both choices.
- The dead explorer is a BIG narrative seed. No body. Just equipment. EVE can't explain it. This is the first hint that others crashed here.
- Weather shifting at the end creates urgency without a timer. The player FEELS like they should move.

---

## Room 6: Observation Clearing
**File:** `Content/levels/observation-clearing.json` (NEW)

### Brief
| | |
|---|---|
| **Job** | Discovery + Pressure (climax of the slice, emotional landing) |
| **Mood** | Awe → tension → relief. The world is bigger than you thought, and something knows you're here. |
| **Teaches** | Shelters/saving, weather as gameplay, the Adapted exist, predators as environmental pressure |
| **Size** | 100×45 tiles (3200×1440px) — wide clearing, elevated rocky outcrop, shelter cave. |

### Kishōtenketsu
1. **Introduce:** Both paths from Forest Edge converge here. Open clearing under the canopy — a rare gap in the trees where sky is visible. Rain begins. Light rain first — atmospheric, beautiful. Droplets on leaves. EVE shakes off water (visual personality). Everything is wet and glistening. A stone cairn sits in the center of the clearing. Clearly arranged — not natural. EVE: "These stones were placed deliberately. Recently — within the last few years."
2. **Develop:** Approach the cairn. EVE scans it. "The arrangement follows a pattern. Not random. This is communication. Someone is telling us..." She pauses. On the cairn: a small carved object. Chitin, not stone. Shaped like a simplified humanoid figure. Not from the ship. Not from the ancient ruins. Something CURRENT made this. EVE: "Adam. We're not alone here. And whatever left this... it knows about us." The Adapted. First sign.
3. **Twist:** Rain intensifies. Storm. Lightning. Visibility drops. Then — from the tree line — a Leaper pack. Three of them. Circling. They're not attacking yet. They're stalking. Sizing you up. But the storm is driving them toward the clearing (they're seeking shelter too). EVE: "Multiple hostiles. Predatory formation. I count three — no, four. Adam, we need cover." The player can fight (hard, 4 Leapers in rain with reduced visibility) or RUN toward the rocky outcrop where a shelter cave is visible.
4. **Conclude:** Reach the shelter. A small cave — natural, but someone has been here before. Dried plant bedding. A crude fire pit (cold). The carved chitin figure's twin sits on a ledge inside. The Adapted know about this shelter. They USE it. **Save point.** The rain hammers outside. Through the cave mouth, lightning illuminates the Leapers pacing outside — then leaving. They gave up. EVE, quietly: "Someone prepared this for us. Or for whoever came next." Beat. "The distress signal we followed... how many others followed it too?" FADE. END OF VERTICAL SLICE.

### Key Items
- **Chitin Figure** (on the cairn — scannable. EVE can't fully analyze it. "Organic material, carved with precision tools. The style doesn't match any database entry. This is new.")
- **Chitin Figure Twin** (inside shelter — confirms the Adapted are active, watching, possibly helping)
- **Save Point** (shelter = save/rest. First save outside the ship.)

### Creature Placement
- 4 Leapers (emerge during storm — stalking pack, driven by weather. If player fights, they're tough but beatable. If player runs to shelter, they pace and leave)
- 2 Birds (flee when storm starts — realistic behavior, establishes weather affects creatures)
- 1 Scavenger (inside the shelter, flees when player enters — it was using the shelter too. Connects to the ship scavengers — these things are EVERYWHERE)

### EVE Lines
- Cairn: "Deliberately placed. A territorial marker? A message? A welcome?"
- Chitin figure: "Carved chitin. Precision work. This was made by hands. Or something like hands."
- Storm: "Atmospheric discharge increasing. We're exposed. Find cover, Adam."
- Leaper pack: "They're not attacking — they're evaluating. Multiple predators hunting as a unit. They're intelligent."
- Shelter discovery: "Structural integrity acceptable. Someone has used this as a waypoint. The bedding is recent."
- Twin figure: "A second one. Identical craftsmanship. This isn't abandoned — it's maintained."
- Final: "How many others followed that signal, Adam? How many found this shelter?"

### Connections
- **Exit left** → Forest Edge (back to the path split area)
- **Exit right** → [LOCKED — future Room 7: deeper forest, currently blocked by fallen tree / dense growth]
- **Shelter** → Save point, rest, end of vertical slice

### Design Notes
- The storm is the setpiece. This room earns its weather system.
- The Leaper pack should feel TERRIFYING — not because they're hard (they are), but because it's dark, raining, visibility is low, and there are four of them. The player should feel hunted.
- Running to the shelter is the INTENDED solution for most first-time players. Fighting is the skilled player's path.
- The shelter reveal is the emotional payoff. You've been surviving. Someone else survived too. They left breadcrumbs. You're not alone — but you don't know if that's good or bad.
- The twin chitin figures are the game's first TRUE mystery. Not the Dragon (that's macro). This is personal. Someone is watching. Someone prepared for your arrival.
- END the vertical slice on a question, not an answer. The player should close the game thinking about who left those figures.

---

## Progression Summary

| Room | Player Gains | Player Learns | Mystery Introduced |
|------|-------------|---------------|-------------------|
| 1. Ship Interior | Knife, EVE | Move, crouch, interact | Signal was an invitation |
| 2. Crash Perimeter | First scan | Hazards have multiple solutions | Deep chasm exists (grapple gate) |
| 3. Debris Field | Battery, combat confidence | Fighting, scanning in combat | The ecosystem is coherent |
| 4. Upper Hull | World awareness | Where to go, what exists | Ruins on cliff, Transformed Lands, something massive flies |
| 5. Forest Edge | Grapple (optional), observation skill | Watch before acting, path choices matter | Someone else was here (dead explorer) |
| 6. Observation Clearing | Save point, shelter knowledge | Weather = real, shelters = safety | The Adapted are watching you |

### Emotional Arc
```
Confined → Disoriented → Exposed → Awed → Wondering → Hunted → Safe (but not alone)
```

### Ability Gate Map
- **Now accessible:** Ship Interior, Crash Perimeter, Debris Field, Upper Hull, Forest Edge, Observation Clearing
- **Grapple gate (shown in Room 2):** Deep cave system
- **Wall-climb gate (shown in Room 4):** Native Ruins
- **Lantern gate (implied in Room 5 LEFT path):** Deeper fungal areas
- **Cipher gate (not shown yet):** Transformed Lands

---

## Build Order

1. **Ship Interior** — redesign existing level. Tutorial flow. Scavengers + EVE boot.
2. **Crash Perimeter** — redesign existing crashsite. Vertical cave. Spore hazard. Dead creature.
3. **Debris Field** — new level. First combat. Wide open ground.
4. **Upper Hull** — new level. Vista moment. Camera work.
5. **Forest Edge** — new level. Atmosphere. Path split. Observation teaching.
6. **Observation Clearing** — new level. Storm setpiece. Shelter. Adapted mystery.

Each room can be built and tested independently. Wire transitions as we go.

---

*"The game should always be a little bit unknowable." — This slice ends with two carved figures and a question.*
