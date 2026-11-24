import numpy as np
from PIL import Image, ImageDraw, ImageFilter

size = 64
img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

# Draw an elegant curved teardrop rose petal shape
# Tip at (48, 12), bulb at (24, 38), base at (16, 48)
points = [
    (18, 48),
    (14, 40),
    (16, 28),
    (24, 18),
    (36, 12),
    (48, 12),
    (46, 22),
    (40, 34),
    (32, 44),
    (22, 50),
    (18, 48)
]

# Draw petal body with vivid crimson-scarlet gradient
for r in range(40, 0, -1):
    alpha = int(255 * (1.0 - (r / 40.0) * 0.3))
    col = (int(255 - r * 1.5), int(20 + r * 0.8), int(35 + r * 1.2), alpha)
    draw.polygon(points, fill=col)

# Add a delicate bright inner highlight streak
highlight = [
    (26, 22),
    (34, 18),
    (42, 16),
    (36, 26),
    (30, 34)
]
draw.polygon(highlight, fill=(255, 160, 180, 180))

# Soft outer glow
glow = img.filter(ImageFilter.GaussianBlur(1.5))
final = Image.alpha_composite(glow, img)

final.save('Content/Visuals/DriftingPetal.png')
print('DriftingPetal.png generated successfully')
