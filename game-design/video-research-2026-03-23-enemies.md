# Enemy Design Research — DM Studio (2026-03-23)
## Source: https://www.youtube.com/watch?v=bZeHah4eg-E

## Key Takeaways

### 1. Design from GAMEPLAY first, not lore/visuals
- Wrong: "It's a guy named Gregory who throws his phone" → designing from character/story
- Right: Start with an **action** (dash, shoot, burrow, explode) → enemy designs itself around that
- Write a list of enemy ACTIONS, pick one, build the enemy around it
- Visuals come AFTER the feel is right

### 2. Movement: Don't use force-based sliding
- Force + drag = enemies feel like sliding on ice
- **Set velocity directly** instead of adding forces
- Smooth between current direction and target direction (lerp per frame)
- High lerp rate = barely noticeable but no choppy direction changes
- Bug lesson: If direction smoothing is tied to actual velocity, obstacles can reset the smoothing and cause old-path-following. Keep current direction INDEPENDENT of actual movement velocity.

### 3. Line-of-sight checks before attacks
- Before dashing/charging: draw lines from enemy to player
- Use FOUR lines from enemy EDGES (not center) to ensure the full body can pass
- If any line hits an obstacle → delay the attack
- Add small cooldown after line clears → prevents close-call collisions, feels more natural

### 4. Telegraphing is CRUCIAL
- Even without animation/sound: color change before attack (darker shade, ~0.5s)
- Small change but extremely important for player reaction time
- Every attack needs a tell

### 5. Multi-enemy testing early
- Enemies dash into each other, disrupt each other
- Fix: Each enemy checks if ANOTHER ENEMY is between it and the player → delay attack
- Test with 3-4 of same enemy type immediately after basic behavior works

### 6. Predictive aiming (the breakthrough)
- Instead of spawning projectiles AT player position...
- Spawn where the player WILL BE based on current velocity
- `predictedPos = playerPos + playerVelocity * leadTime`
- Single change took enemy from "pretty okay" to "genuinely good"
- Makes dodging require actual direction changes, not just moving

### 7. Visuals DO matter for feel (the revelation)
- Pure squares = boring enemies even with good mechanics
- "How a game feels is inherently tied to the way it looks"
- Solution: Use MINIMAL art (old sprites, no animations) — not zero art
- The sweet spot: enough visual identity to feel real, not enough to distract from gameplay iteration

### 8. Attack smoothing
- Dash end: smooth deceleration (don't abruptly stop)
- Follow start after attack: smooth acceleration back into patrol
- Make these toggleable per enemy so you can disable if it doesn't work

## Application to Genesis

### Bombardier Crawler Variant
- **Action**: ranged spray (hot liquid stream)
- **Behavior**: When threatened (player within aggro range), turns away and fires heated pixel stream backward (like real bombardier beetles)
- **Telegraphing**: Abdomen glows orange ~0.5s before spray
- **Predictive aiming**: Stream aims at predicted player position
- **Line-of-sight**: Only fires if clear path to player
- **Multi-enemy**: If another crawler is between bombardier and player, delay spray

### All Enemies Should Have
- [ ] Telegraphing (color/shape change before attack)
- [ ] Line-of-sight checks before ranged/charge attacks (4-edge, not center)
- [ ] Predictive aiming on projectiles (`pos + vel * leadTime`)
- [ ] Multi-enemy awareness (don't attack through allies)
- [ ] Smooth transitions between states (no abrupt stops)
- [ ] Direction smoothing independent of actual velocity
- [ ] Test with 3-4 instances immediately
