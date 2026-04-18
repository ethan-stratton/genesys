using System.Collections.Generic;

namespace Genesis;

/// <summary>
/// Static data for a creature species — ecology, scan text, evolution gates.
/// Not serialized; this is the "game design" definition.
/// BestiaryEntry stores the player's progress discovering this data.
/// </summary>
public class CreatureProfile
{
    public string SpeciesId;          // matches enemy type string ("forager", "leaper", etc.)
    public string DisplayName;        // what EVE calls it after enough observations
    public string Classification;     // ecological classification
    public EcologicalRole Role;

    // Progressive L1 reveal text — keyed by observation count threshold (full text for bestiary)
    public (int threshold, string text)[] L1Reveals;

    // Short spoken quips EVE says aloud — keyed by same thresholds (personality, not data dumps)
    public (int threshold, string text)[] SpokenQuips;

    // L2 active scan data — physiology, the "something is wrong with the timeline"
    public string Temperament;
    public string Weaknesses;
    public string Resistances;
    public string DetailedRole;       // Full ecological role description
    public string L2ScanText;         // EVE's physiological analysis — notes impossible adaptation speed

    // L3 deep scan data — empathy/mind-reading, feeling what the creature feels
    public string L3ScanText;         // What you experience through cipher empathy
    public string CreatureFeeling;    // The dominant emotion/drive (shown as raw sensation)
    public string EvolutionaryHistory; // Cipher history-sight: compressed lineage vision

    // Biome-specific L3 variants (same creature, different biome = different inner life)
    public Dictionary<string, string> BiomeFeelingVariants;

    // Evolution gate — creature only appears after this flag is set
    // null = always present from start
    public string EvolutionGateFlag;
    // EVE commentary when this species first appears (evolution event)
    public string EvolutionEventText;
    // What generation/wave this creature belongs to (for UI/lore tracking)
    public int EvolutionWave; // 0 = primordial (always present), 1+ = gated
}

/// <summary>
/// Static registry of all creature profiles. Accessed by species ID.
/// </summary>
public static class CreatureDatabase
{
    public static readonly Dictionary<string, CreatureProfile> Profiles = new();

    static CreatureDatabase()
    {
        // =============================================
        // WAVE 0 — Primordial species (always present)
        // The baseline ecosystem. Simple, filling obvious niches.
        // =============================================

        Register(new CreatureProfile
        {
            SpeciesId = "forager",
            DisplayName = "Forager Crawler",
            Classification = "Arthropod — Grazer",
            Role = EcologicalRole.Herbivore,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Contact. Unknown arthropod. Low threat posture — it hasn't noticed us."),
                (2, "Same species again. Ground-level feeder. Predictable patrol patterns."),
                (3, "Herbivore. Grazing on surface deposits. Completely ignores us."),
                (5, "Classifying as Forager. Docile grazer. Patrols a fixed territory and feeds."),
                (7, "Full behavioral profile logged. Harmless unless cornered. Avoids predators instinctively."),
            },
            SpokenQuips = new[]
            {
                (1, "Something alive. Doesn't seem interested in us."),
                (2, "Same little guy. Just... eating."),
                (3, "Definitely herbivore. Harmless."),
                (5, "Calling it a Forager. Bottom of the food chain."),
                (7, "Profile complete. I almost feel bad for it."),
            },
            Temperament = "Docile — flees when threatened, never initiates combat",
            Weaknesses = "Slow, no armor, easily flanked",
            Resistances = "None",
            DetailedRole = "Base of the food chain. Foragers convert surface minerals into biomass that sustains everything above them.",
            L2ScanText = "Its digestive system is remarkably efficient — almost no waste. For a species this simple, that level of optimization usually takes geological time. This ecosystem is younger than it looks.",
            L3ScanText = "Contentment. A deep, warm satisfaction when it finds a good patch. Fear is there too, but distant — background noise. Mostly it just feels... full. Or wants to be.",
            CreatureFeeling = "contentment / hunger-satisfaction loop",
            EvolutionaryHistory = "You see it compressed — something simpler, softer, splitting and changing. Dozens of generations in seconds. It wasn't always an arthropod. It adapted to the ground because that's where the food was.",
            BiomeFeelingVariants = new Dictionary<string, string>
            {
                ["forest"] = "Contentment, tinged with damp warmth. The ground here is rich.",
                ["bone-reef"] = "Unease. The food is here but something is wrong. It eats fast and moves on.",
                ["deep-ruins"] = "Hunger. Sharp, desperate. There's almost nothing to eat down here.",
            },
        });

