sampler2D uImage0 : register(s0);

float uTime;
float2 uResolution;
float2 uCenter;
float uOpacity;
float2 uRippleCenter;
float uRippleRadius;
float uRippleActive;

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    // Reflection line is directly at the middle (equator) of the black hole!
    float horizonY = uCenter.y;
    float2 uv = coords;
    bool isReflection = uv.y > horizonY;

    // Reflective floor below the horizon line with water ripples
    if (isReflection)
    {
        float depth = uv.y - horizonY;
        uv.y = horizonY - depth * 0.90;
        uv.x += sin(depth * 80.0 - uTime * 2.5) * 0.007 * depth * 8.0;
    }

    // Aspect-correct vector from black hole center
    float aspect = uResolution.x / uResolution.y;
    float2 delta = uv - uCenter;
    delta.x *= aspect;

    float dist = length(delta);
    float angle = atan2(delta.y, delta.x);

    // Balanced black hole with subtle upward lensing flare
    float radius = 0.33;
    float edgeDist = dist - radius;
    float outsideCore = smoothstep(0.0, 0.006, edgeDist);

    // Upward gravitational lensing flare (lensing halo looping over the top, delta.y < 0)
    float upFactor = saturate(-delta.y / (dist + 0.0001));
    float topFlare = pow(upFactor, 1.8) * 0.38;

    // Dynamic noise texture along the photon ring
    float2 polarUV = float2(angle / 6.2831853 + 0.5 + uTime * 0.02, dist * 1.5 - uTime * 0.05);
    float noise = tex2D(uImage0, polarUV).r;

    // Intense, crisp white photon ring around the entire horizon
    float ringSharpness = 30.0 - topFlare * 8.0;
    float innerRim = exp(-max(edgeDist, 0.0) * ringSharpness) * (1.5 + topFlare * 0.6);

    // Soft atmospheric bloom
    float wideBloom = exp(-max(edgeDist, 0.0) * 7.0) * (0.38 + 0.15 * noise);

    // Secondary gravitational lensing outer ring
    float outerDist = dist - (radius + 0.10 + topFlare * 0.04);
    float outerRing = exp(-(outerDist * outerDist) / 0.00035) * (0.35 + topFlare * 0.25);

    // Lensed accretion disk arc looping around the equator
    float diskY = delta.y * 3.2 + delta.x * 0.3;
    float diskDist = length(float2(delta.x, diskY));
    float accretionArc = exp(-abs(diskDist - 0.38) * 18.0) * 0.38 * (0.6 + 0.4 * noise);

    float blackHoleGlow = (innerRim + wideBloom + outerRing + accretionArc) * outsideCore;

    // Background Spiral Galaxy dust wisps
    float2 gDelta = uv - float2(0.28, 0.28);
    gDelta.x *= aspect;
    float gDist = length(gDelta);
    float galaxy = exp(-gDist * 3.2) * 0.14 * outsideCore;

    // Secondary distant dwarf nebula
    float2 nDelta = uv - float2(0.85, 0.20);
    nDelta.x *= aspect;
    float nDist = length(nDelta);
    float nebula = exp(-nDist * 4.5) * 0.08 * outsideCore;

    // Composite total background lighting (stars are rendered persistently via DomainStarSystem to eliminate grid popping)
    float totalLight = blackHoleGlow + galaxy + nebula;

    // Dark reflective water floor adjustments
    if (isReflection)
    {
        totalLight *= 0.60;
        totalLight += exp(-abs(coords.y - horizonY) * 70.0) * 0.40; // Horizon seam glow
    }

    float3 baseVoid = float3(0.012, 0.012, 0.016);
    float3 finalColor = saturate(lerp(baseVoid, float3(1.0, 1.0, 1.0), totalLight));

    // Core remains pure black void
    finalColor *= outsideCore;

    float finalAlpha = uOpacity;

    // Expanding Reflection Wave Masking:
    // When the blade is drawn, the wave expands outward from the player's position.
    // Inside the wave: mundane tiles/walls are darkened and replaced by the domain background!
    // Ahead of the wave: transparent, showing normal world.
    // At the wave boundary: brilliant glowing water reflection shockwave!
    if (uRippleActive > 0.5)
    {
        float2 rDelta = coords - uRippleCenter;
        rDelta.x *= aspect;
        float rDist = length(rDelta);

        // Wave edge: 1.0 inside the wave, 0.0 ahead of the wave
        float waveEdge = 1.0 - smoothstep(uRippleRadius - 0.02, uRippleRadius + 0.04, rDist);
        float shockwave = exp(-abs(rDist - uRippleRadius) * 50.0) * 1.8;

        finalAlpha = saturate(finalAlpha * waveEdge + shockwave * 0.9);
        finalColor = (finalColor * waveEdge + float3(1.0, 0.94, 0.98) * shockwave);
    }

    return float4(finalColor * finalAlpha, finalAlpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
