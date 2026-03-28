#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// CRT shader for MonoGame — Flowerwall-inspired
// RGB subpixel mask, scanlines, film grain, barrel distortion, vignette, bloom, color bleed

sampler2D TextureSampler : register(s0);

float2 TextureSize;    // render target resolution
float2 OutputSize;     // screen resolution
float Time;            // elapsed game time for animation

// --- Toggles & Parameters ---
float ScanlineWeight;   // scanline darkness 0-1 (default 0.45)
float ScanlineInterval; // pixel rows between scanlines (default 3)
float Curvature;        // barrel distortion power (default 0.02, 0 = off)
float VignetteStrength; // 0-1 (default 0.3)
float Brightness;       // overall brightness (default 1.15)

// RGB mask
float MaskStrength;     // 0-1, how much the RGB subpixel mask dims off-channels (default 0.4)
float PixelSize;        // subpixel cell size in screen pixels (default 3)

// Film grain
float GrainStrength;    // 0-1 (default 0.15)

// Color bleed / smearing
float BleedAmount;      // horizontal chromatic aberration (default 0.001)
float SmearStrength;    // horizontal smear amount 0-1 (default 0.15)

// Bloom
float BloomThreshold;   // luminance cutoff for bloom (default 0.6)
float BloomIntensity;   // bloom brightness multiplier (default 0.3)
float BloomSize;        // blur radius for bloom (default 0.003)

// ---- Helpers ----