        Register(new CreatureProfile
        {
            SpeciesId = "skitter",
            DisplayName = "Skitter",
            Classification = "Arthropod — Prey",
            Role = EcologicalRole.Flighty,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Fast contact — something small just bolted. Couldn't get a clear reading."),
                (2, "There it is again. Tiny, fast, runs from everything. Pure flight response."),
                (3, "It's avoiding us specifically now. Some kind of threat memory."),
                (5, "Skitter. Harmless. Fastest thing on six legs down here."),
                (7, "Full profile. The Skitter exists to be chased. Every predator here hunts them."),
            },
            SpokenQuips = new[]
            {
                (1, "Something just bolted. Fast."),
                (2, "There it goes again. Terrified of us."),
                (3, "It remembers us now. Smart."),
                (5, "Skitter. Born to run. Nothing else."),
                (7, "Profile done. Everything eats these."),
            },
            Temperament = "Extremely flighty — bolts at the slightest disturbance",
            Weaknesses = "Fragile, panics into walls, predictable flee direction",
            Resistances = "Speed — very hard to land a clean hit",
            DetailedRole = "Primary prey species. Rapid reproduction. The ecosystem depends on them as a caloric base for predators.",
            L2ScanText = "Its legs are 60% of its body mass. That ratio doesn't develop gradually — something pushed this species hard toward one solution: run. The selection pressure here is extraordinary.",
            L3ScanText = "Terror. Constant, low-grade terror. Not panic — it's adapted to the fear. This is just what existing feels like for a Skitter. But between the fear... curiosity. It notices things. It just can't stop to look.",
            CreatureFeeling = "fear-as-baseline / flickers of curiosity",
            EvolutionaryHistory = "Generations blur past. You see something slow become something fast. Legs lengthen, armor drops away. Everything sacrificed for speed. The ones that couldn't run didn't get to have children.",
        });

        Register(new CreatureProfile
        {
            SpeciesId = "hopper",
            DisplayName = "Hopper",
            Classification = "Arthropod — Scavenger",
            Role = EcologicalRole.Scavenger,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Small contact, erratic movement. Bouncing around — hard to track."),
                (2, "Scavenger behavior. Picking at something on the ground. Ran when we approached."),
                (3, "Not aggressive, but unpredictable. The jumping makes it annoying to deal with."),
                (5, "Hopper. Scavenger, feeds on scraps. More nuisance than threat."),
                (7, "Full profile. Hoppers fill the gap between grazers and decomposers. Population spikes after kills."),
            },
            SpokenQuips = new[]
            {
                (1, "Erratic. Bouncing everywhere."),
                (2, "Scavenger. Picking at scraps."),
                (3, "Annoying, but not dangerous."),
                (5, "Hopper. Nature's janitor."),
                (7, "Profile done. More show up after kills."),
            },
            Temperament = "Skittish — erratic movement, low aggression, flees unpredictably",
            Weaknesses = "Low HP, predictable jump arc, easy to bait",
            Resistances = "Hard to hit due to constant movement",
            DetailedRole = "Niche scavenger. Processes what's too big for swarms and too small for grazers. Population tracks predator activity.",
            L2ScanText = "The legs store mechanical energy in a compressed organic spring. It doesn't choose to jump — it's on a hair trigger. This is a stress adaptation. Something scared this species into perpetual motion.",
            L3ScanText = "Excitement. Everything is interesting, everything is food, everything is danger, all at once. It's not stressed — it's overstimulated. The world is too much and it loves it anyway.",
            CreatureFeeling = "manic excitement / overstimulation-as-joy",
            EvolutionaryHistory = "A ground-dwelling scavenger that kept getting eaten. The survivors bounced. Each generation bounced higher. Now they can't stop.",
        });

        Register(new CreatureProfile
        {
            SpeciesId = "swarm",
            DisplayName = "Insect Swarm",
            Classification = "Micro-colony — Collective",
            Role = EcologicalRole.Scavenger,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Multiple tiny contacts. A cloud of something. They're investigating us."),
                (2, "It's a swarm — hundreds of tiny organisms moving as one."),
                (3, "Attracted to light and warmth. Consider the lantern near swarms."),
                (5, "Insect Swarm. Collective scavenger. Individually harmless, en masse they strip carcasses fast."),
                (7, "Full profile. Swarms are the cleanup crew. They process anything dead or dying."),
            },
            SpokenQuips = new[]
            {
                (1, "Cloud of... something. Watching us."),
                (2, "A swarm. Moving as one."),
                (3, "They like the lantern. Be careful."),
                (5, "Cleanup crew. Don't be the cleanup."),
                (7, "Profile done. They eat the dead."),
            },
            Temperament = "Opportunistic — ignores healthy targets, swarms the injured",
            Weaknesses = "Dispersed by concussive force or loud sounds",
            Resistances = "Can't be killed conventionally — destroying individuals doesn't stop the swarm",
            DetailedRole = "Decomposers. Break down dead biomass and redistribute nutrients. Without them, everything chokes.",
            L2ScanText = "No individual has a brain. Intelligence is emergent — chemical signals create group behavior. Separate a cluster and they go inert. This is collective evolution, not individual. The swarm is the organism.",
            L3ScanText = "No single voice. A hum. A thousand faint impulses adding up to something like intention. Not thought — more like a tide. It doesn't feel like a mind. It feels like weather.",
            CreatureFeeling = "collective hum / intention-without-self",
            EvolutionaryHistory = "You can't see individuals evolving. The swarm evolves as a whole. Patterns shift, density changes, behavior rewrites itself across the colony in real time. This isn't generational — it's continuous.",
        });

        // =============================================
        // WAVE 0 — Larger primordial species
        // =============================================

        Register(new CreatureProfile
        {
            SpeciesId = "thornback",
            DisplayName = "Thornback",
            Classification = "Megafauna — Armored Grazer",
            Role = EcologicalRole.Herbivore,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Large contact. Heavily armored. It's... not attacking. Moving slowly."),
                (2, "Those spines are defensive. It curled up when we got close."),
                (3, "Massive compared to everything else. Eats the same deposits as Foragers."),
                (5, "Thornback. Armored grazer. Leave it alone and it returns the favor. The spines are no joke."),
                (7, "Full profile. Gentle giant. Other creatures shelter near it — the spines deter predators."),
            },
            SpokenQuips = new[]
            {
                (1, "Big one. Armored. Not hostile... yet."),
                (2, "Those spines are a warning. Noted."),
                (3, "Gentle, actually. Just huge."),
                (5, "Thornback. Don't pick a fight with it."),
                (7, "Profile done. Others hide behind it."),
            },
            Temperament = "Passive — attacks only when directly threatened",
            Weaknesses = "Extremely slow, can't turn quickly, underbelly is unarmored",
            Resistances = "Dorsal spines deflect most attacks, high HP, knockback-resistant",
            DetailedRole = "Keystone species. Grazing creates clearings. Presence deters predators. Smaller creatures use them as mobile safe zones.",
            L2ScanText = "The armor plating has hexagonal micro-structure. This isn't chitin — it's closer to carbon composite. A biological material with no business being this strong in a natural species. Whatever is driving evolution here doesn't do half measures.",
            L3ScanText = "Patience. A vast, slow calm. It knows the small things near it. Not by name — by rhythm. When the Skitters scatter, it tenses. When they return, it relaxes. It's protecting them. Not because it was told to. Because they're there.",
            CreatureFeeling = "deep patience / protective awareness",
            EvolutionaryHistory = "Something large and soft that kept getting bitten. Armor grew. Spines grew. Predators learned. Now nothing bothers it, and everything hides behind it.",
        });

        // =============================================
        // WAVE 0 — Predators (primordial)
        // =============================================

        Register(new CreatureProfile
        {
            SpeciesId = "leaper",
            DisplayName = "Leaper",
            Classification = "Arthropod — Ambush Predator",
            Role = EcologicalRole.Predator,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Hostile contact! Fast-closing arthropod. It jumped — watch the ceiling."),
                (2, "Same species. It was waiting on the wall. Ambush behavior confirmed."),
                (3, "They hunt from elevated positions. Check above before moving through open areas."),
                (5, "Leaper. Ambush predator, uses vertical surfaces. Attacks anything in its kill zone."),
                (7, "Full profile. Solitary and territorial. Two won't share the same hunting ground."),
            },
            SpokenQuips = new[]
            {
                (1, "Hostile! It jumped at us!"),
                (2, "An ambusher. Waits on walls."),
                (3, "Check ceilings. Always."),
                (5, "Leaper. Territorial ambush hunter."),
                (7, "Profile done. They don't share turf."),
            },
            Temperament = "Aggressive — ambush predator, attacks on detection",
            Weaknesses = "Vulnerable mid-leap, slow recovery after missing a pounce",
            Resistances = "Hard carapace on top — attack from below",
            DetailedRole = "Mid-tier predator. Keeps grazer and prey populations in check. Territorial — one per area.",
            L2ScanText = "Its eyes are vestigial — it hunts by vibration. Every step you take is a dinner bell. The sensory shift from sight to vibration usually takes millions of years. Here? Maybe a few hundred generations.",
            L3ScanText = "Focus. An almost meditative stillness, then a spike of pure predatory clarity. It doesn't hate you. It doesn't feel anything about you. You're a vibration pattern that means food. When it misses, there's frustration — brief, sharp, then back to stillness.",
            CreatureFeeling = "patience-spike-patience / detached precision",
            EvolutionaryHistory = "A ground predator that couldn't catch the fast prey. Some climbed. Some waited. The ones that climbed and waited ate. Now they all climb and wait.",
            BiomeFeelingVariants = new Dictionary<string, string>
            {
                ["forest"] = "Patient hunger. The canopy is home. Everything below is food.",
                ["deep-ruins"] = "Claustrophobia inverted — the tight spaces comfort it. More walls to cling to.",
                ["bone-reef"] = "Agitation. Too open. It doesn't like being seen.",
            },
        });

        Register(new CreatureProfile
        {
            SpeciesId = "bombardier",
            DisplayName = "Bombardier",
            Classification = "Arthropod — Territorial Defender",
            Role = EcologicalRole.Defensive,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Projectile! That thing is shooting something. Keep distance."),
                (2, "It didn't chase us. Shot and stayed put. Defending territory, not hunting."),
                (3, "Projectiles are organic — some kind of chemical reaction. Don't let them hit the suit."),
                (5, "Bombardier. Stationary defender. Won't chase, but its range is significant."),
                (7, "Full profile. Bombardiers anchor to resource-rich spots. Where there's one, there's something worth guarding."),
            },
            SpokenQuips = new[]
            {
                (1, "It's shooting at us!"),
                (2, "Territorial. Won't chase, at least."),
                (3, "Chemical rounds. Bad for the suit."),
                (5, "Bombardier. Guards the good stuff."),
                (7, "Profile done. Where it sits matters."),
            },
            Temperament = "Territorial — attacks anything in range but won't pursue",
            Weaknesses = "Immobile during firing, slow turn rate, blind spot behind",
            Resistances = "Heavy frontal armor, resistant to blunt damage",
            DetailedRole = "Area denial. Inadvertently protects resource deposits and nesting sites. Other creatures have learned to nest near them.",
            L2ScanText = "Binary chemical system — two inert substances that become corrosive on mixing. That's a sophisticated defense mechanism for a creature this simple. Evolution is being... creative here. Unusually creative.",
            L3ScanText = "Stubbornness. A deep, immovable sense of place. This is mine. This spot. Not anger — certainty. It doesn't want to fight you. It wants you to leave. The firing is a boundary, not aggression.",
            CreatureFeeling = "territorial certainty / this-is-mine",
            EvolutionaryHistory = "A soft grazer near a chemical deposit. Predators came. The ones near the chemicals survived — some absorbed them. Absorption became production. Now it makes its own weapons from its own body.",
        });

        Register(new CreatureProfile
        {
            SpeciesId = "wingbeater",
            DisplayName = "Wingbeater",
            Classification = "Pseudo-avian — Aerial Predator",
            Role = EcologicalRole.Predator,
            EvolutionWave = 0,
            L1Reveals = new[]
            {
                (1, "Airborne contact. Large wingspan. It's circling — that's not random."),
                (2, "Same species. Wing pattern is wrong for efficient flight — too aggressive."),
                (3, "Dive-bombed us. Aerial predator. Wings generate concussive downdraft."),
                (5, "Wingbeater. Aerial predator, dive attacks and wind pressure. Dangerous in open areas."),
                (7, "Full profile. Nests high, hunts low. Won't enter tight spaces — use corridors."),
            },
            SpokenQuips = new[]
            {
                (1, "Airborne. Circling us."),
                (2, "Those wings hit hard. Not for flying."),
                (3, "Dive attack! Stay in tight spaces."),
                (5, "Wingbeater. Owns the open air."),
                (7, "Profile done. Corridors are safe from it."),
            },
            Temperament = "Aggressive — patrols and attacks from above",
            Weaknesses = "Can't maneuver in tight spaces, vulnerable during dive recovery",
            Resistances = "Difficult to hit in flight, wind pressure deflects small projectiles",
            DetailedRole = "Apex aerial predator. Controls vertical space. Forces ground creatures into tunnels and cover.",
            L2ScanText = "Those wings aren't optimized for flight — they're optimized for impact. It traded efficiency for force. That's a predator that evolved to hit things from above, not to fly well. The ecosystem must have pushed hard for aerial hunters.",
            L3ScanText = "Exhilaration. The dive is joy. Pure, physical, screaming joy. It doesn't hunt because it's hungry — the hunger is just permission. The dive is the point. It was born to fall and pull up at the last second.",
            CreatureFeeling = "dive-joy / hunger-as-permission-to-fly",
            EvolutionaryHistory = "Something that glided. Then something that dove. Each generation, the wings got broader, heavier. Less flight, more weapon. It's becoming a living projectile.",
        });

        // =============================================
        // WAVE 1 — First evolution event
        // Triggered by: first area boss defeated or Dragon proximity detected
        // New niche-fillers and adaptations that respond to the player's presence
        // =============================================

        Register(new CreatureProfile
        {
            SpeciesId = "stalker",
            DisplayName = "Stalker",
            Classification = "Arthropod — Pursuit Predator",
            Role = EcologicalRole.Predator,
            EvolutionWave = 1,
            EvolutionGateFlag = "evolution_wave_1",
            EvolutionEventText = "New species detected. This wasn't here before. The ecosystem is... adapting. Rapidly.",
            L1Reveals = new[]
            {
                (1, "New contact. It's following us. Keeping distance but matching our speed."),
                (2, "It tracked us across two rooms. This is a pursuit predator — it doesn't ambush."),
                (3, "It's learning our movement patterns. It cut us off at the last junction."),
                (5, "Stalker. Pursuit predator. It doesn't pounce — it follows until you make a mistake."),
                (7, "Full profile. Stalkers are patient, intelligent hunters. They test defenses before committing."),
            },
            SpokenQuips = new[]
            {
                (1, "It's... following us."),
                (2, "Still following. It's patient."),
                (3, "It learned our route. Careful."),
                (5, "Stalker. Waits for mistakes."),
                (7, "Profile done. Respect this one."),
            },
            Temperament = "Calculating — follows prey, probes for weakness before attacking",
            Weaknesses = "Won't attack head-on if you face it, fragile compared to Leapers",
            Resistances = "Fast, hard to corner, disengages when outmatched",
            DetailedRole = "Fills the pursuit predator niche that Leapers leave empty. Leapers ambush; Stalkers chase. Together they cover both strategies.",
            L2ScanText = "This species shares genetic markers with the Leaper. It diverged recently — maybe a dozen generations. One lineage became ambush specialists. This one became marathoners. The ecosystem needed both, so it made both.",
            L3ScanText = "Patience, but not like the Leaper's empty stillness. This is active patience. It's thinking. Watching. It knows you're dangerous. It's deciding if you're worth it. There's respect in there. Predator to predator.",
            CreatureFeeling = "calculating respect / active patience",
            EvolutionaryHistory = "A Leaper that missed. And missed again. The ones that followed instead of pouncing caught the prey that pounced ones couldn't. A new strategy emerged in a handful of generations.",
        });

        Register(new CreatureProfile
        {
            SpeciesId = "spitter",
            DisplayName = "Acid Spitter",
            Classification = "Arthropod — Ranged Predator",
            Role = EcologicalRole.Predator,
            EvolutionWave = 1,
            EvolutionGateFlag = "evolution_wave_1",
            EvolutionEventText = "Another new predator variant. The Bombardier's chemical trait has jumped species. That shouldn't be possible this fast.",
            L1Reveals = new[]
            {
                (1, "Ranged hostile! Acidic projectile — suit took minor damage."),
                (2, "It's mobile, unlike the Bombardier. Shoots and repositions."),
                (3, "The acid is similar to Bombardier chemistry but weaker. Diluted."),
                (5, "Acid Spitter. Mobile ranged predator. Bombardier chemistry in a hunter's body."),
                (7, "Full profile. Spitters kite — maintain distance, wear prey down. Don't let them dictate range."),
            },
            SpokenQuips = new[]
            {
                (1, "Acid! It's shooting acid!"),
                (2, "Mobile shooter. Moves after firing."),
                (3, "Same chemicals as the Bombardier..."),
                (5, "Spitter. Close the gap fast."),
                (7, "Profile done. Don't let it kite you."),
            },
            Temperament = "Aggressive but cautious — maintains range, retreats if closed on",
            Weaknesses = "Low HP, acid runs out (limited shots before recharge), slow in melee",
            Resistances = "Keeps distance effectively, acid damages suit integrity",
            DetailedRole = "Ranged predator niche. Evolved the Bombardier's chemical defense into an offensive weapon. Horizontal gene transfer in real time.",
            L2ScanText = "The chemical glands share 90% structure with Bombardier organs. This is horizontal gene transfer — traits jumping between species. In normal evolution that takes millions of years. Here it took... I'm going to need to recalibrate my timescale assumptions.",
            L3ScanText = "Caution. Always caution. It feels the distance between you like a physical thing. Closing that gap triggers something close to dread. It wasn't built for close contact and it knows it.",
            CreatureFeeling = "distance-anxiety / measured aggression",
            EvolutionaryHistory = "A scavenger near Bombardier territory. Chemical residue in the food supply. Some absorbed it. Some weaponized it. The ones that could spit survived the predators the Bombardier kept out.",
        });

        // =============================================
        // WAVE 2 — Second evolution event  
        // Triggered by: mid-game story flag (approaching Dragon territory)
        // Creatures responding to increased Dragon field intensity
        // =============================================

        Register(new CreatureProfile
        {
            SpeciesId = "mimic",
            DisplayName = "Mimic Crawler",
            Classification = "Arthropod — Deceptive Predator",
            Role = EcologicalRole.Predator,
            EvolutionWave = 2,
            EvolutionGateFlag = "evolution_wave_2",
            EvolutionEventText = "I almost misidentified this as a Forager. It's not. The ecosystem is producing deception now. That's a significant threshold.",
            L1Reveals = new[]
            {
                (1, "Wait — that Forager just attacked. That's not a Forager."),
                (2, "It mimics Forager movement patterns until prey is close. Then it strikes."),
                (3, "Coloration is nearly identical. Only difference is a slight twitch before it lunges."),
                (5, "Mimic Crawler. Disguises itself as a Forager. Watch for the pre-attack twitch."),
                (7, "Full profile. Mimics are rare — one per several Forager groups. Ecosystem regulates the ratio."),
            },
            SpokenQuips = new[]
            {
                (1, "That Forager just... that's NOT a Forager."),
                (2, "It copies their walk. Perfectly."),
                (3, "Watch for the twitch. Only tell."),
                (5, "Mimic. Trust nothing that grazes."),
                (7, "Profile done. Nature learned to lie."),
            },
            Temperament = "Deceptive — passive until ambush range, then extremely aggressive",
            Weaknesses = "Identical stats to Forager once exposed, fragile",
            Resistances = "Surprise factor — first hit is often free",
            DetailedRole = "Evolved from Forager stock to exploit trust. Keeps prey species alert, which indirectly strengthens them.",
            L2ScanText = "Genetically, this IS a Forager. Same species, diverging phenotype. It's not a new creature — it's a Forager that learned to bite. The same species is producing both prey and predator variants. That's... unsettling.",
            L3ScanText = "Hunger. The same contentment as a Forager, but twisted. It eats the same things and it's never enough. It feels the satisfaction of feeding and then nothing. So it feeds again. Harder.",
            CreatureFeeling = "corrupted contentment / hunger that can't be filled",
            EvolutionaryHistory = "A Forager that bit back. Once. That was enough. Its children bit more. The shape stayed the same but the instinct inverted. Same body, opposite purpose.",
        });

        Register(new CreatureProfile
        {
            SpeciesId = "resonant",
            DisplayName = "Resonant",
            Classification = "Unknown — Field-Adapted",
            Role = EcologicalRole.Defensive,
            EvolutionWave = 2,
            EvolutionGateFlag = "evolution_wave_2",
            EvolutionEventText = "This creature is generating an electromagnetic field. It evolved to interact with the ionized zones. The Dragon's influence is reshaping the ecosystem to fit the environment.",
            L1Reveals = new[]
            {
                (1, "Anomalous contact. Getting interference near this one. Battery fluctuating."),
                (2, "It's generating a localized EM field. Tech equipment malfunctions in proximity."),
                (3, "Seems to feed on electrical discharge. The ionized zones aren't just its habitat — they're its food source."),
                (5, "Resonant. Field-adapted organism. Tech bane. Keep your distance or lose battery fast."),
                (7, "Full profile. Resonants are the ionized zone's apex. Everything else avoids them. So should we."),
            },
            SpokenQuips = new[]
            {
                (1, "Interference. Something's draining us."),
                (2, "It's generating an EM field. Stay back."),
                (3, "It eats electricity. Our electricity."),
                (5, "Resonant. Death sentence for tech."),
                (7, "Profile done. Avoid. Just... avoid."),
            },
            Temperament = "Passive unless tech is used nearby — then attracted to the energy source",
            Weaknesses = "Bio weapons unaffected by its field, slow-moving, vulnerable to blunt force",
            Resistances = "Tech weapons malfunction near it, absorbs electrical damage, drains battery on contact",
            DetailedRole = "Ionized zone keystone. Regulates the EM field intensity by feeding on excess charge. Without Resonants, ionized zones would expand uncontrollably.",
            L2ScanText = "It has organs that don't correspond to anything in the other species. Entirely new biological structures for generating and absorbing electromagnetic fields. This isn't adaptation — this is innovation. The mutation rate here must be astronomical.",
            L3ScanText = "Static. A buzzing, vibrating awareness that doesn't think in words or images. It senses fields. It feels the shape of electricity the way you feel heat. When you turn on the lantern near it, you're screaming in its language.",
            CreatureFeeling = "field-awareness / electric synesthesia",
            EvolutionaryHistory = "No clear ancestor. This one jumped. Multiple radical mutations in a single generation near the ionized zone. The Dragon's field was strongest here. Something crawled in and came out changed.",
        });
    }

    private static void Register(CreatureProfile profile)
    {
        Profiles[profile.SpeciesId] = profile;
    }

    /// <summary>
    /// Get the appropriate L1 reveal text for a given observation count.
    /// Returns null if no new text at this count.
    /// </summary>
    public static string GetL1RevealText(string speciesId, int sightCount)
    {
        if (!Profiles.TryGetValue(speciesId, out var profile)) return null;
        if (profile.L1Reveals == null) return null;

        foreach (var (threshold, text) in profile.L1Reveals)
        {
            if (threshold == sightCount)
                return text;
        }
        return null;
    }

    /// <summary>
    /// Get the short spoken quip EVE says aloud for a given sighting count.
    /// Returns null if no quip at this count (EVE stays quiet on repeat sightings).
    /// </summary>
    public static string GetSpokenQuip(string speciesId, int sightCount)
    {
        if (!Profiles.TryGetValue(speciesId, out var profile)) return null;
        if (profile.SpokenQuips == null) return null;

        foreach (var (threshold, text) in profile.SpokenQuips)
        {
            if (threshold == sightCount)
                return text;
        }
        return null;
    }

    /// <summary>
    /// Get the most recent L1 text the player should have seen (for bestiary UI).
    /// Returns the highest-threshold text at or below the current sight count.
    /// </summary>
    public static string GetCurrentL1Summary(string speciesId, int sightCount)
    {
        if (!Profiles.TryGetValue(speciesId, out var profile)) return "No data.";
        if (profile.L1Reveals == null) return "No data.";

        string best = null;
        int bestThreshold = 0;
        foreach (var (threshold, text) in profile.L1Reveals)
        {
            if (threshold <= sightCount && threshold >= bestThreshold)
            {
                best = text;
                bestThreshold = threshold;
            }
        }
        return best ?? "No data.";
    }

    /// <summary>
    /// Get all species that should exist for a given set of active evolution flags.
    /// </summary>
    public static List<string> GetAvailableSpecies(HashSet<string> activeFlags)
    {
        var result = new List<string>();
        foreach (var (id, profile) in Profiles)
        {
            if (profile.EvolutionGateFlag == null || activeFlags.Contains(profile.EvolutionGateFlag))
                result.Add(id);
        }
        return result;
    }

    /// <summary>
    /// Get species newly unlocked by a specific flag (for EVE evolution event alerts).
    /// </summary>
    public static List<CreatureProfile> GetSpeciesForFlag(string flag)
    {
        var result = new List<CreatureProfile>();
        foreach (var (_, profile) in Profiles)
        {
            if (profile.EvolutionGateFlag == flag)
                result.Add(profile);
        }
        return result;
    }
}
