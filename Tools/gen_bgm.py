# -*- coding: utf-8 -*-
"""调用本地 ComfyUI AceStep 1.5 生成乡村午后 BGM"""
import json
import os
import shutil
import time
import urllib.request

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
SAVE_TO = r"E:\UnityProject\Dapaolou\Assets\Audio\bgm_countryside.wav"

TAGS = "Chinese rural countryside, warm acoustic guitar, gentle banjo, peaceful summer afternoon, birds chirping, nostalgic folk instrumental, warm sunlight, village atmosphere, slow relaxed tempo, 90bpm"

WF = {
    "105": {"class_type": "DualCLIPLoader", "inputs": {
        "clip_name1": "qwen_0.6b_ace15.safetensors",
        "clip_name2": "qwen_4b_ace15.safetensors",
        "type": "ace", "device": "default"}},
    "106": {"class_type": "VAELoader", "inputs": {"vae_name": "ace_1.5_vae.safetensors"}},
    "104": {"class_type": "UNETLoader", "inputs": {
        "unet_name": "acestep_v1.5_turbo.safetensors", "weight_dtype": "default"}},
    "78":  {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["104", 0], "shift": 3.1}},
    "98":  {"class_type": "EmptyAceStep1.5LatentAudio", "inputs": {"seconds": 120, "batch_size": 1}},
    "94":  {"class_type": "TextEncodeAceStepAudio1.5", "inputs": {
        "clip": ["105", 0], "tags": TAGS, "lyrics": "",
        "seed": 42, "bpm": 90, "duration": 120,
        "timesignature": "4", "language": "en", "keyscale": "C major",
        "generate_audio_codes": True, "cfg_scale": 2.0,
        "temperature": 0.85, "top_p": 0.9, "top_k": 0, "min_p": 0}},
    "47":  {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["94", 0]}},
    "3":   {"class_type": "KSampler", "inputs": {
        "model": ["78", 0], "positive": ["94", 0], "negative": ["47", 0],
        "latent_image": ["98", 0], "seed": 42,
        "steps": 8, "cfg": 1.0,
        "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
    "18":  {"class_type": "VAEDecodeAudio", "inputs": {"samples": ["3", 0], "vae": ["106", 0]}},
    "17":  {"class_type": "SaveAudio", "inputs": {"audio": ["18", 0], "filename_prefix": "bgm_countryside"}},
}


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())


def main():
    os.makedirs(os.path.dirname(SAVE_TO), exist_ok=True)
    resp = post(f"{COMFY}/prompt", {"prompt": WF})
    if "error" in resp or resp.get("node_errors"):
        raise RuntimeError(json.dumps(resp, ensure_ascii=False)[:800])
    pid = resp["prompt_id"]
    print("submitted", pid, flush=True)
    start = time.time()
    while True:
        time.sleep(5)
        try:
            h = get(f"{COMFY}/history/{pid}")
        except Exception:
            continue
        entry = h.get(pid)
        if not entry:
            continue
        if entry.get("status", {}).get("status_str") == "error":
            raise RuntimeError(json.dumps(entry["status"], ensure_ascii=False)[:800])
        done = False
        for nid, out in entry.get("outputs", {}).items():
            for item in out.get("audio", []):
                fname = item.get("filename")
                if fname and fname.endswith(".wav"):
                    src = os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
                    shutil.copy2(src, SAVE_TO)
                    print(f"saved: {SAVE_TO} ({os.path.getsize(SAVE_TO)//1024}KB, {int(time.time()-start)}s)", flush=True)
                    done = True
        if done:
            break
        if time.time() - start > 600:
            raise TimeoutError("timeout")


if __name__ == "__main__":
    main()
