"""
gen_tank.py — 生成第一关 boss「坦克」的分件 sprite(写实军事 + 霓虹描边,透明背景)
俯视角(玩家从下往上看)。在 v1(几根线条)基础上大幅增加体积感与细节:
- 履带画成一节一节的踏板块(带高光/阴影),不再是空框 + 横线;
- 车体加多块装甲板分区 + 铆钉 + 斜面高光,有金属厚重感;
- 炮塔做圆柱体积的径向明暗 + 边缘高光 + 顶部舱盖细节;
- 炮管做圆筒立体(中间亮两侧暗)+ 炮口环。
分三件:tank_body(256) / tank_main_gun(128, 红, 血50) / tank_sub_gun(128, 青, 血20)。
炮塔单独成图、锚点居中,Unity 里作为子物体叠在车体上。
"""
from PIL import Image, ImageDraw, ImageFilter
import neon_common as nc

SS = nc.SS


def _glow(base, glow_layer, blur=14, intensity=1.3, passes=3):
    return nc.add_glow(base, glow_layer, blur=blur, intensity=intensity, passes=passes)


def _radial_shade(size, cx, cy, r, col_center, col_edge):
    """画一个有径向明暗的圆(中心亮、边缘暗),返回 RGBA 图层。模拟圆柱/球体积。"""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    steps = max(8, r // (2 * SS))
    for i in range(steps, 0, -1):
        t = i / steps               # 1=外圈 0=中心
        rr = int(r * t)
        col = nc.lerp(col_center, col_edge, t)
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=col)
    return layer


# ---------- 车体 ----------
def make_body(size=256):
    W = size * SS
    cx = W // 2
    base = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)

    # --- 两条履带:一节节踏板块 ---
    track_w = int(0.19 * W)
    track_top = int(0.10 * W)
    track_bot = int(0.90 * W)
    for side in (-1, 1):
        x_center = cx + side * int(0.35 * W)
        x0 = x_center - track_w // 2
        x1 = x_center + track_w // 2
        # 履带底槽(深色)
        d = ImageDraw.Draw(base)
        d.rounded_rectangle([x0, track_top, x1, track_bot],
                            radius=int(0.03 * W), fill=(20, 24, 21, 255))
        # 踏板块:逐节画,带上高光下阴影
        seg_h = int(0.058 * W)
        gap = int(0.012 * W)
        y = track_top + gap
        while y + seg_h < track_bot:
            d.rounded_rectangle([x0 + 3 * SS, y, x1 - 3 * SS, y + seg_h],
                                radius=int(0.012 * W), fill=(58, 66, 58, 255))
            # 顶部高光条
            d.line([(x0 + 5 * SS, y + 2 * SS), (x1 - 5 * SS, y + 2 * SS)],
                   fill=(110, 125, 105, 220), width=int(1.5 * SS))
            # 底部阴影
            d.line([(x0 + 5 * SS, y + seg_h - 2 * SS), (x1 - 5 * SS, y + seg_h - 2 * SS)],
                   fill=(12, 15, 13, 220), width=int(1.5 * SS))
            y += seg_h + gap
        # 履带外缘霓虹
        d.rounded_rectangle([x0, track_top, x1, track_bot],
                            radius=int(0.03 * W), outline=(60, 220, 200, 255),
                            width=int(2.2 * SS))
        gd.rounded_rectangle([x0, track_top, x1, track_bot],
                             radius=int(0.03 * W), fill=(40, 180, 160, 110))

    # --- 车身底盘:梯形 + 装甲分块 ---
    body = [
        (int(0.25 * W), int(0.18 * W)),
        (int(0.75 * W), int(0.18 * W)),
        (int(0.81 * W), int(0.82 * W)),
        (int(0.19 * W), int(0.82 * W)),
    ]
    fill = nc.vgrad_poly(W, body, (78, 102, 60, 255), (48, 66, 40, 255))
    base = Image.alpha_composite(base, fill)
    d = ImageDraw.Draw(base)

    # 中央装甲条带(更亮,做出斜面高光)
    cband = [(int(0.34 * W), int(0.20 * W)), (int(0.66 * W), int(0.20 * W)),
             (int(0.70 * W), int(0.80 * W)), (int(0.30 * W), int(0.80 * W))]
    cband_fill = nc.vgrad_poly(W, cband, (96, 124, 74, 255), (60, 82, 50, 255))
    base = Image.alpha_composite(base, cband_fill)
    d = ImageDraw.Draw(base)

    # 装甲分块横缝
    for yf in (0.34, 0.5, 0.66):
        yy = int(yf * W)
        x_l = int((0.26 + (yf - 0.18) * 0.04) * W)
        x_r = W - x_l
        d.line([(x_l, yy), (x_r, yy)], fill=(30, 40, 26, 230), width=int(1.6 * SS))
        d.line([(x_l, yy + int(1.6 * SS)), (x_r, yy + int(1.6 * SS))],
               fill=(120, 150, 95, 120), width=int(1.0 * SS))

    # 铆钉(四角 + 中线)
    rivet_r = int(0.012 * W)
    for (rx, ry) in [(0.31, 0.24), (0.69, 0.24), (0.32, 0.76), (0.68, 0.76),
                     (0.31, 0.5), (0.69, 0.5)]:
        cxr, cyr = int(rx * W), int(ry * W)
        d.ellipse([cxr - rivet_r, cyr - rivet_r, cxr + rivet_r, cyr + rivet_r],
                  fill=(150, 175, 120, 255), outline=(30, 40, 26, 255), width=int(1 * SS))

    # 车体外缘霓虹描边
    nc.stroke_poly(d, body, (120, 230, 160, 255), int(2.6 * SS))
    gd.polygon(body, fill=(80, 200, 120, 100))

    out = _glow(base, glow, blur=16, intensity=1.15, passes=3)
    out = nc.scanlines(out, gap=int(4 * SS), alpha=16)
    return nc.finalize(out, size)


