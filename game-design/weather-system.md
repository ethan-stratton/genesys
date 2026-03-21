# Weather System — Genesis

## Core Philosophy
Weather isn't cosmetic. It's gameplay. Every weather type should change how you play, what routes are viable, and what threats emerge. Combine them for emergent danger.

## Atmospheric Pressure System
Underlying simulation drives weather transitions. Pressure drops → storms form. Volcanic activity → pressure spikes → acid rain. Not random — the planet has a circulatory system and players who scan enough will learn to predict weather.

## Weather Types

### Precipitation
- **Rain** — reduced visibility at distance, slippery surfaces, fills water pools. Crawlers slow, flying enemies ground themselves
- **Heavy Rain** — flooding begins. Low areas become impassable without swimming/waterproofing
- **Hail** — direct damage to unshielded player. Breaks fragile structures. Crawlers take shelter under overhangs (exploitable)
- **Acid Rain** — volcanic byproduct. Damages player, corrodes certain armor. Creates acid puddles. Plants die and change the ecosystem
- **Volcanic Ash** — reduced visibility (like fog but orange-tinted). Accumulates on surfaces. Breathing hazard over time

### Electrical
- **Lightning** — random strikes target highest point. Player on elevated terrain = vulnerable. Metal armor attracts. Can power ancient machinery
- **Storm** — rain + lightning + wind combined. Dark clouds, reduced visibility, dramatic

### Atmospheric
- **Fog** — heavy fog decreases visibility to ~100px around player. EVE thermal scan cuts through it. Predators love fog
- **Wind** — pushes player movement, affects projectiles. Strong wind prevents certain jumps. Debris hazard
- **Typhoon** — extreme wind + rain. Player slides on surfaces. Small enemies blown away. Terrain destruction

### Temperature
- **Heatstroke** — in volcanic/desert biomes. Stamina drain, blur effects. Water consumption mechanic. Shade = recovery
- **Freezing** — ice biomes. Movement slows gradually. Need heat sources or insulated armor

### Geological (weather-adjacent)
- **Earthquake** — screen shake, falling debris, opens new passages, collapses others. Enemies stumble. Triggered by volcanic activity or boss events
- **Landslide** — slope terrain collapses. Dynamic terrain destruction. Rain-triggered in mountain biomes
- **Lava Flow** — slow-moving deadly terrain change. Forces retreat/rerouting. Creates obsidian when hitting water (new platforms!)
- **Flooding** — water level rises over time during heavy rain. Low tunnels become submerged. Changes available routes dynamically

## Combination Effects

| Weather A | Weather B | Result |
|-----------|-----------|--------|
| Acid Rain | Flooding | **Acid Flood** — low areas become acid pools. Devastating |
| Rain | Cold | **Ice Storm** — surfaces freeze, hail forms |
| Volcanic Ash | Rain | **Mud** — movement speed halved, crawlers stuck |
| Lightning | Water/Flood | **Electrified Water** — instant death in water |
| Wind + Volcanic Ash | — | **Sandstorm** — zero visibility, abrasion damage |
| Earthquake + Rain | — | **Mudslide/Landslide** — terrain collapses |
| Heatstroke + Wind | — | **Dust Devil** — localized tornado hazard |

## Volcanic Weather Chain (Real Science)
Volcanic eruption → ash cloud (visibility) → SO₂ release → acid rain → vegetation death → ecosystem collapse → new hostile creature behavior. This isn't just weather, it's a BIOME EVENT that transforms the area for multiple play sessions.

Reference: Real volcanic acid rain forms when sulfur dioxide (SO₂) and hydrogen sulfide (H₂S) from eruptions combine with atmospheric water. Produces sulfuric acid (H₂SO₄). Can persist for weeks after major eruptions.

## Weather Research Notes
- Acid rain real-world: pH < 5.6, caused by SO₂ and NOₓ. Kills aquatic life, damages structures, strips soil nutrients
- Volcanic lightning (dirty thunderstorm): volcanic ash particles create static electricity → lightning WITHIN the ash cloud
- Microbursts: sudden intense downdrafts. Could be a rare weather event — sudden wind slam that pins player to ground
- Fog types: radiation fog (ground cooling), advection fog (warm air over cold surface), volcanic fog (vog)
- Real flood cascades: rain → soil saturation → flash flood. Time delay makes it strategic (you hear rain above, flood comes later)

## Design Rules
1. Every weather type must have at least one gameplay consequence
2. Every weather type must have at least one player countermeasure (armor, item, ability, or terrain use)
3. Weather combinations should create emergent gameplay, not just stacked damage
4. Weather should change creature behavior (not just player experience)
5. Biome identity partly defined by dominant weather patterns
6. EVE should provide weather forecasting at higher scan levels
7. Some quests/areas only accessible during specific weather (knowledge gates: "scan the rain")
