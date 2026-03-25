# GENESYS — Weapons Design Document
# Last updated: 2026-03-25

*Weapons across three tiers: Tech, Bio, Cipher. Each tier has a distinct feel.*
*Weapons interact with the elemental tile system — they're ecosystem tools, not just damage.*

---

## TIER PHILOSOPHY

### Tech Tier
- Grounded in realism. Manufactured, electrical, mechanical.
- Uses Battery as resource. Higher-tier tech requires a player-built generator (EVE guides crafting with specific chemicals/materials).
- Feel: Industrial, crackling, humming. Sparks and arcs.

### Bio Tier
- Almost entirely non-technical. Sticks, clubs, bows, blades, bone, chitin.
- Organic materials, crafted from the planet's ecosystem.
- Feel: Primal, organic, brutal. Crunches and thuds.

### Cipher Tier
- Reality-bending. Quantum mechanics, gravity manipulation, dimensional distortion.
- Characteristic crimson-purple energy signature.
- Feel: Eerie, otherworldly. Hums that feel wrong. Visual distortion.

---

## WEAPONS LIST

### Starting Weapon
| Weapon | Tier | Description | Ammo | Notes |
|--------|------|-------------|------|-------|
| **Combat Knife** | Bio | Short-range melee, fast 3-hit combo. Slashes fungus, pries containers. | ∞ | Always available |

### Ranged — Tech Tier
| Weapon | Tier | Description | Ammo/Cost | Notes |
|--------|------|-------------|-----------|-------|
| **Sidearm** | Tech | Standard projectile pistol. Mouse-aimed. 12 rounds/clip, reload time. Gunshot attracts enemies. | 12/clip, ∞ clips (for now) | First ranged weapon found. Muzzle flash + shell casing particles |
| **Shock Pulse** | Tech | Close-range EMP burst. Damages + stuns enemies. Electrifies water tiles, arcs through metal. | Battery: 2% per pulse | Like a sci-fi shotgun. Rewards aggressive play |
| **Arc Tether** | Tech | Tesla arc / Jacob's ladder. Hold-to-fire chain lightning between conductors. Auto-targets nearest enemy, chains to others in water/metal contact. | Battery: 5%/sec (heavy drain) | High-tier tech. Requires player-built generator unlock |
| **Incendiary Rounds** | Tech | Sidearm mod. Creates burning tile on bullet impact. Ignites Oil tiles, spreads fire. | Same as sidearm ammo | Upgrade/attachment, not separate weapon |
| **Plasma Torch** | Tech | Short-range continuous beam. Melts Ice tiles, ignites Oil tiles, damages through thin walls. | Battery: 3%/sec | Utility + combat. Essential for ice biome traversal |

### Ranged — Bio Tier
| Weapon | Tier | Description | Ammo/Cost | Notes |
|--------|------|-------------|-----------|-------|
| **Pheromone Dart** | Bio | Enrages target enemy, causing it to attack surrounding enemies. High chance of triggering aggro chain. | Craftable (limited) | Crowd control. Turn enemies against each other. Environmental storytelling — you learned the planet's chemistry |
| **Tranquilizer Dart** | Bio | Puts target to sleep for X seconds. Allows sneak past or setup. | Craftable (limited) | Stealth option. Synergizes with observation playstyle |
| **Bow** | Bio | Silent ranged weapon. No aggro sound (unlike sidearm). Lower damage but stealthy. | Craftable arrows | Counterpart to sidearm — silent vs loud tradeoff |

### Ranged — Cipher Tier
| Weapon | Tier | Description | Ammo/Cost | Notes |
|--------|------|-------------|-----------|-------|
| **Gravity Charge** | Cipher | Slow-moving gravity distortion orb. Pulls nearby enemies toward detonation point before exploding. Crimson-purple energy. | Limited (rare resource) | Mini black hole ballistics. Crowd-pull + AoE |
| **Quantum Flak** | Cipher | Bullets exist in superposition — simultaneously everywhere and nowhere across their range. Covers vast distance, hits probabilistically. | Limited (rare resource) | Bizarre, unsettling weapon. Enemies take damage from bullets that were never visibly fired at them |

### Melee — Bio Tier
| Weapon | Tier | Description | Notes |
|--------|------|-------------|-------|
| **Club/Mace** | Bio | Heavy melee. Slow, high damage, knockback. Bone or wood. | Breaks BreakableGlass easily |
| **Bone Blade** | Bio | Medium melee. Faster than club, good range. Carved from large creature bone. | Upgrade from knife |
| **Spear** | Bio | Long reach melee + throwable. Can pin enemies to walls. | Retrievable after throw |

---

## TIER COMBINATIONS (Hybrid Weapons)

