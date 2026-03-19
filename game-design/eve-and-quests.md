# EVE & Quest Design — Progression Spine

*"Without a guiding story, quest system, or world, it's super hard to remain grounded."* — Ethan, 2026-03-19

---

## THE PROBLEM (Ethan's Realization)

We've been building muscles without a skeleton. Combat is tight, movement is SOTN-tier, enemies have physics. But:
- Why does the combat system exist? "I followed the rule of cool."
- What's the player working toward? Unclear.
- What story do the systems serve? Not defined.
- Result: development stagnates because there's no next step that *matters*

**Solution: Build the progression spine FIRST. Every system justifies itself by serving the story chain.**

---

## PROGRESSION SPINE (Start → Finish)

*To be developed: a beat-by-beat map of story progression, keyed to gameplay milestones.*

### Design Constraints
1. **Know the ending before building the middle.** Every quest, every system, every mechanic exists to get the player from beat 1 to the final beat
2. **Chains, not loops.** Each system feeds the next: combat → materials → crafting → new abilities → new areas → new story → combat. No dead ends
3. **No chores.** (Anti-Bloodstained principle — see below)
4. **Player chooses engagement depth.** Find a cool tree? Examine it with EVE or don't. Shouldn't be necessary, but IS rewarded. Sometimes it won't be either
5. **No pauses in gameplay.** Story happens DURING play, not between play. Player decides when to interact, if at all

### SOTN vs Bloodstained: Why Grinding Feels Different
- **SOTN:** You find cool stuff. Most of it isn't necessary. Discovery is the reward. Grinding is *optional* — you do it because shield dashing through enemies is fun, not because a quest told you to
- **Bloodstained:** Item crafting + soul upgrades are locked behind boss kills and enemy grinds. Getting silver bromide to take a picture feels like a *chore* because it IS a chore. The quest is a gate, not a reward
- **The difference:** SOTN rewards the action you're already doing (exploring, fighting). Bloodstained makes you do something you wouldn't choose to do (grind 20 of the same enemy)
- **Our rule:** Never require the player to do something they wouldn't do for fun. Crafting materials should drop from enemies you'd fight anyway, in areas you'd explore anyway

---

## EVE: THE KNOWLEDGE COMPANION

EVE is not a tutorial NPC. She's a scientific mind with personality.

### Core Role
- **Chemist/Scientist:** Deeply knowledgeable about chemicals, periodic table, material science, biology
- **Scanner:** Can examine anything — enemies, corpses, plants, terrain, artifacts, invisible tiles
- **Translator:** Eventually learns the native language, enabling NPC communication
- **Delegate:** Can talk to natives FOR you — relays quest summaries so you can choose: talk yourself or get the digest and keep moving
- **Quest Tracker:** EVE menu shows movement options, quests, world data, undiscovered locations, dialogue threads, mystery clues

### EVE Menu (Settings/Pause)
- Movement options reference
- Active quests & story threads
- Scanned data (bestiary, materials, locations)
- Undiscovered map locations
- Dialogue thread summaries
- Mystery clues / open questions
- Crafting recipes (discovered through scanning + experimentation)

### Design Rules for EVE
- **Hints, not answers.** "This compound is unstable near heat" not "Use fire on the red crystal"
- **Personality over utility.** She has opinions, preferences, curiosity. She gets excited about rare finds
- **Optional depth.** Players who engage with EVE get more out of the game. Players who don't still progress fine
- **Scientific accuracy as flavor.** Real chemistry where possible. Players with science background get bonus enjoyment. Paraffin wax + linseed oil = waterproof jacket (real technique)

---

## CRAFTING PHILOSOPHY

### Anti-Chore Principles
1. Materials drop from things you'd kill/explore anyway
2. Recipes discovered through EVE scanning + experimentation, not quest gates
3. Crafted items open NEW GAMEPLAY, not just stat boosts
4. Crafting should feel like *discovery*, not *shopping*

