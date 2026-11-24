sampler2D uImage0 : register(s0);

float uProgress;     // 0.0 at click start -> 1.0 at fade out
float uIntensity;    // Overall brightness multiplier (1.0 - 2.5)
float uRotation;     // Katana blade angle in radians
float3 uColorCore;   // White-hot core color
float3 uColorEdge;   // Radiant neon crimson flare
float3 uColorGlow;   // Deep nihility violet halo

float4 PS_SheathFlash(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float2 p = coords - float2(0.5, 0.5);
    float d = length(p);

    if (d > 0.495)
        return float4(0, 0, 0, 0);

    // Fade envelope: explosive punch at start (progress 0..0.15), smooth decay to 1.0
    float lifeAlpha = 1.0 - smoothstep(0.0, 1.0, uProgress);
    float expand = 1.0 + uProgress * 0.65;

    // Rotate into blade local coordinates
    float cosR = cos(uRotation);
    float sinR = sin(uRotation);
    float2 pBlade = float2(
        p.x * cosR + p.y * sinR,
        -p.x * sinR + p.y * cosR
    );

    // 1. Central White-Hot Star Burst Core
    float core = exp(-d * (28.0 / expand)) * 2.2;

    // 2. Anamorphic Horizontal Lens Streak across screen
    float streak = exp(-abs(p.y) * 65.0) * exp(-abs(p.x) * (2.8 * expand)) * 1.8;

    // 3. Katana Blade Axis Cross Spikes (Blade axis + perpendicular)
    float spikeAxis = exp(-abs(pBlade.y) * 42.0) * exp(-abs(pBlade.x) * (3.2 * expand)) * 1.6;
    float spikePerp = exp(-abs(pBlade.x) * 42.0) * exp(-abs(pBlade.y) * (4.2 * expand)) * 1.2;

    // 4. Secondary 45-degree diffraction diamond rays
    float diag1 = exp(-abs(pBlade.x - pBlade.y) * 48.0) * exp(-d * 8.0) * 0.75;
    float diag2 = exp(-abs(pBlade.x + pBlade.y) * 48.0) * exp(-d * 8.0) * 0.75;

    // 5. Expanding Dimensional Shockwave Ring
    float ringR = uProgress * 0.40;
    float ringDiff = abs(d - ringR);
    float ring = exp(-ringDiff * ringDiff * 1200.0) * (1.0 - uProgress) * 1.4;

    // 6. Chromatic Dispersion (Prismatic Red & Cyan fringe)
    float dRed = length(p * 0.96);
    float dBlue = length(p * 1.04);
    float fringeR = exp(-abs(dRed - ringR) * 25.0) * 0.45;
    float fringeB = exp(-abs(dBlue - ringR) * 25.0) * 0.45;

    // Combine light channels
    float totalWhite = (core + streak * 0.5 + spikeAxis * 0.4) * lifeAlpha;
    float totalColor = (spikeAxis + spikePerp + diag1 + diag2 + ring + streak * 0.6) * lifeAlpha;
    float totalHalo = exp(-d * 8.0) * 0.55 * lifeAlpha;

    // Color grading: core hot white -> neon crimson edge -> electric violet halo
    float3 finalRGB = uColorCore * totalWhite
                    + uColorEdge * totalColor
                    + uColorGlow * totalHalo;

    // Add chromatic fringe
    finalRGB.r += fringeR * uColorEdge.r * lifeAlpha;
    finalRGB.b += fringeB * uColorGlow.b * lifeAlpha;

    finalRGB *= uIntensity;

    float alpha = saturate((totalWhite + totalColor * 0.8 + totalHalo * 0.5) * lifeAlpha);

    return float4(finalRGB * alpha, alpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PS_SheathFlash();
    }
}
