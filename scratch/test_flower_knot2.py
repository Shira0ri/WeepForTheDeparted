import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

def create_flowery_knot(theme="red", size=256):
    # Working at 2x resolution (512x512)
    s_size = 512
    cx = 256.0
    cy = 205.0 # Center flower slightly above middle to leave room for flowing tassels below

    img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Palette
    if theme == "purple":
        col_core = (255, 255, 255)
        col_hot = (245, 195, 255)
        col_bright = (210, 85, 255)
        col_mid = (150, 30, 225)
        col_dark = (70, 10, 120)
        col_deep = (30, 3, 55)
        col_gold = (255, 230, 175)
        col_glow = (195, 55, 255)
    else: # red
        col_core = (255, 255, 255)
        col_hot = (255, 200, 180)
        col_bright = (255, 45, 75)
        col_mid = (215, 18, 48)
        col_dark = (110, 10, 28)
        col_deep = (45, 3, 12)
        col_gold = (255, 235, 165)
        col_glow = (255, 30, 60)

    # -------------------------------------------------------------
    # 1. Soft Background Floral Glow
    # -------------------------------------------------------------
    glow_img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow_img)
    
    max_gr = 180.0
    for gr in range(int(max_gr), 15, -6):
        alpha = int(40 * (1.0 - gr / max_gr)**1.8)
        c = (col_glow[0], col_glow[1], col_glow[2], alpha)
        glow_draw.ellipse([cx - gr, cy - gr, cx + gr, cy + gr], fill=c)

    # -------------------------------------------------------------
    # 2. Flowing Silk Tassels Hanging Below Flower
    # -------------------------------------------------------------
    tassel_y_start = cy + 42.0
    for side in [-1, 1]:
        # S-curve control points
        pts = [
            (cx + side * 12.0, tassel_y_start),
            (cx + side * 34.0, tassel_y_start + 50.0),
            (cx + side * 18.0, tassel_y_start + 115.0),
            (cx + side * 28.0, tassel_y_start + 185.0)
        ]
        
        steps = 50
        curve_pts = []
        for step in range(steps + 1):
            t = step / float(steps)
            x = (1-t)**3 * pts[0][0] + 3*(1-t)**2*t * pts[1][0] + 3*(1-t)*t**2 * pts[2][0] + t**3 * pts[3][0]
            y = (1-t)**3 * pts[0][1] + 3*(1-t)**2*t * pts[1][1] + 3*(1-t)*t**2 * pts[2][1] + t**3 * pts[3][1]
            curve_pts.append((x, y))

        # Cord background shadow
        draw.line(curve_pts, fill=col_deep, width=7)
        # Main silk cord body
        draw.line(curve_pts, fill=col_mid, width=5)
        # Highlight silk cord
        draw.line(curve_pts, fill=col_hot, width=2)

        # Golden beads along cord
        for bp in [0.28, 0.65, 1.0]:
            idx = int(bp * steps)
            bx, by = curve_pts[idx]
            br = 5.0 if bp < 1.0 else 6.5
            draw.ellipse([bx - br, by - br, bx + br, by + br], fill=col_gold, outline=col_deep, width=2)
            # Center bright shine
            draw.ellipse([bx - br*0.4, by - br*0.4, bx + br*0.2, by + br*0.2], fill=(255, 255, 255))

        # Tassel fringe brush
        end_x, end_y = curve_pts[-1]
        for f_angle in np.linspace(-0.35, 0.35, 8):
            fx = end_x + math.sin(f_angle + side * 0.15) * 32.0
            fy = end_y + math.cos(f_angle) * 34.0
            draw.line([(end_x, end_y + 4.0), (fx, fy)], fill=col_bright, width=2)
            draw.line([(end_x, end_y + 4.0), (fx * 0.7 + end_x * 0.3, fy * 0.7 + end_y * 0.3)], fill=col_hot, width=1)

    # -------------------------------------------------------------
    # 3. Organic 5-Petal Flower Geometry
    # -------------------------------------------------------------
    num_petals = 5
    # Petal orientation: Top petal at -90 deg, rest spaced at 72 deg intervals
    petal_angles = [-90 + i * (360 / num_petals) for i in range(num_petals)]

    def generate_petal_polygon(angle_deg, length, max_w, tip_roundness=0.85):
        rad = math.radians(angle_deg)
        ux, uy = math.cos(rad), math.sin(rad)
        vx, vy = -math.sin(rad), math.cos(rad)

        pts_l = []
        pts_r = []
        subdiv = 30
        for i in range(subdiv + 1):
            t = i / float(subdiv)
            # Beautiful natural petal curvature:
            # Narrow at base (t=0), swells to max_w around t=0.6, gently curves into a teardrop tip at t=1.0
            w = max_w * math.sin(math.pi * (t ** 0.8))
            if t > 0.88:
                # Smooth rounded tip
                w *= (1.0 - (t - 0.88) / 0.12) ** 0.6
                
            dist = length * t
            pl = (cx + ux * dist + vx * w, cy + uy * dist + vy * w)
            pr = (cx + ux * dist - vx * w, cy + uy * dist - vy * w)
            pts_l.append(pl)
            pts_r.append(pr)

        tip = (cx + ux * length, cy + uy * length)
        return pts_l + [tip] + list(reversed(pts_r))

    # Layer A: Outer Large Translucent Petals (Graceful, organic, vibrant borders)
    outer_len = 118.0
    outer_w = 40.0
    for a in petal_angles:
        poly = generate_petal_polygon(a, outer_len, outer_w)
        draw.polygon(poly, fill=col_dark, outline=col_mid, width=3)
        draw.line(poly, fill=col_bright, width=2)

    # Layer B: Inner Layer Petals (Vibrant velvety crimson / purple with satin sheen)
    inner_len = 96.0
    inner_w = 32.0
    for a in petal_angles:
        poly = generate_petal_polygon(a, inner_len, inner_w)
        draw.polygon(poly, fill=col_mid, outline=col_hot, width=2)

        # Petal central vein arch
        rad = math.radians(a)
        bx = cx + math.cos(rad) * 16.0
        by = cy + math.sin(rad) * 16.0
        tx = cx + math.cos(rad) * (inner_len * 0.90)
        ty = cy + math.sin(rad) * (inner_len * 0.90)
        draw.line([(bx, by), (tx, ty)], fill=col_hot, width=2)
        draw.line([(bx, by), (tx * 0.65 + bx * 0.35, ty * 0.65 + by * 0.35)], fill=col_core, width=1)

    # -------------------------------------------------------------
    # 4. Graceful Curving Stamens (Spider Lily Tendrils)
    # -------------------------------------------------------------
    # Arching filaments extending between petals with bright glowing golden pollen anthers
    for i in range(num_petals):
        stamen_angle = petal_angles[i] + (360 / (num_petals * 2)) # in between petals
        rad = math.radians(stamen_angle)
        
        stamen_len = 112.0
        ux, uy = math.cos(rad), math.sin(rad)
        vx, vy = -math.sin(rad), math.cos(rad)
        
        # Elegant arching curve
        p0 = (cx + ux * 20.0, cy + uy * 20.0)
        p1 = (cx + ux * 68.0 + vx * 16.0, cy + uy * 68.0 + vy * 16.0)
        p2 = (cx + ux * stamen_len + vx * 22.0, cy + uy * stamen_len + vy * 22.0)
        
        stamen_pts = []
        for step in range(30):
            t = step / 30.0
            x = (1-t)**2 * p0[0] + 2*(1-t)*t * p1[0] + t**2 * p2[0]
            y = (1-t)**2 * p0[1] + 2*(1-t)*t * p1[1] + t**2 * p2[1]
            stamen_pts.append((x, y))
            
        draw.line(stamen_pts, fill=col_hot, width=2)
        
        # Golden anther head
        tip_x, tip_y = stamen_pts[-1]
        ar = 4.2
        draw.ellipse([tip_x - ar*1.2, tip_y - ar, tip_x + ar*1.2, tip_y + ar], fill=col_gold, outline=col_core, width=1)

    # -------------------------------------------------------------
    # 5. Center Woven Floral Rosette (Hanamusubi Knot Center)
    # -------------------------------------------------------------
    knot_ring_r = 24.0
    for a in range(0, 360, 45):
        rad = math.radians(a)
        kx = cx + math.cos(rad) * 18.0
        ky = cy + math.sin(rad) * 18.0
        kr = 12.0
        draw.ellipse([kx - kr, ky - kr, kx + kr, ky + kr], outline=col_deep, fill=col_dark, width=2)
        draw.ellipse([kx - kr + 1, ky - kr + 1, kx + kr - 1, ky + kr - 1], outline=col_bright, width=2)

    # -------------------------------------------------------------
    # 6. Radiant Blossom Gem Core
    # -------------------------------------------------------------
    core_r = 17.0
    draw.ellipse([cx - core_r, cy - core_r, cx + core_r, cy + core_r], fill=col_deep, outline=col_gold, width=2)
    
    # Inner 5-pointed floral star
    gem_pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = (core_r * 0.85) if (i % 2 == 0) else (core_r * 0.40)
        gem_pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    draw.polygon(gem_pts, fill=col_hot, outline=col_core, width=1)
    
    # White-hot center sparkle
    sr = 5.0
    draw.ellipse([cx - sr, cy - sr, cx + sr, cy + sr], fill=col_core)

    # -------------------------------------------------------------
    # 7. Final Composite & Downsampling to 256x256
    # -------------------------------------------------------------
    final_canvas = Image.alpha_composite(glow_img, img)
    final_icon = final_canvas.resize((size, size), Image.Resampling.LANCZOS)
    return final_icon

if __name__ == "__main__":
    purple_icon = create_flowery_knot("purple", 256)
    purple_icon.save(r"scratch\test_purple_flowery2.png")
    red_icon = create_flowery_knot("red", 256)
    red_icon.save(r"scratch\test_red_flowery2.png")
    print("Generated flowery test icons 2 successfully!")