# ---------- 炮塔通用 ----------
def _hexagon(cx, cy, r):
    """返回一个略扁的六边形顶点(俯视装甲炮塔轮廓),尖角朝下(炮口方向)。"""
    import math
    pts = []
    # 6 边,稍微压扁(y 方向 0.92),旋转使平边朝上下
    for k in range(6):
        ang = math.radians(60 * k - 90)   # -90 使顶点朝上下
        pts.append((cx + r * math.cos(ang), cy + r * 0.92 * math.sin(ang)))
    return pts


def _make_turret(size, dome_r_frac, barrel_w_frac, barrel_len_frac,
                 dome_center, dome_edge, edge_col, glow_col, core_col, hatch=True):
    W = size * SS
    cx = W // 2
    cy = int(0.40 * W)
    base = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    glow = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)

    r = int(dome_r_frac * W)
    bw = int(barrel_w_frac * W)
    blen = int(barrel_len_frac * W)
    barrel_top = cy                       # 炮管从炮塔中心下方伸出
    barrel_bot = cy + int(r * 0.9) + blen

    # --- 炮管:圆筒立体(中间亮两侧暗) ---
    bl = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bl)
    for i in range(bw):
        t = abs(i - bw / 2) / (bw / 2)
        col = nc.lerp((130, 140, 130, 255), (30, 36, 32, 255), t)
        x = cx - bw // 2 + i
        bd.line([(x, barrel_top), (x, barrel_bot)], fill=col)
    base = Image.alpha_composite(base, bl)
    bd = ImageDraw.Draw(base)
    bd.rectangle([cx - bw // 2, barrel_top, cx + bw // 2, barrel_bot],
                 outline=edge_col, width=int(1.8 * SS))
    gd.rectangle([cx - bw // 2, barrel_top, cx + bw // 2, barrel_bot], fill=glow_col)
    # 炮口环
    muzzle_r = int(bw * 0.62)
    bd.ellipse([cx - muzzle_r, barrel_bot - muzzle_r // 2,
                cx + muzzle_r, barrel_bot + muzzle_r // 2],
               fill=(18, 22, 20, 255), outline=edge_col, width=int(2.2 * SS))

    # --- 炮盾:炮管根部一块梯形装甲,过渡到炮塔 ---
    mant_w = int(bw * 1.9)
    mant_top = cy + int(r * 0.45)
    mant_bot = cy + int(r * 0.95)
    mantlet = [(cx - mant_w // 2, mant_top), (cx + mant_w // 2, mant_top),
               (cx + int(mant_w * 0.4), mant_bot), (cx - int(mant_w * 0.4), mant_bot)]
    mf = nc.vgrad_poly(W, mantlet, (90, 116, 72, 255), (52, 70, 44, 255))
    base = Image.alpha_composite(base, mf)
    bd = ImageDraw.Draw(base)
    nc.stroke_poly(bd, mantlet, edge_col, int(1.6 * SS))

    # --- 炮塔本体:六边形装甲(径向明暗叠在六边形里) ---
    hexpts = _hexagon(cx, cy, r)
    # 先用径向明暗圆做体积,再用六边形 mask 裁切
    shade = _radial_shade(W, cx, cy, r, dome_center, dome_edge)
    mask = Image.new("L", (W, W), 0)
    ImageDraw.Draw(mask).polygon(hexpts, fill=255)
    hexlayer = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    hexlayer.paste(shade, (0, 0), mask)
    base = Image.alpha_composite(base, hexlayer)
    bd = ImageDraw.Draw(base)
    # 六边形霓虹描边
    nc.stroke_poly(bd, [(int(x), int(y)) for x, y in hexpts], edge_col, int(2.8 * SS))
    gd.polygon(hexpts, fill=glow_col)
    # 顶部装甲高光边
    bd.line([(int(hexpts[4][0]), int(hexpts[4][1])),
             (int(hexpts[5][0]), int(hexpts[5][1]))],
            fill=(220, 235, 210, 170), width=int(2.2 * SS))

    # 舱盖圈
    if hatch:
        hr = int(0.40 * r)
        bd.ellipse([cx - hr, cy - hr, cx + hr, cy + hr],
                   outline=(28, 38, 26, 220), width=int(1.6 * SS))

    # 中心能量核心(发光)
    cr = int(0.24 * r)
    cl = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(cl).ellipse([cx - cr, cy - cr, cx + cr, cy + cr], fill=core_col)
    base = nc.add_glow(base, cl, blur=8, intensity=1.7, passes=3)

    out = _glow(base, glow, blur=12, intensity=1.4, passes=3)
    return nc.finalize(out, size)


def make_main_gun(size=128):
    # 主炮:大六边形炮塔 + 粗炮管,红色核心(显眼=主目标)
    return _make_turret(
        size, dome_r_frac=0.30, barrel_w_frac=0.16, barrel_len_frac=0.30,
        dome_center=(110, 140, 84, 255), dome_edge=(48, 66, 40, 255),
        edge_col=(255, 90, 90, 255), glow_col=(255, 60, 60, 130),
        core_col=(255, 120, 120, 255))


def make_sub_gun(size=128):
    # 副炮:小六边形炮塔 + 短炮管,青色核心
    return _make_turret(
        size, dome_r_frac=0.24, barrel_w_frac=0.12, barrel_len_frac=0.26,
        dome_center=(96, 126, 104, 255), dome_edge=(44, 62, 50, 255),
        edge_col=(80, 230, 220, 255), glow_col=(60, 200, 200, 130),
        core_col=(150, 255, 255, 255))


if __name__ == "__main__":
    make_body(256).save("tank_body.png")
    make_main_gun(128).save("tank_main_gun.png")
    make_sub_gun(128).save("tank_sub_gun.png")
    print("saved tank_body.png, tank_main_gun.png, tank_sub_gun.png")
