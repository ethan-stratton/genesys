# Video Research — 2026-03-22

## Video 1: "How to Keep Players Engaged (Without Being Evil)" — Game Maker's Toolkit (Mark Brown)
**URL:** https://www.youtube.com/watch?v=hbzGO_Qonu0

### Core Framework
Mark Brown identifies **5 engagement pillars** that keep players playing WITHOUT psychological manipulation:

### 1. PACING — The rhythm of gameplay types
- Games have **pillars** (combat, climbing, puzzles, cutscenes, exploration)
- Swap between them constantly — never linger too long on one type
- Modulate **intensity**: calm → intense → calm. Too much of either = boring or exhausting
- **Uncharted 2 example**: puzzle → firefight → exploration → train battle → calm village → siege. Almost impossible to put down
- Linear games can control pacing tightly; open-world games need enough activity variety so players can self-modulate

### 2. NOVELTY — New ideas, constantly
- Mario games: you never know what the next level will bring
- Even within a single pillar (platforming), novel mechanics keep engagement high
- The key is **consistently introducing new ways to play**

### 3. MYSTERY & ANTICIPATION — Teasing what's coming
- **The Witness**: impossible door early on → burns in your mind → keeps you playing to solve it
- **Dark Souls**: Sen's Fortress giant door → you MUST see what's behind it
- **Hollow Knight**: environment teases powers you don't have yet
- Teasing extends engagement time BETWEEN drops of new content. Economical!
- **Narrative mystery**: Firewatch kept players going just to find out the ending. Surprisingly few games nail this

### 4. LONG-TERM GOALS — Progress toward something meaningful
- **Stardew Valley**: messy farm → dream farm. Sustains hours of toil
- Why it works:
  - **Self-expression**: make it YOUR way, set YOUR goals
  - **Short-term milestones**: community center, house upgrades — immediate targets toward long-term dream
  - **Strategic planning**: not mindless trudging, but making choices about crops/seasons/partners
  - **Exponential growth**: money → seeds → more money → better tools → more money (positive feedback loop)
  - **System optimization**: manual watering → sprinklers → automation (Factorio does this even better)
  - **Fantasy of power**: skill tree where you look forward to using ALL skills to wreck shop

### 5. COMPELLING CHALLENGE — Pitch-perfect difficulty
- Flow state: not too easy (boring), not too hard (stressful)
- RE4 uses dynamic difficulty to maintain the sweet spot
- Failure isn't bad IF: runs are short, you feel yourself improving, next attempt is different
- Mix challenge types: reflexes, problem-solving, spatial awareness, decision-making

### Mark's personal ranking:
"Nothing gets me going like **novel experiences teased through mystery and anticipation**. Dark Souls, Metroid, The Witness — impossible to put down."

---

## Applying GMTK Engagement Pillars to Genesis (First 30 Minutes)

### PACING — Already strong, needs tuning
Our beat sheet already modulates well:
- **Crash site** (0-6 min): Exploration → discovery (knife) → environmental puzzle (fungus) → first combat
- **Forest** (6-28 min): Observation → pack encounter (choice) → traversal (grapple) → THE SCREAM → quiet discovery (Adapted) → shelter

**Improvement opportunities:**
- [ ] Ensure no single pillar runs more than ~3 minutes without a shift
- [ ] After The Scream (intense), the Adapted encounter should be VERY calm — contrast sells impact
- [ ] The crash site might be TOO consistently calm. Add a brief spike: a drone malfunctions and zaps Adam (suit spark moment), resolved in 10 seconds but breaks the monotony
- [ ] Weather shift at minute 14 should coincide with a gameplay shift (arriving at a new area?)

### NOVELTY — Needs more distinct "new things"
Current novel introductions: knife → EVE reboot → scanning → first creature → sidearm → fungus puzzle → predator → forest → grapple → weather → pack hunt → scream → adapted
That's ~12 novel moments in 30 minutes. Good density!

