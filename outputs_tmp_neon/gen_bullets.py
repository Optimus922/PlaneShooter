"""
gen_bullets.py — 生成科幻霓虹风子弹
player_bullet: 64x128 竖直能量束(青色),核心白热 + 外层青色辉光。
enemy_bullet:  64x64 能量球(红/橙),核心白热 + 红色辉光。
"""
from PIL import Image, ImageDraw
import neon_common as nc


def make_player_bullet():
    W, H = 64, 128
    ss = nc.SS
    sw, sh = W * ss, H * ss
    cx = sw // 2

    # 发光层:一条胶囊状能量束
    glow = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    bw = int(0.30 * sw)
    gd.rounded_rectangle([cx - bw, int(0.10 * sh), cx + bw, int(0.90 * sh)],
                         radius=bw, fill=(40, 220, 255, 255))
    base = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    out = nc.add_glow(base, glow, blur=14, intensity=1.4, passes=3)

    # 核心白热束
    core = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    cw = int(0.13 * sw)
    ImageDraw.Draw(core).rounded_rectangle(
        [cx - cw, int(0.14 * sh), cx + cw, int(0.86 * sh)],
        radius=cw, fill=(235, 255, 255, 255))
    out = nc.add_glow(out, core, blur=5, intensity=1.5, passes=2)
    return out.resize((W, H), Image.LANCZOS)


def make_enemy_bullet():
    W = 64
    ss = nc.SS
    sw = W * ss
    c = sw // 2

    glow = Image.new("RGBA", (sw, sw), (0, 0, 0, 0))
    r = int(0.32 * sw)
    ImageDraw.Draw(glow).ellipse([c - r, c - r, c + r, c + r], fill=(255, 60, 50, 255))
    base = Image.new("RGBA", (sw, sw), (0, 0, 0, 0))
    out = nc.add_glow(base, glow, blur=12, intensity=1.5, passes=3)

    # 橙白核心
    core = Image.new("RGBA", (sw, sw), (0, 0, 0, 0))
    r2 = int(0.17 * sw)
    ImageDraw.Draw(core).ellipse([c - r2, c - r2, c + r2, c + r2], fill=(255, 230, 200, 255))
    out = nc.add_glow(out, core, blur=5, intensity=1.5, passes=2)
    return out.resize((W, W), Image.LANCZOS)


if __name__ == "__main__":
    make_player_bullet().save("player_bullet.png")
    make_enemy_bullet().save("enemy_bullet.png")
    print("bullets done")
