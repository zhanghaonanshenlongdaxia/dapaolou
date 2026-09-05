# -*- coding: utf-8 -*-
"""程序化合成弹珠弹跳音效：短促、清脆、零拖音零回音（比 AceStep 生成更可控）
bounce_cement.wav 玻璃珠磕水泥：高频阻尼振荡簇 + 噪声起振，~0.16s
bounce_dirt.wav   玻璃珠落泥地：低频闷响 + 软噪声，~0.22s
"""
import math
import random
import struct
import wave

SR = 44100
SAVE_DIR = r"E:\UnityProject\Dapaolou\Assets\Audio\SFX"


def write_wav(name, samples):
    peak = max(abs(s) for s in samples) or 1.0
    scale = 0.89 / peak  # 峰值归一 -1dBFS
    path = rf"{SAVE_DIR}\{name}.wav"
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s * scale)) * 32767)) for s in samples))
    print(f"  [{name}] {len(samples)/SR*1000:.0f}ms -> {path}")


def damped_sine(freq, tau, amp, n, phase=0.0):
    """单频阻尼正弦"""
    return [amp * math.exp(-i / (tau * SR)) * math.sin(2 * math.pi * freq * i / SR + phase) for i in range(n)]


def gen_cement():
    """玻璃磕水泥：清脆 tick——三个高频部分音快速衰减 + 短噪声起振"""
    dur = 0.16
    n = int(SR * dur)
    rng = random.Random(20260906)
    out = [0.0] * n
    # 玻璃共振部分音（高频、快衰减，双近频制造自然拍频）
    for f, a, tau in [(2620, 1.0, 0.030), (2690, 0.7, 0.026), (4350, 0.55, 0.016), (6100, 0.30, 0.011)]:
        for i, v in enumerate(damped_sine(f, tau, a, n)):
            out[i] += v
    # 起振：2ms 噪声爆点（接触瞬间的 mechanical tick）
    na = int(0.002 * SR)
    for i in range(na):
        out[i] += 0.5 * (rng.random() * 2 - 1) * math.exp(-i / (0.0006 * SR))
    # 轻微低频体感（80Hz 快衰减，"嗒"的实体感）
    for i, v in enumerate(damped_sine(180, 0.008, 0.25, n)):
        out[i] += v
    return out


def gen_dirt():
    """玻璃落泥地：低沉 thud——低频阻尼 + 软噪声，无高频亮点"""
    dur = 0.22
    n = int(SR * dur)
    rng = random.Random(20260907)
    out = [0.0] * n
    # 低频体（泥地吸收高频，只剩闷响）
    for f, a, tau in [(115, 1.0, 0.055), (185, 0.5, 0.032)]:
        for i, v in enumerate(damped_sine(f, tau, a, n)):
            out[i] += v
    # 软噪声：800Hz 单极点低通的噪声串，8ms 衰减（泥土颗粒感）
    na = int(0.02 * SR)
    noise = [rng.random() * 2 - 1 for _ in range(na)]
    lp = 0.0
    a_coef = math.exp(-2 * math.pi * 800 / SR)
    for i in range(na):
        lp = a_coef * lp + (1 - a_coef) * noise[i]
        out[i] += 0.55 * lp * math.exp(-i / (0.006 * SR))
    return out


if __name__ == "__main__":
    write_wav("bounce_cement", gen_cement())
    write_wav("bounce_dirt", gen_dirt())
    print("done")