**Improvement opportunities:**
- [ ] Each novel introduction should feel mechanically distinct, not just narratively different
- [ ] The grapple is the biggest mechanical novelty — make sure it arrives at a moment of exploration hunger (after the player feels constrained by jump-only traversal)
- [ ] Consider: what if the forest has ONE environmental mechanic the crash site doesn't? (e.g., bioluminescent plants you can interact with — touch them and they light up, revealing hidden paths)

### MYSTERY & ANTICIPATION — Genesis's STRONGEST suit
Already built into the design:
- **The signal**: broadcasting TO the planet. Why? Players will need to know
- **The Dragon**: only seen as a red eye, then heard as a scream. Maximum mystery
- **The Adapted**: barely glimpsed, human-ish. What ARE they?
- **The cliff face**: Native Ruins visible but unreachable. What's up there?
- **The Transformed Lands**: geometric shapes on the horizon. Unsettling. What IS that?
- **EVE's beacon line**: "The Federation will have received it by now." What does THAT mean?

This is already doing exactly what Dark Souls/Hollow Knight do — showing locked doors, unreachable areas, mysterious entities.

**Improvement opportunities:**
- [ ] Make the vista moment (canopy platform) explicitly show ALL mysteries simultaneously — player sees the cliff, the storm, the geometric shapes, and knows they'll go to each one
- [ ] Consider ONE visible but unreachable thing in the crash site itself (a sealed door in the ship hull that requires a code/tech — tease for return visit)
- [ ] EVE's signal revelation should raise MORE questions than it answers

### LONG-TERM GOALS — Weakest in first 30 min (by design)
In the first 30 minutes there's no farm to build, no skill tree to fill. The long-term goal is implicit: survive, understand, escape(?).

**This is fine.** The first 30 minutes are the HOOK, not the progression system. But we should PLANT seeds:
- [ ] EVE could mention suit modules that could be repaired/upgraded (fantasy of future power)
- [ ] The grapple anchor points in later areas hint at traversal evolution
- [ ] Scan log filling up is a subtle long-term goal in itself — completion instinct
- [ ] The Adapted chitin piece is useless NOW but suggests future significance

### COMPELLING CHALLENGE — Needs careful tuning
- Solo predator (minute 7): should be hard but learnable. 3-4 knife hits, 2 sidearm shots. Player needs to learn dodge timing
- Pack encounter (minute 18): should feel dangerous — 3 predators flanking. Multiple valid approaches = player agency
- NO encounter in first 30 minutes should feel unfair or impossible

