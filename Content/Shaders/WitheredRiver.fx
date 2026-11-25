// ============================================================================
// WitheredRiver.fx
// Debuff shader for Scourge of the Withered River (WitheredRiverDebuff)
// Inspired by Calamity Overhaul's KikasaWispFire.fx (TechBurnBody) & SHPCModThermal.fx:
//
// TechWitherBody:
//   - NPC sprite post-processing: drains living hue toward dried Lethe ash,
//     carves flowing crimson river-bed armor-peel fissures across the body,
//     and wraps the silhouette in a pulsing ashen-crimson contour rim.
//   - All UV distortion & neighbor alpha taps strictly clamped inside uUvRect
//     (half-pixel inset of npc.frame) so multi-frame sprite sheets never bleed.
//
// TechWitherSeal:
//   - Analytical quad overlay rendered via CrimsonKnotRenderHandle:
//     counter-drifting broken Lethe current arcs + 3 armor-shred fracture chevrons
//     so even bosses with overridden PreDraw always show a clear debuff visual.
//
// FNA3D / ps_3_0 safe: Cartesian procedural vnoise only, no atan2 in noise UVs,
// no tex2Dlod, no dynamic branching early returns after gradient ops.
// Premultiplied alpha output for BlendState.AlphaBlend.
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float2 uTexelSize;   // 1/texWidth, 1/texHeight
float4 uUvRect;      // xy=minUV, zw=maxUV (half-pixel inset of npc.frame)
float uFade;         // 0..1 smooth debuff presence envelope
float uFlash;        // 0..1 apply/refresh pulse
float uSeed;         // per-NPC phase seed

// Dried Lethe / Withered River palette
static const float3 COL_ASH        = float3(0.46, 0.41, 0.43);
static const float3 COL_PALE_BONE  = float3(0.84, 0.76, 0.78);
static const float3 COL_WINE_DEEP  = float3(0.36, 0.03, 0.09);
static const float3 COL_WITHER_RED = float3(0.92, 0.11, 0.24);

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vnoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// ============================================================================
// TechWitherBody: NPC Sprite Post-Process
// ============================================================================
float4 PSWitherBody(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float fade = saturate(uFade);
    float flash = saturate(uFlash);

    // Normalized coordinates inside the single NPC animation frame (0..1)
    float2 frameSpan = max(uUvRect.zw - uUvRect.xy, float2(0.00001, 0.00001));
    float2 fl = (coords - uUvRect.xy) / frameSpan;

    // Subtle downward-flowing Lethe current ripple (horizontal offset only, clamped to frame)
    float flowT = uTime * 1.45;
    float rip0 = vnoise(fl * float2(5.5, 4.0) + float2(uSeed * 3.1, -flowT));
    float rip1 = vnoise(fl * float2(11.0, 8.5) + float2(-uSeed * 2.3, -flowT * 1.6));
    float rip = (rip0 - 0.5) * 0.65 + (rip1 - 0.5) * 0.35;

    float2 distorted = coords;
    distorted.x += rip * uTexelSize.x * (1.2 + flash * 1.6) * fade;
    distorted = clamp(distorted, uUvRect.xy, uUvRect.zw);

    float4 src = tex2D(uImage0, distorted);
    float4 base = src * vc;

    // 4-neighbor contour rim clamped inside uUvRect
    float2 t2 = uTexelSize * 2.0;
    float aL = tex2D(uImage0, clamp(distorted - float2(t2.x, 0.0), uUvRect.xy, uUvRect.zw)).a;
    float aR = tex2D(uImage0, clamp(distorted + float2(t2.x, 0.0), uUvRect.xy, uUvRect.zw)).a;
    float aU = tex2D(uImage0, clamp(distorted - float2(0.0, t2.y), uUvRect.xy, uUvRect.zw)).a;
    float aD = tex2D(uImage0, clamp(distorted + float2(0.0, t2.y), uUvRect.xy, uUvRect.zw)).a;

    float minNeighbor = min(min(aL, aR), min(aU, aD));
    float innerEdge = saturate(src.a * 1.15 - minNeighbor);
    float outerHalo = saturate((aL + aR + aU + aD) * 0.25 - src.a * 0.65);

    // Withered Lethe ash drain: mute & cool body color without obscuring readability
    float luma = dot(base.rgb, float3(0.299, 0.587, 0.114));
    float3 ashCol = COL_ASH * (0.38 + 0.92 * luma) * base.a;
    float drainMask = (0.34 + 0.16 * rip0) * fade;
    base.rgb = lerp(base.rgb, ashCol, drainMask);

    // Flowing armor-peel river-bed veins (ridge noise across body)
    float vn1 = vnoise(fl * float2(6.5, 5.2) + float2(uSeed * 4.7, -flowT * 0.75));
    float vn2 = vnoise(fl * float2(13.0, 10.5) + float2(-uSeed * 3.9, -flowT * 1.15));
    float ridge = 1.0 - abs((vn1 * 0.62 + vn2 * 0.38) * 2.0 - 1.0);
    float veins = pow(saturate(ridge), 5.0);

    float pulse = 0.72 + 0.28 * sin(uTime * 3.6 + uSeed * 9.0);
    float3 veinCol = lerp(COL_WINE_DEEP, COL_WITHER_RED, veins * 0.75 + flash * 0.35);
    base.rgb += veinCol * veins * base.a * fade * (0.48 + 0.45 * flash) * pulse;

    // Ashen-crimson silhouette rim & apply flash
    float rimStr = (innerEdge * 0.75 + outerHalo * 0.85) * fade * (0.78 + 0.22 * pulse);
    float3 rimCol = lerp(COL_WITHER_RED, COL_PALE_BONE, saturate(rip0 * 0.55 + flash * 0.45));
    base.rgb += rimCol * rimStr * (0.68 + 0.55 * flash) * vc.a;

    // Brief bone-ash + crimson flash across body when freshly applied/refreshed
    base.rgb += lerp(COL_WITHER_RED, COL_PALE_BONE, 0.35) * base.a * flash * 0.32;

    float outAlpha = max(base.a, outerHalo * fade * 0.45 * vc.a);
    return float4(base.rgb, outAlpha);
}

