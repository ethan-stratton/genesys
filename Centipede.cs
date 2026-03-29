using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public struct CentipedeSegment
{
    public Vector2 Position;
    public float Angle;
    public float Width;
}

public class Centipede : Creature
{
    public const int HeadW = 8, HeadH = 6;
    public const int SegmentCount = 14;
    public const float SegmentSpacing = 6f;

    public CentipedeSegment[] Segments;

    private Vector2[] _pathHistory = new Vector2[120];
    private int _pathHead;

    private float _speed = 45f;
    private bool _onGround;
    private readonly Random _rng;
    private float _animTime;

    // Wall-walking
    private enum GravDir { Down, Left, Right, Up }
    private GravDir _gravDir = GravDir.Down;
    private float _wallCheckTimer;

    // Behavior
    private float _coilTimer;
    private bool _isCoiling;
    private float _startleTimer;
    private Vector2[] _startleVelocities;

    // Hunting
    private Creature _huntTarget;
    private float _huntScanTimer;

    // Colors
    private static readonly Color HeadColor = new Color(139, 37, 0);       // #8B2500
    private static readonly Color BandA = new Color(92, 51, 23);           // #5C3317
    private static readonly Color BandB = new Color(107, 58, 42);          // #6B3A2A
    private static readonly Color LegColor = new Color(80, 40, 20);
    private static readonly Color MandibleColor = new Color(60, 20, 5);
    private static readonly Color AntennaColor = new Color(120, 60, 30);

    public override bool IsNocturnal => true;
    public override int ContactDamage => 3;
    public override int CreatureWidth => HeadW;
    public override int CreatureHeight => HeadH;
    public override Rectangle Rect => new((int)Segments[0].Position.X - HeadW / 2, (int)Segments[0].Position.Y - HeadH / 2, HeadW, HeadH);

    public override float GetDamageMultiplier(DamageType type) => type switch
    {
        DamageType.Fire => 1.5f,
        DamageType.Blunt => 0.7f,
        _ => 1f,
    };

    public Centipede(Vector2 pos, Random rng)
    {
        _rng = rng;
        Position = pos;
        Hp = 25; MaxHp = 25;
        SpeciesName = "centipede";
        Role = EcologicalRole.Predator;
        Needs = CreatureNeeds.Default;
        Needs.Hunger = 0.3f + (float)rng.NextDouble() * 0.3f;
        DeathParticleColor = HeadColor;
        HitColor = BandA;
        SpawnOrigin = pos;
        HungerRate = 0.003f;
        FatigueRate = 0.001f;
        Dir = rng.NextDouble() < 0.5 ? 1 : -1;

        // Init segments
        Segments = new CentipedeSegment[SegmentCount];
        _startleVelocities = new Vector2[SegmentCount];
        for (int i = 0; i < SegmentCount; i++)
        {
            float t = (float)i / (SegmentCount - 1);
            Segments[i] = new CentipedeSegment
            {
                Position = pos - new Vector2(i * SegmentSpacing * Dir, 0),
                Angle = 0,
                Width = MathHelper.Lerp(7f, 3f, t),
            };
        }
        // Head is widest
        Segments[0].Width = 8f;

        // Fill path history with initial position
        for (int i = 0; i < _pathHistory.Length; i++)
            _pathHistory[i] = pos;
    }

