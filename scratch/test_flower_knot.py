import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

def create_flowery_knot(theme="red", size=256):
    # Supersampling 2x (512x512) for razor-sharp antialiasing and ultra-smooth curves
    scale = 2
    s_size = size * scale
    s_center = s_size / 2.0
    
    img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Palette
    if theme == "purple":
        col_core = (255, 250, 255)
        col_hot = (245, 190, 255)
        col_bright = (205, 80, 255)
        col_mid = (145, 30, 220)
        col_dark = (65, 12, 115)
        col_deep = (28, 4, 52)
        col_gold = (255, 225, 170)
        col_glow = (195, 60, 255, 80)
    else: # red
        col_core = (255, 250, 245)
        col_hot = (255, 195, 175)
        col_bright = (255, 45, 70)
        col_mid = (210, 18, 45)
        col_dark = (105, 10, 24)
        col_deep = (42, 3, 10)
        col_gold = (255, 230, 160)
        col_glow = (255, 35, 65, 80)

    # -------------------------------------------------------------
    # 1. Background Radiant Glow & Floral Aura
    # -------------------------------------------------------------
    glow_img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow_img)
    
    # Soft circular bloom
    r_glow = 150.0 * scale
    for gr in range(int(r_glow), 10, -8):
        alpha = int(45 * (1.0 - gr / r_glow)**1.5)
        c = (col_bright[0], col_bright[1], col_bright[2], alpha)
        glow_draw.ellipse([s_center - gr, s_center - gr, s_center + gr, s_center + gr], fill=c)

    # -------------------------------------------------------------
    # 2. Hanging Silk Cord Tassels (Draping elegantly below the flower)
    # -------------------------------------------------------------
    # Tassels hang from the base of the flower (offset slightly down)
    tassel_origin_y = s_center + 55.0 * scale
    for side in [-1, 1]:
        # Smooth S-curve control points
        pts = [
            (s_center + side * 14.0 * scale, tassel_origin_y),
            (s_center + side * 36.0 * scale, tassel_origin_y + 40.0 * scale),
            (s_center + side * 22.0 * scale, tassel_origin_y + 90.0 * scale),
            (s_center + side * 32.0 * scale, tassel_origin_y + 145.0 * scale)
        ]
        
        # Evaluate smooth cubic Bezier
        steps = 60
        curve_pts = []
        for step in range(steps + 1):
            t = step / float(steps)
            x = (1-t)**3 * pts[0][0] + 3*(1-t)**2*t * pts[1][0] + 3*(1-t)*t**2 * pts[2][0] + t**3 * pts[3][0]
            y = (1-t)**3 * pts[0][1] + 3*(1-t)**2*t * pts[1][1] + 3*(1-t)*t**2 * pts[2][1] + t**3 * pts[3][1]
            curve_pts.append((x, y))

        # Cord shadow/deep line
        draw.line(curve_pts, fill=col_deep, width=int(6.5 * scale))
        # Cord main crimson/violet body
        draw.line(curve_pts, fill=col_mid, width=int(4.5 * scale))
        # Cord silk highlight
        draw.line(curve_pts, fill=col_hot, width=int(1.8 * scale))

        # Golden knot beads along cord
        bead_positions = [0.25, 0.65, 1.0]
        for bp in bead_positions:
            idx = int(bp * steps)
            bx, by = curve_pts[idx]
            br = (4.5 if bp < 1.0 else 6.0) * scale
            draw.ellipse([bx - br, by - br, bx + br, by + br], fill=col_gold, outline=col_deep, width=int(1.5 * scale))
            # Tiny shine
            draw.ellipse([bx - br*0.4, by - br*0.4, bx + br*0.2, by + br*0.2], fill=(255, 255, 255))

        # Tassel fringe brush at the end
        end_x, end_y = curve_pts[-1]
        for f_angle in np.linspace(-0.35, 0.35, 7):
            fx = end_x + math.sin(f_angle + side * 0.2) * 26.0 * scale
            fy = end_y + math.cos(f_angle) * 28.0 * scale
            draw.line([(end_x, end_y + 4.0 * scale), (fx, fy)], fill=col_bright, width=int(1.8 * scale))
            draw.line([(end_x, end_y + 4.0 * scale), (fx * 0.7 + end_x * 0.3, fy * 0.7 + end_y * 0.3)], fill=col_hot, width=int(1.2 * scale))

    # -------------------------------------------------------------
    # 3. 5 Flowery Petals (Organic Cherry Blossom / Plum Blossom)
    # -------------------------------------------------------------
    # Angles: 5 petals evenly spaced, top petal pointing straight up (-90 deg)
    num_petals = 5
    petal_angles = [-90 + i * (360 / num_petals) for i in range(num_petals)]
    # [-90, -18, 54, 126, 198]

    # Helper function to generate an organic, scalloped/curved flower petal polygon
    def get_petal_points(angle_deg, length, max_w, notch_depth=0.12):
        rad = math.radians(angle_deg)
        cos_a = math.cos(rad)
        sin_a = math.sin(rad)
        
        # Unit vectors: along petal axis (u), perpendicular (v)
        ux, uy = cos_a, sin_a
        vx, vy = -sin_a, cos_a
        
        # Generate outline using cubic bezier sides and notched/curved tip
        # Base: (0, 0)
        # Left side control points, tip, right side control points
        # Normalized t from 0 (base) to 1 (tip)
        pts_left = []
        pts_right = []
        
        subdiv = 24
        for i in range(subdiv + 1):
            t = i / float(subdiv)
            # Width profile: narrow at base (t=0), swell to max_w around t=0.65, soft rounded curve to tip
            # Shape function: sin(t * pi)^0.75 with asymmetric swelling
            w = max_w * math.sin(math.pi * (t ** 0.85))
            
            # Distance along length
            d = length * t
            
            # At the tip (t near 1.0), create a delicate floral petal curve with a gentle tip
            if t > 0.92:
                # Slight organic tip peak or gentle scallop
                w *= (1.0 - (t - 0.92) / 0.08) ** 0.5
            
            pt_l = (s_center + ux * d + vx * w, s_center + uy * d + vy * w)
            pt_r = (s_center + ux * d - vx * w, s_center + uy * d - vy * w)
            
            pts_left.append(pt_l)
            pts_right.append(pt_r)
            
        tip_pt = (s_center + ux * length, s_center + uy * length)
        # Combine: base -> left -> tip -> right reversed -> base
        return pts_left + [tip_pt] + list(reversed(pts_right))

    # Outer Layer Petals (Larger, translucent dark wine/crimson with bright neon rims)
    outer_len = 142.0 * scale
    outer_w = 46.0 * scale
    for angle_deg in petal_angles:
        poly = get_petal_points(angle_deg, outer_len, outer_w)
        # Draw outer petal body
        draw.polygon(poly, fill=col_dark, outline=col_mid, width=int(3.0 * scale))
        
        # Delicate glowing petal edge
        draw.line(poly, fill=col_bright, width=int(1.8 * scale))

    # Inner Layer Petals (Layered, vibrant crimson with satin ribbon sheen)
    inner_len = 120.0 * scale
    inner_w = 38.0 * scale
    for angle_deg in petal_angles:
        poly = get_petal_points(angle_deg, inner_len, inner_w)
        draw.polygon(poly, fill=col_mid, outline=col_hot, width=int(2.2 * scale))
        
        # Graceful center petal midrib line (curved vein)
        rad = math.radians(angle_deg)
        bx = s_center + math.cos(rad) * (20.0 * scale)
        by = s_center + math.sin(rad) * (20.0 * scale)
        tx = s_center + math.cos(rad) * (inner_len * 0.92)
        ty = s_center + math.sin(rad) * (inner_len * 0.92)
        draw.line([(bx, by), (tx, ty)], fill=col_hot, width=int(2.0 * scale))
        draw.line([(bx, by), (tx * 0.7 + bx * 0.3, ty * 0.7 + by * 0.3)], fill=col_core, width=int(1.2 * scale))

    # -------------------------------------------------------------
    # 4. Floral Stamens & Anthers (Spider Lily / Blossom Tendrils)
    # -------------------------------------------------------------
    # 5 long arching stamen filaments positioned between the petals
    for i in range(num_petals):
        stamen_angle = petal_angles[i] + (360 / (num_petals * 2)) # in between petals
        rad = math.radians(stamen_angle)
        
        # Curved filament arching outwards
        arch_len = 130.0 * scale
        ux, uy = math.cos(rad), math.sin(rad)
        vx, vy = -math.sin(rad), math.cos(rad)
        
        # Curve points
        p0 = (s_center + ux * (25.0 * scale), s_center + uy * (25.0 * scale))
        p1 = (s_center + ux * (75.0 * scale) + vx * (18.0 * scale), s_center + uy * (75.0 * scale) + vy * (18.0 * scale))
        p2 = (s_center + ux * (arch_len) + vx * (25.0 * scale), s_center + uy * (arch_len) + vy * (25.0 * scale))
        
        # Quadratic curve
        stamen_pts = []
        for step in range(30):
            t = step / 30.0
            x = (1-t)**2 * p0[0] + 2*(1-t)*t * p1[0] + t**2 * p2[0]
            y = (1-t)**2 * p0[1] + 2*(1-t)*t * p1[1] + t**2 * p2[1]
            stamen_pts.append((x, y))
            
        draw.line(stamen_pts, fill=col_hot, width=int(1.6 * scale))
        
        # Anther tip (golden radiant teardrop/oval pollen head)
        tip_x, tip_y = stamen_pts[-1]
        anther_r = 4.2 * scale
        draw.ellipse([tip_x - anther_r*1.3, tip_y - anther_r, tip_x + anther_r*1.3, tip_y + anther_r], fill=col_gold, outline=col_core, width=int(1.2 * scale))

    # -------------------------------------------------------------
    # 5. Interwoven Flower Knot Center (Mizuhiki Hanamusubi Weave)
    # -------------------------------------------------------------
    # A circular woven rosette at the heart of the petals
    center_ring_r = 38.0 * scale
    # Outer braided knot ring
    for angle_deg in range(0, 360, 45):
        rad = math.radians(angle_deg)
        kx = s_center + math.cos(rad) * (26.0 * scale)
        ky = s_center + math.sin(rad) * (26.0 * scale)
        kr = 16.0 * scale
        draw.ellipse([kx - kr, ky - kr, kx + kr, ky + kr], outline=col_deep, fill=col_dark, width=int(3.0 * scale))
        draw.ellipse([kx - kr + 2, ky - kr + 2, kx + kr - 2, ky + kr - 2], outline=col_bright, width=int(2.0 * scale))

    # -------------------------------------------------------------
    # 6. Radiant Blossom Jewel Heart (Faceted Core & Calyx)
    # -------------------------------------------------------------
    # Inner glowing flower pistil
    core_r = 22.0 * scale
    draw.ellipse([s_center - core_r, s_center - core_r, s_center + core_r, s_center + core_r], fill=col_deep, outline=col_gold, width=int(2.5 * scale))
    
    # 5-pointed mini star gem in the very center
    gem_pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = (core_r * 0.85) if (i % 2 == 0) else (core_r * 0.40)
        gem_pts.append((s_center + math.cos(a) * r, s_center + math.sin(a) * r))
    draw.polygon(gem_pts, fill=col_hot, outline=col_core, width=int(1.5 * scale))
    
    # Brilliant white-hot center sparkle
    sparkle_r = 6.0 * scale
    draw.ellipse([s_center - sparkle_r, s_center - sparkle_r, s_center + sparkle_r, s_center + sparkle_r], fill=col_core)

    # -------------------------------------------------------------
    # 7. Final Composite & Downsampling
    # -------------------------------------------------------------
    final_canvas = Image.alpha_composite(glow_img, img)
    # Downsample with Lanczos for smooth antialiasing
    final_icon = final_canvas.resize((size, size), Image.Resampling.LANCZOS)
    return final_icon

if __name__ == "__main__":
    purple_icon = create_flowery_knot("purple", 256)
    purple_icon.save(r"scratch\test_purple_flowery.png")
    red_icon = create_flowery_knot("red", 256)
    red_icon.save(r"scratch\test_red_flowery.png")
    print("Generated flowery test icons successfully!")
