import math
import os
import numpy as np
from PIL import Image, ImageDraw

def generate_knot(theme="red", variant="swirl", size=256):
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

    base_angles = [-90.0, -18.0, 54.0, 126.0, 198.0]
    
    if variant == "swirl":
        # Dynamic swirling flow (Candidate A's unified clockwise swirl)
        shifts = [-4.0, 5.0, -3.0, 4.0, -4.0]
        lengths = [214.0, 208.0, 216.0, 206.0, 212.0]
        widths = [47.0, 45.0, 48.0, 45.0, 46.0]
        waves = [28.0, 24.0, 30.0, 26.0, 28.0]
        twists = [18.0, 14.0, 20.0, 16.0, 18.0]
    elif variant == "scurve":
        # Natural alternating S-curves (organic spider-lily sway)
        shifts = [-3.0, 4.0, -2.0, 3.0, -3.5]
        lengths = [212.0, 205.0, 215.0, 207.0, 211.0]
        widths = [46.0, 44.0, 47.0, 44.0, 45.0]
        waves = [24.0, -22.0, 26.0, -20.0, 23.0]
        twists = [14.0, -14.0, 16.0, -12.0, 15.0]
    else: # floral_knot
        # Dynamic swirl with flowing 5-lobed calyx knot center
        shifts = [-4.0, 5.0, -3.0, 4.0, -4.0]
        lengths = [214.0, 208.0, 216.0, 206.0, 212.0]
        widths = [47.0, 45.0, 48.0, 45.0, 46.0]
        waves = [28.0, 24.0, 30.0, 26.0, 28.0]
        twists = [18.0, 14.0, 20.0, 16.0, 18.0]

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

    # 1. Dark Shadow Border
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly = make_wavy_petal(a, lengths[i] + 4, widths[i] + 5, waves[i], twists[i], extra_w=4.0)
        draw.polygon(poly, fill=c_shadow)

    # 2. Main Petal Body
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly = make_wavy_petal(a, lengths[i], widths[i], waves[i], twists[i])
        draw.polygon(poly, fill=c_dark, outline=c_fill, width=4)

    # 3. Inner Petal Vibrant Fill (Clean luminous fill — NO white lines!)
    for i in range(num_petals):
        a = base_angles[i] + shifts[i]
        poly_inner = make_wavy_petal(a, lengths[i] * 0.85, widths[i] * 0.65, waves[i] * 0.95, twists[i] * 0.95)
        draw.polygon(poly_inner, fill=c_fill)

    # 4. Center Core
    cr = 38.0
    if variant == "floral_knot":
        # 5-lobed organic calyx heart that twists with the petals
        draw.ellipse([cx - cr - 4, cy - cr - 4, cx + cr + 4, cy + cr + 4], fill=c_shadow)
        calyx_pts = []
        for step in range(50):
            th = (step / 50.0) * 2 * math.pi
            r_c = cr * (0.84 + 0.18 * math.sin(5 * th + 0.4))
            calyx_pts.append((cx + r_c * math.cos(th), cy + r_c * math.sin(th)))
        draw.polygon(calyx_pts, fill=c_dark, outline=c_edge, width=4)
        
        # Swirling 5-point star
        star_pts = []
        for i in range(10):
            a = math.radians(-90 + i * 36 + 6) # slight matching swirl
            r = (cr * 0.78) if (i % 2 == 0) else (cr * 0.38)
            star_pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        draw.polygon(star_pts, fill=c_fill, outline=c_hot, width=2)
        draw.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=(255, 255, 255))
    else:
        # Candidate C's classic circular calyx + faceted star jewel
        draw.ellipse([cx - cr, cy - cr, cx + cr, cy + cr], fill=c_shadow, outline=c_edge, width=4)
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
    art_dir = r"C:\Users\Shiraori\.gemini\antigravity\brain\aa0487bb-0dc4-44b4-af65-3cbcc51e1417"

    variants = [
        ("swirl", "WavyC1_Swirl"),
        ("scurve", "WavyC2_SCurve"),
        ("floral_knot", "WavyC3_FloralKnot")
    ]

    for var_code, var_name in variants:
        for theme in ["Red", "Purple"]:
            im = generate_knot(theme.lower(), variant=var_code, size=256)
            path = os.path.join(art_dir, f"CrimsonKnot_{var_name}_{theme}.png")
            im.save(path)
            print(f"Saved: {path}")

    # Also save the top recommended one (WavyC1_Swirl) as CrimsonKnot_WavyC_Red.png and Purple.png
    im_red = generate_knot("red", "swirl", 256)
    im_red.save(os.path.join(art_dir, "CrimsonKnot_WavyC_Red.png"))
    im_pur = generate_knot("purple", "swirl", 256)
    im_pur.save(os.path.join(art_dir, "CrimsonKnot_WavyC_Purple.png"))
    print("All candidate variants generated successfully!")
