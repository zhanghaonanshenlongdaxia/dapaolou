# -*- coding: utf-8 -*-
"""批量生成 3 首不同风格 2 分钟 BGM（本地 ComfyUI AceStep 1.5），全部提交队列后逐个收割"""
import json
import os
import shutil
import subprocess
import time
import urllib.request

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
SAVE_DIR = r"E:\UnityProject\Dapaolou\Assets\Audio"

TRACKS = [
    ("bgm_folk", 95, "C major", 7101,
     "Chinese rural countryside folk, warm acoustic guitar, gentle banjo, peaceful summer morning, birds chirping, nostalgic light-hearted instrumental, village atmosphere"),
    ("bgm_afternoon", 110, "G major", 7102,
     "playful rural afternoon, light ukulele and accordion, cheerful carefree melody, sunny farm village, bouncy happy folk instrumental"),
    ("bgm_evening", 75, "A minor", 7103,
     "calm village evening, soft piano with warm strings, serene nostalgic dusk, peaceful countryside night, slow gentle ballad instrumental"),
]


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())


def build_wf(tags, seed, bpm, keyscale, duration=120):
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
        "17":  {"class_type": "SaveAudio", "inputs": {"audio": ["18", 0], "filename_prefix": "bgm_v2_" + name_prefix[0]}},
    }


name_prefix = ["x"]


def wait_output(pid, timeout=900):
    start = time.time()
    while time.time() - start < timeout:
        time.sleep(10)
        try:
            h = get(f"{COMFY}/history/{pid}")
        except Exception:
            continue
        entry = h.get(pid)
        if not entry:
            continue
        if entry.get("status", {}).get("status_str") == "error":
            raise RuntimeError("ComfyUI exec error: " + json.dumps(entry["status"], ensure_ascii=False)[:300])
        for nid, out in entry.get("outputs", {}).items():
            for item in out.get("audio", []):
                fname = item.get("filename")
                if fname:  # .flac/.wav 都收，后面统一转 wav
                    return os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
    raise TimeoutError("wait timeout")


def to_wav(src, dst):
    if src.lower().endswith(".wav"):
        shutil.copy2(src, dst)
    else:
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", src, dst], check=True)


if __name__ == "__main__":
    os.makedirs(SAVE_DIR, exist_ok=True)
    # 全部先提交进队列（GPU 排队连续跑），再逐个收割
    pids = {}
    for name, bpm, keyscale, seed, tags in TRACKS:
        name_prefix[0] = name
        wf = build_wf(tags, seed, bpm, keyscale)
        resp = post(f"{COMFY}/prompt", {"prompt": wf})
        if "error" in resp or resp.get("node_errors"):
            raise RuntimeError(f"{name} rejected: " + json.dumps(resp, ensure_ascii=False)[:300])
        pids[name] = resp["prompt_id"]
        print(f"[{name}] submitted {resp['prompt_id']}", flush=True)

    for name, bpm, keyscale, seed, tags in TRACKS:
        print(f"[{name}] waiting...", flush=True)
        src = wait_output(pids[name])
        dst = os.path.join(SAVE_DIR, name + ".wav")
        to_wav(src, dst)
        print(f"[{name}] saved {dst} ({os.path.getsize(dst)//1024//1024}MB)", flush=True)
    print("All BGM done", flush=True)
