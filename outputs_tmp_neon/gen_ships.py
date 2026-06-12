"""
gen_ships.py — 生成科幻霓虹风玩家机 / 敌机 (256x256, 透明背景)
玩家机:朝上,青/蓝霓虹,机体偏冷,引擎喷青色火焰。
敌机:朝下,品红/红霓虹,造型更尖锐凶狠,中心有红色能量核心。
"""
from PIL import Image, ImageDraw, ImageFilter
import neon_common as nc

SIZE = 256
S = SIZE * nc.SS  # 超采样尺寸


def _draw_body(poly_layers, glow_polys, glow_color, core=None, blur=14):
    """通用合成:glow_polys 发光轮廓 + poly_layers 实体填充图层 + 可选 core 光点。"""
    # 发光层
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for poly in glow_polys:
        gd.polygon(poly, fill=glow_color)
    base = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    for layer in poly_layers:
        base = Image.alpha_composite(base, layer)
    out = nc.add_glow(base, glow, blur=blur, intensity=1.3, passes=3)
    if core:
        cx, cy, r, col = core
        cl = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        cd = ImageDraw.Draw(cl)
        cd.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
        out = nc.add_glow(out, cl, blur=10, intensity=1.5, passes=3)
    return out


def make_player():
    cx = S // 2
    # 朝上的尖锐战机轮廓(顶点在上)
    body = [
        (cx, int(0.06 * S)),            # 机鼻
        (int(0.60 * S), int(0.42 * S)),
        (int(0.92 * S), int(0.74 * S)), # 右翼尖
        (int(0.66 * S), int(0.72 * S)),
        (cx, int(0.62 * S)),
        (int(0.34 * S), int(0.72 * S)),
        (int(0.08 * S), int(0.74 * S)), # 左翼尖
        (int(0.40 * S), int(0.42 * S)),
    ]
    # 机体渐变填充(深蓝 -> 青)
    fill = nc.vgrad_poly(S, body, (30, 70, 140, 255), (40, 200, 230, 255))
    # 描边发光青
    sd_layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sdd = ImageDraw.Draw(sd_layer)
    nc.stroke_poly(sdd, body, (120, 255, 255, 255), int(2.5 * nc.SS))
    # 座舱
    cockpit = [(cx, int(0.20 * S)), (int(0.565 * S), int(0.40 * S)),
               (cx, int(0.50 * S)), (int(0.435 * S), int(0.40 * S))]
    cd = ImageDraw.Draw(sd_layer)
    cd.polygon(cockpit, fill=(190, 255, 255, 230))
    # 引擎喷焰(向下)
    flame = [(int(0.46 * S), int(0.70 * S)), (cx, int(0.95 * S)),
             (int(0.54 * S), int(0.70 * S))]
    fl = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(fl).polygon(flame, fill=(120, 240, 255, 255))

    glow_polys = [body, flame]
    out = _draw_body([fill, sd_layer], glow_polys, (40, 220, 255, 255), blur=16)
    out = nc.add_glow(out, fl, blur=12, intensity=1.4, passes=3)
    out = nc.finalize(out, SIZE)
    out = nc.scanlines(out, gap=3, alpha=22)
    return out


def make_enemy():
    cx = S // 2
    # 朝下的凶狠 V 形战机(尖端在下)
    body = [
        (cx, int(0.94 * S)),            # 下方尖端
        (int(0.62 * S), int(0.58 * S)),
        (int(0.94 * S), int(0.26 * S)), # 右翼
        (int(0.70 * S), int(0.30 * S)),
        (cx, int(0.42 * S)),
        (int(0.30 * S), int(0.30 * S)),
        (int(0.06 * S), int(0.26 * S)), # 左翼
        (int(0.38 * S), int(0.58 * S)),
    ]
    fill = nc.vgrad_poly(S, body, (180, 30, 60, 255), (90, 10, 30, 255))
    sd_layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sdd = ImageDraw.Draw(sd_layer)
    nc.stroke_poly(sdd, body, (255, 120, 140, 255), int(2.5 * nc.SS))
    # 红色能量核心
    core = (cx, int(0.40 * S), int(0.085 * S), (255, 80, 60, 255))
    out = _draw_body([fill, sd_layer], [body], (255, 50, 80, 255), core=core, blur=16)
    out = nc.finalize(out, SIZE)
    out = nc.scanlines(out, gap=3, alpha=22)
    return out


if __name__ == "__main__":
    make_player().save("player_ship.png")
    make_enemy().save("enemy_ship.png")
    print("ships done")
