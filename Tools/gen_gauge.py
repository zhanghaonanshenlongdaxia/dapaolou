# -*- coding: utf-8 -*-
"""调用本地 ComfyUI 生成油门盘仪表盘 UI 贴图"""
import json
import shutil
import time
import urllib.request
import os

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
SAVE_TO = r"E:\UnityProject\Dapaolou\TempAIGen\gauge_raw.png"

PROMPT = ("A circular game UI gauge dial background for a marble shooting power meter, "
          "dark bronze metal ring with small tick marks around the edge, "
          "inner area is a slightly recessed dark panel, "
          "stylized game HUD element, clean flat vector style, top-down view, "
          "perfectly centered circle, pure white background outside the circle, no text, no needle.")

NEGATIVE = ("low quality, blurry, distorted, extra objects, text, watermark, "
            "square frame, rectangular, complex background, gradient background")


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())


WF = {
    "248": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "qwen-image-2512-Q4_K_M.gguf"}},
    "259": {"class_type": "LoraLoaderModelOnly", "inputs": {
        "model": ["248", 0],
        "lora_name": "Qwen-Image-2512-Lightning-4steps-V1.0-bf16.safetensors",
        "strength_model": 1.0}},
    "247": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["259", 0], "shift": 3.1}},
    "245": {"class_type": "CLIPLoader", "inputs": {
        "clip_name": "qwen_2.5_vl_7b_fp8_scaled.safetensors",
        "type": "qwen_image", "device": "default"}},
    "246": {"class_type": "VAELoader", "inputs": {"vae_name": "qwen_image_vae.safetensors"}},
    "249p": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["245", 0], "text": PROMPT}},
    "249n": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["245", 0], "text": NEGATIVE}},
    "252": {"class_type": "EmptySD3LatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
    "253": {"class_type": "KSampler", "inputs": {
        "model": ["247", 0], "positive": ["249p", 0], "negative": ["249n", 0],
        "latent_image": ["252", 0], "seed": 20260903,
        "steps": 4, "cfg": 4.0,
        "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
    "251": {"class_type": "VAEDecode", "inputs": {"samples": ["253", 0], "vae": ["246", 0]}},
    "255": {"class_type": "SaveImage", "inputs": {"images": ["251", 0], "filename_prefix": "gauge_dial"}},
}


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
            for item in out.get("images", []):
                fname = item.get("filename")
                if fname and fname.endswith(".png"):
                    src = os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
                    shutil.copy2(src, SAVE_TO)
                    print("saved:", SAVE_TO, f"({os.path.getsize(SAVE_TO)//1024}KB, {int(time.time()-start)}s)", flush=True)
                    done = True
        if done:
            break
        if time.time() - start > 300:
            raise TimeoutError("timeout")


if __name__ == "__main__":
    main()
