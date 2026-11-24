import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import math

def create_knot_icon(theme="purple", size=256):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    center = size / 2.0
    
    # We will work at 2x resolution (512x512) for supreme supersampled antialiasing
    scale = 2
    s_size = size * scale
    s_center = s_size / 2.0
    
    # Canvas
    s_img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(s_img)
    
    # Colors setup
    if theme == "purple":
        col_core = (255, 245, 255)
        col_hot = (235, 170, 255)
        col_bright = (195, 70, 255)
        col_mid = (135, 25, 210)
        col_dark = (55, 10, 100)
        col_deep = (25, 4, 45)
        col_rim = (205, 90, 255, 180)
        glow_color = (185, 55, 255)
    else: # red
        col_core = (255, 250, 245)
        col_hot = (255, 180, 160)
        col_bright = (255, 45, 65)
        col_mid = (200, 18, 40)
        col_dark = (90, 8, 20)
        col_deep = (35, 3, 8)
        col_rim = (255, 95, 115, 190)
        glow_color = (255, 30, 55)

    # 1. Draw outer 4 diagonal diamond petal blades (45, 135, 225, 315 deg)
    diag_pts_list = []
    diag_len = 160.0 * scale
    diag_width = 38.0 * scale
    for angle_deg in [45, 135, 225, 315]:
        rad = math.radians(angle_deg)
        cos_a = math.cos(rad)
        sin_a = math.sin(rad)
        
        tip = (s_center + cos_a * diag_len, s_center + sin_a * diag_len)
        side1 = (s_center + cos_a * (diag_len * 0.45) - sin_a * diag_width, s_center + sin_a * (diag_len * 0.45) + cos_a * diag_width)
        side2 = (s_center + cos_a * (diag_len * 0.45) + sin_a * diag_width, s_center + sin_a * (diag_len * 0.45) - cos_a * diag_width)
        base = (s_center + cos_a * (diag_len * 0.15), s_center + sin_a * (diag_len * 0.15))
        
        diag_poly = [base, side1, tip, side2]
        diag_pts_list.append(diag_poly)
        
        # Draw blade outline & body
        draw.polygon(diag_poly, fill=col_dark, outline=col_bright, width=int(2.5 * scale))
        
        # Center crest ridge along blade
        draw.line([base, tip], fill=col_hot, width=int(2 * scale))

    # 2. Draw 4 interlocking Mizuhiki / Hanamusubi knot loops (0, 90, 180, 270 deg)
    loop_radius = 62.0 * scale
    loop_dist = 68.0 * scale
    for angle_deg in [0, 90, 180, 270]:
        rad = math.radians(angle_deg)
        lx = s_center + math.cos(rad) * loop_dist
        ly = s_center + math.sin(rad) * loop_dist
        
        # Outer loop
        r_out = loop_radius
        draw.ellipse([lx - r_out, ly - r_out, lx + r_out, ly + r_out], outline=col_bright, width=int(6.5 * scale), fill=col_mid)
        # Inner cutout of loop (creating a true hollow ribbon loop)
        r_in = loop_radius - 16.0 * scale
        draw.ellipse([lx - r_in, ly - r_in, lx + r_in, ly + r_in], outline=col_deep, width=int(3.0 * scale), fill=col_deep)
        
        # Gilded rim accent on loop
        draw.ellipse([lx - r_out + 2, ly - r_out + 2, lx + r_out - 2, ly + r_out - 2], outline=col_hot, width=int(1.5 * scale))

    # 3. Hanging cord tassels from bottom loop (hanging down-left and down-right)
    tassel_base_y = s_center + loop_dist + loop_radius * 0.5
    for sign in [-1, 1]:
        ctrl_pts = [
            (s_center + sign * 18.0 * scale, tassel_base_y - 10 * scale),
            (s_center + sign * 36.0 * scale, tassel_base_y + 40 * scale),
            (s_center + sign * 28.0 * scale, tassel_base_y + 85 * scale),
            (s_center + sign * 34.0 * scale, tassel_base_y + 115 * scale)
        ]
        # Draw curve
        for t_step in range(40):
            t0 = t_step / 40.0
            t1 = (t_step + 1) / 40.0
            # Cubic bezier
            def bezier(t, p0, p1, p2, p3):
                return (1-t)**3*p0 + 3*(1-t)**2*t*p1 + 3*(1-t)*t**2*p2 + t**3*p3
            x0 = bezier(t0, ctrl_pts[0][0], ctrl_pts[1][0], ctrl_pts[2][0], ctrl_pts[3][0])
            y0 = bezier(t0, ctrl_pts[0][1], ctrl_pts[1][1], ctrl_pts[2][1], ctrl_pts[3][1])
            x1 = bezier(t1, ctrl_pts[0][0], ctrl_pts[1][0], ctrl_pts[2][0], ctrl_pts[3][0])
            y1 = bezier(t1, ctrl_pts[0][1], ctrl_pts[1][1], ctrl_pts[2][1], ctrl_pts[3][1])
            draw.line([(x0, y0), (x1, y1)], fill=col_bright, width=int(4 * scale))
            draw.line([(x0, y0), (x1, y1)], fill=col_hot, width=int(1.5 * scale))
        
        # Tassel end bead
        end_pt = ctrl_pts[3]
        bead_r = 6.0 * scale
        draw.ellipse([end_pt[0] - bead_r, end_pt[1] - bead_r, end_pt[0] + bead_r, end_pt[1] + bead_r], fill=col_core, outline=col_mid, width=int(1.5 * scale))

    # 4. Central Talismanic Rhombus / Diamond Crest
    rhombus_r = 46.0 * scale
    r_pts = [
        (s_center, s_center - rhombus_r * 1.25),
        (s_center + rhombus_r * 1.25, s_center),
        (s_center, s_center + rhombus_r * 1.25),
        (s_center - rhombus_r * 1.25, s_center)
    ]
    draw.polygon(r_pts, fill=col_deep, outline=col_bright, width=int(5 * scale))
    
    # Inner gold border
    r_pts_mid = [
        (s_center, s_center - rhombus_r * 1.05),
        (s_center + rhombus_r * 1.05, s_center),
        (s_center, s_center + rhombus_r * 1.05),
        (s_center - rhombus_r * 1.05, s_center)
    ]
    draw.polygon(r_pts_mid, outline=col_hot, width=int(2 * scale))

    # 5. Glowing Central Faceted Jewel Core
    gem_r = 28.0 * scale
    g_pts = [
        (s_center, s_center - gem_r),
        (s_center + gem_r, s_center),
        (s_center, s_center + gem_r),
        (s_center - gem_r, s_center)
    ]
    draw.polygon(g_pts, fill=col_bright, outline=col_core, width=int(3 * scale))
    
    # Gem facet facets
    for pt in g_pts:
        draw.line([pt, (s_center, s_center)], fill=col_core, width=int(2 * scale))
        
    # Hot center gleam
    gleam_r = 10.0 * scale
    draw.ellipse([s_center - gleam_r, s_center - gleam_r, s_center + gleam_r, s_center + gleam_r], fill=col_core)

    # 6. Generate outer bloom aura
    alpha_layer = s_img.getchannel("A")
    glow_mask = alpha_layer.filter(ImageFilter.GaussianBlur(radius=8 * scale))
    glow_img = Image.new("RGBA", (s_size, s_size), (*glow_color, 0))
    glow_img.putalpha(glow_mask)

    # Composite: glow layer under s_img
    final_high = Image.alpha_composite(glow_img, s_img)
    
    # Resample down to 256x256 with Lanczos for perfect smoothness
    final_img = final_high.resize((size, size), Image.Resampling.LANCZOS)
    return final_img

# Generate Purple and Red icons
purple_icon = create_knot_icon("purple")
purple_icon.save("Content/Projectiles/CrimsonKnotPurple.png")
print("Saved Content/Projectiles/CrimsonKnotPurple.png")

red_icon = create_knot_icon("red")
red_icon.save("Content/Projectiles/CrimsonKnotRed.png")
print("Saved Content/Projectiles/CrimsonKnotRed.png")