// ============================================================================
// TechWitherSeal: Analytical Lethe Current & Armor-Peel Seal Quad Overlay
// ============================================================================
float4 PSWitherSeal(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float fade = saturate(uFade);
    float flash = saturate(uFlash);

    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);
    float2 dir = p / max(r, 0.0001);

    // Seamless rotated directions (Cartesian, zero atan2 seam)
    float rotA = uTime * 0.85 + uSeed * 6.28318;
    float sA = sin(rotA);
    float cA = cos(rotA);
    float2 dirA = float2(dir.x * cA - dir.y * sA, dir.x * sA + dir.y * cA);

    float rotB = -uTime * 1.20 - uSeed * 4.18879;
    float sB = sin(rotB);
    float cB = cos(rotB);
    float2 dirB = float2(dir.x * cB - dir.y * sB, dir.x * sB + dir.y * cB);

    // 3-fold angular harmonic (100% seamless via Chebyshev / vector math)
    // cos(3*theta) = 4*x^3 - 3*x
    float c3A = 4.0 * dirA.x * dirA.x * dirA.x - 3.0 * dirA.x;
    float c3B = 4.0 * dirB.x * dirB.x * dirB.x - 3.0 * dirB.x;

    // Flowing noise distortion for organic river-current feel
    float flowN = vnoise(dirA * 2.4 + float2(uTime * 0.55, uSeed * 5.0));
    float ripple = (flowN - 0.5) * 0.045;

    // Outer broken Lethe current ring (3 flowing segments)
    float rOuter = 0.72 + ripple + 0.03 * flash;
    float dOuter = abs(r - rOuter);
    float arcMaskA = smoothstep(-0.22, 0.35, c3A);
    float outerBand = exp(-(dOuter * dOuter) / (0.036 * 0.036)) * arcMaskA;
    float outerCore = exp(-dOuter * 58.0) * arcMaskA;

    // Inner counter-drifting ashen current ring
    float rInner = 0.56 - ripple * 0.7;
    float dInner = abs(r - rInner);
    float arcMaskB = smoothstep(-0.05, 0.50, c3B);
    float innerBand = exp(-(dInner * dInner) / (0.028 * 0.028)) * arcMaskB;

    // 3 sharp armor-peel fracture nodes riding the outer current
    float nodePeak = pow(saturate(c3A * 0.5 + 0.5), 14.0);
    float dNode = abs(r - rOuter);
    float nodeGlow = exp(-(dNode * dNode) / (0.065 * 0.065)) * nodePeak;

    // Expanding apply/refresh ripple wave
    float waveR = lerp(0.35, 0.94, 1.0 - flash);
    float dWave = abs(r - waveR);
    float flashRing = exp(-(dWave * dWave) / (0.045 * 0.045)) * flash;

    float maskGate = 1.0 - smoothstep(0.92, 1.0, r);
    float breath = 0.78 + 0.22 * sin(uTime * 3.8 + uSeed * 7.0);

    float3 col = COL_WINE_DEEP * (outerBand * 0.65 + innerBand * 0.45)
               + COL_WITHER_RED * (outerCore * 0.95 + nodeGlow * 0.85 + flashRing * 1.10)
               + COL_PALE_BONE * (innerBand * 0.55 + nodeGlow * 0.45 + flashRing * 0.65);

    float alpha = saturate(outerBand * 0.52 + outerCore * 0.75 + innerBand * 0.46 + nodeGlow * 0.72 + flashRing * 0.80);
    float env = fade * maskGate * breath;

    return float4(col * env, alpha * env) * vc;
}

technique TechWitherBody
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSWitherBody();
    }
}

technique TechWitherSeal
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSWitherSeal();
    }
}
