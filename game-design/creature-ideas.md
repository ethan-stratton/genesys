
## Box Jellyfish (Water Creature)
- Warm, shallow, coastal water habitat
- Harmless dome (top) — player can touch/stand on safely
- Ultra-deadly tentacles (bottom) — drift below the body
- Tentacles sway with water current/wind
- Unique hitbox: safe zone above, kill zone below
- Requires water creature physics system (not yet implemented)
- Visual: translucent boxy dome, trailing tentacle strands
- Behavior: passive drifter, follows water currents, doesn't chase
- Could be a level hazard rather than an enemy — something you navigate around

## Wingbeater Cross-Screen Movement (Tier 3)
- Wingbeaters can leave their current level to hunt in neighboring outdoor levels
- Will NOT enter caves/interiors — only outdoor levels
- ALWAYS navigate back to nest before nightfall (worldTime > 18)
- Need level metadata: "outdoor" vs "interior" flag per level
- Transit system: creature removed from current level → stored in transit registry → spawns in target level
- Grace period: 2 seconds on level entry, no creatures spawn from edges
- Edge buffer: transit creatures spawn 50px in from boundary
- Population cap: 15-20 creatures per level
