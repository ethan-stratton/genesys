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
    public float DecayTimer;  // corpses decay over time
    public float MaxDecayTime = 30f; // seconds before corpse disappears
    
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
                break;
            case FoodType.Corpse:
                Size = 8f;
                DrawColor = new Color(140, 50, 50);
                DecayTimer = MaxDecayTime;
                break;
            case FoodType.Insect:
                Size = 3f;
                DrawColor = new Color(80, 70, 50);
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
    
    public void Update(float dt)
    {
        if (Type == FoodType.Corpse)
        {
            DecayTimer -= dt;
            if (DecayTimer <= 0) Amount = 0;
            float fade = MathHelper.Clamp(DecayTimer / MaxDecayTime, 0f, 1f);
            DrawColor = new Color((int)(140 * fade), (int)(50 * fade), (int)(50 * fade));
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
