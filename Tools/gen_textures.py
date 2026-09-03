# -*- coding: utf-8 -*-
"""批量生成无缝地形纹理（本地 ComfyUI, Qwen-Image 4 步）"""
import json
import os
import shutil
import time
import urllib.request

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"
SAVE_DIR = r"E:\UnityProject\Dapaolou\TempAIGen\textures"

TEXTURES = {
    "cement_surface": "seamless tileable texture of old gray concrete pavement surface, fine rough grain, subtle weathering stains and small cracks, even flat lighting, top-down view, fills entire frame edge to edge, no objects, no shadows, no text, no borders",
    "trench_soil": "seamless tileable texture of dark damp compacted soil, fine dirt grain with tiny pebbles, even flat lighting, top-down view, fills entire frame edge to edge, no objects, no shadows, no text, no borders",
    "dirt_ground": "seamless tileable texture of dry brown dirt ground with small stones and sparse dry grass patches, uneven rough surface, even flat lighting, top-down view, fills entire frame edge to edge, no objects, no shadows, no text, no borders",
    "grass_ground": "seamless tileable texture of green grass with scattered dry yellow straws and tiny weeds, even flat lighting, top-down view, fills entire frame edge to edge, no objects, no shadows, no text, no borders",
    "sand_fine": "seamless tileable texture of fine pale yellow loose sand with tiny grains and small ripples, even flat lighting, top-down view, fills entire frame edge to edge, no objects, no shadows, no text, no borders",
    "wall_brick": "seamless tileable texture of old gray weathered brick wall, mortar lines between bricks, subtle moss stains, even flat lighting, straight-on view, fills entire frame edge to edge, no objects, no shadows, no text, no borders",
}

NEGATIVE = ("low quality, blurry, distorted, objects, shadows, text, watermark, "
            "vignette, borders, frame, people, animals")


def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())


def build_wf(prompt, seed):
    return {
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
        "249p": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["245", 0], "text": prompt}},
        "249n": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["245", 0], "text": NEGATIVE}},
        "252": {"class_type": "EmptySD3LatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
        "253": {"class_type": "KSampler", "inputs": {
            "model": ["247", 0], "positive": ["249p", 0], "negative": ["249n", 0],
            "latent_image": ["252", 0], "seed": seed,
            "steps": 4, "cfg": 4.0,
            "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
        "251": {"class_type": "VAEDecode", "inputs": {"samples": ["253", 0], "vae": ["246", 0]}},
        "255": {"class_type": "SaveImage", "inputs": {"images": ["251", 0], "filename_prefix": "tex_" + prompt[:12].replace(" ", "_")}},
    }


def main():
    os.makedirs(SAVE_DIR, exist_ok=True)
    only = None
    for a in sys.argv[1:]:
        if a.startswith("--only "):
            only = a.split(" ", 1)[1].split(",")
    seed = 20260904
    for name, prompt in TEXTURES.items():
        if only is not None and name not in only:
            continue
        out = os.path.join(SAVE_DIR, f"{name}.png")
        if os.path.exists(out):
            print(f"[{name}] 已存在，跳过", flush=True)
            continue
        wf = build_wf(prompt, seed)
        resp = post(f"{COMFY}/prompt", {"prompt": wf})
        if "error" in resp or resp.get("node_errors"):
            print(f"[{name}] REJECTED", flush=True)
            continue
        pid = resp["prompt_id"]
        start = time.time()
        saved_path = None
        while time.time() - start < 300:
            time.sleep(5)
            try:
                h = get(f"{COMFY}/history/{pid}")
            except Exception:
                continue
            entry = h.get(pid)
            if not entry:
                continue
            if entry.get("status", {}).get("status_str") == "error":
                print(f"[{name}] EXEC ERROR", flush=True)
                break
            for nid, out_ in entry.get("outputs", {}).items():
                for item in out_.get("images", []):
                    fname = item.get("filename")
                    if fname and fname.endswith(".png"):
                        src = os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
                        shutil.copy2(src, out)
                        print(f"[{name}] 完成 ({int(time.time()-start)}s)", flush=True)
                        saved_path = out
                        break
                if saved_path:
                    break
            if saved_path:
                break
        if not saved_path:
            print(f"[{name}] 失败/超时", flush=True)
        seed += 1
    print("全部纹理任务结束", flush=True)


if __name__ == "__main__":
    import sys
    main()
