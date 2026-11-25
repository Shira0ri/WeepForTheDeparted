// ============================================================================
// BloodLotusShockwave.fx
// Anti-aliased procedural Blood Lotus shockwave & expanding mandala shader
// Premultiplied alpha output for BlendState.AlphaBlend (works inside & outside Nihility Domain)
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uProgress;     // 0..1 lifetime completion
float uRingNorm;     // currentRadius / quadHalfRadius (0..~0.87)
float uIntensity;    // knot stack intensity multiplier (~0.9..1.35)
float uRotation;     // slow mandala rotation (radians)

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

#define PI 3.14159265

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
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        return float4(0, 0, 0, 0);

    // Seamless polar direction without raw atan2 discontinuities in noise sampling
    float2 dir = p / max(r, 1e-4);
    float2 noiseUV1 = dir * 0.28 + 0.5 + float2(r * 0.35 - uTime * 0.14, uTime * 0.06);
    float2 noiseUV2 = float2(-dir.y, dir.x) * 0.22 + 0.5 - float2(r * 0.28 + uTime * 0.10, -uTime * 0.08);
    float n1 = tex2D(noiseSamp, noiseUV1).r;
    float n2 = tex2D(noiseSamp, noiseUV2).r;
    float noise = lerp(n1, n2, 0.5);

    // Integer-harmonic angular coordinates (C-infinity seamless across -PI/+PI)
    float theta = atan2(p.y, p.x);
    float aOuter = theta + uRotation;
    float aInner = theta - uRotation * 1.35 + PI / 8.0;

    // 8-petal outer lotus & 8-petal counter-rotating inner lotus silhouette
    float cos8Outer = 0.5 + 0.5 * cos(8.0 * aOuter);
    float cos8Inner = 0.5 + 0.5 * cos(8.0 * aInner);
    float outerPetalShape = pow(cos8Outer, 2.2);
    float innerPetalShape = pow(cos8Inner, 3.0);

    // Petal veins (sharp bright ribs along petal centerlines)
    float veinOuter = pow(cos8Outer, 12.0);
    float veinInner = pow(cos8Inner, 14.0);

    // Overall lifetime envelope
    float fadeIn = smoothstep(0.0, 0.08, uProgress);
    float fadeOut = 1.0 - smoothstep(0.55, 1.0, uProgress);
    float lifeAlpha = fadeIn * fadeOut;

    // Boundary containment inside the expanding shockwave radius
    float insideWave = 1.0 - smoothstep(uRingNorm - 0.035, uRingNorm + 0.015, r);
    float mandalaFade = (1.0 - smoothstep(0.38, 0.96, uProgress)) * insideWave;

    // Mandala radial profile: blooms outward behind the shockwave
    float rNormInWave = r / max(uRingNorm, 0.02);
    float petalReach = smoothstep(0.12, 0.45, rNormInWave) * (1.0 - smoothstep(0.78, 0.98, rNormInWave));
    float mandalaEnergy = (outerPetalShape * 0.48 + innerPetalShape * 0.42) * petalReach * (0.72 + 0.45 * noise);
    float veinEnergy = (veinOuter * 0.55 + veinInner * 0.45) * petalReach * (1.0 - smoothstep(0.45, 0.90, rNormInWave));

    // Expanding primary shockwave ring right at r == uRingNorm
    float dRing = r - uRingNorm;
    // Slight organic ripple on the outer plasma halo, while keeping the white core razor-accurate
    float haloWidth = 0.048 + 0.014 * (1.0 - uProgress);
    float violetHalo = exp(-(dRing * dRing) / (haloWidth * haloWidth)) * (0.65 + 0.45 * noise);
    float crimsonCrest = exp(-abs(dRing) * (dRing > 0.0 ? 82.0 : 30.0)) * (0.95 + 0.25 * noise);
    float whiteEdge = exp(-(dRing * dRing) / (0.0085 * 0.0085));

    // Secondary trailing inner echo ring
    float echoRadius = uRingNorm * 0.62;
    float dEcho = r - echoRadius;
    float echoFade = (1.0 - smoothstep(0.12, 0.78, uProgress)) * smoothstep(0.04, 0.16, uRingNorm);
    float echoRing = exp(-(dEcho * dEcho) / (0.018 * 0.018)) * echoFade * 0.58;

    // Color palette tuned for vivid visibility both inside & outside Nihility Domain
    float3 colVoidViolet = float3(0.66, 0.14, 1.12);
    float3 colDeepWine   = float3(0.42, 0.02, 0.10);
    float3 colCrimson    = float3(1.38, 0.09, 0.22);
    float3 colWhiteHot   = float3(1.85, 1.38, 1.52);

    float3 mandalaCol = lerp(colDeepWine, colCrimson, outerPetalShape * 0.75 + 0.25) * mandalaEnergy
                      + lerp(colCrimson, colVoidViolet, 0.45) * veinEnergy * 0.85;

    float3 ringCol = colVoidViolet * (violetHalo * 0.78)
                   + colCrimson * (crimsonCrest * 1.10 + echoRing * 0.85)
                   + colVoidViolet * (echoRing * 0.45)
                   + colWhiteHot * (whiteEdge * 1.15);

    // Combine with soft outer vignette at quad boundary so nothing ever hard-clips
    float edgeGuard = 1.0 - smoothstep(0.92, 1.0, r);

    float3 totalRGB = (mandalaCol * mandalaFade + ringCol * lifeAlpha) * uIntensity * edgeGuard;
    float totalAlpha = saturate((mandalaEnergy * 0.68 * mandalaFade
                               + veinEnergy * 0.45 * mandalaFade
                               + (violetHalo * 0.55 + crimsonCrest * 0.90 + whiteEdge * 0.95 + echoRing * 0.55) * lifeAlpha)
                               * edgeGuard) * input.Color.a;

    return float4(totalRGB * input.Color.a, totalAlpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
