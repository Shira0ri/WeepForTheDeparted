sampler2D uImage0 : register(s0);

float uTime;
float uOpacity;
float uNegativeFlash;
float uAnomalyPulse;
float2 uScreenSize;
float2 uRippleCenter;
float uRippleRadius;
float uRippleActive;

float4 PS_NihilityDomain(float2 coords : TEXCOORD0) : COLOR0
{
    float2 uv = coords;
    
    // Chromatic anomaly pulse during slashes
    float3 src;
    if (uAnomalyPulse > 0.001)
    {
        float shift = (3.5 / uScreenSize.x) * uAnomalyPulse;
        float r = tex2D(uImage0, uv + float2(shift, 0.0)).r;
        float g = tex2D(uImage0, uv).g;
        float b = tex2D(uImage0, uv - float2(shift, 0.0)).b;
        src = float3(r, g, b);
    }
    else
    {
        src = tex2D(uImage0, uv).rgb;
    }
    
    // Luminance calculation
    float luma = dot(src, float3(0.299, 0.587, 0.114));
    
    // Clean high-contrast monochrome base
    float contrastLuma = saturate((luma - 0.40) * 1.25 + 0.40);
    float3 mono = contrastLuma.xxx;

    // Selective color retention: keep pure Crimson and Void Violet vivid while suppressing mundane brown wood/dirt!
    // Brown wood and dirt in Terraria have significant green relative to red (g / r > 0.45).
    // True crimson (Acheron red, Crimson Knot, red blade, blood) has very low green (g / r < 0.32).
    float greenRatio = src.g / max(src.r, 0.001);
    float redDominance = src.r - max(src.g * 1.55, src.b * 1.35);
    float redMask = smoothstep(0.08, 0.28, redDominance);
    // Strict cutoff for brown wood, dirt, and orange tiles
    redMask *= saturate(1.0 - (greenRatio - 0.28) / 0.16);
    
    // Void Violet (Acheron purple / scabbard aura): high R and B, low G
    float violetDominance = min(src.r, src.b) - src.g * 1.5;
    float violetMask = smoothstep(0.06, 0.22, violetDominance);
    
    float preserveMask = saturate(redMask * 1.2 + violetMask);
    
    // Boosted crimson vibrancy: amplify redness for true crimson so red elements blaze through the abyss!
    float3 redBoostedSrc = src;
    if (redMask > 0.05)
    {
        redBoostedSrc.r = saturate(src.r * 1.35 + 0.10);
        redBoostedSrc.gb = src.gb * 0.82;
    }
    
    // Toned color grading:
    // Retain 40% of original color for non-red elements (so scabbard, player, terrain are visible and distinct!),
    // blended with 60% monochrome for that bleak, cool, stylish Honkai/anime aesthetic.
    // Red and violet elements are preserved at 100% full vibrancy!
    float3 mutedColor = lerp(mono, src * 0.95, 0.42);
    float3 domainGraded = lerp(mutedColor, redBoostedSrc, preserveMask * 0.98);

    // Subtle dark vignette at screen edges (adds cinematic framing without crushing the player)
    float2 centerOffset = (uv - 0.5) * 1.25;
    float vignette = saturate(1.0 - dot(centerOffset, centerOffset) * 0.28);
    domainGraded *= vignette;

    // Wave Masking: Tied directly to the expanding / contracting wave boundary!
    float waveMask = uOpacity;
    float shockwave = 0.0;

    if (uRippleActive > 0.5)
    {
        float aspect = uScreenSize.x / uScreenSize.y;
        float2 rDelta = uv - uRippleCenter;
        rDelta.x *= aspect;
        float rDist = length(rDelta);

        // 1.0 inside the wave, 0.0 ahead of the wave
        float waveEdge = 1.0 - smoothstep(uRippleRadius - 0.02, uRippleRadius + 0.04, rDist);
        shockwave = exp(-abs(rDist - uRippleRadius) * 55.0) * 1.6;

        waveMask = saturate(uOpacity * waveEdge);
    }

    // Ahead of wave: 100% normal bright vanilla world (src).
    // Inside wave: 100% graded domain world (domainGraded).
    float3 finalColor = lerp(src, domainGraded, waveMask);

    // Brilliant glowing reflection shockwave line at the wave boundary!
    finalColor += float3(1.0, 0.92, 0.96) * (shockwave * uOpacity);
    
    // Negative flash on activation
    finalColor = lerp(finalColor, 1.0 - finalColor, uNegativeFlash * 0.88);
    
    return float4(finalColor, 1.0);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PS_NihilityDomain();
    }
}
