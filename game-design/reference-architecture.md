# Reference Code Architecture Overview
*What we can learn from Celeste, Stardew Valley, and AM2R to build Genesis faster.*

---

## 1. CELESTE — Movement & Physics (C#/FNA)
**Source:** `reference/celeste/Source/Player/Player.cs` (5,471 lines)
**Blog:** Maddy Thorson's "Celeste and TowerFall Physics"

### Core Architecture: Actors & Solids
Celeste uses a dead-simple two-class physics system:
- **Solids**: Collidable level geometry (tiles, moving platforms)
- **Actors**: Anything that moves (player, enemies, projectiles)

**Key constraints:**
- ALL colliders are axis-aligned bounding boxes (AABBs)
- ALL positions, widths, heights are **integers** (no float positions!)
- Actors and Solids **never overlap** (invariant maintained at all times)
- Solids don't interact with other Solids

### Movement System: MoveX / MoveY
Actors don't store velocity internally. Each Actor manages its own speed and passes it to:
```csharp
public void MoveX(float amount, Action onCollide);
public void MoveY(float amount, Action onCollide);
```
- Float movement accumulates in a **remainder** counter
- Only moves when rounded remainder ≠ 0
- Moves **one pixel at a time**, checking collision each step
- On collision → calls delegate callback (customizable per-context)

**Why this matters for Genesis:** Our current movement uses float positions directly. Celeste's integer-position + remainder system prevents sub-pixel jitter and makes collision detection deterministic. We should adopt this.

### Moving Platforms: Push vs Carry
When a Solid moves:
1. Build list of Actors riding it (`IsRiding` check — customizable per Actor type)
2. Temporarily disable Solid's collision
3. Move the Solid
4. **Push** any now-overlapping Actors (amount = edge difference, callback = Squish)
5. **Carry** riding Actors (full movement, no callback)
6. Re-enable collision

**Push > Carry priority**: If a Solid both pushes and carries an Actor, push wins.

### Key Constants We Should Study/Adopt
```
MaxFall = 160f           Gravity = 900f          MaxRun = 90f
RunAccel = 1000f         AirMult = 0.65f         JumpSpeed = -105f
JumpGraceTime = 0.1f     (coyote time)
VarJumpTime = 0.2f        (hold jump for higher)
WallJumpForceTime = 0.16f
DashSpeed = 240f          DashTime = 0.15f
```

### Critical Feel Tricks
1. **Coyote Time (JumpGraceTime = 0.1s)**: Can jump for 0.1s after leaving a platform edge
2. **Variable Jump Height (VarJumpTime = 0.2s)**: Hold jump = higher, tap = lower
3. **Wall Boost Timer**: If you climb-jump then input sideways quickly, switches to wall jump
4. **Corner Correction (4px)**: If you'd bonk a ceiling corner by ≤4px, nudge horizontally to clear it
5. **AirMult = 0.65**: Horizontal acceleration reduced in air (prevents floaty over-control)
6. **HalfGravThreshold = 40f**: Near jump apex, gravity halves (extends hang time for precise landings)

### What to Implement for Genesis Phase 1:
- [ ] Integer position + float remainder system
- [ ] MoveX/MoveY with collision callbacks
- [ ] Coyote time (0.1s grace period after leaving ground)
- [ ] Variable jump height (hold = higher)
- [ ] Corner correction (nudge past near-miss ceiling bonks)
- [ ] Half-gravity at jump apex
- [ ] Air control reduction (0.65x horizontal accel in air)

---

## 2. STARDEW VALLEY — Systems Architecture (C#/MonoGame)
**Source:** `reference/stardew/` (decompiled 1.6, .NET 6)
**EXACT same framework as Genesis.**

### Save System (`SaveGame.cs`)
- Uses **XML serialization** with `XmlSerializer`
- SaveGame class contains ALL world state: player, locations, weather, mail, flags
- Key fields: `player` (Farmer), `locations` (List<GameLocation>), weather bools, day/year, dailyLuck, unique ID
- Separate serializers for different data types (farmer, locations, game)
- Compression via `Ionic.Zlib`

