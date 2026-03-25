# Biome Research — Earth's 14 Biomes → Genesis Adaptation
## Source: "Every Ecosystem on Earth" (2026-03-25)

---

## Earth's 14 Biomes Summary

| # | Biome | Key Traits | Notable Species | Avg Temp | Rainfall |
|---|-------|-----------|----------------|----------|----------|
| 1 | Desert & Xeric Shrublands | Lowest rainfall, 2nd largest | Reptiles thrive, few amphibians | Moderate (extremes vary) | Lowest |
| 2 | Montane Grasslands & Shrublands | Highest elevation | Snow leopard, Tibetan gazelle, Ethiopian wolf | Very low | Low |
| 3 | Flooded Grasslands & Savannas | Low elevation, seasonal flooding | High bird/reptile diversity, migrations | High | Moderate |
| 4 | Tropical Grasslands & Savannas | Hot, between desert and forest | Iconic megafauna, highest bird density after tropical moist | Hottest tier | Moderate |
| 5 | Tropical Dry Broadleaf Forest | Less rain than moist, slightly hotter | High reptile density, Asiatic lions (unique) | Very high | High |
| 6 | Tropical Moist Broadleaf Forest | Extreme rainfall, highest biodiversity | ALL species groups peak here | High | Highest |
| 7 | Mangroves | Smallest biome, coastal, lowest elevation | Birds, reptiles, tigers adapted to water | Hottest | Very high |
| 8 | Tropical Coniferous Forest | Elevated tropical, 15 eco-regions only | Monarch butterfly migration | Cooler tropical | Moderate |
| 9 | Temperate Coniferous Forest | Northern hemisphere, high elevation | Highest mammal density of temperate biomes | Low | Moderate |
| 10 | Temperate Broadleaf & Mixed Forest | Most UNESCO sites (350+), moderate everything | Birds best outside tropics | Average | Above average |
| 11 | Mediterranean Forest | Coastal, warm, low rain | Reptile-rich, unique flora | Warm | Low |
| 12 | Temperate Grasslands | Low rainfall, moderate elevation | Bison, pronghorn, Pampas fauna | Average | Low |
| 13 | Boreal Forest (Taiga) | Largest biome, sub-Arctic | Moose, lynx, grouse, bears | Very low | Low |
| 14 | Tundra | Lowest temp, lowest species richness | Polar bears, caribou, reindeer | Lowest | Near-lowest |
| — | Rock & Ice | Not a biome, extreme elevation/cold | Minimal life, glaciers, volcanoes | Extreme low | Very low |

---

## Key Design Lessons for Genesis

### 1. Biodiversity Follows a Gradient
Real-world pattern: **Tropical moist forest → tropical dry → grassland → desert** is a biodiversity gradient. Species richness drops predictably as you move away from warm+wet.

**Genesis application**: Our alien planet should follow a similar gradient anchored to the Dragon's influence:
- **High Resonance zones** = peak biodiversity (equivalent to tropical moist). Dense, dangerous, teeming.
- **Moderate Resonance** = mixed zones (equivalent to temperate). Balanced risk/reward.
- **Low Resonance** = sparse zones (equivalent to tundra/desert). Few species, resource-scarce, but safer.

### 2. Elevation Creates Distinct Biomes in Small Spaces
Montane grasslands exist because altitude overrides latitude. Same latitude can have tropical forest at base and tundra at peak.

**Genesis application**: Vertical world design. A single area can have multiple biome layers:
- Valley floor: dense growth, high creature density, flooding risk
- Mid-slope: mixed forest, moderate density, shelter locations
- Ridge/peak: sparse, windswept, high Conductivity (electrical storms), few creatures but valuable resources

### 3. Flooding Creates Temporary Biomes
Flooded grasslands are defined by seasonal change. Dry season → wet season completely transforms the landscape, and **animals migrate in response**.

**Genesis application**: Weather events should transform areas:
- Dragon-proximity storms flood lowlands → creatures migrate uphill → player encounters different fauna
- Receding water reveals cave entrances, submerged ruins
- "Seasonal" could be tied to Dragon's movement cycle rather than time

### 4. Mangrove Principle: Tiny Biomes Can Be the Most Extreme
Mangroves are the smallest biome but hottest, wettest, lowest elevation, and home to man-eating tigers adapted to swim. Size ≠ insignificance.

**Genesis application**: Special micro-biomes that are small but memorable:
- Spore vents (fungal zones, tiny but extremely dangerous, unique resources)
- Dragon scar sites (burned/corrupted land, small patches with unique tile reactions)
- Oasis zones in hostile areas (rare water source surrounded by desert, creature convergence point)

### 5. Creatures Specialize to Their Biome
- Reptiles dominate deserts (adapted to heat, low water)
- Amphibians dominate wet forests (need moisture)
- Mammals peak in temperate conifer forests
- Birds peak in tropical moist forests

**Genesis application**: Creature types tied to biome conditions:
| Condition | Dominant Creature Type | Examples |
|-----------|----------------------|----------|
| Dry + Hot (low Viscosity, low Particulate) | Armored/scaled creatures | Desert crawlers, burrowers |
| Wet + Warm (high Viscosity, high Particulate) | Amphibious creatures | Swimmers, ambush predators in water |
| Cold + Elevated (low temp, high Conductivity) | Furred/insulated creatures | Mountain variants, hibernators |
| Dense vegetation (high Particulate) | Ambush predators, canopy dwellers | Wingbeaters, web-spinners |
| Open terrain (low Particulate) | Pack hunters, herd prey | Packs of leapers, grazing herbivores |

