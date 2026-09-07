from pathlib import Path
from PIL import Image, ImageDraw
import cairosvg

root = Path(__file__).resolve().parents[1]
svg = root / "assets" / "orvian.svg"
png = root / "assets" / "orvian.png"
ico = root / "assets" / "orvian.ico"

cairosvg.svg2png(url=str(svg), write_to=str(png), output_width=512, output_height=512)
img = Image.open(png).convert("RGBA")
sizes = [(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)]
images = [img.resize(s, Image.Resampling.LANCZOS) for s in sizes]
images[0].save(ico, format="ICO", sizes=sizes, append_images=images[1:])
print(f"Generated {ico}")