### Real-World Chemistry Hook
- EVE knows the periodic table. Crafting ties to real chemical properties where fun
- Getting soaked → cold debuff → craft waxed jacket (paraffin wax + linseed oil, historical technique EVE teaches you)
- Shelter crafting from proper leaves (EVE identifies which trees have waterproof leaves)
- This creates a chain: explore → find materials → EVE identifies them → craft tools → access new areas

### Crafting Progression Chain
1. **Early:** Craft basic tools — axe (melee weapon + tree chopping dual use), shelter, fire
2. **Mid:** Bow and arrows, body armor, specialized gear
3. **Late:** Gun and bullets, advanced armor, elemental resistance

### Melee Weapon Durability
- Random melee weapons found along the way that **break at certain stages**
- Creates constant scavenging pressure — you're always looking for the next weapon
- Crafted weapons are more durable (reward for engaging with crafting system)
- Special weapons (story items) don't break

---

## WEAPON SPECIAL MOVES

Most weapons have 1-2 special moves:

| Weapon | Special | Notes |
|--------|---------|-------|
| Sling | Air Slam | Charged attack straight down from air. Brief hover/pause before slam |
| Gun (specific) | Radial Spray | Air-only. Fires in all directions. Massive ammo cost |
| Shield | Block/Parry | Replaces either weapon slot. Key binding customizable |
| Axe | Tree Chop | Dual-use: melee combat + resource gathering |

**Key binding:** Option to swap either weapon slot for a shield. All movement and combat keys rebindable.

---

## INTERACTIVE / SCANNABLE TILES

### Invisible Scannable Tile
- New tile type: invisible to naked eye
- Slight glint effect (some variants don't glint at all)
- EVE can scan them to reveal hidden information, materials, lore
- Generated on enemy kill? (enemy corpses become scannable)
- Environmental placement for secrets and lore fragments

### Enemy Corpse Scanning
- Defeated enemies leave scannable remains
- EVE examines: anatomy, weaknesses, material drops, lore
- Builds bestiary organically through gameplay
- "Animal corpses" — EVE can work on them for crafting materials

---

## ENEMY BEHAVIOR DEPTH

### Swarm (Insect Swarm)
- **EVE insight:** "They likely won't bother you if you walk slowly through them"
- Only aggro on running/fast movement
- Creates meaningful player choice: slow and safe, or fast and fight
- This is the SOTN philosophy — knowledge rewards different playstyles

---

## NPC / NATIVE INTERACTION

### EVE as Translator
- Native language is initially incomprehensible
- EVE learns it progressively through exposure
- Early game: gesture-based communication, EVE interprets body language
- Mid game: partial translation, EVE fills gaps
- Late game: full translation, but EVE can also handle conversations for you

### Delegation System
- Talk to NPC yourself → full dialogue, more story, relationship building
- Send EVE → she gives you a summary of what they wanted + quest info
- **Gameplay purpose:** Player chooses their engagement level. Some people love NPC dialogue. Others want to get on with it. Both are valid
- EVE's summaries should have personality — she editorializes

---

## EXPLORATION PHILOSOPHY

**Goal: no pauses in gameplay. User chooses when to interact.**

- Find something interesting? Examine it with EVE or walk past
- Nothing should be MANDATORY to examine (except rare story-critical moments)
- Examination is REWARDED but not REQUIRED
- Sometimes examining something reveals nothing useful — and that's fine. Keeps it honest
- EVE occasionally prompts: "That's unusual..." (subtle, not intrusive)

---

## NEXT STEP: PROGRESSION MAP

**Priority:** Create a start-to-finish progression map that:
1. Lists every story beat (inciting incident → rising action → climax → resolution)
2. Maps each beat to a gameplay milestone (new area, new ability, boss fight)
3. Shows which systems unlock at each stage (crafting tier 1, EVE translation, shield, etc.)
4. Defines what can be tested in the debug room gym/zoo at each stage

This map becomes the development roadmap. We never ask "what should I build next?" — the next story beat tells us.

---

*"Why is the combat system developed at all? What story purpose does it serve? I don't really know." — This document exists to make sure we always know.*
