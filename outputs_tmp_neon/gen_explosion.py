"""gen_explosion.py — 程序化霓虹爆炸序列帧(8 帧, 128x128, 透明)
演变:核心白热闪光 -> 膨胀青/橙火球 -> 碎片向外扩散 -> 辉光消散。
配色与霓虹美术统一(青 + 橙 + 白热核心)。4x 超采样,辉光用高斯模糊。
"""
from PIL import Image, ImageDraw, ImageFilter
import math, random

SIZE = 128
SS = 4
S = SIZE * SS
N = 8  # 帧数


def frame(i):
    t = i / (N - 1)          # 0..1 进度
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    dd = ImageDraw.Draw(img)
    c = S / 2
    rng = random.Random(100 + i)

    # 整体不透明度:后段渐隐
    fade = 1.0 if t < 0.5 else max(0.0, 1.0 - (t - 0.5) / 0.5)

    # 1) 火球半径:快速膨胀后趋于稳定
    ball_r = (0.12 + 0.42 * math.sqrt(t)) * S
    # 火球颜色:前期偏白热/橙,后期偏青并变淡
    if t < 0.35:
        ball_col = (255, 220, 140)
    else:
        ball_col = (90, 210, 255)
    a_ball = int(150 * fade)
    if a_ball > 0:
        gd.ellipse([c-ball_r, c-ball_r, c+ball_r, c+ball_r],
                   fill=ball_col + (a_ball,))

    # 2) 白热核心:仅前段,快速收缩
    if t < 0.5:
        core_r = (0.22 - 0.30 * t) * S
        core_r = max(core_r, 0.02 * S)
        dd.ellipse([c-core_r, c-core_r, c+core_r, c+core_r],
                   fill=(255, 255, 255, int(255 * (1 - t * 1.6))))

    # 3) 碎片:从中心向外飞,带拖尾感(小圆点)
    n_frag = 10
    spread = (0.15 + 0.7 * t) * S
    for k in range(n_frag):
        ang = (k / n_frag) * 2 * math.pi + rng.uniform(-0.2, 0.2)
        dist = spread * rng.uniform(0.6, 1.0)
        fx = c + math.cos(ang) * dist
        fy = c + math.sin(ang) * dist
        fr = max(1, (0.05 - 0.03 * t) * S)
        col = (255, 180, 90) if rng.random() < 0.5 else (120, 230, 255)
        a = int(220 * fade)
        if a > 0:
            gd.ellipse([fx-fr, fy-fr, fx+fr, fy+fr], fill=col + (a,))

    glow = glow.filter(ImageFilter.GaussianBlur(SS * 3))
    out = Image.alpha_composite(glow, img)
    return out.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    frames = [frame(i) for i in range(N)]
    for i, fr in enumerate(frames):
        fr.save(f"explosion_{i}.png")
    # 预览大图(深底横排)
    pad = 8
    prev = Image.new("RGB", (N*SIZE + (N+1)*pad, SIZE + 2*pad), (10, 12, 26))
    for i, fr in enumerate(frames):
        prev.paste(fr, (pad + i*(SIZE+pad), pad), fr)
    prev.save("explosion_preview.png")
    print("explosion frames done:", N)


if __name__ == "__main__":
    main()
