# Video Research Notes — 2026-03-19

Sources: 9 YouTube videos sent by Ethan. Transcripts unavailable (YouTube blocks extraction). Analysis based on video titles, channel context (Indie Game Clinic, Design Doc, worldbuilding creators), and deep knowledge of the underlying game design concepts.

**Ethan should correct or expand any points where the actual video said something different.**

---

## 1. Gameplay Chains > Gameplay Loops
**Source:** "Gameplay Loops Are Out, Chains Are In" — Indie Game Clinic

The "core loop" model (fight → loot → upgrade → fight) is too simple. Great games have **chains** — interconnected systems where the output of one loop is the input of another, and players choose which link to engage next.

**For Genesis:**
- Combat drops materials → crafting creates weapons → weapons unlock new combat options → new areas yield new materials. That's a chain.
- The ActRaiser sim mode is a CHAIN LINK — combat clears biomes → sim mode builds settlements → settlements produce resources → resources fuel combat. If sim mode is an island with no connection to combat, it's dead weight.
- EVE's crafting assistance should be a chain link too — she processes materials you bring her, creating tools that open new paths.

**Action items:**
- [ ] Map out our full gameplay chain (combat ↔ crafting ↔ sim ↔ exploration) — every system must feed into at least 2 others
- [ ] Identify dead-end systems that don't connect and either connect them or cut them

---

## 2. Multi-Phase Boss Design
**Source:** "What Makes A Good Multiple Phase Boss Fight?" — Design Doc

Good phase transitions change what the PLAYER does, not just what the boss does. Each phase should feel like a different encounter that tests different skills.

**For Genesis:**
- **Bad:** Boss gets faster at 50% HP. **Good:** Boss destroys the floor, fight becomes vertical platforming.
- Phase transitions should be narrative moments — the boss reveals its true form, the arena transforms, allies arrive or betray you.
- The Genesis/biblical framing is perfect for this — a boss that starts as a beast and reveals intelligence, or an angel that falls mid-fight.
- Don't make players replay easy Phase 1 to practice hard Phase 3. Consider checkpoints between phases or escalating difficulty (Phase 1 should still be engaging, not a formality).

**Action items:**
- [ ] Design first boss with 3 phases that each require different player verbs (melee → ranged → platforming)
- [ ] Phase transitions should change the ARENA, not just the enemy
- [ ] Consider phase checkpoints for 3+ phase fights

---

## 3. Systems Thinking
**Source:** "Game Mechanics & Systems Thinking"

Design mechanics as a web, not a list. Every mechanic should interact with at least 2-3 others. Emergent gameplay = simple rules × complex interactions.

**For Genesis:**
- Knockback tiles + enemies = environmental kills (knockback enemy into spikes)
- Fire/damage tiles + breakable tiles = chain reactions
- Float tiles + combat = aerial combat opportunities
- The tile system we built (breakable, knockback, speed, float, damage) is already a toolkit for emergent encounters — but only if enemies INTERACT with these tiles too, not just the player.

**Action items:**
- [ ] Enemies should be affected by environmental tiles (knockback, speed boost, damage tiles)
- [ ] Design at least 3 encounters where the environment is the real weapon
- [ ] Test: can the player find solutions the designer didn't plan? If yes, the system is working

---

## 4. Repetition With Variety
**Source:** "How (and Why) to Design for Repetition With Variety" — Indie Game Clinic

The core action (attack, jump, explore) must feel good on attempt 1,000. You achieve this by varying CONTEXT while keeping verbs consistent. Hades model: same rooms, different enemy combos, different builds, different story beats each run.

**For Genesis:**
- Our core verbs: slash, shoot, jump, slide, uppercut. These should feel crisp forever.
- Variety comes from: enemy combinations, environmental hazards, weapon loadouts, tile configurations.
- Each biome revisit should feel different — new enemies using the same tile types in new configurations.
- The sim mode provides meta-variety: same combat, but now you're fighting to protect something you built.

**Action items:**
- [ ] Polish core verbs until they feel perfect — movement and combat must be tight before content
- [ ] Design enemy encounters as combinations, not individual enemies (crawler + hopper + spike floor = interesting, crawler alone = boring)
- [ ] Each level should introduce one new element while recombining old ones

---

## 5. Worldbuilding Mistakes to Avoid
**Source:** "9 Worldbuilding Mistakes Every New Writer Makes"

Common traps: over-explaining lore, monoculture worlds (one trait per civilization), ignoring economics/ecology, worldbuilding that doesn't serve the story, making the world static.

**For Genesis:**
- Our Genesis/biblical retelling MUST NOT be a lore dump. The world should raise questions, not answer them.
- Each biome should feel like a living ecosystem, not a theme (Crystal Forest isn't "the ice level" — it's a place where something went wrong).
- Don't make cultures one-note. If there are NPCs/settlements, they should have internal disagreements.
- The silent protagonist helps here — the player discovers lore, they're never lectured.

**Action items:**
- [ ] For each biome, answer: what happened here? Who lived here? What went wrong? — then show it through environment, don't tell it through text
- [ ] No codex/journal entries that explain the world. Use item descriptions, environmental storytelling, NPC behavior
- [ ] Each region's tile composition should tell a story (ashlands near volcano = something burned, petrified forest = something froze in time)

