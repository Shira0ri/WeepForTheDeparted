import math
import os
import numpy as np
from PIL import Image, ImageDraw

def generate_wavy_candidate_C_v2(theme="red", size=256, variant="balanced"):
    s_size = 512
    cx, cy = 256.0, 256.0
    img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if theme == "purple":
        c_fill = (165, 35, 235)
        c_dark = (65, 8, 105)
        c_edge = (245, 160, 255)
        c_hot = (255, 240, 255)
        c_shadow = (28, 3, 48)
    else: # red
        c_fill = (235, 25, 55)
        c_dark = (105, 10, 26)
        c_edge = (255, 140, 160)
        c_hot = (255, 240, 235)
        c_shadow = (42, 2, 10)

    # 5 Petals:
    # Candidate C base angles: -90, -18, 54, 126, 198
    base_angles = [-90.0, -18.0, 54.0, 126.0, 198.0]
    
    if variant == "subtle":
        # Subtle organic shifts and waves
        shifts = [-2.0, 3.0, -1.5, 2.5, -2.0]
        lengths = [212.0, 206.0, 214.0, 208.0, 210.0]
        widths = [45.0, 44.0, 46.0, 44.0, 45.0]
        waves = [18.0, -16.0, 20.0, -18.0, 19.0]
        twists = [10.0, -12.0, 14.0, -10.0, 12.0]
    elif variant == "swirl":
        # Dynamic swirling flow (like Candidate A's swirling pinwheel petals)
        shifts = [-4.0, 5.0, -3.0, 4.0, -4.0]
        lengths = [214.0, 208.0, 216.0, 206.0, 212.0]
        widths = [47.0, 45.0, 48.0, 45.0, 46.0]
        # Positive wave on all petals gives a unified dynamic swirling petal flow
        waves = [28.0, 24.0, 30.0, 26.0, 28.0]
        twists = [18.0, 14.0, 20.0, 16.0, 18.0]
    else: # balanced
        # Elegant organic spider lily wave
        shifts = [-3.0, 4.0, -2.0, 3.0, -3.5]
        lengths = [212.0, 205.0, 215.0, 207.0, 211.0]
        widths = [46.0, 44.0, 47.0, 44.0, 45.0]
        waves = [24.0, -22.0, 26.0, -20.0, 23.0]
        twists = [14.0, -14.0, 16.0, -12.0, 15.0]

    def make_wavy_petal(angle_deg, length, max_w, wave_amp, wave_tw, extra_w=0.0):
        rad = math.radians(angle_deg)
        cos_a, sin_a = math.cos(rad), math.sin(rad)
        ux, uy = cos_a, sin_a
        vx, vy = -sin_a, cos_a

        pts_l = []
        pts_r = []
        subdiv = 48
        for i in range(subdiv):
            t = i / float(subdiv)
            # Sinuous curve along spine
            spine_wave = (math.sin(t * math.pi) * wave_amp + 
                          math.sin(t * math.pi * 2.0) * (wave_amp * 0.28) + 
                          (t ** 2.0) * wave_tw)
            
            # Width profile: Candidate C's tapered pointed curve
            w = (max_w + extra_w) * math.sin(math.pi * (t ** 0.60)) * (1.0 - t * 0.45)
            d = length * t
            
            sx = cx + ux * d + vx * spine_wave
            sy = cy + uy * d + vy * spine_wave
            
            pts_l.append((sx + vx * w, sy + vy * w))
            pts_r.append((sx - vx * w, sy - vy * w))

        # Tip at t = 1.0
        t1 = 1.0
        spine_wave1 = (math.sin(t1 * math.pi) * wave_amp + 
                       math.sin(t1 * math.pi * 2.0) * (wave_amp * 0.28) + 
                       (t1 ** 2.0) * wave_tw)
        tip = (cx + ux * length + vx * spine_wave1, cy + uy * length + vy * spine_wave1)
        
        return pts_l + [tip] + list(reversed(pts_r))

    num_petals = 5

    # 1. Dark Shadow Border (bold crisp silhouette against all backgrounds)
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly = make_wavy_petal(a, lengths[i] + 4, widths[i] + 5, waves[i], twists[i], extra_w=4.0)
        draw.polygon(poly, fill=c_shadow)

    # 2. Main Petal Body (rich velvety crimson/dark purple)
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly = make_wavy_petal(a, lengths[i], widths[i], waves[i], twists[i])
        draw.polygon(poly, fill=c_dark, outline=c_fill, width=4)

    # 3. Inner Petal Vibrant Fill (Clean luminous fill — NO white lines!)
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly_inner = make_wavy_petal(a, lengths[i] * 0.85, widths[i] * 0.65, waves[i] * 0.95, twists[i] * 0.95)
        draw.polygon(poly_inner, fill=c_fill)

    # 4. Central Flower Calyx / Rosette Core (Candidate C's faceted star jewel)
    cr = 38.0
    draw.ellipse([cx - cr, cy - cr, cx + cr, cy + cr], fill=c_shadow, outline=c_edge, width=4)
    
    # Inner 5-pointed star jewel
    star_pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = (cr * 0.80) if (i % 2 == 0) else (cr * 0.38)
        star_pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    draw.polygon(star_pts, fill=c_fill, outline=c_hot, width=2)
    draw.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=(255, 255, 255))

    final = img.resize((size, size), Image.Resampling.LANCZOS)
    return final

if __name__ == "__main__":
    for v in ["balanced", "swirl"]:
        for t in ["red", "purple"]:
            im = generate_wavy_candidate_C_v2(t, 256, variant=v)
            im.save(f"scratch/wavy_c_{v}_{t}.png")
    print("Generated test variants!")
