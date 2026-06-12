"""gen_player_bank.py — 由玩家机生成 5 档倾斜帧(模拟机身滚转)
帧索引 0..4:大左倾 / 左倾 / 正中 / 右倾 / 大右倾
原理:旋转(机鼻向转弯方向偏)+ 横向压缩(翼展透视收缩),两者叠加 -> 真实的"压坡度"姿态,左右清晰不对称。
输出:player_bank_0..4.png (256x256, 透明) + player_bank_sheet.png
"""
from PIL import Image
import gen_ships

SIZE = 256


def bank_frame(base, amount):
    """amount: -1..+1。负=左倾(机鼻偏左),正=右倾。"""
    if abs(amount) < 1e-3:
        return base.copy()
    a = abs(amount)
    # 1) 旋转:机鼻向转弯方向倾(右倾时顺时针,PIL 正角=逆时针,故取负)
    angle = -amount * 16.0  # 最大 16°
    rot = base.rotate(angle, resample=Image.BICUBIC, center=(SIZE/2, SIZE/2),
                      expand=False)
    # 2) 横向压缩:翼展因滚转而透视收缩
    squash = 1.0 - 0.34 * a
    new_w = max(1, int(SIZE * squash))
    comp = rot.resize((new_w, SIZE), Image.LANCZOS)
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.paste(comp, ((SIZE - new_w)//2, 0), comp)
    return out


def main():
    base = gen_ships.make_player()
    amounts = [-1.0, -0.5, 0.0, 0.5, 1.0]
    frames = [bank_frame(base, amt) for amt in amounts]
    for i, fr in enumerate(frames):
        fr.save(f"player_bank_{i}.png")
    sheet = Image.new("RGBA", (SIZE*len(frames), SIZE), (0, 0, 0, 0))
    for i, fr in enumerate(frames):
        sheet.paste(fr, (i*SIZE, 0))
    sheet.save("player_bank_sheet.png")
    print("bank frames done:", len(frames))


if __name__ == "__main__":
    main()
