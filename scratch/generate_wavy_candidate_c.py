import math
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

def cubic_bezier(p0, p1, p2, p3, n=60):
    pts = []
    for i in range(n + 1):
        t = i / float(n)
        u = 1.0 - t
        x = (u**3)*p0[0] + 3*(u**2)*t*p1[0] + 3*u*(t**2)*p2[0] + (t**3)*p3[0]
        y = (u**3)*p0[1] + 3*(u**2)*t*p1[1] + 3*u*(t**2)*p2[1] + (t**3)*p3[1]
        pts.append((x, y))
    return pts

def rotate_pt(pt, angle_rad, center=(512.0, 512.0)):
    cx, cy = center
    x, y = pt[0] - cx, pt[1] - cy
    cos_a = math.cos(angle_rad)
    sin_a = math.sin(angle_rad)
    nx = cx + x * cos_a - y * sin_a
    ny = cy + x * sin_a + y * cos_a
    return (nx, ny)

def rotate_pts(pts, angle_rad, center=(512.0, 512.0)):
    return [rotate_pt(p, angle_rad, center) for p in pts]

def generate_wavy_crimson_knot(theme="red", size=256):
    s = 1024
    cx, cy = 512.0, 512.0
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if theme == "purple":
        c_shadow = (24, 2, 42, 255)       # Deep violet shadow outline
        c_dark = (68, 8, 112, 255)        # Rich dark purple base
        c_mid = (145, 22, 215, 255)       # Vibrant imperial purple
        c_light = (205, 75, 255, 255)     # Luminous magenta/violet
        c_highlight = (245, 195, 255, 255)# Bright edge glint
        c_gem_rim = (195, 60, 245, 255)
        c_gem_hot = (255, 240, 255, 255)
    else: # red
        c_shadow = (38, 2, 8, 255)        # Deep shadow outline
        c_dark = (108, 10, 24, 255)       # Rich dark crimson
        c_mid = (218, 22, 48, 255)        # Searing scarlet / Acheron red
        c_light = (255, 65, 88, 255)      # Luminous carmine
        c_highlight = (255, 190, 198, 255)# Bright edge glint
        c_gem_rim = (255, 85, 105, 255)
        c_gem_hot = (255, 242, 238, 255)

    # 5 Petals: Base angles around circle
    # Slight organic asymmetry / swirling shift:
    # 5 angles: -90 (top), -18 (top-right), 54 (bottom-right), 126 (bottom-left), 198 (top-left)
    petal_angles_deg = [-90.0, -18.0, 54.0, 126.0, 198.0]
    
    # Warping parameters for each petal to give Candidate A's dynamic flowing wavy personality:
    # curls: how much the petal sways laterally (S-curve amplitude)
    # len_scale: length of each petal
    # width_scale: fullness of each petal
    warps = [
        {"curl": 32.0, "tip_curl": 18.0, "len": 415.0, "w": 96.0, "rot_shift": -2.0},
        {"curl": 28.0, "tip_curl": -22.0, "len": 395.0, "w": 92.0, "rot_shift": 3.5},
        {"curl": 35.0, "tip_curl": 20.0, "len": 420.0, "w": 98.0, "rot_shift": -1.5},
        {"curl": -30.0, "tip_curl": -16.0, "len": 405.0, "w": 94.0, "rot_shift": 4.0},
        {"curl": 34.0, "tip_curl": -24.0, "len": 410.0, "w": 95.0, "rot_shift": -3.0},
    ]

    # Function to create a wavy petal polygon via smooth cubic beziers
    def build_wavy_petal(length, width, curl, tip_curl, offset_w=0.0):
        # Petal extends along Y axis upwards from (0,0), then we rotate to angle
        # Root is at (0, 40)
        # Left edge bezier: root -> control1 -> control2 -> tip
        # Right edge bezier: tip -> control3 -> control4 -> root
        
        # Centerline wavy S-curve:
        # Mid-body shifts by `curl`, tip shifts by `tip_curl`
        tip_x = tip_curl
        tip_y = -length
        
        w = width + offset_w
        
        # Left side controls
        p0_l = (-w * 0.25, -50.0)
        p1_l = (-w * 1.05 + curl * 0.6, -length * 0.35)
        p2_l = (-w * 0.75 + curl * 1.1, -length * 0.72)
        p3_l = (tip_x, tip_y)
        
        left_curve = cubic_bezier(p0_l, p1_l, p2_l, p3_l, n=36)
        
        # Right side controls
        p0_r = (tip_x, tip_y)
        p1_r = (w * 0.45 + curl * 0.9, -length * 0.70)
        p2_r = (w * 0.95 + curl * 0.4, -length * 0.32)
        p3_r = (w * 0.25, -50.0)
        
        right_curve = cubic_bezier(p0_r, p1_r, p2_r, p3_r, n=36)
        
        # Combine into closed loop
        raw_poly = left_curve + right_curve[1:] # avoid duplicate tip
        # Offset to center (cx, cy)
        poly = [(cx + px, cy + py) for px, py in raw_poly]
        return poly

    # 1. Shadow / Dark Outlines (Creates bold, crisp silhouette against all backgrounds)
    for i in range(5):
        ang = math.radians(petal_angles_deg[i] + warps[i]["rot_shift"] + 90.0)
        poly = build_wavy_petal(
            warps[i]["len"] + 10,
            warps[i]["w"] + 14,
            warps[i]["curl"] * 1.02,
            warps[i]["tip_curl"],
            offset_w=12.0
        )
        r_poly = rotate_pts(poly, ang, (cx, cy))
        draw.polygon(r_poly, fill=c_shadow)

    # 2. Main Petal Body (Velvety dark crimson/purple outer petal layer)
    for i in range(5):
        ang = math.radians(petal_angles_deg[i] + warps[i]["rot_shift"] + 90.0)
        poly = build_wavy_petal(
            warps[i]["len"],
            warps[i]["w"],
            warps[i]["curl"],
            warps[i]["tip_curl"],
            offset_w=0.0
        )
        r_poly = rotate_pts(poly, ang, (cx, cy))
        draw.polygon(r_poly, fill=c_dark, outline=c_mid, width=6)

    # 3. Inner Flowing Luminous Petal Layer (Vibrant scarlet/magenta fill - NO STRAIGHT WHITE SPOKES)
    for i in range(5):
        ang = math.radians(petal_angles_deg[i] + warps[i]["rot_shift"] + 90.0)
        poly_in = build_wavy_petal(
            warps[i]["len"] * 0.86,
            warps[i]["w"] * 0.65,
            warps[i]["curl"] * 0.95,
            warps[i]["tip_curl"] * 0.9,
            offset_w=-8.0
        )
        r_poly_in = rotate_pts(poly_in, ang, (cx, cy))
        draw.polygon(r_poly_in, fill=c_mid)
        # Delicate organic ridge line along the curved flow
        draw.line(r_poly_in, fill=c_light, width=4)

    # 4. Subtle Inner Highlight Crescent / Ridge (soft luminous flow inside each petal)
    for i in range(5):
        ang = math.radians(petal_angles_deg[i] + warps[i]["rot_shift"] + 90.0)
        poly_core = build_wavy_petal(
            warps[i]["len"] * 0.60,
            warps[i]["w"] * 0.34,
            warps[i]["curl"] * 0.90,
            warps[i]["tip_curl"] * 0.8,
            offset_w=-16.0
        )
        r_poly_core = rotate_pts(poly_core, ang, (cx, cy))
        draw.polygon(r_poly_core, fill=c_light)

    # -------------------------------------------------------------------------
    # 5. Central Flower Knot Rosette / Pistil Crest
    # Elegant 5-lobed organic flower knot heart that naturally blends into petals
    # -------------------------------------------------------------------------
    center_r = 82.0
    # Outer dark shadow backing
    draw.ellipse([cx - center_r - 8, cy - center_r - 8, cx + center_r + 8, cy + center_r + 8], fill=c_shadow)
    
    # 5-lobed swirling flower calyx (like Candidate A's swirling knot center)
    knot_pts = []
    num_k_pts = 50
    for j in range(num_k_pts):
        th = (j / float(num_k_pts)) * 2 * math.pi
        # 5-fold modulated radius with swirling phase offset
        r = center_r * (0.82 + 0.22 * math.sin(5 * th + 0.35))
        kx = cx + r * math.cos(th)
        ky = cy + r * math.sin(th)
        knot_pts.append((kx, ky))
    draw.polygon(knot_pts, fill=c_dark, outline=c_mid, width=6)

    # Inner swirling 5-armed pinwheel flower star (blends gracefully like Acheron's knot)
    star_r = center_r * 0.72
    inner_star = []
    for k in range(10):
        th = math.radians(-90 + k * 36)
        r_k = (star_r * 0.95) if (k % 2 == 0) else (star_r * 0.44)
        inner_star.append((cx + math.cos(th) * r_k, cy + math.sin(th) * r_k))
    draw.polygon(inner_star, fill=c_mid, outline=c_light, width=4)

    # Center jewel / knot core
    jewel_r = 22.0
    draw.ellipse([cx - jewel_r, cy - jewel_r, cx + jewel_r, cy + jewel_r], fill=c_light, outline=c_highlight, width=3)
    draw.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=c_gem_hot)

    # Downsample cleanly to final 256x256 with Lanczos for perfect antialiasing
    final = img.resize((size, size), Image.Resampling.LANCZOS)
    return final

if __name__ == "__main__":
    artifact_dir = r"C:\Users\Shiraori\.gemini\antigravity\brain\aa0487bb-0dc4-44b4-af65-3cbcc51e1417"

    red = generate_wavy_crimson_knot("red", 256)
    red_path = os.path.join(artifact_dir, "CrimsonKnot_WavyCandidateC_Red.png")
    red.save(red_path)

    purple = generate_wavy_crimson_knot("purple", 256)
    purple_path = os.path.join(artifact_dir, "CrimsonKnot_WavyCandidateC_Purple.png")
    purple.save(purple_path)

    print("Generated Wavy Candidate C icons successfully!")
