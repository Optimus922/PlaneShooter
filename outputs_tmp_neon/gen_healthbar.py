"""
gen_healthbar.py — 科幻霓虹风血条贴图(track 轨道 + fill 填充)
风格与项目其它霓虹资源一致:暗底 + 青色霓虹边 + 外发光,4x 超采样抗锯齿。

产出(均为横向条状 512x96,RGBA 透明背景):
- hp_track.png : 血条外框/轨道。暗色圆角底 + 青色霓虹描边 + 外发光。
- hp_fill.png  : 血条填充。近白圆角条 + 顶部高光 + 纵向微渐变;
                 运行时由脚本按血量 tint(青绿→黄→红),并用 Image.Filled 横向裁切。

用法:python3 gen_healthbar.py  → 生成到当前目录,再拷进 Assets/Sprites/ui/。
要调形状/配色改这里重生成即可。
"""
from PIL import Image, ImageDraw, ImageFilter

SS = 4                      # 超采样倍数
W, H = 512, 96              # 目标尺寸(横向条)
sw, sh = W * SS, H * SS     # 超采样尺寸
RAD = sh // 2               # 圆角半径 = 半高 → 胶囊形


def blur_glow(shape_layer, blur, intensity, passes=2):
    """把发光形状层模糊叠加成外发光,返回累积发光层。"""
    acc = Image.new("RGBA", shape_layer.size, (0, 0, 0, 0))
    for i in range(passes):
        b = shape_layer.filter(ImageFilter.GaussianBlur(blur * (i + 1)))
        r, g, bl, a = b.split()
        a = a.point(lambda v: min(255, int(v * intensity)))
        acc = Image.alpha_composite(acc, Image.merge("RGBA", (r, g, bl, a)))
    return acc


def make_track():
    """暗底胶囊 + 青色霓虹边 + 外发光。"""
    base = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    d = ImageDraw.Draw(base)
    inset = 6 * SS
    box = [inset, inset, sw - inset, sh - inset]

    # 暗色半透明底
    d.rounded_rectangle(box, radius=RAD - inset, fill=(8, 12, 28, 220))

    # 青色霓虹描边(画在单独层,用于发光)
    edge = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    ed = ImageDraw.Draw(edge)
    ed.rounded_rectangle(box, radius=RAD - inset, outline=(0, 230, 255, 255),
                         width=4 * SS)
    glow = blur_glow(edge, blur=6 * SS, intensity=0.9, passes=3)

    out = Image.alpha_composite(base, glow)
    out = Image.alpha_composite(out, edge)
    return out.resize((W, H), Image.LANCZOS)


def make_fill():
    """近白圆角条 + 纵向渐变 + 顶部高光。运行时被 tint 成血色。"""
    inset = 12 * SS               # 比 track 内缩,留出边框可见
    box = [inset, inset, sw - inset, sh - inset]
    rad = (sh - 2 * inset) // 2

    # 形状蒙版
    mask = Image.new("L", (sw, sh), 0)
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=rad, fill=255)

    # 纵向渐变:顶部更亮,底部稍暗 → tint 后有圆柱体积感
    grad = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    y0, y1 = inset, sh - inset
    for y in range(y0, y1):
        t = (y - y0) / max(1, (y1 - y0))
        # 顶亮(255)→ 底(200),保留近白便于 tint
        v = int(255 - t * 55)
        gd.line([(0, y), (sw, y)], fill=(v, v, v, 255))

    fill = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    fill.paste(grad, (0, 0), mask)

    # 顶部高光条(更亮的窄带)
    gloss = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    gld = ImageDraw.Draw(gloss)
    gh = int((sh - 2 * inset) * 0.32)
    gld.rounded_rectangle([inset + 6 * SS, inset + 4 * SS,
                           sw - inset - 6 * SS, inset + gh],
                          radius=gh // 2, fill=(255, 255, 255, 90))
    gloss = gloss.filter(ImageFilter.GaussianBlur(3 * SS))
    g2 = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    g2.paste(gloss, (0, 0), mask)

    out = Image.alpha_composite(fill, g2)
    return out.resize((W, H), Image.LANCZOS)


if __name__ == "__main__":
    make_track().save("hp_track.png")
    make_fill().save("hp_fill.png")
    print("generated hp_track.png, hp_fill.png")