### Tech + Bio
| Weapon | Description | Notes |
|--------|-------------|-------|
| **Electro-Spine Launcher** | Bone spines (bio) with battery-charged tips (tech). Embeds in enemies and delivers shock damage over time. Sticks in water to electrify area. | Combines bio crafting with tech power source |
| **Venom Cell** | Bio toxin loaded into a tech delivery system. Grenade that releases organic acid cloud. Corrodes metal tiles, poisons enemies. | Bio chemicals + tech engineering |
| **Fungal Battery** | Bioluminescent spores harnessed as power cells. Lower capacity than tech batteries but renewable (harvest from plants). | Resource hybrid — powers tech weapons with bio materials |

### Tech + Cipher
| Weapon | Description | Notes |
|--------|-------------|-------|
| **Phase Grapple** | Grapple module enhanced with cipher energy. Can attach to surfaces through walls/obstacles. Brief phase-shift on pull. | Exploration upgrade with combat utility |
| **Disruption Field** | Tech shield generator warped by cipher energy. Creates a zone where projectiles slow to a crawl (time dilation). | Defensive. Enemies' ranged attacks become trivially dodgeable inside the field |
| **Overclocked Arc** | Arc Tether supercharged with cipher energy. Lightning becomes crimson-purple, ignores conductivity — arcs through air, stone, anything. | Late-game power fantasy |

### Bio + Cipher
| Weapon | Description | Notes |
|--------|-------------|-------|
| **Entangled Pheromone** | Cipher-enhanced pheromone that creates quantum entanglement between two creatures. Damage to one transfers to the other. | Bizarre, powerful crowd control |
| **Living Grenade** | Cipher-mutated spore cluster. Thrown, then grows rapidly into a temporary hostile plant entity that attacks everything nearby. | Area denial. The plant is hostile to player too — timing matters |
| **Resonance Bow** | Bio bow firing cipher-infused arrows. Arrows phase through walls and materialize inside targets. | Stealth + cipher. See-through-walls assassination tool |

### Tech + Bio + Cipher (Tri-Tier)
| Weapon | Description | Notes |
|--------|-------------|-------|
| **The Synthesis** | End-game weapon. Mechanical frame (tech), organic power core (bio), cipher-warped barrel. Fires adaptive projectiles that change behavior based on what they pass through — water makes them electric, oil makes them incendiary, air makes them phase-shift. | THE late-game weapon. Rewards mastering all three tiers and the elemental system |
| **Genesis Cannon** | Requires all three trees maxed. Fires a beam that temporarily resets an area to its "primordial" state — enemies revert to passive, tiles regenerate, hazards neutralize. 60-second cooldown. | Thematic capstone. "Genesis" — creation, not destruction |

---

## PLAYER-BUILT GENERATOR (Tech Tree Gate)

- Higher-tier tech weapons (Arc Tether, Plasma Torch) require more power than the suit battery alone provides
- EVE identifies specific chemicals/materials in the environment through scanning
- Player crafts a portable generator from gathered components:
  - **Conductive ore** (found in cave/mine areas)
  - **Thermal catalyst** (harvested from geothermal vents or lava-adjacent areas)
  - **Circuit salvage** (from ship wreckage)
- Generator unlocks: higher battery capacity, faster recharge at shelters, access to tier-2 tech weapons
- Design intent: gives scanning and exploration a concrete mechanical reward

---

## ELEMENTAL INTERACTIONS (Weapons × Tiles)

| Weapon | + Water/Puddle | + Oil | + Ice | + Metal | + Wood |
|--------|---------------|-------|-------|---------|--------|
| Shock Pulse | Electrifies area | No effect | No effect | Conducts further | No effect |
| Arc Tether | Chain through water | No effect | No effect | Extended range | No effect |
| Incendiary | Steam (obscures) | Fire spread | Melts → water | Heats (dmg zone) | Burns |
| Plasma Torch | Evaporates → steam | Ignites | Melts | Heats | Burns |
| Gravity Charge | Pulls water toward center | Pulls oil | Cracks | Warps | Splinters |
| Pheromone Dart | Diluted (reduced effect) | Enhanced (oil carries scent) | No effect | No effect | Sticks (area effect) |

---

## IMPLEMENTATION PRIORITY (First 30 Minutes)

1. **Sidearm** — 12 rounds/clip, infinite clips, reload, muzzle flash, shell casings, gunshot aggro
2. **Combat Knife** — Already exists, needs combo polish
3. **Shock Pulse** — Battery-powered, close range, elemental interaction
4. **Incendiary Rounds** — Sidearm mod, fire tile creation

*Everything else is mid-to-late game. Document now, build later.*

---

*This document defers to genesis-core-design.md for any conflicts.*
