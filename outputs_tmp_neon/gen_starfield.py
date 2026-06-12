"""gen_starfield.py — 可纵向无缝平铺的科幻星空贴图(配合霓虹风)
bg_stars_far.png  1024x1024 深空底+星云+远景密集小星(不透明,作底)
bg_stars_near.png 1024x1024 稀疏明亮大星+偶发霓虹星(透明,叠上层快速滚动)
无缝:星点环绕补画;星云环绕模糊(拼3份模糊裁回中心)。
"""
from PIL import Image, ImageDraw, ImageFilter
import random

W = H = 1024


def wrap_points(n, rng):
    return [(rng.uniform(0, W), rng.uniform(0, H)) for _ in range(n)]


def draw_star(layer, x, y, r, color):
    d = ImageDraw.Draw(layer)
    for ox in (-W, 0, W):
        for oy in (-H, 0, H):
            px, py = x + ox, y + oy
            if -r * 3 <= px <= W + r * 3 and -r * 3 <= py <= H + r * 3:
                d.ellipse([px - r, py - r, px + r, py + r], fill=color)


def make_far():
    rng = random.Random(20260612)
    img = Image.new("RGBA", (W, H), (8, 12, 28, 255))
    neb = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    nd = ImageDraw.Draw(neb)
    clouds = [
        (300, 240, 300, (34, 78, 124, 16)),
        (760, 520, 320, (38, 82, 128, 15)),
        (480, 820, 300, (30, 74, 118, 15)),
        (150, 640, 300, (36, 80, 126, 14)),
    ]
    for cx, cy, cr, col in clouds:
        for ox in (-W, 0, W):
            for oy in (-H, 0, H):
                nd.ellipse([cx+ox-cr, cy+oy-cr, cx+ox+cr, cy+oy+cr], fill=col)
    tiled = Image.new("RGBA", (W*3, H*3), (0, 0, 0, 0))
    for i in range(3):
        for j in range(3):
            tiled.paste(neb, (i*W, j*H))
    tiled = tiled.filter(ImageFilter.GaussianBlur(90))
    neb = tiled.crop((W, H, W*2, H*2))
    img = Image.alpha_composite(img, neb)
    star_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for (x, y) in wrap_points(420, rng):
        r = rng.choice([0.6, 0.8, 1.0, 1.0, 1.4])
        b = rng.randint(120, 210)
        tint = rng.choice([(b, b, b), (b-20, b-10, b), (b, b-20, b-10)])
        draw_star(star_layer, x, y, r, tint + (rng.randint(120, 220),))
    img = Image.alpha_composite(img, star_layer)
    return img.convert("RGBA")


def make_near():
    rng = random.Random(99)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for (x, y) in wrap_points(70, rng):
        r = rng.uniform(1.6, 3.2)
        if rng.random() < 0.22:
            base = rng.choice([(60, 220, 255), (255, 70, 120), (180, 120, 255)])
        else:
            base = (255, 255, 255)
        draw_star(glow, x, y, r * 2.4, base + (90,))
        draw_star(layer, x, y, r, (255, 255, 255, 255))
    glow = glow.filter(ImageFilter.GaussianBlur(6))
    return Image.alpha_composite(glow, layer)


if __name__ == "__main__":
    make_far().save("bg_stars_far.png")
    make_near().save("bg_stars_near.png")
    print("starfield done")
