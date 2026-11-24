sampler2D uImage0 : register(s0);

float uTime;
float2 uTexSize;      // e.g. (256, 256)
float uEdgeSharpness; // e.g. 0.45 (0.0 to 1.0)
float uSheenIntensity;// e.g. 0.25 (subtle living luster)

float4 PS_CrimsonKnot(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 baseCol = tex2D(uImage0, coords);
    
    // Quick early out on fully transparent pixels
    if (baseCol.a <= 0.002)
        return float4(0, 0, 0, 0);

    // 1. High-Precision Unsharp Mask (Adaptive Edge Clarification)
    // Eliminates minification softness from bilinear sampling without causing pixelation
    float2 d = (1.0 / uTexSize) * 0.65;
    float4 s0 = tex2D(uImage0, coords + float2(-d.x, -d.y));
    float4 s1 = tex2D(uImage0, coords + float2( d.x, -d.y));
    float4 s2 = tex2D(uImage0, coords + float2(-d.x,  d.y));
    float4 s3 = tex2D(uImage0, coords + float2( d.x,  d.y));
    float4 avg = (s0 + s1 + s2 + s3) * 0.25;

    // Contrast boost on edges
    float4 sharpCol = baseCol + (baseCol - avg) * uEdgeSharpness;
    sharpCol = saturate(sharpCol);

    // 2. Subtle Living Luster / Ethereal Pulse
    // A faint sweeping diagonal gleam across the petals
    float sheenPhase = coords.x * 1.5 + coords.y * 1.0 - uTime * 1.8;
    float sheen = pow(saturate(sin(sheenPhase) * 0.5 + 0.5), 12.0) * uSheenIntensity;
    
    // Only apply sheen to non-shadow areas (where color is vibrant)
    float luma = dot(baseCol.rgb, float3(0.299, 0.587, 0.114));
    sharpCol.rgb += float3(sheen, sheen * 0.85, sheen * 0.95) * saturate(luma * 1.8);

    // 3. Blend with incoming vertex color (tint/alpha)
    float4 finalCol = sharpCol * sampleColor;

    // Return pre-multiplied alpha for perfect blending
    return float4(finalCol.rgb * finalCol.a, finalCol.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PS_CrimsonKnot();
    }
}
