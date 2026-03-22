# GENESIS — Core Design Document (Living Draft)
# Last updated: 2026-03-22

*This is the authoritative design document. Everything else defers to this.*

---

## IDENTITY STATEMENT

Genesis is a 2D action-exploration platformer about crashing on an alien planet, losing everything, and discovering that the catastrophe was necessary for your growth. The player experiences the Fall — not as theology, but as feeling.

The game's innovation is a world with layers of meaning that reveal themselves through observation, where the act of understanding IS the progression, and where communities form around interpreting what they found.

---

## WHY SOMEONE PLAYS THIS OVER SUPER METROID

Super Metroid is about isolation and survival. Genesis is about **understanding and becoming.** The world isn't just alive — it has opinions about you. The Dragon isn't just a boss — it's a question. EVE isn't just a companion — she's the relationship that gives everything meaning.

---

## THE CRASH (Prologue)

Adam is a high-ranking explorer who receives a distress signal from a planet no one has returned from. EVE recommends aborting. Adam overrides her — "Someone's down there."

On approach, the planet's tech-suppression field activates. Systems fail cascading. EVE screams to pull up. Adam can't. The crash is the consequence of Adam's compassion meeting the planet's nature.

**The crash is Adam's FAULT.** His best quality (courage/compassion) causes his worst outcome. EVE told him not to. She doesn't say "I told you so" but it hangs between them.

The Dragon didn't attack — it intercepted. Like a parent taking car keys. Not to destroy Adam, but to ground him. Adam doesn't know this. He thinks he was attacked. It takes the entire game to understand the crash was an arrival, not an accident.

---

## THE DRAGON

### Nature
- Has a purpose it didn't choose: forcing growth through destruction
- Has done this to hundreds of worlds, thousands of visitors over millennia
- Can't stop being what it is — compelled to test, break, challenge
- Self-aware enough to suffer from its own nature
- Deepest desire: to have ONE person look at it and say "I understand what you are, and I don't hate you for it"
- Both mentor and threat — the player shouldn't know which for most of the game

### Presence (The Strahd Model)
The Dragon is PRESENT throughout the game, not just at boss fights:
- Acts without appearing: bridges that shouldn't exist, paths cleared, predators killed as warnings
- The ecosystem responds to it: creatures migrate, weather shifts when it moves
- EVE detects traces: "This was done recently. By something enormous."
- Every community of Adapted has a different story about it: "Jailer" vs "Gardener" vs "Echo of something dead"

### Encounter Arc
1. **Prologue:** Pure terror. Silent. Overwhelming. Establishes it as force of nature.
2. **Early-mid:** Passes overhead. All enemies flee. Doesn't attack. "That thing is scanning US, Adam."
3. **Mid:** Speaks for the first time. One sentence. Makes no sense now, perfect sense by the end.
4. **Late:** Saves your life. Destroys something about to kill you. Then leaves. "Why would it DO that?"
5. **Pre-finale:** You can approach it. It speaks at length. The monster reveals it's a person.
6. **Final:** The fight. But by now the player doesn't want to fight.

### The Fight
Not a health-bar slog. A vetting process:
- **Test of Will:** Can you survive its raw power?
- **Test of Understanding:** Can you navigate its shifting environment using scan knowledge?
- **Test of Sacrifice:** The Dragon stops fighting and presents its vulnerability. The real choice.

---

## EVE

