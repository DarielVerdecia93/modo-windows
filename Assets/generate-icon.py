# Genera Assets/AppIcon.ico. Uso: python Assets/generate-icon.py Assets/AppIcon.ico vista-previa.png (requiere Pillow)
import sys
from PIL import Image, ImageDraw, ImageChops

OUT_ICO, OUT_PREVIEW = sys.argv[1], sys.argv[2]
DAY = (255, 196, 46, 255)      # sol
NIGHT = (30, 42, 78, 255)      # noche
RING = (20, 20, 20, 90)        # contorno suave, visible sobre fondos claros
MOON = (245, 247, 255, 255)

def render(size):
    s = 1024
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    c, r = s / 2, s * 0.46

    disc = Image.new("L", (s, s), 0)
    ImageDraw.Draw(disc).ellipse((c - r, c - r, c + r, c + r), fill=255)

    # Mitad superior izquierda de día y mitad inferior derecha de noche (diagonal).
    halves = Image.new("RGBA", (s, s), NIGHT)
    ImageDraw.Draw(halves).polygon([(0, 0), (s, 0), (0, s)], fill=DAY)
    img.paste(halves, (0, 0), disc)

    d = ImageDraw.Draw(img)
    # Luna creciente en la mitad de noche
    mc, mr = (s * 0.635, s * 0.635), s * 0.15
    moon = Image.new("L", (s, s), 0)
    ImageDraw.Draw(moon).ellipse((mc[0] - mr, mc[1] - mr, mc[0] + mr, mc[1] + mr), fill=255)
    cut = Image.new("L", (s, s), 0)
    off = mr * 0.55
    ImageDraw.Draw(cut).ellipse((mc[0] - mr + off, mc[1] - mr - off * 0.6, mc[0] + mr + off, mc[1] + mr - off * 0.6), fill=255)
    moon = ImageChops.subtract(moon, cut)
    img.paste(Image.new("RGBA", (s, s), MOON), (0, 0), moon)

    # Rayos de sol en la mitad de día (solo se aprecian a partir de 32 px)
    sun_c, sun_r = (s * 0.355, s * 0.355), s * 0.11
    if size >= 32:
        d.ellipse((sun_c[0] - sun_r, sun_c[1] - sun_r, sun_c[0] + sun_r, sun_c[1] + sun_r), fill=(255, 255, 255, 235))

    ring_w = max(int(s * 0.008), int(s / size * 0.9))
    d.ellipse((c - r, c - r, c + r, c + r), outline=RING, width=ring_w)
    return img.resize((size, size), Image.LANCZOS)

sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
images = [render(n) for n in sizes]
images[-1].save(OUT_ICO, format="ICO", sizes=[(n, n) for n in sizes], append_images=images[:-1])

# Vista previa sobre fondo claro y oscuro
prev = Image.new("RGBA", (sum(sizes) + 12 * len(sizes), 2 * 270), (255, 255, 255, 255))
ImageDraw.Draw(prev).rectangle((0, 270, prev.width, prev.height), fill=(32, 32, 32, 255))
x = 6
for n, im in zip(sizes, images):
    prev.alpha_composite(im, (x, 135 - n // 2)); prev.alpha_composite(im, (x, 405 - n // 2)); x += n + 12
prev.save(OUT_PREVIEW)
