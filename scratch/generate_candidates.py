import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

# -------------------------------------------------------------------------
# Candidate A: High-Definition Enhanced In-Game HSR Crimson Knot
# -------------------------------------------------------------------------
def generate_candidate_A(theme="red", size=256):
    # Upscale and clean official icon with edge preservation & contrast enhancement
    im = Image.open('scratch/official_crimson_knot.png')
    
    # 4x upscale with Lanczos
    up = im.resize((size, size), Image.Resampling.LANCZOS)
    arr = np.array(up, dtype=np.float32)
    
    # Contrast stretch and edge sharpening on alpha
    alpha = arr[:, :, 3] / 255.0
    # Sigmoid contrast curve on alpha to make silhouette crisp and bold
    alpha_sharp = 1.0 / (1.0 + np.exp(-14.0 * (alpha - 0.45)))
    alpha_sharp = np.clip(alpha_sharp * 1.15, 0.0, 1.0) * 255.0
    
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    
    out_arr = np.zeros_like(arr)
    if theme == "purple":
        # Enhanced luminous electric purple/violet
        out_arr[:, :, 0] = np.clip(r * 1.05 + 15, 0, 255)
        out_arr[:, :, 1] = np.clip(g * 0.9, 0, 255)
        out_arr[:, :, 2] = np.clip(b * 1.15 + 25, 0, 255)
    else: # red
        # Searing Acheron crimson / scarlet
        luma = (r * 0.3 + g * 0.5 + b * 0.2) / 255.0
        out_arr[:, :, 0] = np.clip(b * 1.15 + r * 0.25 + 30, 0, 255) # Intense red
        out_arr[:, :, 1] = np.clip(g * 0.25, 0, 255)
        out_arr[:, :, 2] = np.clip(g * 0.35 + 10, 0, 255)
        # White hot highlights
        hot_mask = luma > 0.75
        out_arr[hot_mask, 0] = np.clip(out_arr[hot_mask, 0] + 40, 0, 255)
        out_arr[hot_mask, 1] = np.clip(out_arr[hot_mask, 1] + 120 * (luma[hot_mask] - 0.75) * 4, 0, 255)
        out_arr[hot_mask, 2] = np.clip(out_arr[hot_mask, 2] + 140 * (luma[hot_mask] - 0.75) * 4, 0, 255)

    out_arr[:, :, 3] = alpha_sharp
    result = Image.fromarray(out_arr.astype(np.uint8))
    
    # Unsharp mask for crisp anime vector look
    result_rgb = result.convert("RGB").filter(ImageFilter.UnsharpMask(radius=2, percent=160, threshold=3))
    r_im, g_im, b_im = result_rgb.split()
    final = Image.merge("RGBA", (r_im, g_im, b_im, Image.fromarray(alpha_sharp.astype(np.uint8))))
    return final