float2 CurveUV(float2 uv)
{
    uv = uv * 2.0 - 1.0;
    float2 offset = abs(uv.yx) * Curvature;
    uv = uv + uv * offset * offset;
    uv = uv * 0.5 + 0.5;
    return uv;
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float3 RGB2YIQ(float3 c)
{
    return float3(
        0.2989 * c.r + 0.5870 * c.g + 0.1140 * c.b,
        0.5959 * c.r - 0.2744 * c.g - 0.3216 * c.b,
        0.2115 * c.r - 0.5229 * c.g + 0.3114 * c.b
    );
}

float3 YIQ2RGB(float3 c)
{
    return float3(
        c.x + 0.956 * c.y + 0.621 * c.z,
        c.x - 0.272 * c.y - 0.647 * c.z,
        c.x - 1.106 * c.y + 1.703 * c.z
    );
}

// Simple 5-tap horizontal blur for smearing
float3 HBlur(float2 uv, float spread)
{
    float2 px = float2(1.0 / TextureSize.x, 0);
    float3 sum = float3(0, 0, 0);
    sum += tex2D(TextureSampler, uv - px * spread * 2.0).rgb * 0.06;
    sum += tex2D(TextureSampler, uv - px * spread).rgb * 0.24;
    sum += tex2D(TextureSampler, uv).rgb * 0.40;
    sum += tex2D(TextureSampler, uv + px * spread).rgb * 0.24;
    sum += tex2D(TextureSampler, uv + px * spread * 2.0).rgb * 0.06;
    return sum;
}

// Bloom: threshold + gaussian
float3 GetBloom(float2 uv)
{
    float3 sum = float3(0, 0, 0);
    float total = 0;
    
    // 8-tap circular blur
    for (int i = 0; i < 8; i++)
    {
        float angle = (3.14159 * 2.0 / 8.0) * i;
        float2 off = float2(cos(angle), sin(angle)) * BloomSize;
        float3 s = tex2D(TextureSampler, uv + off).rgb;
        float lum = dot(s, float3(0.299, 0.587, 0.114));
        float3 bright = max(s - BloomThreshold, float3(0, 0, 0));
        sum += bright;
        total += 1.0;
    }
    
    // Second ring
    for (int j = 0; j < 8; j++)
    {
        float angle = (3.14159 * 2.0 / 8.0) * j + 0.39;
        float2 off = float2(cos(angle), sin(angle)) * BloomSize * 2.0;
        float3 s = tex2D(TextureSampler, uv + off).rgb;
        float3 bright = max(s - BloomThreshold, float3(0, 0, 0));
        sum += bright;
        total += 1.0;
    }
    
    return (sum / total) * BloomIntensity;
}

float4 MainPS(float2 texCoord : TEXCOORD0) : COLOR0
{
    // --- Barrel Distortion ---
    float2 uv = texCoord;
    if (Curvature > 0.001)
        uv = CurveUV(texCoord);

    // Out of bounds = black
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return float4(0, 0, 0, 1);

    // --- Color Smearing (YIQ space, horizontal blur on chroma) ---
    float3 color;
    if (SmearStrength > 0.01)
    {
        float smearPx = SmearStrength * 8.0; // scale to pixel offset
        
        // Luminance: sharp
        float y = RGB2YIQ(tex2D(TextureSampler, uv).rgb).x;
        
        // Chrominance I: blurred more
        float i_val = RGB2YIQ(HBlur(uv + float2(0.001, 0), smearPx)).y;
        
        // Chrominance Q: blurred even more
        float q_val = RGB2YIQ(HBlur(uv + float2(0.002, 0), smearPx * 1.5)).z;
        
        color = YIQ2RGB(float3(y, i_val, q_val));
    }
    else
    {
        // Chromatic aberration (simple 3-tap)
        float r = tex2D(TextureSampler, float2(uv.x + BleedAmount, uv.y)).r;
        float g = tex2D(TextureSampler, uv).g;
        float b = tex2D(TextureSampler, float2(uv.x - BleedAmount, uv.y)).b;
        color = float3(r, g, b);
    }

    // --- Bloom ---
    if (BloomIntensity > 0.01)
    {
        float3 bloom = GetBloom(uv);
        // Screen blend: 1 - (1-a)*(1-b)
        color = 1.0 - (1.0 - color) * (1.0 - bloom);
    }

    // --- Film Grain ---
    if (GrainStrength > 0.01)
    {
        float grain = Hash(uv * TextureSize + float2(Time * 100.0, Time * 73.0));
        color = lerp(color, float3(0, 0, 0), grain * GrainStrength);
    }

    // --- RGB Subpixel Mask (Slot Mask pattern) ---
    if (MaskStrength > 0.01)
    {
        float2 fragCoord = uv * OutputSize;
        int lineIndex = ((int)fragCoord.y / (int)PixelSize) % 4;
        int rgbIndex = ((int)(fragCoord.x + (float)(lineIndex * 2))) % 4;
        
        if (rgbIndex == 0)
        {
            color.g *= 1.0 - MaskStrength;
            color.b *= 1.0 - MaskStrength;
        }
        else if (rgbIndex == 1)
        {
            color.r *= 1.0 - MaskStrength;
            color.b *= 1.0 - MaskStrength;
        }
        else if (rgbIndex == 2)
        {
            color.r *= 1.0 - MaskStrength;
            color.g *= 1.0 - MaskStrength;
        }
        else
        {
            color.rgb *= 1.0 - MaskStrength * 0.666;
        }
    }

    // --- Scanlines ---
    if (ScanlineWeight > 0.01)
    {
        float2 fragCoord = uv * OutputSize;
        float scanline = fmod(fragCoord.y, ScanlineInterval);
        scanline = 1.0 - step(1.0, scanline); // thin dark line every N pixels
        scanline *= ScanlineWeight;
        color *= 1.0 - scanline;
    }

    // --- Vignette ---
    if (VignetteStrength > 0.01)
    {
        float2 vigUV;
        if (Curvature > 0.001)
        {
            // Vignette based on original UV for curved screens
            vigUV = texCoord;
        }
        else
        {
            vigUV = uv;
        }
        float2 vig = vigUV * (1.0 - vigUV);
        float vignette = vig.x * vig.y * 15.0;
        vignette = pow(vignette, VignetteStrength);
        color *= vignette;
    }

    // --- Brightness ---
    color *= Brightness;

    return float4(saturate(color), 1.0);
}

technique CRT
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