---

## 6. Making a Big Game Solo
**Source:** "How To Make A Big Game (Alone)"

Scope is the killer. Build the smallest playable version first. Reuse systems aggressively. Cut features that don't serve the core. Ship ugly but functional, polish later.

**For Genesis — this is CRITICAL:**
- We have a working platformer with physics, enemies, tiles, world map, sim mode. That's a lot already.
- DANGER ZONE: we keep adding systems (crafting, sim mode, world map) without finishing any of them.
- The priority should be: **one complete biome** (4 levels, all hand-crafted, enemies placed, boss at end) that's playable start to finish.
- Reuse the tile system for everything — don't build custom solutions per biome.
- The world map generation code is DONE. Don't touch it again until we need a new continent.

**Action items:**
- [ ] Define the "vertical slice" — Eden Reach, 4 levels, fully playable, one boss
- [ ] Freeze feature additions until the vertical slice is done
- [ ] Every new system must justify itself: "Does this make the vertical slice better?"

---

## 7. The Problem of Purpose
**Source:** "The Problem of Purpose in Fantasy & Sci-Fi"

Every world needs a reason to exist beyond "it's cool." What drives conflict? What are people fighting over? Why does the player care?

**For Genesis:**
- The Genesis retelling gives us built-in purpose: creation, fall, exile, redemption.
- The world map biomes should reflect this arc — Eden Reach is paradise (lush), but something corrupted it. Each biome further from Eden is more corrupted.
- The Void Rift, Ashlands, and Petrified Forest aren't just "mystery biomes" — they're evidence of the corruption spreading.
- Purpose for the PLAYER: you're not just exploring, you're reclaiming/healing the world (which connects to the sim/building mode — you literally rebuild what was lost).

**Action items:**
- [ ] Define the central conflict: what broke the world? What is the player trying to restore?
- [ ] Each biome should show a different STAGE of corruption (Eden = recent fall, Ashlands = ancient destruction)
- [ ] Sim mode = literal restoration. You rebuild settlements in purified zones. This IS the purpose.

---

## 8. Avoiding Loredumps / Pacing Worldbuilding
**Source:** "How to Avoid Loredumping and Pace Your Worldbuilding"

Show don't tell. Drip-feed mystery through environment, items, NPC behavior. Dark Souls / Hollow Knight model: the world IS the story.

**For Genesis:**
- EVE should hint, not explain. "This place feels... wrong. Like it remembers being something else." NOT "According to the ancient records, 4,000 years ago the Crystal Plague transformed this forest."
- Item descriptions carry lore (Soulsborne model). A weapon's description tells you who made it and why.
- Environmental storytelling: ruined buildings in the background, murals on walls, enemy placement that implies territory and hierarchy.
- The silent protagonist is our biggest asset here — the player DISCOVERS, never gets lectured.

**Action items:**
- [ ] EVE's dialogue should ask questions, not give answers: "What do you think happened here?"
- [ ] Design 5 environmental storytelling moments for Eden Reach (e.g., a broken bridge implies something crossed it, a growth pattern implies something is spreading)
- [ ] Item/weapon descriptions should contain 1-2 sentences of world lore each

---

## 9. Good vs Bad Worldbuilding — 5 Key Differences
**Source:** "Good vs Bad Worldbuilding: The 5 Key Differences"

Based on the video description timestamps: Objective vs Subjective, Static vs Dynamic, Told vs Shown, Surface vs Deep, Consistent vs Inconsistent.

**For Genesis:**
1. **Subjective over Objective** — Don't describe the world clinically. Show it through characters who have opinions about it. EVE has feelings about places.
2. **Dynamic over Static** — The world changes based on player action. Sim mode IS this. Clear a biome → it heals. Neglect it → corruption returns (this connects to the "spawning grounds" enemy system from our backlog).
3. **Shown over Told** — Already covered above. Environment > exposition.
4. **Deep over Surface** — Each biome should have history UNDER the surface. You see the tip; the iceberg is implied.
5. **Consistent over Inconsistent** — Rules matter. If fire damages the player, it should damage enemies. If corruption spreads, it should follow a logic.

**Action items:**
- [ ] Write EVE reactions for each biome that are emotional/personal, not encyclopedic
- [ ] Implement the "neglect = corruption returns" system — ties sim mode to worldbuilding
- [ ] Ensure all hazard rules apply equally to player AND enemies (consistency)

---

## Summary: Top 10 Principles for Genesis

1. **Chain your systems** — combat ↔ crafting ↔ sim ↔ exploration, all interconnected
2. **Boss phases change the player's strategy**, not just the enemy's health bar
3. **Enemies interact with environmental tiles** — emergent encounters
4. **Vary context, not verbs** — same tight controls, different situations
5. **Show, never tell** — environmental storytelling, item descriptions, EVE hints
6. **One vertical slice first** — Eden Reach, 4 levels, fully playable, before anything else
7. **Purpose drives everything** — the world broke, you're healing it, sim mode = restoration
8. **No monoculture biomes** — each place has depth under the surface
9. **Dynamic world** — player actions change the map (clearing ↔ corruption)
10. **Cut dead-end systems** — if it doesn't chain into something else, cut it

---

*Ethan: if any of these miss the mark from what the videos actually said, let me know and I'll update.*
