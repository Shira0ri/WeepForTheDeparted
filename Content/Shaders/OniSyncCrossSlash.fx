// ============================================================================
// OniSyncCrossSlash.fx
// Onikiri-inspired multi-mode shader for Synchronized Execution:
//   uMode = 0 : Convergent Cross-Slash Beam (razor white-hot spine + torn crimson/violet aura)
//   uMode = 1 : Clone Afterimage Trail (flowing void-violet/crimson silk ribbon with comet taper)
//   uMode = 2 : Crimson Rupture Flash (radial polar-flow burst + intersecting X-cut lens flare)
//
// Straight-line branchless HLSL (no if/return/tex2Dlod) for FNA3D D3D11 safety.
// Premultiplied alpha output for BlendState.AlphaBlend.
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uMode;
float uProgress;
float uOpacity;
float uFlash;
float uSeed;

float3 uColHot;
float3 uColBright;
float3 uColDeep;
float3 uColDark;

texture uBrushTex;
sampler brushSamp = sampler_state
{
    texture = <uBrushTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = clamp;
};

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
#define TWO_PI 6.28318531

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
    float2 uv = input.TexCoords;
    float u = uv.x;
    float cy = (uv.y - 0.5) * 2.0; // -1..+1 transverse
    float absCy = abs(cy);

    // Mode weights (0 = CrossSlashBeam, 1 = AfterimageTrail, 2 = RuptureFlash)
    float wBeam = 1.0 - step(0.5, uMode);
    float wTrail = step(0.5, uMode) * (1.0 - step(1.5, uMode));
    float wFlash = step(1.5, uMode);

    // ------------------------------------------------------------------------
    // Mode 0: Convergent Cross-Slash Beam
    // ------------------------------------------------------------------------
    float sweep0 = saturate(uProgress * 2.6);
    float erode0 = saturate((uProgress - 0.25) / 0.75);
    float beamNoise = tex2D(noiseSamp, float2(u * 2.4 - uTime * 1.4 + uSeed, cy * 0.45 + uSeed * 0.7)).r;
    float beamBrush = tex2D(brushSamp, float2(u * 1.8 - uTime * 0.6 + uSeed * 0.3, uv.y)).r;

    // Sharp diamond taper along u (pointed at both ends, widest in middle)
    float endTaper0 = pow(saturate(sin(u * PI)), 0.42);
    float reveal0 = smoothstep(u - 0.08, u + 0.02, sweep0);
    float bodyWidth0 = (0.85 - erode0 * 0.55 - (1.0 - beamNoise) * 0.22) * endTaper0;
    float outerMask0 = smoothstep(bodyWidth0, bodyWidth0 - 0.28, absCy) * reveal0;

    // Razor-sharp white-hot core spine
    float spine0 = exp(-pow(absCy / max(0.035, 0.14 * endTaper0 * (1.0 - erode0 * 0.65)), 2.0)) * reveal0;
    float streak0 = saturate(beamBrush * 0.65 + beamNoise * 0.55) * outerMask0;

    float3 col0 = lerp(uColDark, uColDeep, saturate(outerMask0 * 1.2));
    col0 = lerp(col0, uColBright, saturate(streak0 * 1.15));
    col0 += uColHot * (spine0 * (1.35 + uFlash * 0.8));
    float alpha0 = saturate(outerMask0 * 0.85 + spine0 * 0.95) * (1.0 - erode0 * erode0);

    // ------------------------------------------------------------------------
    // Mode 1: Clone Afterimage Trail (Onikiri-style void-violet/crimson ribbon)
    // ------------------------------------------------------------------------
    float2 warpUV1 = float2(u * 1.6 - uTime * 0.9 + uSeed, cy * 0.35 + uSeed * 1.3);
    float warpN1 = tex2D(noiseSamp, warpUV1).r - 0.5;
    float flowN1 = tex2D(noiseSamp, float2(u * 3.0 - uTime * 2.1 + uSeed * 0.5, cy * 0.5 + warpN1 * 0.35)).r;
    float taper1 = pow(saturate(u), 0.55) * pow(saturate((1.0 - u) / 0.22 + 0.05), 0.45);
    float tear1 = 0.28 * (1.0 - u) * flowN1;
    float edge1 = smoothstep(0.92 * taper1 - tear1, 0.48 * taper1, absCy);
    float core1 = exp(-pow(absCy / max(0.05, 0.22 * taper1), 2.0));
    float fade1 = 1.0 - saturate(uProgress);

    float3 col1 = lerp(uColDark, uColDeep, edge1);
    col1 = lerp(col1, uColBright, saturate(flowN1 * edge1 * 1.2));
    col1 += uColHot * core1 * 0.75;
    float alpha1 = saturate(edge1 * (0.35 + flowN1 * 0.75) + core1 * 0.45) * fade1;

    // ------------------------------------------------------------------------
    // Mode 2: Crimson Rupture Flash (Polar radial burst + intersecting X-cut)
    // ------------------------------------------------------------------------
    float2 p2 = (uv - 0.5) * 2.0;
    float r2 = length(p2);
    float ang2 = atan2(p2.y, p2.x);
    float polarU2 = ang2 / TWO_PI + 0.5;
    float polarNoise2 = tex2D(noiseSamp, float2(polarU2 * 3.0 + uSeed, r2 * 1.4 - uProgress * 1.6)).r;

    // Expanding shockwave ring + soft inner bloom
    float ringR = 0.15 + uProgress * 0.75;
    float ring2 = exp(-pow((r2 - ringR) / 0.085, 2.0)) * (0.65 + polarNoise2 * 0.55);
    float bloom2 = exp(-r2 * r2 * 5.5) * (1.0 - uProgress * 0.7);

    // Intersecting X-cut lens flare (45 deg & -45 deg)
    float dA = abs(p2.x + p2.y) * 0.7071;
    float dB = abs(p2.x - p2.y) * 0.7071;
    float xCut = (exp(-pow(dA / 0.045, 2.0)) + exp(-pow(dB / 0.045, 2.0))) * smoothstep(0.95, 0.1, r2);

    float edgeGuard2 = 1.0 - smoothstep(0.88, 1.0, r2);
    float3 col2 = uColDeep * bloom2 + uColBright * (ring2 * 1.15) + uColHot * (xCut * 1.35 + bloom2 * 0.6);
    float alpha2 = saturate((ring2 * 0.85 + bloom2 * 0.75 + xCut * 0.95) * edgeGuard2) * (1.0 - uProgress);

    // ------------------------------------------------------------------------
    // Combine modes (branchless)
    // ------------------------------------------------------------------------
    float3 finalCol = col0 * wBeam + col1 * wTrail + col2 * wFlash;
    float finalAlpha = saturate(alpha0 * wBeam + alpha1 * wTrail + alpha2 * wFlash) * uOpacity * input.Color.a;

    return float4(finalCol * finalAlpha * input.Color.rgb, finalAlpha);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
