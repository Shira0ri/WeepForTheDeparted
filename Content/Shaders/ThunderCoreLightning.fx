// ============================================================================
// ThunderCoreLightning.fx
// Custom procedural lightning & impact scorch shader for Charm 13: Thunder Core
// Inspired by Calamity Overhaul's OniRaikiri, adapted to Naught's crimson/void-violet palette.
//
// BoltTech:
//   - Dual-frequency jagged trunk converging cleanly onto impact point (v = 1)
//   - Segmented side branches that grow on discharge and burn out in afterglow
//   - Electric bloom + high-frequency flicker + 3-tier crimson/violet/white-hot palette
//   - Smooth flash -> main discharge -> afterglow fade curve driven by uAge (0..1)
//
// ScorchTech:
//   - Grounded elliptical impact scorch & plasma flash at TargetPos
//
// Premultiplied alpha output for BlendState.AlphaBlend (works inside & outside Nihility Domain)
// vs_3_0 / ps_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        // seconds (flicker)
float uSeed;        // per-bolt random seed (0..1)
float uAge;         // 0..1 normalized lifetime progress
float uJitter;      // trunk displacement amplitude in UV space
float uBranch;      // 0..1 side branch intensity
float uOpacity;     // overall opacity

float3 uColHot;     // white-hot overexposed core
float3 uColCrimson; // Naught crimson body
float3 uColViolet;  // void-violet electric bloom & afterglow

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

// Jagged trunk offset along vertical coordinate v (0 = sky, 1 = impact point)
float TrunkOffset(float v)
{
    float n1 = tex2D(noiseSamp, float2(v * 2.4 + uSeed * 1.7, 0.17 + uSeed * 0.5)).r - 0.5;
    float n2 = tex2D(noiseSamp, float2(v * 6.8 - uSeed * 0.9, 0.63 + uSeed * 0.3)).r - 0.5;
    // Pin the bottom of the bolt cleanly onto the target impact point
    float converge = 1.0 - smoothstep(0.64, 1.0, v);
    return (n1 * 0.65 + n2 * 0.35) * uJitter * converge;
}

float4 BoltPS(PSInput input) : COLOR0
{
    float u = input.TexCoords.x;
    float v = saturate(input.TexCoords.y);
    float x = (u - 0.5) * 2.0;

    // Three-stage lifecycle: sky flash -> main discharge -> void-violet/crimson afterglow
    float flash = 1.0 - smoothstep(0.0, 0.14, uAge);
    float decay = smoothstep(0.42, 1.0, uAge);
    float live = 1.0 - decay;

    // High-frequency electric flicker during active discharge
    float flicker = 0.86 + 0.14 * sin(uTime * 54.0 + uSeed * 37.0 + v * 11.0);

    float trunk = TrunkOffset(v);
    float d = abs(x - trunk);

    // Core & body tighten during main discharge, bloom slightly in afterglow
    float halfWidth = lerp(0.28, 0.075, smoothstep(0.0, 0.32, uAge)) + flash * 0.30 + decay * 0.05;
    float core = saturate(1.0 - d / max(halfWidth * 0.32, 1e-4));
    float body = saturate(1.0 - d / max(halfWidth, 1e-4));
    float bloom = exp(-(d * d) / max(halfWidth * halfWidth * 2.4, 1e-4)) * 0.65;

    // Jagged side branches shooting off the main trunk
    float branch = 0.0;
    if (uBranch > 0.001)
    {
        [unroll]
        for (int i = 0; i < 3; i++)
        {
            float fi = (float)i;
            float freq = 3.0 + fi * 1.15;
            float seg = frac(v * freq + uSeed * 3.1 + fi * 0.37);
            float pick = tex2D(noiseSamp, float2(floor(v * freq) * 0.23 + uSeed, 0.38 + fi * 0.21)).r;
            if (pick < 0.50)
                continue;

            float dir = (frac(pick * 13.37 + fi * 0.5) > 0.5) ? 1.0 : -1.0;
            float grow = saturate(uAge / 0.28) * (1.0 - decay);
            float reach = dir * (pick - 0.48) * 1.85 * grow;
            float bx = trunk + reach * seg;
            float bw = 0.045 * (1.0 - seg) + 0.007;
            branch += saturate(1.0 - abs(x - bx) / max(bw, 1e-4)) * (1.0 - seg) * uBranch;
        }
        branch = saturate(branch);
    }

    float alpha = saturate((body * body + core + bloom + branch * 0.90) * flicker);
    // Fade top into the sky, keep bottom anchored at impact point
    alpha *= smoothstep(0.0, 0.10, v);
    if (alpha <= 0.004)
        return float4(0, 0, 0, 0);

    // Naught palette: void-violet outer bloom -> crimson discharge -> white-hot core
    float3 col = uColViolet;
    col = lerp(col, uColCrimson, saturate(body * 1.25 + branch * 0.55));
    col = lerp(col, uColHot, saturate(core * 1.45 + flash * 0.85));
    // Afterglow cools into void-violet & deep crimson
    col = lerp(col, lerp(uColCrimson, uColViolet, 0.55), decay * 0.72);

    alpha *= uOpacity * saturate(live * 0.88 + flash * 0.90 + 0.08) * input.Color.a;
    return float4(col * alpha, alpha);
}

// Impact scorch & electric ground flash at TargetPos
float4 ScorchPS(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    // Flatten into an impact ellipse
    p.y *= 2.3;
    float r = length(p);
    if (r > 1.0)
        return float4(0, 0, 0, 0);

    float noise = tex2D(noiseSamp, input.TexCoords * 1.8 + float2(uSeed, uTime * 0.25)).r;
    float ring = saturate(1.0 - r * (0.88 + 0.22 * noise));

    float flash = 1.0 - smoothstep(0.0, 0.22, uAge);
    float decay = smoothstep(0.32, 1.0, uAge);

    float3 col = lerp(uColCrimson, uColViolet, saturate(r * 0.9 + decay * 0.5));
    col = lerp(col, uColHot, saturate(pow(ring, 4.5) * (1.0 - decay) + flash * 0.65));

    float alpha = pow(ring, 2.0) * uOpacity * (1.0 - decay * 0.88) * input.Color.a;
    return float4(col * alpha, alpha);
}

technique BoltTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 BoltPS();
    }
}

technique ScorchTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 ScorchPS();
    }
}
