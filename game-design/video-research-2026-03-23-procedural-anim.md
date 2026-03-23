# Procedural Animation Research — 2026-03-23

Research notes from 4 YouTube videos on procedural animation techniques. Context: Genesis 2D action RPG, MonoGame/C#, CPU-based. We have 6-legged crawlers with 2-segment IK (law of cosines), tripod gait, segmented body (head/thorax/abdomen).

---

## 1. Video Metadata

| # | Title | Channel | URL |
|---|-------|---------|-----|
| yt3 | Giving Personality to Procedural Animations using Math | t3ssel8r | https://www.youtube.com/watch?v=KPoeNZZ6H4s |
| yt4 | Generating Monsters: BEAST SOCKET Devlog 01 | RujiK the Comatose | https://www.youtube.com/watch?v=z_fmMD-Gazw |
| yt8 | Procedural Animation in 5 Minutes \| devlog 1 | Benjamin Blodgett | https://www.youtube.com/watch?v=PcpkBzcRdSU |
| yt9 | Procedurally Animating Creatures for my Game \| Withersworn Devlog | Asarge | https://www.youtube.com/watch?v=I7BRv5wjeZg |

---

## 2. Key Techniques per Video

### yt3 — t3ssel8r: Second-Order Dynamics (⭐ Most Actionable)

The core idea: replace rigid tracking (`y = x`) with a **second-order system** that adds inertia, overshoot, and anticipation to any value.

**The System:**
```
k2 * ÿ + k1 * ẏ + y = x + k3 * ẋ
```

Reparameterized into artist-friendly controls:
- **f** — natural frequency (Hz). Speed of response. Higher = snappier.
- **zeta (ζ)** — damping. 0 = infinite vibration, 1 = critical damping (Unity SmoothDamp), >1 = sluggish.
- **r** — initial response. 0 = slow start, >1 = overshoot, <0 = anticipation.

**Computing k1, k2, k3 from f, ζ, r:**
```csharp
float k1 = zeta / (MathF.PI * f);
float k2 = 1f / (4f * MathF.PI * MathF.PI * f * f);
float k3 = r * zeta / (2f * MathF.PI * f);
```

**Semi-implicit Euler update (per frame):**
```csharp
// State: float y, yd (position, velocity)
// Input: float x (target), float xd (target velocity, can estimate), float dt
float xd_est = (x - xPrev) / dt; // if xd unknown
xPrev = x;
y += dt * yd;
yd += dt * (x + k3 * xd_est - y - k1 * yd) / k2;
```

**Stability fix** — clamp k2 or subdivide timestep:
```csharp
float T_crit = MathF.Sqrt(4f * k2 + k1 * k1) - k1;
// If dt > T_crit, either subdivide or clamp k2:
float k2_stable = MathF.Max(k2, (dt * dt + dt * k1) / 4f);
```

**Applications from the video:**
- Body position: ζ=0.5, r=-2 (underdamped + anticipation)
- Body orientation: ζ=1, r=0 (smooth turn, no overshoot)
- Head orientation: same but lower f → lags behind body (secondary motion)
- Tilt body toward movement direction for intent
- Modulate f/ζ/r based on creature state (health, alert, staggered)

### yt4 — RujiK: Body Generation & Segment Chain (Beast Socket)

**Follow-the-leader body chain:**
1. Chain of circles/segments, each follows the one ahead (like snake game)
2. Smooth collisions with terrain so body doesn't jerk
3. Set max bend angle between joints for rigidity control
4. Wrap with polygon mesh (belly texture + back texture)

**Foot placement (distance-threshold method):**
- Each foot has a "rest position" (where it should be after stepping)
- When current foot position drifts too far from rest → initiate step
- Otherwise foot stays planted (sticky feet)

**IK via law of cosines** (same as what we have):
- Hip + foot positions known → solve for knee angle
- Translate 2D knee position into final coordinates

**Body construction:**
- Segmented polygon mesh over the ball chain
- Separate belly/back textures
- Head as distinct segment with face details
- Legs attached to specific body segments

### yt8 — Benjamin Blodgett: Foot Placement Theory & Problems

**IK function (matches our approach):**
- Two-segment IK, law of cosines
- Local space: subtract hip global pos from foot global pos
- Handle NaN when limb over-extends (clamp angle to 0)
- Positive elbow angle = knee leg, negative = ankle leg

**Basic foot placement (and its problems):**
1. Each foot has a target that hovers at fixed offset from body
2. Target always on ground
3. Lerp current pos → target pos each frame
4. Trigger step when foot-to-hip distance > upper+lower leg length

**Problems identified (relevant to us!):**
- ❌ Leg pairs move in sync (looks robotic)
- ❌ Feet don't lift off ground (shuffling)
- ❌ Movement is abrupt — no arc, no easing
- ❌ Single step distance doesn't work for varying speeds