**For Genesis:** We should use **JSON** (simpler, human-readable, already using it for levels). Our SaveData is much simpler:
```csharp
public class SaveData {
    public Vector2 PlayerPosition;
    public int Health, MaxHealth;
    public float SuitIntegrity, Battery;
    public List<string> Inventory;
    public HashSet<string> ScannedObjects;
    public Dictionary<string, bool> BehaviorFlags; // ScavengerKilled, etc.
    public string LastShelterId;
    public float WorldTime;
    public string WeatherState;
    // ... compact, JSON-serialized
}
```

### Dialogue System (`Dialogue.cs`, `DialogueLine.cs`)
- Dialogue is **string-encoded with command prefixes**: `$h` (happy), `$s` (sad), `$b` (page break), `$q` (question), `$r` (response)
- `DialogueLine` has `Text` + `SideEffects` (Action delegate — code runs when dialogue displays)
- Multi-box dialogues split by `||`
- Conditional branches via `$p` (prerequisite), `$d` (world state), `$query` (game state query)

**For Genesis / EVE:** We need something simpler:
```csharp
public class EveDialogue {
    public string Id;
    public string Text;
    public float Duration;
    public int Priority; // 0=ambient, 1=triggered, 2=critical
    public ScanLevel? ScanLevelColor; // null=white, Blue/Green/Gold
    public string[] Variants; // random selection
    public string Condition; // flag check
}
```
Queue system with priority override (critical > triggered > ambient).

### Inventory System (`Inventory.cs`)
- Implements `IList<Item>` — literally a managed list with index tracking
- `NetObjectList<Item>` underneath (networked for multiplayer — we don't need this)
- `InventoryIndex` for fast lookup by item ID
- Null slots represent empty spaces

**For Genesis:** Simple `List<InventoryItem>`:
```csharp
public class InventoryItem {
    public string Id;      // "knife", "sidearm", "grapple", "battery_cell"
    public int Count;       // stackable items
    public string SlotType; // "weapon", "tool", "consumable"
}
```

### Weather
- Stardew uses **global bools**: `isRaining`, `isSnowing`, `isLightning`, `isDebrisWeather`
- `weatherForTomorrow` is a string ID
- `WeatherDebris` class handles visual particles
- NPC behavior checks weather directly: `if (Game1.isRaining)`

**For Genesis:** State machine is better than bools (we need smooth transitions):
```csharp
public enum WeatherState { Clear, LightRain, HeavyRain, Overcast, Fog, Storm }
public class WeatherSystem {
    public WeatherState Current, Target;
    public float TransitionProgress; // 0-1, lerp between states
    public float StateTimer;         // time until next transition
    public Vector2 WindDirection;
}
```

---

## 3. AM2R — Design Patterns (GameMaker/GML)
**Source:** `reference/am2r/` (4,293 files, GML)
**Not portable but pattern-valuable.**

### Room/Level Transitions
- GameMaker rooms = individual screens/areas
- Transitions trigger on room boundary collision
- Fade-to-black between rooms, camera reset
- **For Genesis:** We already have portal/exit system. Study AM2R's transition timing.

### Enemy State Machines
- GML objects with `state` variable and switch-case in Step event
- States: idle, patrol, detect, chase, attack, stunned, death
- Detection via distance checks + line-of-sight raycasts
- **For Genesis:** Same pattern we're using. Validate our predator/scavenger FSMs match this structure.

### Metroidvania Gating
- Ability flags checked against room requirements
- Visual indicators: blocked paths have distinct visual cues
- **For Genesis:** Our gating (wall climb for ruins, breathing for reef) follows this pattern

---

## Summary: What to Build Using These References

| Genesis System | Primary Reference | Key Insight |
|---|---|---|
| Player movement | Celeste Player.cs | Integer positions, coyote time, variable jump, corner correction |
| Physics/collision | Celeste blog post | Actor/Solid separation, MoveX/MoveY with callbacks |
| Save system | Stardew SaveGame.cs | Serialize all world state; JSON instead of XML |
| EVE dialogue | Stardew Dialogue.cs | Priority queue, side effects on display, variant selection |
| Inventory | Stardew Inventory.cs | Simple list with slot types, null = empty |
| Weather | Stardew (pattern) | State machine > bools, smooth transitions |
| Enemy AI | AM2R objects | State machine with detect/chase/attack/cooldown |
| Level transitions | AM2R rooms | Fade timing, camera reset, spawn point |

---

*These repos are in `reference/` — browse them freely during development.*
