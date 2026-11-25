// ============================================================================
// VoidHorizonPortal.fx
// Dimensional Void Fissure portal shader for VoidHorizon (Charm 15)
// Features polar swirl UV distortion, dark event-horizon core, jagged
// crimson/violet rift rim, and smooth open/close scale-in and collapse.
// Premultiplied alpha output for BlendState.AlphaBlend.
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uOpen;     // 0..1 opening / collapsing scale factor
float uFade;     // 0..1 overall alpha envelope
float uAspect;   // Y/X elliptical aspect ratio (e.g. 0.78)
float uSeed;     // Per-portal phase seed

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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    if (uOpen <= 0.005 || uFade <= 0.005)
        return float4(0, 0, 0, 0);

    // Map UV 0..1 to centered coordinates -1..1, scaled by open/close envelope & elliptical aspect
    float2 p = (input.TexCoords - 0.5) * 2.0;
    p.y /= max(uAspect, 0.25);
    p /= max(uOpen, 0.01);

    float r = length(p);
    if (r > 1.15)
        return float4(0, 0, 0, 0);

    float2 dir = p / max(r, 1e-4);
    float theta = atan2(p.y, p.x);

    // Polar swirl UV distortion: twist intensifies toward the event horizon
    float swirlAngle = (1.0 - saturate(r)) * 3.4 + uTime * 1.55 + uSeed;
    float s = sin(swirlAngle);
    float c = cos(swirlAngle);
    float2 swirlDir = float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);

    // Seamless polar noise sampling (inward-pulling accretion streams)
    float2 noiseUV1 = swirlDir * 0.26 + 0.5 + float2(r * 0.45 + uTime * 0.22, uSeed * 0.37);
    float2 noiseUV2 = float2(-swirlDir.y, swirlDir.x) * 0.34 + 0.5 + float2(r * 0.30 - uTime * 0.16, uSeed * 0.71);
    float n1 = tex2D(noiseSamp, noiseUV1).r;
    float n2 = tex2D(noiseSamp, noiseUV2).r;

    // Jagged dimensional tear boundary
    float harmonic = 0.035 * cos(5.0 * theta + uTime * 1.8 + uSeed * 6.28)
                   + 0.022 * sin(9.0 * theta - uTime * 2.4 + uSeed * 3.14);
    float rimRadius = 0.68 + (n1 - 0.5) * 0.15 + (n2 - 0.5) * 0.09 + harmonic;
    float d = r - rimRadius;

    // Dark event-horizon core
    float inside = 1.0 - smoothstep(-0.045, 0.025, d);
    float depth = saturate(1.0 - r / max(rimRadius, 0.01));

    // Swirling accretion wisps inside the void core
    float vortexWisp = pow(saturate(n1 * n2 * 1.95), 2.4) * (1.0 - depth * 0.75);
    float3 colAbyss = float3(0.018, 0.004, 0.032);
    float3 colVoidViolet = float3(0.68, 0.14, 1.15);
    float3 colCrimson = float3(1.35, 0.09, 0.24);
    float3 colWhiteHot = float3(1.85, 1.32, 1.50);

    float3 coreCol = colAbyss + colVoidViolet * (vortexWisp * 0.55) + colCrimson * (vortexWisp * 0.25);
    // Deepest center is pure light-absorbing event horizon
    coreCol *= (0.18 + 0.82 * (1.0 - smoothstep(0.0, 0.55, depth)));
    float aCore = inside * (0.94 - 0.12 * (1.0 - depth));

    // Jagged crimson/violet rift rim & outer plasma halo
    float violetHalo = exp(-(d * d) / (0.075 * 0.075)) * (0.55 + 0.50 * n1);
    float crimsonRim = exp(-abs(d) * (d > 0.0 ? 42.0 : 24.0)) * (0.90 + 0.35 * n2);
    float hotSpark = exp(-abs(d) * 95.0) * saturate(n1 * 1.35);

    float3 rimCol = colVoidViolet * (violetHalo * 0.75)
                  + colCrimson * (crimsonRim * 1.15)
                  + colWhiteHot * (hotSpark * 0.75);

    float openSat = saturate(uOpen);
    float3 finalRgb = coreCol * aCore + rimCol * openSat;
    float finalAlpha = saturate(aCore + (violetHalo * 0.45 + crimsonRim * 0.85 + hotSpark * 0.55) * openSat);

    finalRgb *= uFade;
    finalAlpha *= uFade;

    return float4(finalRgb, finalAlpha) * input.Color;
}

technique Technique1
{
    pass VoidHorizonPortalPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
