import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import distance_transform_edt, binary_fill_holes

def test_extract_official(theme="red", size=256):
    im = Image.open('scratch/official_crimson_knot.png')
    
    # 8x upscale
    up = im.resize((512, 512), Image.Resampling.LANCZOS)
    arr = np.array(up, dtype=np.float32)
    
    # Normalize channels
    r, g, b, a = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2], arr[:, :, 3]
    luma = (r * 0.299 + g * 0.587 + b * 0.114) / 255.0
    alpha_norm = a / 255.0
    
    # We want to extract:
    # 1. Main outer silhouette (remove the soft blurry glow fringe, keep the crisp flower shape)
    # 2. Inner petal layers
    # 3. Center pistil star
    
    # Glow threshold: outer blurry glow has low alpha and low luma
    # Core flower has alpha_norm > 0.40
    flower_mask = (alpha_norm > 0.38) & (luma > 0.15)
    
    # Clean distance transform for crisp vector-like multi-tone shading
    dist_inside = distance_transform_edt(flower_mask)
    dist_outside = distance_transform_edt(~flower_mask)
    
    # Outer dark shadow border (1-3 px in 512 space, so crisp outline in 256 space)
    border_mask = (dist_inside > 0) & (dist_inside <= 6.0)
    outer_body_mask = (dist_inside > 6.0) & (dist_inside <= 16.0)
    inner_body_mask = (dist_inside > 16.0)
    
    # Create clean image
    out = np.zeros((512, 512, 4), dtype=np.uint8)
    
    if theme == "purple":
        c_shadow = [24, 2, 45, 255]
        c_dark = [75, 10, 125, 255]
        c_mid = [165, 30, 235, 255]
        c_light = [215, 85, 255, 255]
        c_hot = [255, 220, 255, 255]
    else: # red
        c_shadow = [40, 2, 8, 255]
        c_dark = [115, 10, 26, 255]
        c_mid = [225, 24, 52, 255]
        c_light = [255, 75, 95, 255]
        c_hot = [255, 225, 220, 255]
        
    out[flower_mask] = c_shadow
    out[dist_inside > 4.0] = c_dark
    out[dist_inside > 12.0] = c_mid
    out[dist_inside > 22.0] = c_light
    
    # Center core highlight
    # Find center
    cy, cx = 256, 256
    y, x = np.ogrid[:512, :512]
    r_dist = np.sqrt((x - cx)**2 + (y - cy)**2)
    center_gem = (r_dist < 18.0) & flower_mask
    out[center_gem] = c_hot
    
    img = Image.fromarray(out)
    final = img.resize((size, size), Image.Resampling.LANCZOS)
    return final

if __name__ == "__main__":
    res = test_extract_official("red", 256)
    res.save("scratch/test_extracted_official.png")
    print("Extracted official knot test saved.")
