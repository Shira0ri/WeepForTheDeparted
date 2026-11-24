import math
import os
import numpy as np
from PIL import Image, ImageDraw

def generate_ultra_knot(theme="red", size=256):
    # 4x Supersampling canvas for pristine vector antialiasing
    s_size = 1024
    cx, cy = 512.0, 512.0
    img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if theme == "purple":
        c_fill = (175, 40, 245)
        c_dark = (68, 8, 110)
        c_edge = (245, 168, 255)
        c_hot = (255, 242, 255)
        c_shadow = (28, 3, 48)
    else: # red
        c_fill = (238, 26, 56)
        c_dark = (108, 10, 28)
        c_edge = (255, 145, 165)
        c_hot = (255, 242, 238)
        c_shadow = (42, 2, 10)

    # Base angles around circle:
    base_angles = [-90.0, -18.0, 54.0, 126.0, 198.0]
    
    # Option 1 (Dynamic Swirl) parameters (scaled by 2 for 1024 canvas)
    shifts = [-4.0, 5.0, -3.0, 4.0, -4.0]
    lengths = [428.0, 416.0, 432.0, 412.0, 424.0]
    widths = [94.0, 90.0, 96.0, 90.0, 92.0]
    waves = [56.0, 48.0, 60.0, 52.0, 56.0]
    twists = [36.0, 28.0, 40.0, 32.0, 36.0]

    def make_wavy_petal(angle_deg, length, max_w, wave_amp, wave_tw, extra_w=0.0):
        rad = math.radians(angle_deg)
        cos_a, sin_a = math.cos(rad), math.sin(rad)
        ux, uy = cos_a, sin_a
        vx, vy = -sin_a, cos_a

        pts_l = []
        pts_r = []
        subdiv = 96
        for i in range(subdiv):
            t = i / float(subdiv)
            spine_wave = (math.sin(t * math.pi) * wave_amp + 
                          math.sin(t * math.pi * 2.0) * (wave_amp * 0.28) + 
                          (t ** 2.0) * wave_tw)
            
            w = (max_w + extra_w) * math.sin(math.pi * (t ** 0.60)) * (1.0 - t * 0.45)
            d = length * t
            
            sx = cx + ux * d + vx * spine_wave
            sy = cy + uy * d + vy * spine_wave
            
            pts_l.append((sx + vx * w, sy + vy * w))
            pts_r.append((sx - vx * w, sy - vy * w))

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
        poly = make_wavy_petal(a, lengths[i] + 8, widths[i] + 10, waves[i], twists[i], extra_w=8.0)
        draw.polygon(poly, fill=c_shadow)

    # 2. Main Petal Body (rich velvety crimson/dark purple)
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly = make_wavy_petal(a, lengths[i], widths[i], waves[i], twists[i])
        draw.polygon(poly, fill=c_dark, outline=c_fill, width=8)

    # 3. Inner Petal Vibrant Fill (Clean luminous fill — NO white lines!)
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly_inner = make_wavy_petal(a, lengths[i] * 0.85, widths[i] * 0.65, waves[i] * 0.95, twists[i] * 0.95)
        draw.polygon(poly_inner, fill=c_fill)

    # 4. Center Flower Calyx / Rosette Core (Candidate C's faceted star jewel)
    cr = 76.0
    draw.ellipse([cx - cr, cy - cr, cx + cr, cy + cr], fill=c_shadow, outline=c_edge, width=8)
    
    # Inner 5-pointed star jewel
    star_pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = (cr * 0.80) if (i % 2 == 0) else (cr * 0.38)
        star_pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    draw.polygon(star_pts, fill=c_fill, outline=c_hot, width=4)
    draw.ellipse([cx - 16, cy - 16, cx + 16, cy + 16], fill=(255, 255, 255))

    # Downsample cleanly to final 256x256 with Lanczos for super-smooth vector antialiasing
    final = img.resize((size, size), Image.Resampling.LANCZOS)
    return final

if __name__ == "__main__":
    out_red = generate_ultra_knot("red", 256)
    out_pur = generate_ultra_knot("purple", 256)
    
    out_red.save("Content/Projectiles/CrimsonKnotRed.png")
    out_pur.save("Content/Projectiles/CrimsonKnotPurple.png")
    print("Exported ultra-clarity Option 1 assets to Content/Projectiles successfully!")
