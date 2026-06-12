"""
neon_common.py — 科幻霓虹风 sprite 生成公用工具
核心思路:
- 所有图形先在 SS=4 倍超采样画布上绘制,最后 LANCZOS 缩小 -> 边缘干净抗锯齿。
- 辉光 = 把发光形状画到单独图层,多次高斯模糊叠加再合成,营造霓虹外发光。
- 透明背景(RGBA)。
"""
from PIL import Image, ImageDraw, ImageFilter
import math

SS = 4  # supersample factor


def new_canvas(size):
    """返回 (超采样画布 Image, draw, 超采样尺寸)。"""
    w = size * SS
    img = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img), w


def finalize(img, size):
    """超采样画布缩回目标尺寸。"""
    return img.resize((size, size), Image.LANCZOS)


def add_glow(base, glow_layer, blur, intensity=1.0, passes=2):
    """把 glow_layer 模糊后叠到 base 下层,形成外发光。base/glow_layer 同尺寸 RGBA。"""
    acc = Image.new("RGBA", base.size, (0, 0, 0, 0))
    for i in range(passes):
        b = glow_layer.filter(ImageFilter.GaussianBlur(blur * (i + 1)))
        if intensity != 1.0:
            r, g, bl, a = b.split()
            a = a.point(lambda v: min(255, int(v * intensity)))
            b = Image.merge("RGBA", (r, g, bl, a))
        acc = Image.alpha_composite(acc, b)
    return Image.alpha_composite(acc, base)


def lerp(c1, c2, t):
    return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(len(c1)))


def vgrad_poly(draw_size, poly, top_color, bottom_color):
    """在一张新图层上,用上下垂直渐变填充一个多边形,返回该图层。"""
    layer = Image.new("RGBA", (draw_size, draw_size), (0, 0, 0, 0))
    grad = Image.new("RGBA", (draw_size, draw_size), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    ys = [p[1] for p in poly]
    y0, y1 = min(ys), max(ys)
    span = max(1, y1 - y0)
    for y in range(int(y0), int(y1) + 1):
        t = (y - y0) / span
        gd.line([(0, y), (draw_size, y)], fill=lerp(top_color, bottom_color, t))
    mask = Image.new("L", (draw_size, draw_size), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    layer.paste(grad, (0, 0), mask)
    return layer


def stroke_poly(draw, poly, color, width):
    """给多边形描边(闭合)。"""
    pts = poly + [poly[0]]
    draw.line(pts, fill=color, width=width, joint="curve")


def scanlines(img, gap, alpha=40):
    """叠加细微科技扫描线,增强科幻感。"""
    w, h = img.size
    ov = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for y in range(0, h, gap):
        d.line([(0, y), (w, y)], fill=(255, 255, 255, alpha))
    # 仅在不透明像素上保留扫描线
    r, g, b, a = img.split()
    ov.putalpha(Image.composite(ov.split()[3], Image.new("L", (w, h), 0), a))
    return Image.alpha_composite(img, ov)
