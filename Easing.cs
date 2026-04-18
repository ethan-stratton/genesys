using System;

namespace Genesis;

public static class Easing
{
    // Smooth Start (accelerate)
    public static float SmoothStart2(float t) => t * t;
    public static float SmoothStart3(float t) => t * t * t;
    public static float SmoothStart4(float t) => t * t * t * t;

    // Smooth Stop (decelerate)
    public static float SmoothStop2(float t) { float u = 1f - t; return 1f - u * u; }
    public static float SmoothStop3(float t) { float u = 1f - t; return 1f - u * u * u; }
    public static float SmoothStop4(float t) { float u = 1f - t; return 1f - u * u * u * u; }

    // Classic Hermite smooth step: 3t² - 2t³
    public static float SmoothStep(float t) => t * t * (3f - 2f * t);

    // Lerp between two easing functions
    public static float CrossFade(Func<float, float> a, Func<float, float> b, float t)
        => (1f - t) * a(t) + t * b(t);

    // Parabolic arch peaking at 1.0 when t=0.5
    public static float Arch(float t) => t * (1f - t) * 4f;

    // Bounce clamp — reflects value back when it exceeds 1.0
    public static float BounceClampBottom(float t) => MathF.Abs(t);
    public static float BounceClampTop(float t) => 1f - MathF.Abs(1f - t);

    // Normalized cubic bezier easing with 2 control points (p1, p2 are y-values; x assumed uniform: 1/3, 2/3)
    public static float NormalizedBezier3(float p1, float p2, float t)
    {
        float u = 1f - t;
        // B(t) = (1-t)³·0 + 3(1-t)²t·p1 + 3(1-t)t²·p2 + t³·1
        return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
    }
}