    public override void Update(float dt, CreatureUpdateContext ctx)
    {
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;
        TickDirCooldown(dt);
        _animTime += dt;

        TickNeeds(dt);

        // Activity level (nocturnal)
        float activity = GetActivityLevel(ctx.WorldTime);
        float speedMult = activity < 0.3f ? 0.7f : (activity > 0.8f ? 1.5f : 1f);
        if (ctx.IsRaining) speedMult *= 1.2f; // loves moisture

        float effectiveSpeed = _speed * speedMult;

        // Startle response
        if (_startleTimer > 0)
        {
            _startleTimer -= dt;
            for (int i = 0; i < SegmentCount; i++)
            {
                Segments[i].Position += _startleVelocities[i] * dt;
                _startleVelocities[i] *= 0.9f; // dampen
            }
            if (_startleTimer <= 0)
            {
                // Segments will reconverge via normal path history
            }
            UpdateSegmentAngles();
            Position = Segments[0].Position;
            return;
        }

        // Hunting scan
        _huntScanTimer -= dt;
        if (_huntScanTimer <= 0)
        {
            _huntScanTimer = 0.5f;
            _huntTarget = null;
            if (Needs.Hunger > 0.5f && ctx.NearbyCreatures != null)
            {
                float bestDist = 200f;
                foreach (var c in ctx.NearbyCreatures)
                {
                    if (c == this || !c.Alive) continue;
                    if (c.IsBurrowed) continue;
                    if (!WillHunt(c) && c.Role != EcologicalRole.Prey && c.Role != EcologicalRole.Herbivore && c.Role != EcologicalRole.Flighty) continue;
                    float d = Vector2.Distance(Segments[0].Position, c.Position);
                    if (d < bestDist) { bestDist = d; _huntTarget = c; }
                }
            }
        }

        // Head movement
        ref var head = ref Segments[0];

        if (_huntTarget != null && _huntTarget.Alive)
        {
            // Chase prey
            _isCoiling = false;
            Vector2 toTarget = _huntTarget.Position - head.Position;
            float dist = toTarget.Length();
            if (dist > 1f)
            {
                Vector2 moveDir = toTarget / dist;
                if (_gravDir == GravDir.Down)
                {
                    // Ground movement: only horizontal
                    head.Position.X += MathF.Sign(moveDir.X) * effectiveSpeed * dt;
                    if (moveDir.X > 0) TrySetDir(1);
                    else if (moveDir.X < 0) TrySetDir(-1);
                }
                else
                {
                    head.Position += moveDir * effectiveSpeed * 0.8f * dt;
                }
            }
            // Contact damage
            if (dist < 10f)
            {
                _huntTarget.TakeDamage(3);
                Needs.Hunger = MathHelper.Clamp(Needs.Hunger - 0.3f, 0f, 1f);
                _huntTarget = null;
                _huntScanTimer = 1f;
            }
        }
        else if (_isCoiling)
        {
            // Coil behavior - spiral inward
            _coilTimer -= dt;
            if (_coilTimer <= 0) _isCoiling = false;
            // Head stays still when coiling, segments converge
        }
        else
        {
            // Wander
            if (CurrentGoal == CreatureGoal.Rest && Needs.Fatigue > 0.6f)
            {
                _isCoiling = true;
                _coilTimer = 3f + (float)_rng.NextDouble() * 5f;
            }
            else
            {
                if (_gravDir == GravDir.Down)
                {
                    head.Position.X += Dir * effectiveSpeed * dt;
                    // Ledge/wall detection
                    if (ctx.TileGrid != null)
                    {
                        if (HasWallAhead(ctx.TileGrid, ctx.TileSize, head.Position, Dir, 8f))
                        {
                            // Try wall walk
                            _gravDir = Dir > 0 ? GravDir.Right : GravDir.Left;
                        }
                        else if (!HasFloorAhead(ctx.TileGrid, ctx.TileSize, head.Position, Dir, 8f, 48f))
                        {
                            TrySetDir(-Dir, true);
                        }
                    }
                }
                else
                {
                    // Wall/ceiling movement
                    float wallSpeed = effectiveSpeed * 0.78f;
                    switch (_gravDir)
                    {
                        case GravDir.Right:
                            head.Position.Y += Dir * wallSpeed * dt;
                            head.Position.X += 20f * dt; // push toward wall
                            break;
                        case GravDir.Left:
                            head.Position.Y += Dir * wallSpeed * dt;
                            head.Position.X -= 20f * dt;
                            break;
                        case GravDir.Up:
                            head.Position.X += Dir * wallSpeed * dt;
                            head.Position.Y -= 20f * dt; // push toward ceiling
                            break;
                    }
                }
            }
        }

        // Gravity when on ground
        if (_gravDir == GravDir.Down)
        {
            Velocity.Y += 600f * dt;
            head.Position.Y += Velocity.Y * dt;

            // Simple ground collision
            if (ctx.TileGrid != null)
            {
                int col = (int)(head.Position.X / ctx.TileSize);
                int row = (int)((head.Position.Y + HeadH / 2f) / ctx.TileSize);
                if (TileProperties.IsSolid(ctx.TileGrid.GetTileAt(col, row)))
                {
                    head.Position.Y = row * ctx.TileSize - HeadH / 2f;
                    Velocity.Y = 0;
                    _onGround = true;
                }
                else
                {
                    _onGround = false;
                }
            }
        }
        else
        {
            Velocity = Vector2.Zero;
        }

        // Wall-walk transitions
        _wallCheckTimer -= dt;
        if (_wallCheckTimer <= 0 && ctx.TileGrid != null)
        {
            _wallCheckTimer = 0.1f;
            UpdateWallWalk(ctx.TileGrid, ctx.TileSize);
        }

        Segments[0] = head;

        // Record path history
        _pathHistory[_pathHead] = Segments[0].Position;
        _pathHead = (_pathHead + 1) % _pathHistory.Length;

        // Update body segments from path history
        if (_isCoiling)
        {
            // Coiling: segments spiral toward head
            Vector2 center = Segments[0].Position;
            for (int i = 1; i < SegmentCount; i++)
            {
                float angle = _animTime * 2f + i * 0.45f;
                float radius = i * 3f;
                Vector2 target = center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                Segments[i].Position = Vector2.Lerp(Segments[i].Position, target, dt * 4f);
            }
        }
        else
        {
            for (int i = 1; i < SegmentCount; i++)
            {
                int offset = (i) * 4;
                int idx = ((_pathHead - offset) % _pathHistory.Length + _pathHistory.Length) % _pathHistory.Length;
                Vector2 target = _pathHistory[idx];
                Segments[i].Position = Vector2.Lerp(Segments[i].Position, target, dt * 12f);
            }
        }

        UpdateSegmentAngles();
        Position = Segments[0].Position;

        // Bounds
        if (Position.X < ctx.BoundsLeft + 10) { Position.X = ctx.BoundsLeft + 10; TrySetDir(1, true); }
        if (Position.X > ctx.BoundsRight - 10) { Position.X = ctx.BoundsRight - 10; TrySetDir(-1, true); }
    }