**Fixes mentioned:**
- Alternating foot selection with timer (we do this with tripod gait ✓)
- Need separate step distance/period for different speeds
- Velocity-based parameter adjustment

### yt9 — Asarge: Full Creature System (Withersworn)

**10-step procedural walk setup:**
1. IK controls the leg
2. Glue foot to ground
3. Target point attached to body
4. Raycast down from target to find ground height
5. Distance check: foot → target, threshold triggers step
6. Alternate legs (left/right, or tripod groups)
7. Body height = average leg position + offset
8. Body rotation = difference between left/right leg heights
9. Add features: step duration, step height arc, angle thresholds
10. Prohibit feet from stepping into each other's regions

**Additional creature features:**
- **Head tracking:** head turns toward target/player, can chain down spine
- **Spine/tail:** sine-wave bone chain for organic sway
- **Jiggle on hit:** bones simulate impact, impulse knockback
- **Damaged limb compensation:** destroyed legs change gait/stance
- **Boids-like pathfinding:** separation/alignment/cohesion for enemy movement
- **Ragdoll blending:** blend procedural + ragdoll for eerie movement or hit reactions
- **Cloth/hair/ooze sim:** follow-through on mesh deformation

---

## 3. Implementation Checklist — Improvements to Existing Crawler

These target our current `IKLeg` struct and crawler system directly.

### 3.1 Second-Order Dynamics System (from yt3)

Create a reusable `SecondOrderDynamics` class:

```csharp
public class SecondOrderDynamics {
    private float xPrev, y, yd; // state
    private float k1, k2, k3;   // computed constants

    public SecondOrderDynamics(float f, float zeta, float r, float x0) {
        k1 = zeta / (MathF.PI * f);
        k2 = 1f / (4f * MathF.PI * MathF.PI * f * f);
        k3 = r * zeta / (2f * MathF.PI * f);
        xPrev = x0;
        y = x0;
        yd = 0f;
    }

    public float Update(float dt, float x) {
        float xd = (x - xPrev) / dt;
        xPrev = x;
        // Stability: clamp k2
        float k2Stable = MathF.Max(k2, MathF.Max(dt * dt / 4f + dt * k1 / 2f, dt * k1));
        y += dt * yd;
        yd += dt * (x + k3 * xd - y - k1 * yd) / k2Stable;
        return y;
    }
}
```

Make a `Vector2` version too (run independently on X and Y).

### 3.2 Apply Second-Order to Existing Systems

| What | f | ζ | r | Effect |
|------|---|---|---|--------|
| Body position (thorax) | 3-5 | 0.5 | -2 | Underdamped + anticipation → bouncy movement |
| Body rotation | 2-3 | 1.0 | 0 | Smooth turning, no jitter |
| Head tracking | 1-2 | 0.8 | 0 | Lags behind body → secondary motion |
| Abdomen position | 1-2 | 0.6 | 0 | Trails behind thorax, slight wobble |

### 3.3 Foot Step Arc (from yt8/yt9 critique)

Our current system likely lerps foot position linearly. Add a vertical arc:

```csharp
// In IKLeg, during step (t goes 0→1 over StepDuration):
float arc = MathF.Sin(t * MathF.PI) * StepHeight; // parabolic arc
Vector2 footPos = Vector2.Lerp(stepStart, stepEnd, t) + new Vector2(0, -arc);
```

### 3.4 Step Easing Curve

Replace linear `t` with eased `t` for snappier steps:

```csharp
// Ease-out cubic — fast start, smooth landing
float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

// Or use second-order dynamics on foot position for organic feel
```

### 3.5 Body Height from Leg Average

```csharp
float avgFootY = legs.Average(l => l.FootPos.Y);
bodyY = avgFootY + bodyHeightOffset; // smoothed via SecondOrderDynamics
```

### 3.6 Body Tilt from Leg Difference

```csharp
float leftAvg = leftLegs.Average(l => l.FootPos.Y);
float rightAvg = rightLegs.Average(l => l.FootPos.Y);
// For 2D side-view: use front/back legs instead
float frontAvg = frontLegs.Average(l => l.FootPos.Y);
float backAvg = backLegs.Average(l => l.FootPos.Y);
float tiltAngle = MathF.Atan2(frontAvg - backAvg, bodyLength);
// Smooth via SecondOrderDynamics
```

### 3.7 Speed-Dependent Step Parameters

```csharp
float speed = body.Velocity.Length();
float stepDistance = MathHelper.Lerp(minStepDist, maxStepDist, speed / maxSpeed);
float stepDuration = MathHelper.Lerp(0.15f, 0.06f, speed / maxSpeed); // slower steps when slow
```

