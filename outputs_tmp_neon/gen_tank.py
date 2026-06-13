"""
gen_tank.py — 生成第一关 boss「坦克」的分件 sprite(霓虹军事风,透明背景)
俯视角(玩家从下往上看),军绿+青/红霓虹描边。分三件,便于在 Unity 里独立摆放/受击:
- tank_body      车体(履带 + 车身底盘,256x256)—— 不可受击,仅视觉
- tank_main_gun  主炮塔(较大,带长炮管,朝下指向玩家,128x128)—— 可受击,血50
- tank_sub_gun   副炮塔(较小,短炮管,128x128)—— 可受击,血20(用两次)
炮塔单独成图,锚点居中,Unity 里作为子物体叠在车体上对应位置。
"""
from PIL import Image, ImageDraw, ImageFilter
import neon_common as nc

S = nc.SS  # supersample factor shorthand


def _glow_compose(base, glow_layer, blur=14, intensity=1.3, passes=3):
    return nc.add_glow(base, glow_layer, blur=blur, intensity=intensity, passes=passes)


# ---------- 车体 ----------
def make_body(size=256):
    W = size * nc.SS
    cx = W // 2
    base = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)

    # 两条履带(左右),深色 + 青描边
    track_w = int(0.18 * W)
    track_top = int(0.12 * W)
    track_bot = int(0.88 * W)
    for side in (-1, 1):
        x_center = cx + side * int(0.34 * W)
        x0 = x_center - track_w // 2
        x1 = x_center + track_w // 2
        track = [(x0, track_top), (x1, track_top), (x1, track_bot), (x0, track_bot)]
        layer = nc.vgrad_poly(W, track, (28, 34, 30, 255), (16, 20, 18, 255))
        base = Image.alpha_composite(base, layer)
        sd = ImageDraw.Draw(base)
        nc.stroke_poly(sd, track, (60, 220, 200, 255), int(2.0 * nc.SS))
        gd.polygon(track, fill=(40, 180, 160, 120))
        # 履带横纹
        for ty in range(track_top, track_bot, int(0.06 * W)):
            sd.line([(x0, ty), (x1, ty)], fill=(70, 90, 80, 200), width=int(1.2 * nc.SS))

    # 车身底盘(中间,军绿渐变)
    body = [
        (int(0.24 * W), int(0.20 * W)),
        (int(0.76 * W), int(0.20 * W)),
        (int(0.80 * W), int(0.80 * W)),
        (int(0.20 * W), int(0.80 * W)),
    ]
    fill = nc.vgrad_poly(W, body, (70, 95, 55, 255), (45, 62, 38, 255))
    base = Image.alpha_composite(base, fill)
    sd = ImageDraw.Draw(base)
    nc.stroke_poly(sd, body, (120, 230, 160, 255), int(2.4 * nc.SS))
    gd.polygon(body, fill=(80, 200, 120, 110))

    # 装甲板纹理线
    sd.line([(int(0.30 * W), int(0.30 * W)), (int(0.70 * W), int(0.30 * W))],
            fill=(150, 200, 150, 120), width=int(1.5 * nc.SS))
    sd.line([(int(0.28 * W), int(0.70 * W)), (int(0.72 * W), int(0.70 * W))],
            fill=(150, 200, 150, 120), width=int(1.5 * nc.SS))

    out = _glow_compose(base, glow, blur=16, intensity=1.2, passes=3)
    out = nc.scanlines(out, gap=int(3 * nc.SS), alpha=22)
    return nc.finalize(out, size)


# ---------- 炮塔通用 ----------
def _make_turret(size, dome_r_frac, barrel_w_frac, barrel_len_frac,
                 dome_col_top, dome_col_bot, edge_col, glow_col, core_col):
    W = size * nc.SS
    cx = W // 2
    cy = int(0.34 * W)   # 圆顶中心上移,给炮管留出伸出空间
    base = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)

    r = int(dome_r_frac * W)

    # 炮管(朝下,指向玩家):从圆顶中心向下伸出,长度算到圆顶下沿之外
    bw = int(barrel_w_frac * W)
    blen = int(barrel_len_frac * W)
    barrel_top = cy
    barrel_bot = cy + r + blen
    barrel = [(cx - bw // 2, barrel_top), (cx + bw // 2, barrel_top),
              (cx + bw // 2, barrel_bot), (cx - bw // 2, barrel_bot)]
    bl = nc.vgrad_poly(W, barrel, (60, 70, 64, 255), (34, 42, 38, 255))
    base = Image.alpha_composite(base, bl)
    bd = ImageDraw.Draw(base)
    nc.stroke_poly(bd, barrel, edge_col, int(2.0 * nc.SS))
    gd.polygon(barrel, fill=glow_col)
    # 炮口环
    bd.ellipse([cx - bw, barrel_bot - bw // 2, cx + bw, barrel_bot + bw // 2],
               fill=(20, 24, 22, 255), outline=edge_col, width=int(2.0 * nc.SS))

    # 炮塔圆顶(画在炮管之上,盖住炮管根部)
    dome_box = [cx - r, cy - r, cx + r, cy + r]
    dl = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    dd = ImageDraw.Draw(dl)
    dd.ellipse(dome_box, fill=dome_col_bot)
    dd.ellipse([cx - r, cy - r, cx + r, cy + int(0.2 * r)], fill=dome_col_top)
    base = Image.alpha_composite(base, dl)
    bd = ImageDraw.Draw(base)
    bd.ellipse(dome_box, outline=edge_col, width=int(2.6 * nc.SS))
    gd.ellipse(dome_box, fill=glow_col)

    # 中心能量核心
    cr = int(0.28 * r)
    cl = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    cd = ImageDraw.Draw(cl)
    cd.ellipse([cx - cr, cy - cr, cx + cr, cy + cr], fill=core_col)
    base = nc.add_glow(base, cl, blur=8, intensity=1.6, passes=3)

    out = _glow_compose(base, glow, blur=12, intensity=1.4, passes=3)
    return nc.finalize(out, size)


def make_main_gun(size=128):
    # 主炮:大圆顶 + 长粗炮管,红色核心(显眼=主目标)
    return _make_turret(
        size, dome_r_frac=0.26, barrel_w_frac=0.13, barrel_len_frac=0.30,
        dome_col_top=(90, 120, 70, 255), dome_col_bot=(55, 75, 45, 255),
        edge_col=(255, 90, 90, 255), glow_col=(255, 60, 60, 130),
        core_col=(255, 120, 120, 255))


def make_sub_gun(size=128):
    # 副炮:小圆顶 + 短炮管,青色核心
    return _make_turret(
        size, dome_r_frac=0.20, barrel_w_frac=0.10, barrel_len_frac=0.24,
        dome_col_top=(80, 110, 90, 255), dome_col_bot=(48, 66, 54, 255),
        edge_col=(80, 230, 220, 255), glow_col=(60, 200, 200, 130),
        core_col=(150, 255, 255, 255))


if __name__ == "__main__":
    make_body(256).save("tank_body.png")
    make_main_gun(128).save("tank_main_gun.png")
    make_sub_gun(128).save("tank_sub_gun.png")
    print("saved tank_body.png, tank_main_gun.png, tank_sub_gun.png")
