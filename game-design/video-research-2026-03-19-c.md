# Video Research — March 19, 2026 (Batch 3)

## Video 1: "The Hand Drawn Graphics Of Silksong" — Acerola
**URL:** https://www.youtube.com/watch?v=au9pce-xg5s

### Key Takeaways

**5 Pillars of Silksong's Graphics:**
1. **Depth of Field** — Background rendered at 1/16th resolution (640×360), progressively blurred and upsampled. Hides low-res backgrounds while selling world scale. Puts focus on the player character.
2. **Parallax Mapping** — Perspective camera positions sprites in 3D space so backgrounds move slower than foreground. Critical for perceived depth in 2D games. *Pixel art games need pixel-perfect parallax* or you get sub-pixel rendering issues.
3. **Particles** — Dust, pollen, grass everywhere. Each uses a tiny texture with Unity's built-in particle tools. Essentially free performance cost on modern hardware. "Add particles to your game."
4. **Post-Processing** — Vignette centered on Hornet (not screen center), bloom (subtle but adds gradient to harsh outlines, prevents flatness), color correction for vibrancy.
5. **Hand-Painted Art** — No shaders replicate hand-authored sprites. Team Cherry imports scans of artist drawings directly into Unity.

**Color Banding Problem & Fix:**
- Silksong suffers from color banding on glow/fog textures — caused by texture compression and magnification
- Human eyes are naturally more sensitive to differences in low brightness values
- Bayer dithering (ordered dithering) doesn't fix it well — pattern is too obvious
- **Interleaved Gradient Noise (IGN)** — one line of shader code, obliterates color banding. The holy grail for debanding and stochastic rendering
- Critical timing: dithering must happen at mesh draw time, not post-processing. Post-processing is "too late" — like trying to clean a stain that's already set
- The fix is literally adding IGN noise to the alpha channel in the transparent geometry shader

**Relevance to Genesis:**
- Our parallax needs to be pixel-perfect since we're pixel art (not painterly like Silksong)
- Particles are essentially free — we should add environmental particles (dust, debris, spores)
- Bloom should be subtle — adds depth without being obvious
- IGN is worth implementing when we have glow/fog effects
- Painter's algorithm (back-to-front) is fine for 2D — "GPUs are really fast"

---

## Video 2: "Gyms, Zoos, and Museums: Your Documentation Should Be In-Game" — Robin-Yann Storm
**URL:** https://www.youtube.com/watch?v=5PJRCz0t7yY

### Key Takeaways

**Core Principle:** Documentation goes out of date because devs update the game, not the docs. Solution: document *in the game itself*, spatially and contextually close to content.

**Three Types of In-Game Documentation:**

1. **Gym** — For character controllers, movement, animation
   - Jump distances (green=easy, orange=hard, red=impossible)
   - Crouch heights, climb edges, vault distances
   - Color-coded test cases for all character-environment interactions
   - "You can't ship a game without hitting the gym" — Jan David Hassel
   - Also useful for smoke testing (run character through overnight, detect stuck spots)
   - Single source of truth for player metrics

2. **Zoo** — For art assets, items, NPCs, VFX, audio, materials
   - Assets laid out visually so you can see scale, lighting, relationships
   - No need to remember asset names — just look and pick
   - Vignettes show how assets are *supposed* to be used together
   - Can be auto-generated (AssetPlacer for Godot)
   - Great for debugging — shader complexity, broken assets instantly visible
   - "Knolling" concept — physical layout of components (like LEGO before building)

3. **Museum** — For technology, systems, shaders, physics, prefabs
   - Live examples of how systems work (cloth sim, destruction physics)
   - Links to API docs for engineers who want depth
   - "Instead of 50 pages on Confluence, load up this level"

4. **Spatial Documentation** (bonus) — Notes, bugs, TODOs placed directly in-game world
   - Zelda: BotW used spatial commenting for their open world
   - Witcher 3's database viewer showed vertex density, foliage counts in top-down view
   - "The world itself becomes the documentation"

**Relevance to Genesis:**
- **Our debug room IS a gym** — we should formalize it with jump distance markers, metric indicators
- We should build an **enemy zoo** — all enemies laid out with stats, behaviors visible
- **Weapon zoo** — all weapons displayed, grabbable, testable
- The spawn menu (P key) is a primitive version of this — could evolve into a proper test environment
- Our design docs are good but will go stale — the game itself should document its own systems

---

## Video 3: "How to make your games look GOOD" — Giant Sloth Games (Short)
**URL:** https://www.youtube.com/shorts/TYx5SgEGemc

### Key Takeaways

**Color Lookup Tables (LUTs) for Color Grading:**
- A 64×64×64 3D texture where each slice is laid out in a 2D grid
- Neutral LUT maps each RGB value to itself (identity function)
- Workflow: screenshot game → adjust colors in Photoshop → apply same adjustments to neutral LUT → export → load in engine
- Engine remaps rendered frame colors through the LUT in real time
- Fragment shader is trivially simple
- Gives you Photoshop-quality color grading at basically zero runtime cost

**Relevance to Genesis:**
- LUT-based color grading could give us a distinct visual identity with almost no performance cost
- Different biomes/areas could have different LUTs (warm desert, cold tech lab, eerie corrupted zones)
- Could pair with the CRT shader we shelved — LUT first, CRT optional
