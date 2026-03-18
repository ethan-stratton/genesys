# Slope Collision Reference — Mega Man / SOTN Engine Family
Date: 2026-03-18

## SOTN Collision System (Decompiled Source)
Source: `github.com/Xeeynamo/sotn-decomp/src/dra/collider.c` (370 lines, PSX)

### Architecture
- **Single `CheckCollision(x, y, result, unk)` function** — queries one point against the tilemap
- Tiles are 16×16 pixels (not 32)
- Each tile has a **collision type byte** looked up from `g_Tilemap.tileDef->collision[tileIndex]`
- Results stored in a `Collider` struct with offsets (unk4/unk8/unk14/unk18 etc.) representing push distances
- **Effects flags** communicate what happened: `EFFECT_SOLID`, `EFFECT_UNK_8000` (floor slope), `EFFECT_UNK_0800` (ceiling slope), `EFFECT_UNK_4000` (left-facing), etc.

### Slope Types Supported
| Type | Hex | Description |
|------|-----|-------------|
| Right 45° floor (/) | 0x00 | `unk1C + unk20 < 0x10` → solid |
| Left 45° floor (\\) | 0x03 | `unk1C >= unk20` → solid |
| Right 45° ceiling (\\) | 0x04 | `unk1C <= unk20` → solid |
| Left 45° ceiling (/) | 0x07 | `unk1C + unk20 > 0x0E` → solid |
| 22.5° slopes | 0x08-0x09 | Two tiles per step, uses `*2` multiplier |
| 11.25° slopes | 0x14-0x17 | Four tiles per step, uses `*4` multiplier |
| Ceiling mirrors of all above | 0x0E-0x0F, 0x12-0x13, etc. | Same math, ceiling flags |
| Drop-through platforms | 0x67 | `EFFECT_SOLID_FROM_ABOVE` |
| Water | 0x6D | `EFFECT_WATER` |
| Quicksand | 0x7B | `EFFECT_QUICKSAND` |

### Key Insights for Our Game

1. **Point-query model**: SOTN checks collision at INDIVIDUAL POINTS, not bounding boxes. The game calls `CheckCollision` multiple times for different parts of the player (feet, head, sides). This is exactly the sensor approach Sonic uses.

2. **Slope math is pure algebra**: For a right 45° floor (/), the test is `localX + localY < tileSize`. For left 45° (\\), it's `localX >= localY`. No height arrays — just inequalities.

3. **Gentle slopes use multipliers**: 22.5° slopes multiply localY by 2 (and use two tiles), 11.25° multiply by 4 (four tiles). The formula generalizes: `localX + offset + localY * N < tileSize * N`.

4. **Ceiling slopes are MIRRORS of floor slopes**: Same formulas with comparisons flipped (< becomes >, offsets inverted). The push direction (unk18 vs unk20) determines which way the player gets pushed.

5. **Recursive solid tile check**: Solid tiles (0x01, 0x02, etc.) recursively call `CheckCollision` on the tile ABOVE (for floors) or BELOW (for ceilings) to chain solid regions. This is how they handle thick walls.

6. **No velocity deflection in the collider**: The collider only reports push distances and flags. Momentum handling happens in the PLAYER code, not the collision code. The collider tells you "you're inside a slope, push out by this much" — the player code decides what to do with velocity.

7. **Effects flags separate concerns**: Floor slopes set `EFFECT_UNK_8000`, ceiling slopes set `EFFECT_UNK_0800`, left-facing set `EFFECT_UNK_4000`. The player code can check these flags to determine slope direction and type without re-querying the tilemap.

## Mega Man X/Zero Engine Notes

### From TAS Research (Mega Man Zero GBA series)
- **Triangle hop**: Wall jump detection is within a few pixels of wall, not touching. Can double-triangle-hop for extra height.
- **Damage animation override**: Can interrupt attack animation with damage for faster recovery.
- **Invincibility frames protect from spikes**: Standard Mega Man behavior.
- **Chain Rod zipping**: Can zip through floors/ceilings, indicating collision is point-based and can be bypassed at high speeds.

### General Mega Man X Slope Approach (from community knowledge)
- Mega Man X uses **height maps per tile** (similar to Sonic) — each column of a 16×16 tile has a height value
- X's feet sensor checks the height at the current column, player Y is set to tile top - height
- Ceiling works the same but inverted — head sensor, height from top
- Wall sensors are horizontal height maps
- When on a slope, X's sprite doesn't rotate — just the Y position changes per-column
- Slope running preserves horizontal speed; only gravity changes (no ground speed variable like Sonic)
- MMX doesn't deflect ceiling momentum — hitting a ceiling slope just stops you (bonk). This is different from what we want for our game.

### What This Means for Our Implementation
1. **Our approach is valid** — SOTN uses algebraic slope checks similar to ours, not height arrays
2. **Ceiling deflection is a CUSTOM feature** — neither SOTN, Sonic, nor Mega Man does it. It's our own movement tech.
3. **Point queries > bounding box**: Both SOTN and Sonic query individual sensor points. Our multi-sensor approach is correct.
4. **Gentle slopes need tile chaining**: SOTN's 22.5° slopes span TWO tiles with an offset. Our gentle slopes do similar math within a single tile (half-height rise).
5. **Separate floor/ceiling collision paths**: SOTN handles floor and ceiling slopes with completely separate flag sets and push directions. Our code already does this.

## Applicable Code Patterns

### SOTN Right 45° Floor Slope (/)
```c
case COLLISION_TYPE_RIGHT_45_ANGLE:
    if (res->unk1C + res->unk20 < 0x10) {
        // Inside the slope — push up
        res->unk14 = res->unk18 = res->unk1C + res->unk20 - 0xF;
        res->effects = EFFECT_UNK_8000 | EFFECT_SOLID;
    } else {
        // Above the slope — just flag it
        res->effects = EFFECT_UNK_8000;
    }
    break;
```
Translation: `localX + localY < 16` means we're inside the solid triangle. Push amount = `localX + localY - 15` (negative = push up).

### SOTN Right 45° Ceiling Slope (\)
```c
case COLLISION_TYPE_RIGHT_CEILING_45_ANGLE:
    if (res->unk1C <= res->unk20) {
        res->unk14 = res->unk1C - res->unk20;
        res->unk20 = -res->unk14;
        res->effects = EFFECT_UNK_0800 | EFFECT_SOLID;
    } else {
        res->effects = EFFECT_UNK_0800;
    }
    break;
```
Translation: `localX <= localY` means inside ceiling slope. Push down by `localX - localY`.
