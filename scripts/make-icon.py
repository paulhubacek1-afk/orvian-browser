from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
png = root / "assets" / "orvian.png"
ico = root / "assets" / "orvian.ico"

SIZE = 1024
img = Image.new("RGBA", (SIZE, SIZE), (10, 14, 28, 255))
p = img.load()
for y in range(SIZE):
    t = y / (SIZE - 1)
    r = int(9 + 35 * t)
    g = int(15 + 35 * t)
    b = int(35 + 95 * t)
    for x in range(SIZE):
        glow = max(0, 1 - (((x - 500) ** 2 + (y - 420) ** 2) ** 0.5) / 700)
        p[x, y] = (min(255, int(r + 25 * glow)), min(255, int(g + 35 * glow)), min(255, int(b + 65 * glow)), 255)

d = ImageDraw.Draw(img)
# Soft outer ring / orbital mark.
d.ellipse((170, 170, 854, 854), outline=(120, 220, 255, 230), width=42)
d.ellipse((250, 250, 774, 774), outline=(105, 130, 255, 210), width=28)
# Orvian "O" / eye-like center.
d.rounded_rectangle((315, 365, 709, 659), radius=147, outline=(245, 250, 255, 255), width=46)
d.ellipse((425, 415, 599, 589), fill=(35, 225, 255, 255))
d.ellipse((471, 461, 553, 543), fill=(15, 27, 55, 255))
# Small orbital nodes.
for cx, cy in [(235, 340), (789, 290), (758, 735), (270, 710)]:
    d.ellipse((cx - 20, cy - 20, cx + 20, cy + 20), fill=(255, 255, 255, 240))

img = img.resize((512, 512), Image.Resampling.LANCZOS)
img.save(png, format="PNG", optimize=True)
sizes = [(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256),(512,512)]
images = [img.resize(s, Image.Resampling.LANCZOS) for s in sizes]
images[-1].save(ico, format="ICO", sizes=sizes, append_images=images[:-1])
print(f"Generated {ico}")