# -------------------------------------------------------------------------
# Candidate B: Japanese Hanamusubi Flower Crest (5-Lobed Ume-Musubi)
# Pronounced 5-fold flower silhouette, bold clean anime icon, zero glow
# -------------------------------------------------------------------------
def generate_candidate_B(theme="red", size=256):
    s_size = 512
    cx, cy = 256.0, 256.0
    img = Image.new("RGBA", (s_size, s_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if theme == "purple":
        c_fill = (145, 25, 220)
        c_dark = (55, 8, 95)
        c_rim = (225, 95, 255)
        c_hot = (255, 225, 255)
        c_bg_deep = (25, 3, 45)
    else: # red
        c_fill = (225, 20, 50)
        c_dark = (95, 8, 22)
        c_rim = (255, 110, 130)
        c_hot = (255, 235, 230)
        c_bg_deep = (40, 2, 10)

    # 5 Petal Ribbon Loops (angles: -90, -18, 54, 126, 198)
    num_petals = 5
    loop_dist = 110.0
    loop_r_out = 92.0
    loop_r_in = 52.0
    
    # 1. Outer dark contour backing for bold read
    for i in range(num_petals):
        angle = math.radians(-90 + i * (360 / num_petals))
        lx = cx + math.cos(angle) * loop_dist
        ly = cy + math.sin(angle) * loop_dist
        draw.ellipse([lx - loop_r_out - 6, ly - loop_r_out - 6, lx + loop_r_out + 6, ly + loop_r_out + 6], fill=c_bg_deep)

    # 2. Outer loop ribbon bodies with vibrant fill
    for i in range(num_petals):
        angle = math.radians(-90 + i * (360 / num_petals))
        lx = cx + math.cos(angle) * loop_dist
        ly = cy + math.sin(angle) * loop_dist
        draw.ellipse([lx - loop_r_out, ly - loop_r_out, lx + loop_r_out, ly + loop_r_out], fill=c_fill, outline=c_rim, width=7)

    # 3. Inner hollow cutouts (creating interlocking ribbon petals)
    for i in range(num_petals):
        angle = math.radians(-90 + i * (360 / num_petals))
        lx = cx + math.cos(angle) * loop_dist
        ly = cy + math.sin(angle) * loop_dist
        draw.ellipse([lx - loop_r_in, ly - loop_r_in, lx + loop_r_in, ly + loop_r_in], fill=(0, 0, 0, 0), outline=c_dark, width=6)

    # 4. Center flower knot rosette (woven pentagon / flower heart)
    center_r = 58.0
    draw.ellipse([cx - center_r, cy - center_r, cx + center_r, cy + center_r], fill=c_bg_deep, outline=c_rim, width=6)
    
    # 5-pointed floral star gem in center
    gem_pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = (center_r * 0.85) if (i % 2 == 0) else (center_r * 0.40)
        gem_pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    draw.polygon(gem_pts, fill=c_fill, outline=c_hot, width=4)
    draw.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=c_hot)

    final = img.resize((size, size), Image.Resampling.LANCZOS)
    return final

# -------------------------------------------------------------------------
# Candidate C: Stylized 5-Petal Spider Lily Blossom Crest (Clean Vector)
# Pronounced organic pointed flower star, sharp silhouette, deep clefts
# -------------------------------------------------------------------------
def generate_candidate_C(theme="red", size=256):
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

    # 5 Long, Elegant, Pointed Petals
    num_petals = 5
    petal_angles = [-90 + i * (360 / num_petals) for i in range(num_petals)]
    petal_len = 210.0
    petal_max_w = 46.0

    def make_clean_petal(angle_deg, length, max_w):
        rad = math.radians(angle_deg)
        cos_a, sin_a = math.cos(rad), math.sin(rad)
        ux, uy = cos_a, sin_a
        vx, vy = -sin_a, cos_a
        
        pts_l = []
        pts_r = []
        subdiv = 24
        for i in range(subdiv + 1):
            t = i / float(subdiv)
            # Tapered teardrop curve with pointed petal tip
            w = max_w * math.sin(math.pi * (t ** 0.60)) * (1.0 - t * 0.45)
            d = length * t
            pts_l.append((cx + ux * d + vx * w, cy + uy * d + vy * w))
            pts_r.append((cx + ux * d - vx * w, cy + uy * d - vy * w))
            
        tip = (cx + ux * length, cy + uy * length)
        return pts_l + [tip] + list(reversed(pts_r))

    # Outer border / shadow for high-contrast crisp readability
    for a in petal_angles:
        poly = make_clean_petal(a, petal_len + 4, petal_max_w + 5)
        draw.polygon(poly, fill=c_shadow)

    # Main vibrant petal body
    for a in petal_angles:
        poly = make_clean_petal(a, petal_len, petal_max_w)
        draw.polygon(poly, fill=c_dark, outline=c_fill, width=4)

    # Inner petal highlight & center vein
    for a in petal_angles:
        poly_inner = make_clean_petal(a, petal_len * 0.85, petal_max_w * 0.65)
        draw.polygon(poly_inner, fill=c_fill)
        
        # Center crisp luminous vein line
        rad = math.radians(a)
        bx = cx + math.cos(rad) * 22.0
        by = cy + math.sin(rad) * 22.0
        tx = cx + math.cos(rad) * (petal_len * 0.88)
        ty = cy + math.sin(rad) * (petal_len * 0.88)
        draw.line([(bx, by), (tx, ty)], fill=c_hot, width=3)
        draw.line([(bx, by), (tx * 0.5 + bx * 0.5, ty * 0.5 + by * 0.5)], fill=(255, 255, 255), width=2)

    # Center flower pistil (faceted calyx)
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
    import os
    artifact_dir = r"C:\Users\Shiraori\.gemini\antigravity\brain\aa0487bb-0dc4-44b4-af65-3cbcc51e1417"
    
    # Generate all candidate assets in both Red and Purple
    candA_red = generate_candidate_A("red", 256)
    candA_red.save(os.path.join(artifact_dir, "CrimsonKnot_CandidateA_Red.png"))
    candA_purple = generate_candidate_A("purple", 256)
    candA_purple.save(os.path.join(artifact_dir, "CrimsonKnot_CandidateA_Purple.png"))

    candB_red = generate_candidate_B("red", 256)
    candB_red.save(os.path.join(artifact_dir, "CrimsonKnot_CandidateB_Red.png"))
    candB_purple = generate_candidate_B("purple", 256)
    candB_purple.save(os.path.join(artifact_dir, "CrimsonKnot_CandidateB_Purple.png"))

    candC_red = generate_candidate_C("red", 256)
    candC_red.save(os.path.join(artifact_dir, "CrimsonKnot_CandidateC_Red.png"))
    candC_purple = generate_candidate_C("purple", 256)
    candC_purple.save(os.path.join(artifact_dir, "CrimsonKnot_CandidateC_Purple.png"))

    print("All candidates successfully generated into artifact directory!")
