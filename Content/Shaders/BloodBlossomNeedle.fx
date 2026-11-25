// ============================================================================
// BloodBlossomNeedle.fx
// Tapered crimson blossom needle & noise-scrolled plasma/petal trail shader
// World-space TriangleStrip (vs_3_0 / ps_3_0), FNA3D / MojoShader safe
// Premultiplied alpha output for BlendState.AlphaBlend (with additive core pop)
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;
float uIntensity;

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
    float u = input.TexCoords.x;               // 0 = tail, 1 = needle tip
    float cy = (input.TexCoords.y - 0.5) * 2.0; // -1..+1 across ribbon width
    float fade = input.Color.a;

    // Tapered needle & wake profile:
    // Grows smoothly from tail, peaks at needle shoulder (u ~ 0.78), tapers to razor tip at u = 1.0
    float tailGrow = pow(saturate(u / 0.78), 0.72);
    float tipTaper = pow(saturate((1.0 - u) / 0.22), 0.55);
    float profile = lerp(tailGrow, tipTaper, step(0.78, u));

    // Two-octave noise-scrolled plasma / petal wisps along trail
    float2 uv1 = float2(u * 1.45 - uTime * 2.2 + uSeed, cy * 0.42 + uSeed * 1.7);
    float2 uv2 = float2(u * 2.90 - uTime * 3.8 + uSeed * 2.3, cy * 0.85 - uTime * 0.35);
    float n1 = tex2D(noiseSamp, uv1).r;
    float n2 = tex2D(noiseSamp, uv2).r;
    float plasma = n1 * 0.62 + n2 * 0.38;

    // Edge erosion stronger in the trailing wake, clean & razor-sharp on the needle head
    float headMask = smoothstep(0.60, 0.88, u);
    float wakeErode = (1.0 - headMask) * (0.28 * (1.0 - u) + 0.16);
    float boundary = max(0.06, profile * (1.0 - wakeErode * (1.0 - plasma)));

    float dist = abs(cy);
    float bodyMask = 1.0 - smoothstep(boundary - 0.14 * profile - 0.025, boundary, dist);
    float gate = step(0.004, bodyMask);

    // Crisp additive edge rim along the needle & upper trail boundary
    float edgeDist = abs(dist - boundary * 0.82);
    float crispEdge = exp(-(edgeDist * edgeDist) / max(0.0025, 0.012 * profile * profile)) * smoothstep(0.18, 0.75, u);

    // Tapered white-hot energy core spine
    float coreWidth = 0.035 + 0.065 * headMask;
    float coreSpine = exp(-(cy * cy) / (coreWidth * coreWidth)) * smoothstep(0.24, 0.86, u) * tipTaper;

    // Outer void-violet plasma aura
    float auraMask = (1.0 - smoothstep(boundary * 0.45, boundary * 1.05, dist)) * (0.45 + 0.65 * plasma);

    // Tail fade-out so there is no hard cut at u = 0
    float tailCap = smoothstep(0.0, 0.14, u);

    // Naught crimson / void-violet / white-hot palette
    float3 colVoidViolet = float3(0.68, 0.14, 1.16);
    float3 colDeepWine   = float3(0.45, 0.02, 0.12);
    float3 colCrimson    = float3(1.42, 0.10, 0.26);
    float3 colWhiteHot   = float3(1.90, 1.48, 1.62);

    float3 trailCol = lerp(colDeepWine, colCrimson, saturate(plasma * 1.15 + headMask * 0.35));
    float3 finalRgb = trailCol * bodyMask * (0.55 + 0.55 * plasma)
                    + colVoidViolet * auraMask * 0.45
                    + colCrimson * crispEdge * 0.95
                    + colWhiteHot * (coreSpine * 1.10 + crispEdge * headMask * 0.45);

    float alpha = saturate((bodyMask * (0.48 + 0.45 * plasma) + coreSpine * 0.40) * tailCap) * fade * gate;
    finalRgb *= tailCap * fade * uIntensity * gate;

    return float4(finalRgb * input.Color.rgb, alpha);
}

technique Technique1
{
    pass BloodBlossomNeedlePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
