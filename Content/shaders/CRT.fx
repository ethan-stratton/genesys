#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// CRT shader for MonoGame
// Scanlines + barrel distortion + vignette + slight color bleed

sampler2D TextureSampler : register(s0);

float2 TextureSize;   // resolution of the render target
float2 OutputSize;     // resolution of the screen
float ScanlineWeight;  // 0.0 = no scanlines, 1.0 = full black lines (default 0.25)
float Curvature;       // barrel distortion strength (default 0.02)
float VignetteStrength; // default 0.3
float BleedAmount;     // horizontal color bleed (default 0.001)
float Brightness;      // overall brightness boost (default 1.15)

float2 CurveUV(float2 uv)
{
    uv = uv * 2.0 - 1.0;
    float2 offset = abs(uv.yx) * Curvature;
    uv = uv + uv * offset * offset;
    uv = uv * 0.5 + 0.5;
    return uv;
}

float4 MainPS(float2 texCoord : TEXCOORD0) : COLOR0
{
    float2 uv = CurveUV(texCoord);

    // Out of bounds = black
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return float4(0, 0, 0, 1);

    // Slight horizontal color bleed (chromatic aberration)
    float r = tex2D(TextureSampler, float2(uv.x + BleedAmount, uv.y)).r;
    float g = tex2D(TextureSampler, uv).g;
    float b = tex2D(TextureSampler, float2(uv.x - BleedAmount, uv.y)).b;
    float3 color = float3(r, g, b);

    // Scanlines
    float scanline = sin(uv.y * TextureSize.y * 3.14159) * 0.5 + 0.5;
    scanline = lerp(1.0, scanline, ScanlineWeight);
    color *= scanline;

    // Phosphor dot pattern (subtle)
    float dotMask = sin(uv.x * TextureSize.x * 3.14159 * 3.0) * 0.04 + 0.96;
    color *= dotMask;

    // Vignette
    float2 vig = uv * (1.0 - uv);
    float vignette = vig.x * vig.y * 15.0;
    vignette = pow(vignette, VignetteStrength);
    color *= vignette;

    // Brightness boost
    color *= Brightness;

    return float4(color, 1.0);
}

technique CRT
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
