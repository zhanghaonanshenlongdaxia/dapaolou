# -*- coding: utf-8 -*-
"""批量生成 10 首不同风格的乡村 BGM（本地 AceStep），转 OGG 入 Assets/Audio/BGM/"""
import json
import os
import shutil
import subprocess
import time
import urllib.request

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
RAW_DIR = r"E:\UnityProject\Dapaolou\TempAIGen\bgm_batch"
SAVE_DIR = r"E:\UnityProject\Dapaolou\Assets\Audio\BGM"

# (文件名, tags, bpm, keyscale, timesignature, seed)
TRACKS = [
    ("bgm_01_morning",  "peaceful chinese village morning, warm acoustic guitar, gentle folk instrumental, birds ambience, nostalgic, slow relaxed", 88, "C major", "4", 1101),
    ("bgm_02_pastoral", "pastoral strings and wood flute, open green fields, calm summer breeze, light orchestral, serene", 80, "G major", "4", 1102),
    ("bgm_03_playful",  "playful marimba and wooden percussion, cheerful countryside stroll, carefree and light, cute", 108, "D major", "4", 1103),
    ("bgm_04_piano",    "gentle piano solo, quiet rural afternoon, nostalgic warm memories, soft and slow", 72, "C major", "4", 1104),
    ("bgm_05_bamboo",   "bamboo flute melody, oriental rural landscape, serene distant mountains, meditative pentatonic", 76, "A minor", "4", 1105),
    ("bgm_06_ukulele",  "cheerful ukulele folk tune, sunny village day, lighthearted happy, simple and bright", 112, "G major", "4", 1106),
    ("bgm_07_waltz",    "gentle accordion waltz, old village dance, warm homely family gathering", 96, "F major", "3", 1107),
    ("bgm_08_lofi",     "warm lofi chill beat, rural afternoon nap, mellow cozy, soft vinyl texture, relaxed", 84, "A minor", "4", 1108),
    ("bgm_09_guzheng",  "guzheng and eastern strings, idyllic countryside river, elegant flowing pentatonic", 82, "D major", "4", 1109),
    ("bgm_10_evening",  "calm evening ambience, soft pads and distant guitar, dusk over the village, serene peaceful", 70, "A major", "4", 1110),
]


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())


def build_wf(tags, duration, seed, bpm, keyscale, timesig):
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
            "timesignature": timesig, "language": "en", "keyscale": keyscale,
            "generate_audio_codes": False, "cfg_scale": 2.0,
            "temperature": 0.85, "top_p": 0.9, "top_k": 0, "min_p": 0}},
        "47":  {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["94", 0]}},
        "3":   {"class_type": "KSampler", "inputs": {
            "model": ["78", 0], "positive": ["94", 0], "negative": ["47", 0],
            "latent_image": ["98", 0], "seed": seed,
            "steps": 8, "cfg": 1.0,
            "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
        "18":  {"class_type": "VAEDecodeAudio", "inputs": {"samples": ["3", 0], "vae": ["106", 0]}},
        "17":  {"class_type": "SaveAudio", "inputs": {"audio": ["18", 0], "filename_prefix": "bgm_batch"}},
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
                if fname:  # .flac/.wav 都收
                    return os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
    raise TimeoutError(f"timeout {timeout}s")


def main():
    os.makedirs(RAW_DIR, exist_ok=True)
    os.makedirs(SAVE_DIR, exist_ok=True)
    ok, fail = [], []
    for name, tags, bpm, keyscale, timesig, seed in TRACKS:
        ogg = os.path.join(SAVE_DIR, name + ".ogg")
        if os.path.exists(ogg):
            print(f"  [{name}] exists, skip", flush=True)
            ok.append(name)
            continue
        print(f"  [{name}] generating (bpm={bpm} {keyscale})...", flush=True)
        try:
            wf = build_wf(tags, 60, seed, bpm, keyscale, timesig)
            resp = post(f"{COMFY}/prompt", {"prompt": wf})
            if "error" in resp or resp.get("node_errors"):
                raise RuntimeError(json.dumps(resp, ensure_ascii=False)[:300])
            pid = resp["prompt_id"]
            print(f"  [{name}] submitted {pid}", flush=True)
            t0 = time.time()
            src = wait_output(pid)
            raw = os.path.join(RAW_DIR, name + ".flac")
            shutil.copy2(src, raw)
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", raw,
                            "-ar", "44100", "-ac", "2", "-q:a", "4", ogg], check=True)
            ok.append(name)
            print(f"  [{name}] OK -> {ogg} ({os.path.getsize(ogg)//1024}KB, {int(time.time()-t0)}s)", flush=True)
        except Exception as e:
            fail.append(name)
            print(f"  [{name}] FAILED: {e}", flush=True)
    print(f"BGM batch done: ok={len(ok)} fail={len(fail)} {('failures: ' + ','.join(fail)) if fail else ''}", flush=True)


if __name__ == "__main__":
    main()
