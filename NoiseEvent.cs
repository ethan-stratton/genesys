using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;

namespace Genesis;

public class NoiseEvent
{
    public Vector2 Position;
    public float Radius;
    public float Intensity;
    public string Label;
    public float Timer;
    public float MaxTimer;
    public Color TextColor;
    public float YOffset;

    public bool Expired => Timer <= 0;

    public NoiseEvent(Vector2 pos, float radius, float intensity, string label, Color color, float duration = 1.5f)
    {
        Position = pos;
        Radius = radius;
        Intensity = intensity;
        Label = label;
        Timer = duration;
        MaxTimer = duration;
        TextColor = color;
        YOffset = 0;
    }

    public void Update(float dt)
    {
        Timer -= dt;
        YOffset -= dt * 30f;
    }

    public void Draw(SpriteBatch sb, SpriteFontBase font, Vector2 cameraOffset)
    {
        if (Expired || font == null) return;
        float alpha = MathHelper.Clamp(Timer / MaxTimer, 0f, 1f);
        var drawPos = Position + new Vector2(0, YOffset) - cameraOffset;
        var color = TextColor * alpha;
        var size = font.MeasureString(Label);
        sb.DrawString(font, Label, drawPos - size / 2f, color);
    }
}