### 3.8 Foot Placement Region Guards (from yt9)

Prevent two feet from targeting the same spot:

```csharp
// Before committing a step target, check no other foot is stepping to nearby position
foreach (var other in legs) {
    if (other != this && other.IsStepping &&
        Vector2.Distance(other.StepTarget, newTarget) < minFootSeparation)
        return; // defer step
}
```

---

## 4. New Techniques to Add

### 4.1 Personality Through Animation Parameters

From yt3 — modulate second-order parameters based on creature state:

| State | f | ζ | r | Visual Effect |
|-------|---|---|---|--------------|
| Idle/calm | 1.5 | 1.0 | 0 | Smooth, relaxed |
| Alert/aggro | 4.0 | 0.4 | -1 | Twitchy, anticipatory |
| Damaged/low HP | 1.0 | 0.3 | 0 | Wobbly, unstable |
| Stunned | 0.5 | 0.2 | 0 | Slow, vibrating |
| Charging | 5.0 | 0.8 | 2 | Snappy with overshoot |

### 4.2 Segmented Body Chain (from yt4)

Evolve our rigid 3-segment body into a follow-the-leader chain:

```csharp
// Each segment follows the one ahead with max distance + max bend angle
for (int i = 1; i < segments.Length; i++) {
    Vector2 dir = segments[i].Pos - segments[i-1].Pos;
    float dist = dir.Length();
    if (dist > segmentSpacing) {
        segments[i].Pos = segments[i-1].Pos + Vector2.Normalize(dir) * segmentSpacing;
    }
    // Clamp bend angle between consecutive segments
    float angle = MathF.Atan2(dir.Y, dir.X);
    float prevAngle = segments[i-1].Rotation;
    float clampedAngle = ClampAngle(angle, prevAngle, maxBendAngle);
    segments[i].Rotation = clampedAngle;
}
```

### 4.3 Head Tracking Target

```csharp
// Head looks toward player or movement direction
Vector2 lookTarget = hasTarget ? targetPos : body.Position + body.Velocity * 2f;
headAngle = headAngleDynamics.Update(dt, targetAngle); // second-order smoothed
```

### 4.4 Spine/Tail Sine Wave (from yt9)

```csharp
// For tail segments or abdomen wobble
float time = (float)gameTime.TotalGameTime.TotalSeconds;
for (int i = 0; i < tailSegments.Length; i++) {
    float offset = MathF.Sin(time * tailFreq + i * tailPhaseOffset) * tailAmplitude;
    tailSegments[i].Pos += new Vector2(0, offset); // or perpendicular to segment direction
}
```

### 4.5 Damaged Limb Compensation (from yt9)

When a leg is destroyed:
- Remove it from tripod gait group
- Redistribute stepping pattern
- Body tilts toward missing leg side
- Movement speed decreases
- Could trigger different animation personality (wobbly ζ)

### 4.6 Hit Reaction / Jiggle (from yt9)

On taking damage, apply impulse to second-order dynamics:

```csharp
// Add velocity impulse to body dynamics
bodyDynamics.AddImpulse(knockbackDirection * force);
// Each segment gets delayed impulse (wave propagation)
```

---

## 5. Priority Order for Implementation

**Tier 1 — Immediate wins (use this week)**
1. **`SecondOrderDynamics` class** — reusable everywhere, ~30 lines of code
2. **Apply to body position** — instant juiciness on crawler movement
3. **Foot step arc** — sin curve during step, replaces flat lerp
4. **Step easing curve** — ease-out on foot movement

**Tier 2 — Polish (next sprint)**
5. **Body height from leg average** — body bobs naturally with terrain
6. **Body tilt from leg positions** — crawler tilts on slopes
7. **Head tracking with second-order lag** — secondary motion on head segment
8. **Abdomen lag** — second-order on abdomen following thorax

**Tier 3 — Depth (when combat system needs it)**
9. **Personality parameters per creature state** — idle/aggro/damaged feel different
10. **Speed-dependent step parameters** — steps adapt to movement speed
11. **Hit reaction impulse** — knockback through second-order velocity injection
12. **Foot placement region guards** — prevent overlapping feet

**Tier 4 — Advanced (future creatures / boss variants)**
13. **Flexible body chain** (replace rigid segments with follow-the-leader)
14. **Tail/spine sine wave**
15. **Damaged limb compensation**
16. **Procedural body generation** (polygon mesh over segment chain, like yt4)

---

*The second-order dynamics system from t3ssel8r (yt3) is the single highest-leverage addition. It's a ~30-line utility class that can improve every moving value in the game — body position, rotation, camera, UI elements, anything. Build it first, apply it everywhere.*
