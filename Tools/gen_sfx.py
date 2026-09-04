# -*- coding: utf-8 -*-
"""批量生成游戏音效（直接构建 workflow，不用模板）"""
import json
import os
import random
import shutil
import time
import urllib.request

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
SAVE_DIR = r"E:\UnityProject\Dapaolou\Assets\Audio\SFX"

SFX = {
    "marble_hit": ("glass marble hitting another glass marble, sharp crisp click, short one-shot, clean", 1.5),
    "marble_roll": ("glass marble rolling on concrete surface, soft continuous friction, looping", 3.0),
    "marble_flick": ("glass marble flick shot, quick sharp flick pop sound, short", 1.0),
    "glass_shatter": ("glass marble shattering into pieces, crisp clear glass breaking, short burst", 1.5),
    "water_splash": ("small object splashing into shallow water puddle, short splash, natural", 1.5),
    "towerscatter": ("small glass marbles scattering and bouncing on concrete floor, multiple short clicks", 2.0),
}


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())


def build_wf(tags, duration, seed):
    return {
        "105": {"class_type": "DualCLIPLoader", "inputs": {
            "clip_name1": "qwen_0.6b_ace15.safetensors",
            "clip_name2": "qwen_4b_ace15.safetensors",
            "type": "ace", "device": "default"}},
        "106": {"class_type": "VAELoader", "inputs": {"vae_name": "ace_1.5_vae.safetensors"}},
        "104": {"class_type": "UNETLoader", "inputs": {
            "unet_name": "acestep_v1.5_turbo.safetensors", "weight_dtype": "default"}},
        "78":  {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["104", 0], "shift": 3.1}},
        "98":  {"class_type": "EmptyAceStep1.5LatentAudio", "inputs": {"seconds": duration, "batch_size": 1}},
        "94":  {"class_type": "TextEncodeAceStepAudio1.5", "inputs": {
            "clip": ["105", 0], "tags": tags, "lyrics": "",
            "seed": seed, "bpm": 120, "duration": duration,
            "timesignature": "4", "language": "en", "keyscale": "C major",
            "generate_audio_codes": True, "cfg_scale": 2.0,
            "temperature": 0.85, "top_p": 0.9, "top_k": 0, "min_p": 0}},
        "47":  {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["94", 0]}},
        "3":   {"class_type": "KSampler", "inputs": {
            "model": ["78", 0], "positive": ["94", 0], "negative": ["47", 0],
            "latent_image": ["98", 0], "seed": seed,
            "steps": 8, "cfg": 1.0,
            "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
        "18":  {"class_type": "VAEDecodeAudio", "inputs": {"samples": ["3", 0], "vae": ["106", 0]}},
        "17":  {"class_type": "SaveAudio", "inputs": {"audio": ["18", 0], "filename_prefix": "sfx_output"}},
    }


def gen_sfx(name, tags, duration):
    seed = random.randint(0, 999999)
    wf = build_wf(tags, duration, seed)
    resp = post(f"{COMFY}/prompt", {"prompt": wf})
    if "error" in resp or resp.get("node_errors"):
        raise RuntimeError(f"ComfyUI rejected: {json.dumps(resp, ensure_ascii=False)[:300]}")
    pid = resp["prompt_id"]
    print(f"  [{name}] submitted {pid}", flush=True)
    start = time.time()
    while True:
        time.sleep(3)
        try:
            h = get(f"{COMFY}/history/{pid}")
        except Exception:
            continue
        entry = h.get(pid)
        if not entry:
            continue
        if entry.get("status", {}).get("status_str") == "error":
            raise RuntimeError(f"[{name}] ComfyUI exec error")
        for nid, out in entry.get("outputs", {}).items():
            for item in out.get("audio", []):
                fname = item.get("filename")
                if fname and fname.endswith(".wav"):
                    src = os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
                    dst = os.path.join(SAVE_DIR, f"{name}.wav")
                    shutil.copy2(src, dst)
                    print(f"  [{name}] OK ({os.path.getsize(dst)//1024}KB, {int(time.time()-start)}s)", flush=True)
                    return
        if time.time() - start > 120:
            raise TimeoutError(f"[{name}] timeout")


def main():
    os.makedirs(SAVE_DIR, exist_ok=True)
    sfx_list = [
        ("marble_hit", "glass marble hitting another glass marble, sharp crisp click, short one-shot, clean", 1.5),
        ("marble_roll", "glass marble rolling on concrete surface, soft continuous friction, looping", 3.0),
        ("marble_flick", "glass marble flick shot, quick sharp flick pop sound, short", 1.0),
        ("glass_shatter", "glass marble shattering into pieces, crisp clear glass breaking, short burst", 1.5),
        ("water_splash", "small object splashing into shallow water puddle, short splash, natural", 1.5),
        ("towerscatter", "small glass marbles scattering and bouncing on concrete floor, multiple short clicks", 2.0),
    ]
    for name, tags, dur in sfx_list:
        out = os.path.join(SAVE_DIR, f"{name}.wav")
        if os.path.exists(out):
            print(f"  [{name}] exists, skip", flush=True)
            continue
        print(f"  [{name}] generating...", flush=True)
        try:
            gen_sfx(name, tags, dur)
        except Exception as e:
            print(f"  [{name}] FAILED: {e}", flush=True)
    print("All SFX done", flush=True)


if __name__ == "__main__":
    main()
