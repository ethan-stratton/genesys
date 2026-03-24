using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public class Thornback : Creature
{
    public const int Width = 32, Height = 28;
    public float DamageCooldown;
    public float MeleeHitCooldown;
    public float SquashResistance = 0.7f;
    private float _squashHoldTimer;

    public Thornback(Vector2 pos)
    {
        Position = pos;
        Hp = 20; MaxHp = 20;
        SpeciesName = "Thornback";
        Role = EcologicalRole.Defensive;
        Needs = CreatureNeeds.Default;
    }

    public override int CreatureWidth => Width;
    public override int CreatureHeight => Height;
    public override Rectangle Rect => new((int)Position.X, (int)Position.Y, Width, Height);

    public void Update(float dt)
    {
        if (!Alive) return;
        if (DamageCooldown > 0) DamageCooldown -= dt;
        if (HitFlash > 0) HitFlash -= dt;
        if (MeleeHitCooldown > 0) MeleeHitCooldown -= dt;

        if (_squashHoldTimer > 0) _squashHoldTimer -= dt;
        else VisualScale = Vector2.Lerp(VisualScale, Vector2.One, 8f * dt);
    }

    public int CheckPlayerDamage(Rectangle playerRect)
    {
        if (!Alive || DamageCooldown > 0) return 0;
        if (Rect.Intersects(playerRect))
        {
            DamageCooldown = 0.5f;
            return 8;
        }
        return 0;
    }

    public override bool TakeHit(int damage, float knockbackX = 0, float knockbackY = 0)
    {
        if (!Alive || MeleeHitCooldown > 0) return false;
        Hp -= damage;
        HitFlash = 0.15f;
        MeleeHitCooldown = 0.2f;
        // Ignore knockback — thornback is stationary
        float squashAmount = 1f - SquashResistance;
        VisualScale = new Vector2(1f + 0.3f * squashAmount, 1f - 0.25f * squashAmount);
        _squashHoldTimer = 0.05f;
        if (Hp <= 0) { Alive = false; return true; }
        return false;
    }

    public override void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!Alive) return;
        Color baseColor = HitFlash > 0 ? Color.White : new Color(60, 100, 30);
        int scaledW = (int)(Width * VisualScale.X);
        int scaledH = (int)(Height * VisualScale.Y);
        int drawX = (int)Position.X + Width / 2 - scaledW / 2;
        int drawY = (int)Position.Y + Height - scaledH;
        sb.Draw(pixel, new Rectangle(drawX, drawY, scaledW, scaledH), baseColor);
        sb.Draw(pixel, new Rectangle((int)Position.X - 3, (int)Position.Y + 6, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 1, (int)Position.Y + 8, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + 8, (int)Position.Y - 3, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + Width - 12, (int)Position.Y - 3, 4, 4), baseColor * 0.8f);
        sb.Draw(pixel, new Rectangle((int)Position.X + 4, (int)Position.Y + 4, Width - 8, Height - 8), new Color(40, 70, 20));
    }
}
