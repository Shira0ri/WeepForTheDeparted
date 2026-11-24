import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

def generate_wavy_candidate_C(theme="red", size=256):
    s_size = 512
    cx, cy = 256.0, 256.0
    img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if theme == "purple":
        c_fill = (175, 45, 245)
        c_dark = (70, 10, 115)
        c_edge = (245, 175, 255)
        c_shadow = (25, 3, 45)
        c_hot = (255, 245, 255)
    else: # red
        c_fill = (235, 28, 58)
        c_dark = (110, 10, 28)
        c_edge = (255, 150, 170)
        c_shadow = (38, 2, 10)
        c_hot = (255, 245, 240)

    # 5 Petal Base Angles (with slight asymmetric organic shifts for dynamic flow)
    # Spaced around circle, starting with top petal
    base_angles = [-90, -18, 54, 126, 198]
    # Organic angle shifts to break rigid radial symmetry (warped & shifted like Candidate A)
    angle_shifts = [-3.0, 4.0, -2.0, 5.0, -4.0]
    lengths = [212.0, 204.0, 216.0, 208.0, 214.0]
    widths = [48.0, 44.0, 47.0, 45.0, 46.0]
    
    # Sinuous wave amplitudes and phases for each petal
    # Gives each petal an elegant, flowing, wavy ribbon-like S-curve
    curls = [1.0, -1.1, 1.2, -0.9, 1.1]

    num_petals = 5

    def make_wavy_petal(angle_deg, length, max_w, curl_factor, extra_w=0.0):
        rad = math.radians(angle_deg)
        cos_a, sin_a = math.cos(rad), math.sin(rad)
        ux, uy = cos_a, sin_a
        vx, vy = -sin_a, cos_a
        
        pts_l = []
        pts_r = []
        subdiv = 32
        for i in range(subdiv + 1):
            t = i / float(subdiv)
            
            # Wavy S-curve spine offset (flows like ribbon folds in Candidate A)
            # Starts smooth at base (t=0), waves outwards and curls at the tip
            wave = (math.sin(t * math.pi) * 22.0 + math.sin(t * math.pi * 2.0) * 8.0) * curl_factor
            
            # Width profile: narrow at base, swells organically in mid-body, tapers to pointed tip
            w = (max_w + extra_w) * math.sin(math.pi * (t ** 0.58)) * (1.0 - t * 0.48)
            d = length * t
            
            # Spine position
            sx = cx + ux * d + vx * wave
            sy = cy + uy * d + vy * wave
            
            pts_l.append((sx + vx * w, sy + vy * w))
            pts_r.append((sx - vx * w, sy - vy * w))
            
        tip_wave = (math.sin(1.0 * math.pi) * 22.0 + math.sin(2.0 * math.pi) * 8.0 + 8.0) * curl_factor
        tip = (cx + ux * length + vx * tip_wave, cy + uy * length + vy * tip_wave)
        return pts_l + [tip] + list(reversed(pts_r))

    # 1. Dark Shadow Border (gives crisp, bold silhouette against any background)
    for i in range(num_petals):
        a = base_angles[i] + angle_shifts[i]
        poly = make_wavy_petal(a, lengths[i] + 4, widths[i], curls[i], extra_w=5.0)
        draw.polygon(poly, fill=c_shadow)

    # 2. Outer Petal Body (rich velvety crimson/dark purple)
    for i in range(num_petals):
        a = base_angles[i] + angle_shifts[i]
        poly = make_wavy_petal(a, lengths[i], widths[i], curls[i])
        draw.polygon(poly, fill=c_dark, outline=c_fill, width=4)

    # 3. Inner Petal Vibrant Fill (Clean, luminous surface — NO white center lines!)
    for i in range(num_petals):
        a = base_angles[i] + angle_shifts[i]
        poly_inner = make_wavy_petal(a, lengths[i] * 0.86, widths[i] * 0.65, curls[i])
        draw.polygon(poly_inner, fill=c_fill)
        # Delicate bright edge along inner petal
        draw.line(poly_inner, fill=c_edge, width=2)

    # 4. Central Flower Calyx / Rosette Core (Clean, faceted anime jewel, no white lines to petals)
    core_r = 38.0
    draw.ellipse([cx - core_r - 4, cy - core_r - 4, cx + core_r + 4, cy + core_r + 4], fill=c_shadow)
    draw.ellipse([cx - core_r, cy - core_r, cx + core_r, cy + core_r], fill=c_dark, outline=c_edge, width=4)

    # Inner 5-pointed floral star gem
    star_pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = (core_r * 0.78) if (i % 2 == 0) else (core_r * 0.36)
        star_pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    draw.polygon(star_pts, fill=c_fill, outline=c_hot, width=2)
    draw.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=c_hot)

    # Downsample cleanly with Lanczos to 256x256
    final = img.resize((size, size), Image.Resampling.LANCZOS)
    return final

if __name__ == "__main__":
    import os
    artifact_dir = r"C:\Users\Shiraori\.gemini\antigravity\brain\aa0487bb-0dc4-44b4-af65-3cbcc51e1417"

    red = generate_wavy_candidate_C("red", 256)
    red.save(os.path.join(artifact_dir, "CrimsonKnot_WavyC_Red.png"))

    purple = generate_wavy_candidate_C("purple", 256)
    purple.save(os.path.join(artifact_dir, "CrimsonKnot_WavyC_Purple.png"))

    print("Generated Wavy Candidate C icons successfully!")
