# -*- coding: utf-8 -*-
"""弹跳音效 v2：AceStep 生成 + ffmpeg 强制后期（截短/去尾/调音色），解决拖音回音问题"""
import json
import os
import random
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_sfx import build_wf, post, get

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
SFX_DIR = r"E:\UnityProject\Dapaolou\Assets\Audio\SFX"
CAND_DIR = os.path.join(SFX_DIR, "Candidates")

# (名字, AceStep tags, 时长, 种子) —— 每个音效 2 个候选
JOBS = [
    ("bounce_cement_ai1", "single glass marble bounce on concrete floor, very short dry percussive tick, bright crisp high click, tight close mic, no reverb, no echo, no tail", 1.0, 4101),
    ("bounce_cement_ai2", "glass marble hitting cement, one sharp dry click, extremely short percussive tap, clean dry signal, close dry mic", 1.0, 9723),
    ("bounce_dirt_ai1", "single glass marble drop on dry dirt ground, very short dull low thud, soft muted impact, tight dry close mic, no reverb, no echo", 1.0, 5588),
    ("bounce_dirt_ai2", "glass marble landing on dry earth, one short muted thump, low dull knock, dry tight signal, no room echo", 1.0, 7314),
]


def wait_output(pid, timeout=300):
    start = time.time()
    while time.time() - start < timeout:
        time.sleep(3)
        try:
            h = get(f"{COMFY}/history/{pid}")
        except Exception:
            continue
        entry = h.get(pid)
        if not entry:
            continue
        if entry.get("status", {}).get("status_str") == "error":
            raise RuntimeError("ComfyUI exec error")
        for nid, out in entry.get("outputs", {}).items():
            for item in out.get("audio", []):
                fname = item.get("filename")
                if fname:  # .flac / .wav 都收
                    return os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
    raise TimeoutError("wait timeout")


def gen(name, tags, duration, seed):
    wf = build_wf(tags, duration, seed)
    resp = post(f"{COMFY}/prompt", {"prompt": wf})
    if "error" in resp or resp.get("node_errors"):
        raise RuntimeError(json.dumps(resp, ensure_ascii=False)[:300])
    pid = resp["prompt_id"]
    print(f"  [{name}] submitted {pid}", flush=True)
    src = wait_output(pid)
    dst = os.path.join(CAND_DIR, f"{name}_raw.flac")
    shutil.copy2(src, dst)
    print(f"  [{name}] raw ok ({os.path.getsize(dst)//1024}KB)", flush=True)
    return dst


def max_volume_db(path):
    r = subprocess.run(["ffmpeg", "-i", path, "-af", "volumedetect", "-f", "null", "-"],
                       capture_output=True, text=True)
    for line in r.stderr.splitlines():
        if "max_volume" in line:
            return float(line.split("max_volume:")[1].split("dB")[0].strip())
    return 0.0


def process(src, dst, is_cement):
    """去静音头 -> 音色EQ -> 硬截断 -> 收尾淡出 -> 峰值归一 -1dB"""
    if is_cement:
        eq = "highpass=f=400,equalizer=f=3500:t=q:w=1.2:g=5"   # 提亮 3.5k 让"嗒"更脆
        end = 0.32
    else:
        eq = "lowpass=f=1100,highpass=f=50"                     # 压掉高频亮点保持闷
        end = 0.38
    fade_st = end - 0.08
    af = (f"silenceremove=start_periods=1:start_threshold=-30dB,"
          f"{eq},"
          f"atrim=end={end},"
          f"afade=t=out:st={fade_st:.2f}:d=0.08")
    tmp = dst + ".tmp.wav"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", src, "-af", af, "-ar", "44100", tmp], check=True)
    gain = -1.0 - max_volume_db(tmp)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp,
                    "-af", f"volume={gain:.1f}dB", dst], check=True)
    os.remove(tmp)
    print(f"  [{os.path.basename(dst)}] processed (gain {gain:.1f}dB)", flush=True)


if __name__ == "__main__":
    os.makedirs(CAND_DIR, exist_ok=True)
    results = {}
    for name, tags, dur, seed in JOBS:
        print(f"  [{name}] generating...", flush=True)
        try:
            raw = gen(name, tags, dur, seed)
            dst = os.path.join(CAND_DIR, name + ".wav")
            process(raw, dst, "cement" in name)
            results[name] = dst
        except Exception as e:
            print(f"  [{name}] FAILED: {e}", flush=True)

    # 候选 1 直接接线（覆盖主文件，场景引用不变）；候选 2 留在 Candidates 里备用
    if "bounce_cement_ai1" in results:
        shutil.copy2(results["bounce_cement_ai1"], os.path.join(SFX_DIR, "bounce_cement.wav"))
        print("  wired bounce_cement.wav <- ai1", flush=True)
    if "bounce_dirt_ai1" in results:
        shutil.copy2(results["bounce_dirt_ai1"], os.path.join(SFX_DIR, "bounce_dirt.wav"))
        print("  wired bounce_dirt.wav <- ai1", flush=True)
    print("All done", flush=True)