### How She Survives Tech Suppression
[TODO: Needs definitive answer. Possibilities:
- She's partially biological already (ship AI that incorporated organic processing)
- The Dragon specifically exempts her (she's part of the test — the companion is always needed)
- She degrades but adapts, incorporating native materials into her hardware
- Her core runs on principles the planet doesn't suppress (quantum, not electronic)]

### Arc
- **Pre-crash:** Ship AI. Professional, capable, clinical
- **Early game:** Diminished but functional. Shaken. Never existed outside a ship before
- **Mid game:** Develops opinions, personality, genuine curiosity. Starts caring beyond mission parameters
- **Late game:** Goes quiet on certain scans. Hides findings. Not bickering — ABSENCE of her usual behavior. The player notices she's protecting them from something
- **The Temptation:** "I found something about you. Do you want to know?"

### Design Rules
- Hints, not answers. Never over-explains
- Personality over utility. She has opinions
- Her tension with Adam is divergence of goals, NEVER nagging/bickering
- When she's worried, she goes QUIET — players feel concern, not frustration

---

## THE WORLD

### Proxies (How the world talks about the Dragon)
- **Digital ghosts:** AI companions from previous ships, centuries old, fragmented/corrupted
- **The Adapted:** Descendants of previous visitors who survived long enough to change. Not quite human. Conflicting beliefs about the Dragon
- **Native wildlife:** Responds to the Dragon's movements. Ecosystem IS a proxy
- **Environmental evidence:** The Dragon has been preparing Adam's path. Clearing dangers, leaving teachings

### Areas (Mapped to Stages of the Fall)

**1. The Wreckage (Shock)**
Crashed ship. Technology failing. EVE confused/diminished. Alien wilderness.
*Mood: disorientation, vulnerability, small moments of beauty*

**2. The Living Forest (Wonder / Denial)**
Lush, alive, overwhelming. Beautiful and indifferent. Teaches observation and respect.
*Mood: awe mixed with unease. Beautiful but you don't belong.*

**3. The Native Ruins (Humility)**
Remnants of civilization that adapted successfully. Architecture grown, not built.
Adam realizes others did better. The ego of "important spacefarer" dies here.
*Mood: respect, smallness, being a student*

**4. The Bone Reef / Tidal Zone (Struggle)**
Brutal. Tides shift, hostile creatures. Weather hits hardest here.
EVE goes quiet — divergence of goals emerges through absence, not argument.
*Mood: exhaustion, feeling of being tested*

**5. The Deep Ruins (Truth)**
Far beneath surface. Previous visitors' ships. Evidence of the cycle.
Dragon's history carved in walls. EVE pieces together what's happening.
*Mood: dread mixed with clarity. Reading something that changes your worldview.*

**6. The Transformed Lands (Acceptance)**
Closest to Dragon. "Corruption" is radical change, not death.
Hostile things are peaceful here. Beautiful things are terrifying.
*Mood: the sublime. Standing at the edge of something vast.*

**7. The Dragon's Sanctum (Transcendence)**
Final area. Quiet. Almost empty. Just you, EVE, and the truth.
*Mood: melancholy, purpose, calm before something irreversible*

### Structure
Interconnected world (not level select). Areas connect through transitions that make geographic and emotional sense. The Transformed Lands visible from high points throughout — you can SEE the Dragon's territory long before you're ready.

---

## PROGRESSION — THE THREE-TIERED UPGRADE ENGINE

Adam's growth splits into three competing philosophies. The balance dictates the ending.

### Tier 1: The EVE Protocol (Technological Dominance)
Adam clings to his old life. Upgrades from ship scrap reinforcing suit and weapons.
- **Vibe:** Industrial, loud, aggressive
- **Example:** Thermal Plating — walk through acid pools by ignoring damage
- **Narrative cost:** EVE stays a "tool." More efficient, more detached. Planet = obstacles to bypass
- **Visual:** Adam looks like a walking tank. UI sharp, digital, full of warnings

### Tier 2: The Native Resonance (Biological Adaptation)
Adam learns from the Adapted. Stops fighting the planet, starts mimicking it.
- **Vibe:** Organic, silent, symbiotic
- **Example:** Gilled Respirator — grafted filter replaces oxygen tanks
- **Narrative cost:** Adam looks less human. EVE worried. "Your heart rate dropped to 30 BPM. You're... syncing."
- **Visual:** Suit cracks, held together by glowing vines or chitinous plates. UI becomes "wet," pulsing

### Tier 3: The Dragon's Cipher (Transcendence)
Found in the Deep Ruins. Not physical upgrades — Permissions.
- **Vibe:** Ethereal, ancient, geometric
- **Example:** Gravity Anchor — gravity isn't a law, it's a suggestion. "Fall" toward the ceiling
- **Narrative cost:** To take these, Adam must acknowledge the Dragon's logic. The "Strahd dinner" of upgrades
- **Visual:** Geometric patterns appear on Adam's body. UI becomes minimal, almost zen

### Tech Degradation
- All tech-tier items have integrity % that falls with use
- Can't stop using them — creates productive stress
- Degradation pushes player naturally toward biological/transcendent tiers
- The planet is WEANING Adam off technology

### The Middle Ground
Using tech to stay human AND biology to stay alive = the "Successor" path.
The balance between tiers IS the player's identity expression.

---

## SCANNING SYSTEM — "UNDERSTANDING ENGINE"

### Three Levels (Never breaks flow)

**Level 1 — Passive (automatic):**
EVE highlights things as you move. Glow, ping, one-line voiceover while running/fighting.
"Interesting composition." "That growth pattern is unusual."
Takes ZERO time. Ambient awareness.

**Level 2 — Quick scan (tap button, keep moving):**
5-second readout. One practical sentence. "Weak to fire. Shell is calcium-based."
Metroid Prime speed. Mid-combat viable.

**Level 3 — Deep scan (hold button while stationary, optional):**
2-3 sentences max. Feels like discovering a SECRET, not reading homework.
Reward isn't just text — it's a CHANGE: reveals exploitable behavior, opens hidden path, lets EVE synthesize something. Knowledge IS the power-up.

### Layered Discovery
Same creature scanned in different contexts reveals different truths:
- Scan in Forest: basic biology
- Scan in Ruins: this species has been here millennia
- Scan in Transformed Lands: "Adam... I think this is what it becomes when it stops fighting the change."

One creature, four scans, a complete arc. THIS is the innovation.

### Gameplay Loop (10-13 minute cycle)
- 8-10 min: moving, fighting, platforming, observing world reactions
- 1-2 min: moment of discovery (deep scan, ghost log, Adapted encounter)
- 1 min: processing (EVE comments while you keep moving)

---

## ENDINGS

### Standard: "Kill the Dragon"
Defeat it. Call for extraction. Leave. The Federation arrives, strip-mines the planet.
The cycle of the Fall repeats elsewhere. "Victory" that feels hollow.

### True: "Replace / Understand the Dragon"  
[TODO: Must differentiate from Dragon's Dogma. Current options:
- Adam stays as the planet's new shield. EVE becomes core of planetary consciousness, guides next ship to crash
- Adam doesn't REPLACE the Dragon — he FREES it. His understanding is what the Dragon needed to finally rest. The planet doesn't need a new dragon because Adam proved the cycle can end
- Something else entirely that we discover through development]

### Hidden: Unknown
The game should always be a little bit unknowable.

---

## THE FEDERATION (Narrative Stakes)

Adam's ship sent a distress signal before crashing. The Federation knows where he is. A recovery fleet is en route — not to save Adam, but to claim the planet. They've wanted this world for decades. The tech-suppression field kept them out, but Adam's crash created a breach.

**Adam's compassion didn't just strand him — it doomed the planet.**

- No literal timer (stressful, unfun). Narrative urgency through EVE picking up long-range fleet chatter, getting closer
- The Adapted sense something coming
- This is WHY the Dragon's cooperation matters — only it can re-seal the breach
- Kill the Dragon = Federation wins. Free the Dragon = it seals the planet forever

The Federation is the TRUE villain — faceless, industrial, inevitable. The Dragon is the tragic figure caught between its nature and its purpose.

---

## THE DRAGON — DEEPER MYTHOLOGY

The Dragon was once something else. A guardian, a consciousness, something that CHOSE to protect this world. But the method — forcing growth through destruction — corroded it over millennia. The intelligence is fading. The instincts are taking over. It's BECOMING the beast everyone thinks it is.

A witness doesn't just validate the Dragon. A witness REMEMBERS what it was before it forgot itself. Adam's understanding is preservation — the last chance for the Dragon's true nature to be known.

### The Fraying
- Early encounters: vast, controlled, deliberate
- Late encounters: vast, DANGEROUS, slipping — the intelligence fading, raw destructive nature emerging
- The final fight isn't "Dragon tests Adam" — it's "the Dragon is losing control and Adam must reach it before it's gone"
- The Test of Sacrifice = a moment of CLARITY where the real Dragon surfaces one last time. Adam must act in that window

### The Lion Analogy (from Ethan)
"When a lion is full, it isn't aggressive and attacking all the time, but it isn't bloodlusted either. It just IS."
- The Dragon isn't evil. It's natural. But what it does to the planet IS the antagonist
- Every hostile creature, toxic zone, and storm = the Dragon's legacy pressing on you
- You fight what the Dragon HAS DONE for 40 hours. The Dragon itself is something more complex

---

## TECH TIER — CHALLENGE RUNS

Tech-heavy playthroughs should be POSSIBLE but require:
- Careful foresight and planning
- Active maintenance (repair stations, scavenging routes)
- Sacrifice of bio/cipher power for human identity preservation
- This is the "hard mode" — staying human on a planet that wants to change you

---

## POST-GAME

If Adam stays, the planet changes:
- Dragon's influence fades or transforms
- Old areas shift — new weather patterns
- Aggressive creatures become curious
- Previously locked areas open
- The world AFTER understanding is different from the world during struggle
- Post-game = exploring the consequences of your choice

---

## OPEN QUESTIONS

1. How does EVE survive tech suppression? (Partial: self-adaptation, incorporating native materials)
2. How do the Adapted communicate? (Gesture? Broken common language? Through EVE?)
3. What specifically is on the distress signal Adam followed?
4. Are there other living visitors on the planet currently, or only traces of past ones?
5. What does the Dragon's speech sound like? (Multiple tones? Alien? Telepathic?)
6. How does the three-tier upgrade system work mechanically? (Skill tree? Found items? Choice gates?)
7. Federation fleet timeline — how long does Adam have narratively?
8. What triggers the Dragon's moments of clarity vs. its feral state?

---

## MCGONIGAL ALIGNMENT CHECK

- **Unnecessary obstacles:** The planet itself. Adam chose to come here. Every challenge is a consequence of that choice
- **Blissful productivity:** Mastery of movement + scanning discovery + understanding accumulation
- **Emotional activation:** Dragon encounters, EVE relationship, world beauty/horror
- **Hope of success:** Each scan/discovery proves the world is knowable. Progress is visible
- **Social connectivity:** Layered lore creates community interpretation (Vaati/Firelink model)
- **Epic meaning:** The Fall, creation, knowledge, sacrifice — experienced, not lectured

---

*This document is the skeleton. Everything we build must serve it.*
