# World Map — Interconnected Hexagonal Structure

## Philosophy
Dark Souls 1 Firelink Shrine model. The Wreckage (Adam's crashed ship) is the central hub.
Every area connects to at least 2 others. You're never more than 2-3 rooms from a shortcut
back to the ship. The world wraps around itself — late-game areas connect back to early ones
via locked shortcuts that open from the other side.

**NOT a level select. NOT a metroidvania gate map.** It's a living interconnected world.
You walk between rooms. Doors are physical places. The map reveals as you explore.

## Hex Layout — 7 Areas (Stages of the Fall)

```
                    ┌─────────┐
                    │  NATIVE │
                    │  RUINS  │
                    │ Humility│
               ┌───┤  3 rooms├───┐
               │   └────┬────┘   │
          ┌────┴───┐    │   ┌────┴────┐
          │ LIVING │    │   │  BONE   │
          │ FOREST │    │   │  REEF   │
          │ Wonder │    │   │Struggle │
          │ 5 rooms├────│   │ 4 rooms │
          └───┬────┘    │   └────┬────┘
              │    ┌────┴────┐   │
              ├────┤WRECKAGE ├───┤
              │    │  Shock  │   │
              │    │  ⚓ HUB │   │
              │    │ 4 rooms │   │
              │    └────┬────┘   │
          ┌───┴────┐    │   ┌────┴────┐
          │TRANSFRM│    │   │  DEEP   │
          │ LANDS  │    │   │  RUINS  │
          │ Accept │    │   │  Truth  │
          │ 3 rooms│    │   │ 4 rooms │
          └───┬────┘    │   └────┬────┘
               │   ┌────┴────┐   │
               └───┤DRAGON'S ├───┘
                   │SANCTUM  │
                   │Transcend│
                   │ 2 rooms │
                   └─────────┘
```

Total: ~25 rooms across 7 areas.

## Area Details

### 1. WRECKAGE (Shock) — Central Hub
**Rooms:** Ship Interior, Crash Perimeter, Debris Field, Upper Hull
- Adam wakes up here. Ship is half-buried.
- Ship Interior = save point, EVE's charging dock, inventory, logs
- Crash Perimeter = tutorial area (first 30 minutes)
- Debris Field = scattered ship parts, faulty drones
- Upper Hull = vantage point, first view of the world
**Connections:** Forest (west), Bone Reef (east), Native Ruins (north via climb), Deep Ruins (south via collapse)

### 2. LIVING FOREST (Wonder) — Largest Area
**Rooms:** Forest Edge, Canopy, Root Caves, Fungal Depths, Ancient Grove
- First real biome. Dense, alive, overwhelming.
- Canopy = vertical, requires grapple or bio climb
- Root Caves = underground shortcut back to Wreckage
- Fungal Depths = bioluminescent, visibility reduced
- Ancient Grove = Adapted presence (observation phase)
**Connections:** Wreckage (east), Native Ruins (north), Transformed Lands (south shortcut, locked from this side)

### 3. NATIVE RUINS (Humility) — Gated Early
**Rooms:** Ruin Entrance, Inner Sanctum, Observatory
- Requires bio-tier wall climb or cipher phase step to access fully
- First real contact with Adapted culture
- Observatory = telescope/viewpoint showing the whole world layout
- Inner Sanctum = lore dump about previous visitors
**Connections:** Forest (south), Bone Reef (east), Wreckage (south via rappel shortcut)

### 4. BONE REEF (Struggle) — Combat Area
**Rooms:** Tidal Flats, Coral Maze, Predator Den, Reef Summit
- Hostile. Apex predators. Water mechanics.
- Tidal Flats = time-based flooding (Dragon's Viscosity variable)
- Coral Maze = navigation puzzle, enemies hidden
- Predator Den = mini-boss gauntlet
- Reef Summit = connects back to Wreckage via zipline shortcut
**Connections:** Wreckage (west), Native Ruins (north), Deep Ruins (south)

### 5. DEEP RUINS (Truth) — Late-Game
**Rooms:** Descent, Flooded Chambers, Machine Room, Dragon's Memory
- Underground. Claustrophobic. Tech-suppression strongest here.
- Machine Room = ancient tech, not Federation — something older
- Dragon's Memory = flashback area (cipher vision reveals the past)
- Flooded Chambers = swimming + water combat
**Connections:** Wreckage (north via elevator shortcut), Bone Reef (north), Dragon's Sanctum (south)

### 6. TRANSFORMED LANDS (Acceptance) — Late-Game
**Rooms:** Threshold, Resonance Garden, Eve's Trial
- The planet at its most alien. Physics behave differently.
- Cipher abilities amplified but unstable
- Resonance Garden = weather at maximum intensity
- Eve's Trial = EVE's own challenge/growth moment
**Connections:** Forest (north shortcut), Deep Ruins (east), Dragon's Sanctum (south)

### 7. DRAGON'S SANCTUM (Transcendence) — Final Area
**Rooms:** Approach, The Sanctum
- Only 2 rooms. The Approach is the gauntlet. The Sanctum is the Dragon.
- Every area connects here eventually — it's the bottom of the hex.
- No enemies. Just the Dragon, the weather, and your choices.
**Connections:** Deep Ruins (north), Transformed Lands (north)

## Connection Rules

1. **Every area connects to at least 2 others** (no dead ends)
2. **Shortcuts open FROM the far side** (Dark Souls elevator/door model)
3. **Wreckage connects to 4 areas** (Forest, Reef, Ruins above, Deep below)
4. **Dragon's Sanctum is at the bottom** — all paths eventually lead down
5. **Cross-connections exist within areas** (rooms link to rooms in adjacent areas)
6. **No fast travel** — you walk everywhere, but shortcuts make it fast

## Room Size & Transition

Each room = one level file. Typical room is 3-5 screens wide, 2-3 screens tall.
Transitions are physical doorways (caves, paths, doors) — the green exit zones.
No loading screens between rooms in the same area (seamless where possible).
Cross-area transitions get a brief fade (the current _transitionActive system).

## Shortcut Types

- **Elevator** (ship tech, requires power) — Wreckage ↔ Deep Ruins
- **Rappel/zipline** (grapple) — Native Ruins → Wreckage, Reef Summit → Wreckage
- **Root tunnels** (bio) — Forest Root Caves → Wreckage
- **Phase gate** (cipher) — Transformed Lands ↔ Forest
- **One-way drops** — become two-way once you find the other side

## First 30 Minutes Mapping

Minutes 0-30 take place entirely in Wreckage (Ship Interior + Crash Perimeter):
- Wake up in Ship Interior
- Explore Crash Perimeter (tutorial)
- Find exit to Forest Edge (but don't enter yet — beat sheet ends at forest threshold)

## Player Map Display

In-game map builds as you explore:
- Undiscovered rooms = hidden
- Visited rooms = filled hex
- Current room = highlighted
- Connections shown only if both rooms discovered
- Save points / shelters marked
- EVE annotations appear as you scan areas

## Implementation Notes

WorldGraph.cs already has the data structure. Next steps:
1. Create the ~25 room entries in worldgraph.json
2. Create skeleton level files for each room (empty but connected)
3. Build the hex map renderer (replaces current overworld screen)
4. Wire room discovery to save system
