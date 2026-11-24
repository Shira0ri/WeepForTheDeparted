import math
import numpy as np
from PIL import Image, ImageDraw

def create_pure_flower_icon(theme="red", size=256):
    # Work at 2x resolution (512x512) for supersampled antialiased edges
    s_size = 512
    cx = 256.0
    cy = 256.0

    img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Palette
    if theme == "purple":
        col_core = (255, 255, 255)
        col_hot = (245, 195, 255)
        col_bright = (205, 75, 255)
        col_mid = (145, 25, 220)
        col_dark = (75, 10, 130)
        col_deep = (32, 4, 58)
        col_edge = (255, 180, 255)
    else: # red
        col_core = (255, 255, 255)
        col_hot = (255, 205, 190)
        col_bright = (255, 45, 75)
        col_mid = (215, 18, 48)
        col_dark = (115, 10, 30)
        col_deep = (45, 3, 12)
        col_edge = (255, 190, 180)

    # -------------------------------------------------------------
    # 6-Petal Spider Lily / Blossom with PRONOUNCED, SLENDER, CURVING PETALS
    # -------------------------------------------------------------
    # 6 petals at 60 degree intervals, rotated so top petal is at -90 deg
    num_petals = 6
    petal_angles = [-90 + i * 60 for i in range(num_petals)]
    # Petals at -90, -30, 30, 90, 150, 210

    # Petal length: extends outwards to ~210 px (leaving 46px margin in 512 space)
    petal_len = 215.0

    # Helper function to generate an organic, slender, curved spider lily petal
    def make_lily_petal(angle_deg, length, max_w, curl_dir=1.0):
        rad = math.radians(angle_deg)
        cos_a = math.cos(rad)
        sin_a = math.sin(rad)
        
        # Axis unit vectors
        ux, uy = cos_a, sin_a
        vx, vy = -sin_a, cos_a
        
        pts_left = []
        pts_right = []
        
        subdiv = 36
        for i in range(subdiv + 1):
            t = i / float(subdiv)
            
            # Width profile: very narrow at base (t=0), swells to max_w around t=0.45, then tapers to a sharp pointed tip
            w = max_w * math.sin(math.pi * (t ** 0.65)) * (1.0 - t * 0.75)
            
            # Sinuous curve: petal curls sideways slightly like a real spider lily petal
            curl = math.sin(t * math.pi) * 16.0 * curl_dir
            
            dist = length * t
            
            # Centerline of petal with curl
            px = cx + ux * dist + vx * curl
            py = cy + uy * dist + vy * curl
            
            # Left and right edge points
            pl = (px + vx * w, py + vy * w)
            pr = (px - vx * w, py - vy * w)
            
            pts_left.append(pl)
            pts_right.append(pr)
            
        tip = (cx + ux * length + vx * (4.0 * curl_dir), cy + uy * length + vy * (4.0 * curl_dir))
        return pts_left + [tip] + list(reversed(pts_right))

    # -------------------------------------------------------------
    # Draw Outer Primary Petals
    # -------------------------------------------------------------
    petal_w = 28.0 # slender, graceful
    for i, a in enumerate(petal_angles):
        # Alternating gentle curl for natural organic blossom asymmetry
        curl = 1.0 if (i % 2 == 0) else -1.0
        poly = make_lily_petal(a, petal_len, petal_w, curl)
        
        # Solid dark wine base
        draw.polygon(poly, fill=col_dark, outline=col_mid, width=3)
        # Vibrant bright petal body
        draw.line(poly, fill=col_bright, width=2)

    # -------------------------------------------------------------
    # Draw Inner Petal Highlights & Central Veins
    # -------------------------------------------------------------
    inner_len = petal_len * 0.82
    inner_w = petal_w * 0.65
    for i, a in enumerate(petal_angles):
        curl = 1.0 if (i % 2 == 0) else -1.0
        poly_inner = make_lily_petal(a, inner_len, inner_w, curl)
        draw.polygon(poly_inner, fill=col_mid)
        
        # Sharp central luminous vein
        rad = math.radians(a)
        cos_a = math.cos(rad)
        sin_a = math.sin(rad)
        ux, uy = cos_a, sin_a
        vx, vy = -sin_a, cos_a
        
        vein_pts = []
        for step in range(25):
            t = step / 24.0
            dist = inner_len * t
            c_offset = math.sin(t * math.pi) * 16.0 * curl
            vx_pt = cx + ux * dist + vx * c_offset
            vy_pt = cy + uy * dist + vy * c_offset
            vein_pts.append((vx_pt, vy_pt))
            
        draw.line(vein_pts, fill=col_hot, width=2)
        # Core white highlight near base of vein
        draw.line(vein_pts[:10], fill=col_core, width=2)

    # -------------------------------------------------------------
    # Secondary Inner Petals (Between the main 6 petals, shorter)
    # -------------------------------------------------------------
    # 6 smaller inner petals staggered between the main petals (at angles -60, 0, 60, 120, 180, 240)
    # This creates a rich, authentic multi-layered flower silhouette!
    sub_angles = [-60 + i * 60 for i in range(num_petals)]
    sub_len = 125.0
    sub_w = 20.0
    for i, a in enumerate(sub_angles):
        poly_sub = make_lily_petal(a, sub_len, sub_w, -1.0 if (i % 2 == 0) else 1.0)
        draw.polygon(poly_sub, fill=col_mid, outline=col_bright, width=2)

    # -------------------------------------------------------------
    # Spider Lily Filaments / Stamens (Dramatic arching tendrils with pollen heads)
    # -------------------------------------------------------------
    stamen_len = 225.0
    for i, a in enumerate(sub_angles):
        rad = math.radians(a)
        ux, uy = math.cos(rad), math.sin(rad)
        vx, vy = -math.sin(rad), math.cos(rad)
        
        # Arching filament curve
        side_curl = 26.0 if (i % 2 == 0) else -26.0
        p0 = (cx + ux * 25.0, cy + uy * 25.0)
        p1 = (cx + ux * 130.0 + vx * side_curl, cy + uy * 130.0 + vy * side_curl)
        p2 = (cx + ux * stamen_len + vx * (side_curl * 1.5), cy + uy * stamen_len + vy * (side_curl * 1.5))
        
        curve = []
        for step in range(30):
            t = step / 29.0
            x = (1-t)**2 * p0[0] + 2*(1-t)*t * p1[0] + t**2 * p2[0]
            y = (1-t)**2 * p0[1] + 2*(1-t)*t * p1[1] + t**2 * p2[1]
            curve.append((x, y))
            
        draw.line(curve, fill=col_deep, width=4)
        draw.line(curve, fill=col_hot, width=2)
        
        # Tipped with curved crescent / oval golden anther
        tip_x, tip_y = curve[-1]
        ar = 6.0
        draw.ellipse([tip_x - ar*1.4, tip_y - ar, tip_x + ar*1.4, tip_y + ar], fill=col_hot, outline=col_core, width=2)

    # -------------------------------------------------------------
    # Flower Pistil Core / Calyx (Geometric Rosette Center)
    # -------------------------------------------------------------
    calyx_r = 30.0
    # Center woven calyx ring
    draw.ellipse([cx - calyx_r, cy - calyx_r, cx + calyx_r, cy + calyx_r], fill=col_deep, outline=col_bright, width=3)
    
    # 6-pointed floral star gem in center
    star_r = 20.0
    gem_pts = []
    for i in range(12):
        angle = math.radians(-90 + i * 30)
        r = star_r if (i % 2 == 0) else (star_r * 0.45)
        gem_pts.append((cx + math.cos(angle) * r, cy + math.sin(angle) * r))
    draw.polygon(gem_pts, fill=col_hot, outline=col_core, width=2)

    # Brilliant central sparkling core
    cr = 6.5
    draw.ellipse([cx - cr, cy - cr, cx + cr, cy + cr], fill=col_core)

    # -------------------------------------------------------------
    # Final Downsampling to 256x256 (Pure Flower, ZERO GLOW!)
    # -------------------------------------------------------------
    final_icon = img.resize((size, size), Image.Resampling.LANCZOS)
    return final_icon

if __name__ == "__main__":
    p = create_pure_flower_icon("purple", 256)
    p.save(r"scratch\pure_flower_purple.png")
    r = create_pure_flower_icon("red", 256)
    r.save(r"scratch\pure_flower_red.png")
    print("Generated pure flower icons successfully!")