    private void UpdateWallWalk(TileGrid tg, int ts)
    {
        int cx = (int)(Segments[0].Position.X / ts);
        int cy = (int)(Segments[0].Position.Y / ts);

        switch (_gravDir)
        {
            case GravDir.Down:
                if (TileProperties.IsSolid(tg.GetTileAt(cx + Dir, cy)))
                    _gravDir = Dir > 0 ? GravDir.Right : GravDir.Left;
                break;
            case GravDir.Right:
                if (!TileProperties.IsSolid(tg.GetTileAt(cx + 1, cy)))
                    _gravDir = GravDir.Up;
                break;
            case GravDir.Up:
                if (!TileProperties.IsSolid(tg.GetTileAt(cx, cy - 1)))
                    _gravDir = GravDir.Down;
                break;
            case GravDir.Left:
                if (!TileProperties.IsSolid(tg.GetTileAt(cx - 1, cy)))
                    _gravDir = GravDir.Up;
                break;
        }
    }

    private void UpdateSegmentAngles()
    {
        for (int i = 0; i < SegmentCount - 1; i++)
        {
            Vector2 diff = Segments[i + 1].Position - Segments[i].Position;
            Segments[i].Angle = MathF.Atan2(diff.Y, diff.X);
        }
        if (SegmentCount > 1)
            Segments[SegmentCount - 1].Angle = Segments[SegmentCount - 2].Angle;
    }

    public override bool TakeDamage(int amount)
    {
        bool killed = base.TakeDamage(amount);
        if (!killed && Alive)
        {
            // Startle response
            _startleTimer = 0.3f;
            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = (float)_rng.NextDouble() * MathF.PI * 2f;
                float force = 50f + (float)_rng.NextDouble() * 80f;
                _startleVelocities[i] = new Vector2(MathF.Cos(angle) * force, MathF.Sin(angle) * force);
            }
        }
        return killed;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        Color flashTint = HitFlash > 0 ? Color.White : Color.Transparent;