### 6. Migration Is the Killer Feature
Almost every biome has a famous migration: wildebeest across the Serengeti, monarch butterflies to Mexico, caribou across tundra, creatures to/from Okavango Delta, Pantanal seasonal shifts.

**Genesis application**: Creature migrations tied to Dragon movement cycle:
- When Dragon moves toward an area → Resonance rises → creatures flee outward
- Player sees herds moving, knows Dragon is coming before weather shifts
- Migration routes = natural pathways player can follow to find new areas
- Blocking a migration route (e.g., with construction) has ecosystem consequences

### 7. Interconnected Biomes Are More Interesting Than Isolated Ones
The video constantly notes how biomes border and interact:
- Desert borders tropical grassland (Kalahari → Okavango)
- Flooded grassland borders mangroves (Everglades)
- Temperate forest borders boreal forest borders tundra

**Genesis application**: No hard biome boundaries. Transition zones:
- Gradual tile palette shifts between areas
- "Edge species" that only appear in transition zones
- Player learns to read transitions as navigation cues

### 8. Unique Indicator Species per Zone
Every biome has a "pride and joy" — a species that defines it:
- Desert → reptiles
- Tundra → polar bears/caribou
- Mangrove → adapted tigers
- Montane → snow leopard

**Genesis application**: Each area needs a signature creature that:
- Is found ONLY there
- Communicates the biome's rules through its behavior
- Gives the player a reason to return (unique drops, lore, scanning value)

---

## Mapping to Genesis's 7 Areas (Stage of the Fall)

Based on `genesis-core-design.md` areas + this biome research:

| Genesis Area | Earth Biome Parallel | Key Atmospheric Vars | Signature Creature | Unique Feature |
|-------------|---------------------|---------------------|-------------------|----------------|
| Crash Site / Surface | Temperate broadleaf forest | Moderate all, low Resonance | Forager crawlers | Tutorial zone, safe |
| Caves & Underground | No Earth parallel (unique) | Zero Conductivity, high Viscosity | Bioluminescent species | Sound-based gameplay |
| Wetlands/Lowlands | Flooded grassland + mangrove | High Viscosity, mod Particulate | Amphibious ambush predators | Flooding events, water puzzles |
| Forest/Jungle | Tropical moist forest | High Particulate, mod Viscosity | Canopy wingbeaters, web-spinners | Vertical exploration, dense |
| Mountain/Ridge | Montane grassland | High Conductivity, low Viscosity | Armored cliff-dwellers | Electrical storms, grapple terrain |
| Dragon's Domain | No Earth parallel (alien) | Extreme Resonance, all vars high | Dragon-corrupted variants | Cipher required, reality warped |
| Deep Ruins | Rock & Ice equivalent | Low everything except Resonance bleed | Ancient automated defenses? | Federation archaeology |

---

## Weather ↔ Biome Integration

Our existing weather system (`weather-atmosphere-system.md`) already has the 4 global variables. This biome research suggests:

1. **Base values per area are already designed** — confirmed by the research. Different biomes = different atmospheric baselines.
2. **Add creature behavior modifiers**: When Viscosity > 0.6, amphibious creatures become more active. When Conductivity > 0.7, all creatures seek shelter.
3. **Flooding mechanic**: When Viscosity exceeds threshold in lowland areas, water level rises tile-by-tile. Creatures pathfind upward. Player can get trapped.
4. **Particulate affects visibility**: Already in weather doc. Confirmed by real biome research — dense vegetation = reduced sightlines.
5. **Migration triggers**: Dragon movement → Resonance spike → creatures flee in predictable directions → player reads the ecosystem.

---

## What's New vs What We Already Had

### Already designed (confirmed by this research):
- ✅ 4 atmospheric variables (Conductivity, Viscosity, Particulate, Resonance)
- ✅ Per-area base values
- ✅ Creature-to-creature awareness
- ✅ Trophic food chain (producers → consumers → apex)
- ✅ Sound-based detection
- ✅ Goal-based AI (food, shelter)

### New ideas from this research:
- 🆕 **Elevation-based biome layering** within single areas (valley → mid → peak)
- 🆕 **Migration as readable game mechanic** (creature movement = Dragon proximity telegraph)
- 🆕 **Micro-biomes** (spore vents, dragon scars, oases) — small but extreme
- 🆕 **Transition zones** between areas with unique edge species
- 🆕 **Flooding as area transformation** tied to Viscosity threshold
- 🆕 **Signature species per area** (unique, defines the zone, rewards scanning)
- 🆕 **Creature type ↔ atmospheric condition mapping** (reptile-likes in dry, amphibious in wet, etc.)
- 🆕 **Seasonal cycles** tied to Dragon movement rather than time

---

## Priority Implementation Notes

For the first 30 minutes, most of this is background design. What matters NOW:
1. Crash site should feel like "temperate broadleaf" — moderate, safe, forgiving
2. Caves should feel alien — no Earth parallel, bioluminescent, sound matters
3. First hint of biome diversity when player sees distant areas from a vista point
4. Forager crawlers near crash site should have visible goals (eating plants, returning to nest)
5. Weather should be calm near crash, increasingly unstable as player explores deeper
