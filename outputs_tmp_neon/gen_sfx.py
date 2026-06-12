"""gen_sfx.py — 程序化合成街机/霓虹风音效(无需素材)。输出 16-bit 单声道 44.1kHz WAV。
sfx_shoot   激光射击:频率下扫的方波+快速衰减
sfx_hit     命中:短促高频哔
sfx_explore 爆炸:白噪声+低频隆隆,长衰减
sfx_hurt    玩家受伤:下行双音
sfx_gameover 游戏结束:下行小调音阶
"""
import numpy as np, wave, struct

SR = 44100


def env(n, attack=0.005, decay=0.0, sustain=1.0, release=0.05, total=None):
    """简单 AD/ASR 包络,长度 n 采样。"""
    e = np.ones(n)
    a = int(attack * SR)
    r = int(release * SR)
    if a > 0:
        e[:a] = np.linspace(0, 1, a)
    if r > 0:
        e[-r:] = np.linspace(1, 0, r)
    return e


def square(freq, n):
    t = np.arange(n) / SR
    return np.sign(np.sin(2 * np.pi * freq * t))


def saw(freq, n):
    t = np.arange(n) / SR
    return 2 * (t * freq - np.floor(0.5 + t * freq))


def sine(freq, n):
    t = np.arange(n) / SR
    return np.sin(2 * np.pi * freq * t)


def sweep(f0, f1, n, kind="square"):
    t = np.arange(n) / SR
    dur = n / SR
    phase = 2 * np.pi * (f0 * t + (f1 - f0) / (2 * dur) * t**2)
    s = np.sin(phase)
    return np.sign(s) if kind == "square" else s


def save(name, data):
    data = np.clip(data, -1, 1)
    pcm = (data * 32767).astype(np.int16)
    with wave.open(name, "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(name, f"{len(data)/SR:.2f}s")


def shoot():
    n = int(0.18 * SR)
    s = sweep(1200, 300, n, "square") * 0.5
    s += sweep(1800, 500, n, "saw" if False else "square") * 0.2
    e = np.exp(-np.linspace(0, 6, n))
    return s * e


def hit():
    n = int(0.08 * SR)
    s = square(1600, n) * 0.4 + sine(2400, n) * 0.3
    e = np.exp(-np.linspace(0, 9, n))
    return s * e


def explore():
    n = int(0.6 * SR)
    noise = np.random.uniform(-1, 1, n)
    low = sine(70, n) * 0.6 + sine(45, n) * 0.4
    e = np.exp(-np.linspace(0, 5, n))
    s = (noise * 0.6 + low) * e
    # 简单低通(滑动平均)让爆炸更"闷"
    k = 8
    s = np.convolve(s, np.ones(k)/k, mode="same")
    return s * 0.9


def hurt():
    n = int(0.3 * SR)
    half = n // 2
    a = square(440, half) * np.exp(-np.linspace(0, 4, half))
    b = square(300, n-half) * np.exp(-np.linspace(0, 4, n-half))
    return np.concatenate([a, b]) * 0.5


def gameover():
    notes = [392, 330, 262, 196]  # G E C G(下行)
    seg = int(0.22 * SR)
    out = []
    for f in notes:
        s = (square(f, seg) * 0.4 + sine(f, seg) * 0.3)
        s *= np.exp(-np.linspace(0, 3, seg))
        out.append(s)
    return np.concatenate(out)


if __name__ == "__main__":
    np.random.seed(7)
    save("sfx_shoot.wav", shoot())
    save("sfx_hit.wav", hit())
    save("sfx_explore.wav", explore())
    save("sfx_hurt.wav", hurt())
    save("sfx_gameover.wav", gameover())
    print("sfx done")
