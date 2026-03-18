# Backlog

## Next Up: New Tile Types (2026-03-19)

### Breakable & Interactive
- [ ] **Breakable tile** — full solid tile, drops health item (+10 HP) when attacked (only if not full health). Disappears after hit, reloads on room re-entry.
- [ ] **Damage tile** — does slow continuous damage while player remains in contact
- [ ] **Knockback tile** — knocks player back when touched at high velocity
- [ ] **Speed boost tile** — briefly increases movement speed on contact
- [ ] **Float tile** — makes player slowly float upward on a timer

### Spikes
- [ ] **Full spike tile** — up, down, left, right variants
- [ ] **Half spike tile** — up, down, left, right variants

### Platforms
- [ ] **Half platform (top)** — platform spanning top half of tile
- [ ] **Half platform (bottom)** — platform spanning bottom half of tile

### Slopes
- [ ] **Gentle slope 1:4** — 1 tile rise over 4 tile run (floor + ceiling, left + right)
- [ ] **Ceiling gentle slope 1:4** — ceiling versions left + right
- [ ] *(1:3 slope — maybe, might look weird)*

### Each tile gets its own unique symbol for the editor

---

## Physics / Movement
- [ ] Ceiling slopes: slide and retain momentum without being sticky
- [ ] Slide cancels too soon on floor slopes
- [ ] Left/right slope climbing asymmetry (multi-sensor added, needs testing)
- [ ] Ceiling tiles between slopes still catch player hitbox
- [ ] Slope animation/rotation — rotate player sprite to match slope angle (deferred)

## Combat
- [ ] Dagger combo tree with vault kick, shine spark air dash details
- [ ] Crafting recipes for each weapon/ammo type

## Design
- [ ] NPC assistant crafting mechanics
- [ ] Weapons & crafting system implementation (design doc at `game-design/weapons-and-crafting.md`)

## Visual
- [ ] Dark purple background with white outlines for debug room (partially done)