        // Draw segments back to front (tail first)
        for (int i = SegmentCount - 1; i >= 0; i--)
        {
            ref var seg = ref Segments[i];
            Color segColor;
            if (i == 0) segColor = HeadColor;
            else segColor = (i % 2 == 0) ? BandA : BandB;

            if (HitFlash > 0) segColor = Color.Lerp(segColor, Color.White, 0.6f);

            float w = seg.Width;
            float h = w * 0.7f;

            // Body rectangle rotated
            Vector2 origin = new Vector2(w / 2f, h / 2f);
            sb.Draw(pixel,
                new Rectangle((int)seg.Position.X, (int)seg.Position.Y, (int)w, (int)h),
                null, segColor, seg.Angle, new Vector2(0.5f, 0.5f), SpriteEffects.None, 0);

            // Legs (2 per segment, except head and tail)
            if (i > 0 && i < SegmentCount - 1)
            {
                // Metachronal wave: each segment pair offset so legs ripple back-to-front
                float legPhase = _animTime * 10f + i * 0.7f;
                float legSwing = MathF.Sin(legPhase); // -1 to 1: forward/back swing

                float perpAngle = seg.Angle + MathF.PI / 2f;
                Vector2 perpDir = new Vector2(MathF.Cos(perpAngle), MathF.Sin(perpAngle));
                Vector2 fwdDir = new Vector2(MathF.Cos(seg.Angle), MathF.Sin(seg.Angle));

                float legLen = w * 0.6f + 2f; // legs extend outward from body
                float liftArc = MathF.Max(0, MathF.Sin(legPhase)) * 2f; // lift during forward stroke

                for (int side = -1; side <= 1; side += 2)
                {
                    // Leg root: at segment edge
                    Vector2 root = seg.Position + perpDir * (w * 0.35f) * side;
                    // Leg tip: extends outward + swings forward/back + lifts
                    Vector2 tip = root
                        + perpDir * legLen * side                          // extend outward
                        + fwdDir * legSwing * 2.5f                        // swing forward/back
                        - new Vector2(0, liftArc * (side == 1 ? 1 : 0));  // alternate lift

                    // Opposite side is on opposite phase
                    if (side == -1)
                    {
                        float legSwing2 = MathF.Sin(legPhase + MathF.PI);
                        float liftArc2 = MathF.Max(0, MathF.Sin(legPhase + MathF.PI)) * 2f;
                        tip = root
                            + perpDir * legLen * side
                            + fwdDir * legSwing2 * 2.5f
                            - new Vector2(0, liftArc2);
                    }

                    DrawLine(sb, pixel, root, tip, LegColor);
                }
            }
        }

        // Head details: antennae
        ref var headSeg = ref Segments[0];
        float antPhase = _animTime * 3f;
        for (int side = -1; side <= 1; side += 2)
        {
            float antAngle = headSeg.Angle + side * 0.5f + MathF.Sin(antPhase + side) * 0.2f;
            Vector2 antEnd = headSeg.Position + new Vector2(MathF.Cos(antAngle), MathF.Sin(antAngle)) * 6f;
            DrawLine(sb, pixel, headSeg.Position, antEnd, AntennaColor);
        }

        // Mandibles
        for (int side = -1; side <= 1; side += 2)
        {
            float mandAngle = headSeg.Angle + side * 0.3f;
            Vector2 mandEnd = headSeg.Position + new Vector2(MathF.Cos(mandAngle), MathF.Sin(mandAngle)) * 4f;
            DrawLine(sb, pixel, headSeg.Position, mandEnd, MandibleColor);
        }
    }

    private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b, Color color)
    {
        Vector2 diff = b - a;
        float length = diff.Length();
        if (length < 0.5f) return;
        float angle = MathF.Atan2(diff.Y, diff.X);
        sb.Draw(pixel, new Rectangle((int)a.X, (int)a.Y, (int)length, 1), null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
    }
}