**Improvement opportunities:**
- [ ] Make sure the solo predator teaches a lesson the pack encounter uses (charging = dodge → counterattack pattern)
- [ ] The pack encounter should add ONE new thing: flanking (solo predator doesn't flank)
- [ ] Include at least one "challenge type" shift: fungus puzzle is problem-solving, predator is reflexes, pack is decision-making

---

## Video 2: "I Studied Character Design — Here's (almost) EVERYTHING I know." — Cuora
**URL:** https://www.youtube.com/watch?v=4iw8XLusQ7s

### Core Principles

### 1. CHARACTER DESIGN = VISUAL COMMUNICATION
- Design should tell about your character **without words**, **within milliseconds**
- "Show don't tell" applies to visual design as much as writing
- First impression: friendly/hostile? Grumpy/energetic? Culture, occupation, financial situation?
- **Difference between "pretty" and "good"**: aesthetically pleasing ≠ good character design. Good design = TELLS you who the character is

### 2. PLAN BEFORE DESIGNING
- Flesh out your character concept BEFORE drawing
- For main characters/villains: plan ahead, don't just thumbnail randomly
- Too many themes → hard to read. Have a **clear direction**
- **Brainstorming method**: key concept in center → associated words → expand → combine
  - Example: "pirate" → sea, ships, treasure, rebellion → squid → Davy Jones
- **Coherent concepts**: themes should be BOUND together by a unifying idea
  - Good: rockstar + science + lightning (all connected)
  - Bad: queen + bee + rainbow + technology (where's the thread?)

### 3. SILHOUETTE — The Foundation
- Silhouette is **step 1** of character design, not an afterthought
- Should be **recognizable** without any detail
- Test: if you fill the character pure black, can you still tell who they are?
- Complex ≠ good silhouette. Sometimes simple is more iconic
- TF2-style: every character instantly recognizable from silhouette alone
- Accessories, weapons, hair contribute to silhouette identity

### 4. SHAPE LANGUAGE
- **Circles** = friendly, approachable, soft (Kirby, Baymax)
- **Squares** = stable, strong, reliable (Minecraft Steve, Heavy from TF2)
- **Triangles** = dynamic, dangerous, aggressive (villains, sharp edges)
- Combine shapes for nuance: square body + triangular details = strong but dangerous
- Shape language creates **subconscious reading** — viewers feel the character before understanding them
- Can be applied to posture, clothing silhouette, hair, accessories

### 5. COLOR THEORY
- Colors carry psychological associations (red=passion/danger, blue=calm/cold, etc.)
- **Color harmony**: complementary, analogous, triadic, etc.
- **Value hierarchy**: areas of light/dark guide the eye and create visual interest
- Muted palettes = cold, stale, reserved characters (intentional low-energy)
- High contrast = dynamic, attention-grabbing
- Intentional color choices: a villain in warm colors subverts expectations but needs REASON

### 6. PROPORTION & EXAGGERATION
- Exaggeration makes characters more expressive and memorable
- BUT lack of exaggeration can also be character — a quiet, human-proportioned character reads as fragile, real, grounded
- "Not every silhouette has to punch you in the eyes. Sometimes less is more."
- Wings shielding a body = vulnerability, isolation — STILLNESS IS STORYTELLING

### 7. DETAILS & COMPLEXITY
- Overdetailing CAN be intentional: overwhelming personality, alien complexity (Bayverse Transformers)
- Level of detail affects how viewers perceive the character
- Simple ≠ good, complex ≠ bad. It depends on INTENT

### 8. SUBVERSION
- Good subversion is **purposeful** — comedic, thematic, or critical
- Bad subversion: goes so far the original intent is unreadable
- Example: glutton character who is thin → clever if the theme is overindulgence, not overeating. BUT if you can't tell what she represents, the subversion went too far

### 9. THE META-PRINCIPLE: INTENT
- "Everything you do to a design — detail, concept, readability, shape, color — EVERYTHING affects how a viewer perceives your character"
- You CAN break rules if you have a REASON that enhances character/story
- Good idea + poor execution = still fails. Intent isn't a shield against bad craft

---

## Applying Character Design Principles to Genesis

### ADAM (Player Character)
**Narrative:** Military-industrial pilot, compassionate to a fault, literally shedding technology over the game

**Design approach:**
- [ ] **Silhouette evolution**: Start bulky (suit) → gradually slimmer as tech pieces are removed → ends human-proportioned
- [ ] **Shape language**: Start SQUARE (military, reliable, protected) → transition to softer shapes as bio-tier takes over
- [ ] **Color**: Start metallic grey/blue (cold, industrial) → exposed skin/bio-materials warm the palette
- [ ] **For first 30 min**: Adam is mostly suit. Should read as "soldier out of his element" — square silhouette, rigid posture, but with small human touches (the way he favors his injured side, hesitation before jumping)

### EVE (AI Companion Orb)
**Narrative:** Ship AI in a floating orb, evolving consciousness, parallels Adam's journey but reaches understanding FIRST

**Design approach:**
- [ ] **Shape**: Circle (friendly, approachable). This is correct for a companion
- [ ] **Color states**: Clean blue/white (healthy) → flickering (damaged) → dark (offline)
- [ ] **Silhouette must be distinct from ALL enemies** — no creature should look orb-like
- [ ] **Size matters**: Small enough to not block gameplay, large enough to notice expressions (glow changes, movement patterns)
- [ ] **Her "personality" IS her movement**: smooth orbit = confident, erratic = worried, tight orbit = scared. The character design IS the animation

### THE DRAGON
**Narrative:** Ancient guardian consciousness, corroding, tragic. Was once protector, now fraying. Intelligence fading

**Design approach:**
- [ ] **First impression (THE EYE)**: Single red mechanical eye on black. TRIANGLE shape language — sharp, dangerous, ancient
- [ ] **Must be ICONIC from silhouette alone** — this is the most important single design in the game
- [ ] **Color**: Red + deep dark metallics. NOT bright — aged, corroded, but still glowing with residual power
- [ ] **Subversion potential**: When players finally see the full Dragon, it should be BEAUTIFUL, not just scary. Tragic beauty — grand but crumbling. The "subversion" is that the most dangerous thing on the planet is also the most magnificent
- [ ] **Proportions**: Massive. The eye in the prologue should feel like just ONE PART of something incomprehensibly large

### THE ADAPTED (Humanoid Descendants)
**Narrative:** Descendants of previous visitors, non-verbal, environmental presence

**Design approach:**
- [ ] **Silhouette**: Thin humanoid — clearly NOT animal, but uncanny. Something about the proportions is off (longer limbs? Different posture?)
- [ ] **Color**: Camouflage — shifts to match environment. When you DO see them clearly, their "base" colors should be natural/organic
- [ ] **Shape language**: Mix of human (relatable) and organic curves (alien). NOT triangular (they're not aggressive by nature)
- [ ] **The chitin piece they leave behind**: should be clearly CRAFTED — recognizable shape language of intentional design, not random natural growth

### CREATURES (Scavenger, Herbivore, Predator)
**Trophic system — each tier should be visually distinct in silhouette**

- [ ] **Scavenger (decomposer)**: SMALL, low profile, circle-ish (non-threatening), fast movement = character through animation
- [ ] **Herbivore**: MEDIUM, soft rounded shapes, gentle, clearly not dangerous from silhouette
- [ ] **Predator**: LARGER, triangular elements (jaw, claws, posture), aggressive silhouette. Should read "danger" before you see any details
- [ ] **Pack predators**: Same species as solo predator but pack formation creates a DIFFERENT silhouette — the group reads as one threat
- [ ] **ALL creatures should be alien** but follow Earth logic enough to be readable (4+ legs = animal, upright = sentient, small + fast = prey, large + angular = predator)

### ENVIRONMENT AS CHARACTER
- [ ] **Crash site**: Sharp, angular, industrial. Metallic colors. SQUARE shapes (human-built, rigid)
- [ ] **Forest**: Organic curves, warm bioluminescence, CIRCLE shapes (safe-ish, natural, alive)
- [ ] **The contrast between crash site and forest is itself character design** — it visually tells the story of tech vs nature
- [ ] **Transition zone**: Where angular ship debris meets organic forest growth. The visual blend = the game's theme in one frame

---

## AM2R Source Code Research

### What AM2R Is
- Fan remake of Metroid II in style of Zero Mission, released 2016 by DoctorM64
- DMCA'd by Nintendo, community continued development
- Source code released publicly (stripped of copyrighted assets)

### Technical Reality: NOT Directly Usable
- **Language**: GameMaker Language (GML) in GameMaker: Studio 1.4
- **Our stack**: C# / MonoGame
- Code is reverse-engineered bytecode reconstruction — "messy, uses unusual solutions"
- No assets included (stripped for legal reasons)
- Uses paid shader assets (palette swapper, GameBoy shader)

### What We CAN Study (Design Patterns, Not Code)
The value isn't copying GML → C#, it's understanding HOW a polished Metroid-style game structures:
1. **Room transitions** — how they handle screen-by-screen vs scrolling camera
2. **Enemy behavior state machines** — patrol → detect → attack → cooldown patterns
3. **Environmental hazards** — acid, spikes, lava implementation patterns
4. **Save system** — checkpoint placement philosophy
5. **Metroidvania gating** — how abilities unlock areas
6. **Boss patterns** — phase transitions, attack telegraphing
7. **Palette swap system** — their Retro Palette Swapper for area color themes (we can do LUT-based equivalent)
8. **Map system** — how the interconnected world is tracked

### Recommendation
Clone the repo to study structure and design decisions. Don't try to port GML code.
The design PHILOSOPHY is valuable; the code implementation is not transferable.

```
git clone https://github.com/AM2R-Community-Developers/AM2R-Community-Updates.git
```
