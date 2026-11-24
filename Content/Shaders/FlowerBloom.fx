sampler2D uImage0 : register(s0);

float uTime;
float uProgress;
float3 uBloomColor;

float4 PS_FlowerBloom(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;
    float4 tex = tex2D(uImage0, uv);
    if (tex.a <= 0.001)
        return float4(0, 0, 0, 0);

    float2 delta = uv - 0.5;
    float dist = length(delta);
    float angle = atan2(delta.y, delta.x);

    // Organic unraveling / blooming threshold
    float unfoldThreshold = uProgress * 1.35;
    float burnDist = dist * 1.5;
    
    if (burnDist > unfoldThreshold + 0.05)
        return float4(0, 0, 0, 0);

    // Edge burning / blooming white-hot rim
    float diff = (unfoldThreshold + 0.05) - burnDist;
    float hotRim = smoothstep(0.0, 0.06, diff) * (1.0 - smoothstep(0.06, 0.16, diff));

    // Petal color grading
    float3 baseCrimson = float3(0.98, 0.08, 0.18);
    float3 darkVelvet = float3(0.40, 0.02, 0.06);
    float3 whiteHot = float3(1.8, 1.4, 1.2);

    // Color from core to tips
    float3 petalColor = lerp(darkVelvet, baseCrimson, saturate(dist * 2.2));
    petalColor = lerp(whiteHot, petalColor, smoothstep(0.04, 0.20, dist));
    
    // Add glowing white-hot edge during unfurling
    petalColor += whiteHot * (hotRim * 2.2 * saturate(1.0 - uProgress * 0.8));

    // Dynamic pulsating emission
    float pulse = sin(uTime * 5.0 - dist * 18.0) * 0.18 + 0.85;
    petalColor *= pulse;

    // Fade out as progress nears 1
    float alpha = tex.a * (1.0 - smoothstep(0.60, 1.0, uProgress));

    return float4(petalColor * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PS_FlowerBloom();
    }
}
