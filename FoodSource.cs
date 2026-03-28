using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Genesis;

public enum FoodType
{
    Debris,    // scrap/detritus — scavengers, forager crawlers
    Plant,     // organic vegetation — herbivores
    Corpse,    // dead creature — scavengers, predators
    Insect,    // small bugs — birds
    FertileGround, // decomposed corpse → accelerates plant growth here
}

public class FoodSource
{
    public Vector2 Position;
    public FoodType Type;
    public float Nutrition;   // how much hunger it satisfies (0-1)
    public float Amount;      // how much is left (0-1), depletes as eaten
    public bool Depleted => Amount <= 0;
    
    // Visual
    public float Size;        // pixel size for drawing
    public Color DrawColor;
    
    // Corpse-specific
    public float DecayTimer;  // corpses: age counter (counts UP); fertile ground: age counter
    public float SmellRadius = 200f; // how far creatures can detect this food
    
    // Physics for falling corpses/debris
    public Vector2 Velocity;
    public bool OnGround;
    
    public FoodSource(Vector2 pos, FoodType type, float nutrition = 0.5f)
    {
        Position = pos;
        Type = type;
        Nutrition = nutrition;
        Amount = 1f;
        
        switch (type)
        {
            case FoodType.Debris:
                Size = 4f;
                DrawColor = new Color(120, 100, 70);
                break;
            case FoodType.Plant:
                Size = 6f;
                DrawColor = new Color(60, 140, 50);
                OnGround = true;
                break;
            case FoodType.Corpse:
                Size = 8f;
                DrawColor = new Color(140, 50, 50);
                DecayTimer = 0f; // age counter, counts UP
                Nutrition = 0.6f;
                SmellRadius = 200f;
                break;
            case FoodType.Insect:
                Size = 3f;
                DrawColor = new Color(80, 70, 50);
                break;
            case FoodType.FertileGround:
                Size = 10f;
                DrawColor = new Color(80, 50, 30); // dark earth
                Amount = 1f; // doesn't deplete
                Nutrition = 0f;
                DecayTimer = 0f; // counts up
                OnGround = true;
                break;
        }
    }
    
    public float Eat(float dt, float eatRate = 0.3f)
    {
        float bite = eatRate * dt;
        float actual = MathF.Min(bite, Amount);
        Amount -= actual;
        return actual * Nutrition;
    }
    
    public void Update(float dt, TileGrid tg = null, int tileSize = 32)
    {
        // Gravity for corpses/debris
        if ((Type == FoodType.Corpse || Type == FoodType.Debris) && !OnGround)
        {
            Velocity.Y += 400f * dt;
            Position += Velocity * dt;
            
            if (tg != null)
            {
                int tx = (int)(Position.X / tileSize);
                int ty = (int)((Position.Y + Size) / tileSize);
                if (TileProperties.IsSolid(tg.GetTileAt(tx, ty)))
                {
                    Position.Y = ty * tileSize - Size;
                    Velocity = Vector2.Zero;
                    OnGround = true;
                }
            }
        }
        
        if (Type == FoodType.Corpse)
        {
            DecayTimer += dt; // now counts UP (age of corpse)
            // Visual aging only — corpse persists until eaten
            float age = DecayTimer;
            float freshness = MathHelper.Clamp(1f - age / 600f, 0.3f, 1f); // darkens over 10 min, never below 30%
            DrawColor = new Color((int)(140 * freshness), (int)(40 * freshness), (int)(40 * freshness));
            // Older corpses are "smellier" — increase detection range
            SmellRadius = 200f + MathHelper.Clamp(age / 2f, 0f, 600f); // 200px fresh → 800px at 10min
        }
        else if (Type == FoodType.FertileGround)
        {
            DecayTimer += dt;
            if (DecayTimer > 120f) Amount = 0; // expire after 2 min
            float life = MathHelper.Clamp(1f - DecayTimer / 120f, 0.2f, 1f);
            DrawColor = new Color((int)(80 * life), (int)(60 * life), (int)(30 * life));
        }
    }
    
    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (Depleted) return;
        int s = (int)(Size * Amount + 2);
        var rect = new Rectangle((int)(Position.X - s/2f), (int)(Position.Y - s/2f), s, s);
        sb.Draw(pixel, rect, DrawColor);
        
        if (s > 3)
        {
            var hlRect = new Rectangle(rect.X + 1, rect.Y + 1, 2, 2);
            sb.Draw(pixel, hlRect, Color.White * 0.3f);
        }
    }
    
    public static bool CanEat(EcologicalRole role, FoodType food)
    {
        // FertileGround is never eaten
        if (food == FoodType.FertileGround) return false;
        
        return (role, food) switch
        {
            (EcologicalRole.Herbivore, FoodType.Plant) => true,
            (EcologicalRole.Herbivore, FoodType.Debris) => true,
            (EcologicalRole.Scavenger, FoodType.Debris) => true,
            (EcologicalRole.Scavenger, FoodType.Corpse) => true,
            (EcologicalRole.Predator, FoodType.Corpse) => true,
            (EcologicalRole.Prey, FoodType.Plant) => true,
            (EcologicalRole.Prey, FoodType.Insect) => true,
            _ => false,
        };
    }
}
