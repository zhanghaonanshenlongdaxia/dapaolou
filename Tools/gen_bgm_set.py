# -*- coding: utf-8 -*-
"""批量生成 3 首不同风格的 120s BGM（本地 AceStep），flac 自动转 wav"""
import json
import os
import shutil
import subprocess
import time
import urllib.request

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
SAVE_DIR = r"E:\UnityProject\Dapaolou\Assets\Audio"

# (文件名, tags, bpm, 调式, seed)
BGM = [
    ("bgm_playful", "playful lighthearted village folk, pizzicato strings, marimba, ukulele, cheerful sunny morning, bouncy rhythm, light and fun, 100bpm", 100, "G major", 5150),
    ("bgm_sunset", "calm peaceful countryside evening, soft piano, warm string pads, gentle nostalgic melody, serene sunset golden light, slow 70bpm", 70, "A minor", 7213),
    ("bgm_battle", "energetic playful competition theme, light marching percussion, rhythmic staccato strings, fun lively battle mood, driving 110bpm", 110, "C major", 9301),
]


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())


def build_wf(tags, bpm, keyscale, seed, duration=120):
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
            "seed": seed, "bpm": bpm, "duration": duration,
            "timesignature": "4", "language": "en", "keyscale": keyscale,
            "generate_audio_codes": True, "cfg_scale": 2.0,
            "temperature": 0.85, "top_p": 0.9, "top_k": 0, "min_p": 0}},
        "47":  {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["94", 0]}},
        "3":   {"class_type": "KSampler", "inputs": {
            "model": ["78", 0], "positive": ["94", 0], "negative": ["47", 0],
            "latent_image": ["98", 0], "seed": seed,
            "steps": 8, "cfg": 1.0,
            "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
        "18":  {"class_type": "VAEDecodeAudio", "inputs": {"samples": ["3", 0], "vae": ["106", 0]}},
        "17":  {"class_type": "SaveAudio", "inputs": {"audio": ["18", 0], "filename_prefix": "bgm"}},
    }


def wait_output(pid, timeout=900):
    start = time.time()
    while time.time() - start < timeout:
        time.sleep(5)
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
                if fname:  # flac/wav 都收
                    return os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
    raise TimeoutError("timeout")


def main():
    for name, tags, bpm, keyscale, seed in BGM:
        dst = os.path.join(SAVE_DIR, f"{name}.wav")
        if os.path.exists(dst):
            print(f"  [{name}] exists, skip", flush=True)
            continue
        print(f"  [{name}] generating...", flush=True)
        try:
            resp = post(f"{COMFY}/prompt", {"prompt": build_wf(tags, bpm, keyscale, seed)})
            if "error" in resp or resp.get("node_errors"):
                raise RuntimeError(json.dumps(resp, ensure_ascii=False)[:300])
            pid = resp["prompt_id"]
            print(f"  [{name}] submitted {pid}", flush=True)
            src = wait_output(pid)
            if src.lower().endswith(".flac"):
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", src, dst], check=True)
            else:
                shutil.copy2(src, dst)
            print(f"  [{name}] saved {os.path.getsize(dst)//1024}KB", flush=True)
        except Exception as e:
            print(f"  [{name}] FAILED: {e}", flush=True)
    print("All BGM done", flush=True)


if __name__ == "__main__":
    main()
