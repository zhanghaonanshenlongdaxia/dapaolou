# 本地 ComfyUI AI 生成操作手册

> 适用于任何 IDE / 任何语言调用。所有操作都是标准的 HTTP API 调用。
> ComfyUI 地址：`http://127.0.0.1:8188`

---

## 目录

1. [启动 ComfyUI](#一启动-comfyui)
2. [通用调用流程（所有生成共用的协议）](#二通用调用流程)
3. [生图（Qwen-Image 2512）](#三生图)
4. [图生图 / 局部重绘（改图）](#四图生图局部重绘)
5. [视频生成（MiniMax H3）](#五视频生成)
6. [音乐生成（AceStep 1.5）](#六音乐生成)
7. [常见问题](#七常见问题)
8. [背景移除（rembg）](#八背景移除rembg)
9. [火山引擎 Seedream 生图（云端 API）](#九火山引擎-seedream-生图云端-api)

---

## 一、启动 ComfyUI

```powershell
# Windows PowerShell 一键启动
Start-Process -FilePath "H:\ComfyUI_windows_portable\python_embeded\python.exe" `
  -ArgumentList "H:\ComfyUI_windows_portable\ComfyUI\main.py","--windows-standalone-build","--use-sage-attention","--auto-launch" `
  -WorkingDirectory "H:\ComfyUI_windows_portable\ComfyUI"

# 等待就绪（返回 200 即可用）
curl http://127.0.0.1:8188/system_stats
```

- 输出目录：`H:\ComfyUI_windows_portable\ComfyUI\output\`（生成结果按 `{前缀}_{序号:05d}_.png` 保存）
- 输入目录：`H:\ComfyUI_windows_portable\ComfyUI\input\`（要喂给模型的参考图放这里）

---

## 二、通用调用流程

所有生成（图/视频/音频）都是同一个三步协议：

```
① POST /prompt        → 提交工作流 JSON，拿到 prompt_id
② GET  /history/{id}  → 轮询（建议 5 秒一次），直到出现 outputs
③ 从 outputs 里取文件名 → 拼路径复制走
```

**标准轮询模板（Python，改 WF 就能换任务）：**

```python
import json, shutil, time, urllib.request, os

COMFY = "http://127.0.0.1:8188"
OUT_ROOT = r"H:\ComfyUI_windows_portable\ComfyUI\output"

def post(url, payload):
    req = urllib.request.Request(url, data=json.dumps(payload).encode(),
                                  headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())

def get(url):
    with urllib.request.urlopen(url, timeout=60) as r:
        return json.loads(r.read())

def run(WF, save_to, media_key="images", exts=(".png",), timeout=600):
    """提交工作流 WF，等待完成，把结果复制到 save_to"""
    resp = post(f"{COMFY}/prompt", {"prompt": WF})
    if "error" in resp or resp.get("node_errors"):
        raise RuntimeError(f"REJECTED: {json.dumps(resp, ensure_ascii=False)[:1000]}")
    pid = resp["prompt_id"]
    start = time.time()
    while True:
        time.sleep(5)
        try:
            h = get(f"{COMFY}/history/{pid}")
        except Exception:
            continue
        entry = h.get(pid)
        if not entry:
            if time.time() - start > timeout: raise TimeoutError("TIMEOUT")
            continue
        if entry.get("status", {}).get("status_str") == "error":
            raise RuntimeError(f"EXEC ERROR: {entry['status'].get('messages')}")
        for nid, out in entry.get("outputs", {}).items():
            for item in out.get(media_key, []):          # images / audio / videos
                fname = item.get("filename")
                if fname and fname.endswith(exts):
                    src = os.path.join(OUT_ROOT, item.get("subfolder", ""), fname)
                    shutil.copy2(src, save_to)
                    return save_to
        if time.time() - start > timeout: raise TimeoutError("TIMEOUT")
```

> 其他语言同理：POST JSON → 轮询 GET → 取文件。Node.js 用 fetch，C# 用 HttpClient。

---

## 三、生图

**模型**：Qwen-Image-2512（Q4 GGUF 量化）+ Lightning 4 步加速 LoRA

### 3.1 文生图工作流

```python
PROMPT = "A single game UI icon, ink-wash painting style, a guqin viewed from above, thick black ink brush strokes, minimalist, centered, pure white background, no text."

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
    "249n": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["245", 0],
        "text": "low quality, blurry, distorted, extra objects, text, watermark, complex background"}},
    "252": {"class_type": "EmptySD3LatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
    "253": {"class_type": "KSampler", "inputs": {
        "model": ["247", 0], "positive": ["249p", 0], "negative": ["249n", 0],
        "latent_image": ["252", 0], "seed": 42,          # 改 seed 换一张
        "steps": 4, "cfg": 4.0,                          # Lightning LoRA 固定 4 步
        "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
    "251": {"class_type": "VAEDecode", "inputs": {"samples": ["253", 0], "vae": ["246", 0]}},
    "255": {"class_type": "SaveImage", "inputs": {"images": ["251", 0], "filename_prefix": "my_image"}},
}

run(WF, r"D:\output\my_image.png")
```

**参数速查**：
| 参数 | 改哪 | 说明 |
|---|---|---|
| 尺寸 | `252.width/height` | 常用 1024×1024；图标 1024×400；多改 32 的倍数 |
| 换张图 | `253.seed` | 任意整数 |
| 提示词 | `249p.text` | 英文效果最佳，负面词 `249n.text` |
| 更高质量 | `259` 换 8steps LoRA + `253.steps=8, cfg=2.5` | 速度减半 |

**耗时**：4 步 LoRA 约 40-60 秒/张（RTX 5060 Ti 16G）

### 3.2 九宫格（Sliced）UI 素材生成

面板底图、按钮底图等需要 9-slice 拉伸的素材，提示词必须约束"四角固定、边框等宽、中心留空"：

**面板类模板：**
```
A rectangular game UI panel background, {风格描述}，designed for 9-slice scaling:
- four corners have identical decorative corner ornaments (same size, symmetrical)
- four edges are simple repeating border patterns of uniform width
- the center area is a large flat {底色} surface with no decorations
- all border and corner elements must stay within 15% of each edge
- absolutely no content in the center, no gradients across the panel
Pure white background outside the panel for keying. No text.
```

**按钮类模板：**
```
A rectangular game UI button background, {风格描述}，designed for 9-slice scaling:
- all four corners are identical rounded ornaments
- top/bottom edges are uniform thin border lines, left/right edges are uniform caps
- center is a completely flat solid {颜色} surface
- decorative elements must not extend beyond 20% from any edge
Pure white background outside. No text, no icon inside.
```

**要点**：
- 强调 "identical corners"（四角相同）和 "uniform edges"（边框等宽）
- 强调 "center is empty/flat"（中心留空给内容）
- 限制装饰范围 "within 15-20% of each edge"
- 负面词：`no gradients across the panel, no content in center, no text`
- 抠图策略：弹窗/底图类用泛洪填充（只抠边框外连通区），不要亮度全抠
- Unity 侧：导入为 Sprite → Sprite Editor 切九宫格边框 → Image 组件设 Sliced

### 3.3 生成后处理（白底抠图 → 透明 PNG）

生成图默认白底。要透明背景用亮度抠图（Pillow）：

```python
from PIL import Image
im = Image.open("my_image.png").convert("RGB")
px, (w, h) = im.load(), im.size
out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
po = out.load()
for y in range(h):
    for x in range(w):
        r, g, b = px[x, y]
        lum = (r*299 + g*587 + b*114) // 1000
        a = 0 if lum >= 245 else (int(255*(245-lum)/45) if lum >= 200 else 255)
        po[x, y] = (r, g, b, a)
bbox = out.getbbox()                       # 紧裁剪
if bbox:
    pad = 8
    bbox = (max(0,bbox[0]-pad), max(0,bbox[1]-pad), min(w,bbox[2]+pad), min(h,bbox[3]+pad))
    out = out.crop(bbox)
out.save("my_image_alpha.png")
```

> ⚠️ **面板/底图类素材（中间不能透明）禁用此法**！改用泛洪填充：只把"与图片边缘连通的白色"抠透明，被墨色边框包围的中心保持不透明。

---

## 四、图生图 / 局部重绘

**场景**：改现有图（换符号、去水印、去图章），**必须图生图保持风格**，不要文生图。

### 4.1 参考图 + 编辑提示词（替换局部元素）

```python
# 1) 参考图复制进 input 目录
shutil.copy2(r"D:\ref\pause_icon.png",
             r"H:\ComfyUI_windows_portable\ComfyUI\input\ref.png")

PROMPT = ("Edit this circular ink-wash button icon: keep the circle background, paper texture and "
          "ink border EXACTLY the same, but REPLACE the double vertical pause bars with a single "
          "solid play triangle pointing right. Same thick black ink brush style. Centered.")

WF = {
    "300": {"class_type": "LoadImage", "inputs": {"image": "ref.png", "upload": "image"}},
    "248": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "qwen-image-2512-Q4_K_M.gguf"}},
    "259": {"class_type": "LoraLoaderModelOnly", "inputs": {
        "model": ["248", 0],
        "lora_name": "Qwen-Image-2512-Lightning-4steps-V1.0-bf16.safetensors", "strength_model": 1.0}},
    "247": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["259", 0], "shift": 3.1}},
    "245": {"class_type": "CLIPLoader", "inputs": {
        "clip_name": "qwen_2.5_vl_7b_fp8_scaled.safetensors", "type": "qwen_image", "device": "default"}},
    "246": {"class_type": "VAELoader", "inputs": {"vae_name": "qwen_image_vae.safetensors"}},
    "310": {"class_type": "TextEncodeQwenImageEditPlus", "inputs": {     # ← 编辑专用节点
        "clip": ["245", 0], "prompt": PROMPT, "vae": ["246", 0],
        "image1": ["300", 0]}},                                          # ← 参考图接这里
    "311": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["245", 0],
        "text": "different style, changed background, text, watermark, low quality"}},
    "312": {"class_type": "VAEEncode", "inputs": {"pixels": ["300", 0], "vae": ["246", 0]}},
    "253": {"class_type": "KSampler", "inputs": {
        "model": ["247", 0], "positive": ["310", 0], "negative": ["311", 0],
        "latent_image": ["312", 0], "seed": 7777,
        "steps": 8, "cfg": 2.5, "sampler_name": "euler", "scheduler": "simple",
        "denoise": 0.62}},          # ← 关键！0.6~0.85，越低越贴近原图
    "251": {"class_type": "VAEDecode", "inputs": {"samples": ["253", 0], "vae": ["246", 0]}},
    "255": {"class_type": "SaveImage", "inputs": {"images": ["251", 0], "filename_prefix": "edited"}},
}
```

### 4.2 真·局部重绘（整图发送 + 遮罩限定重绘区）

去水印/图章用这个——**整张图发给模型，只有遮罩区域被重绘**，其余像素不动：

```python
# 1) 做遮罩：白色=要重绘的区域，黑色=锁定
from PIL import Image, ImageDraw
mask = Image.new("L", (1748, 1190), 0)
ImageDraw.Draw(mask).rectangle([1400, 40, 1650, 290], fill=255)  # 图章区域
mask.save(r"D:\stamp_mask.png")
shutil.copy2(r"D:\panel.png", INPUT + r"\panel_full.png")
shutil.copy2(r"D:\stamp_mask.png", INPUT + r"\stamp_mask.png")

WF = {
    "300": {"class_type": "LoadImage", "inputs": {"image": "panel_full.png", "upload": "image"}},
    "301": {"class_type": "LoadImageMask", "inputs": {"image": "stamp_mask.png", "channel": "red"}},
    "248": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "qwen-image-2512-Q4_K_M.gguf"}},
    "259": {"class_type": "LoraLoaderModelOnly", "inputs": {
        "model": ["248", 0],
        "lora_name": "Qwen-Image-2512-Lightning-4steps-V1.0-bf16.safetensors", "strength_model": 1.0}},
    "247": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["259", 0], "shift": 3.1}},
    "245": {"class_type": "CLIPLoader", "inputs": {
        "clip_name": "qwen_2.5_vl_7b_fp8_scaled.safetensors", "type": "qwen_image", "device": "default"}},
    "246": {"class_type": "VAELoader", "inputs": {"vae_name": "qwen_image_vae.safetensors"}},
    "310": {"class_type": "TextEncodeQwenImageEditPlus", "inputs": {
        "clip": ["245", 0], "vae": ["246", 0], "image1": ["300", 0],
        "prompt": "Remove the red square seal stamp in the top right corner completely. "
                  "Fill with surrounding aged rice paper texture. Keep everything else EXACTLY the same."}},
    "311": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["245", 0],
        "text": "red stamp, seal, watermark, changed borders, altered composition"}},
    "312": {"class_type": "VAEEncodeForInpaint", "inputs": {     # ← inpaint 专用编码
        "pixels": ["300", 0], "vae": ["246", 0], "mask": ["301", 0], "grow_mask_by": 6}},
    "253": {"class_type": "KSampler", "inputs": {
        "model": ["247", 0], "positive": ["310", 0], "negative": ["311", 0],
        "latent_image": ["312", 0], "seed": 1234, "steps": 8, "cfg": 2.5,
        "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
    "251": {"class_type": "VAEDecode", "inputs": {"samples": ["253", 0], "vae": ["246", 0]}},
    "320": {"class_type": "ImageCompositeMasked", "inputs": {    # 保险：只贴回遮罩区
        "destination": ["300", 0], "source": ["251", 0],
        "x": 0, "y": 0, "resize_source": False, "mask": ["301", 0]}},
    "255": {"class_type": "SaveImage", "inputs": {"images": ["320", 0], "filename_prefix": "inpainted"}},
}
```

> ⚠️ **禁止用"裁剪局部→重绘→贴回"或"程序采样填充"**：贴回会产生边界错位，程序填充纹理不融合。一律整图 + 遮罩。

---

## 五、视频生成

**模型**：MiniMax H3 fl2va（首帧生视频）+ turbo 8 步 LoRA

### 5.1 绿幕角色动画（推荐流程，后期好抠像）

```python
# 首帧图放 input（如绿幕猫姿势图）
shutil.copy2(r"D:\cat_green.png", INPUT + r"\first_frame.png")

PROMPT = open(r"D:\prompt.txt", encoding="utf-8").read()

WF = {
    "6":   {"class_type": "UNETLoader", "inputs": {
        "unet_name": "minimax_h3_fl2va_pruned_int8_convrot.safetensors", "weight_dtype": "default"}},
    "121": {"class_type": "LoraLoaderModelOnly", "inputs": {          # turbo 加速
        "model": ["6", 0],
        "lora_name": "minimax_h3_fl2v_turbo_8step_v1.0_comfyui_bf16.safetensors",
        "strength_model": 1.0}},
    "13":  {"class_type": "CLIPLoader", "inputs": {
        "clip_name": "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors",
        "type": "minimax", "device": "default"}},
    "11":  {"class_type": "VAELoader", "inputs": {"vae_name": "minimax_h3_video_vae_fp16.safetensors"}},
    "15":  {"class_type": "RandomNoise", "inputs": {"noise_seed": 42, "control_after_generate": "fixed"}},
    "200": {"class_type": "LoadImage", "inputs": {"image": "first_frame.png", "upload": "image"}},
    "104": {"class_type": "MiniMaxH3ImageToVideo", "inputs": {
        "clip": ["13", 0], "vae": ["11", 0],
        "first_frame": ["200", 0],                        # ← 首帧（唯一参考，别用双锁）
        "prompt": PROMPT, "width": 768, "height": 768, "length": 124}},  # 124帧≈5秒@24fps
    "16":  {"class_type": "BasicGuider", "inputs": {"model": ["121", 0], "conditioning": ["104", 0]}},
    "17":  {"class_type": "KSamplerSelect", "inputs": {"sampler_name": "res_multistep"}},
    "9":   {"class_type": "BasicScheduler", "inputs": {
        "model": ["121", 0], "scheduler": "simple", "steps": 8, "denoise": 1.0}},
    "14":  {"class_type": "SamplerCustomAdvanced", "inputs": {
        "noise": ["15", 0], "guider": ["16", 0], "sampler": ["17", 0],
        "sigmas": ["9", 0], "latent_image": ["104", 1]}},
    "10":  {"class_type": "VAEDecode", "inputs": {"samples": ["14", 0], "vae": ["11", 0]}},
    "91":  {"class_type": "CreateVideo", "inputs": {"images": ["10", 0], "fps": 24, "bit_depth": 8}},
    "92":  {"class_type": "SaveVideo", "inputs": {"video": ["91", 0], "filename_prefix": "my_video"}},
}

run(WF, r"D:\output\my_video.mp4", media_key="videos", exts=(".mp4",), timeout=900)
```

**经验参数**：
| 项 | 值 | 说明 |
|---|---|---|
| steps | 8 + turbo LoRA | 平衡速度/质量；4 步动作幅度小、背景易跑 |
| 首尾帧 | **只用首帧** | 双锁（首尾同图）会导致中段背景漂白 |
| 绿幕提示词 | 必须写 | "solid pure chroma-key green #00FF00, no texture/no gradient/no shadows, fur stays pure white no green spill" |
| 换背景不稳 | 换 seed | 某些 seed 会导致背景漂白，重跑即可 |
| 耗时 | ~3 分钟/5 秒视频 | 768×768@8 步 |

### 5.2 绿幕视频 → 透明序列帧（游戏用）

```python
# ffmpeg 抽帧 → 色键抠绿 → 序列帧
# ffmpeg 路径见"七、常见问题"
subprocess.run([FFMPEG, "-y", "-i", "my_video.mp4", "-vf", "scale=512:512",
                "frames/raw_%04d.png"])
for 每帧:
    dom = g - max(r, b)
    alpha = 0 if dom > 60 else (255 - (dom-15)*255/45 if dom > 15 else 255)
    # 边缘去绿污染: g > r+12 and g > b+12 时 g = max(r, b)
    # 24fps → 12fps: 每 2 帧取 1；→ 6fps: 每 4 帧取 1
```

---

## 六、音乐生成

**模型**：AceStep 1.5 turbo（⚠️ 必须用 1.5 专用节点，用错 v1.0 节点会报 `NoneType.shape`）

```python
PROMPT = "Chinese traditional, guqin solo, serene zen meditation, slow flowing melody, 60bpm"

WF = {
    # 双编码器！必须 DualCLIPLoader
    "105": {"class_type": "DualCLIPLoader", "inputs": {
        "clip_name1": "qwen_0.6b_ace15.safetensors",
        "clip_name2": "qwen_4b_ace15.safetensors",
        "type": "ace", "device": "default"}},
    "106": {"class_type": "VAELoader", "inputs": {"vae_name": "ace_1.5_vae.safetensors"}},
    "104": {"class_type": "UNETLoader", "inputs": {
        "unet_name": "acestep_v1.5_turbo.safetensors", "weight_dtype": "default"}},
    "78":  {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["104", 0], "shift": 3.1}},
    "98":  {"class_type": "EmptyAceStep1.5LatentAudio", "inputs": {"seconds": 60, "batch_size": 1}},
    "94":  {"class_type": "TextEncodeAceStepAudio1.5", "inputs": {   # ← 1.5 节点！不是 TextEncodeAceStepAudio
        "clip": ["105", 0], "tags": PROMPT, "lyrics": "",
        "seed": 42, "bpm": 120, "duration": 60,
        "timesignature": "4", "language": "en", "keyscale": "E minor",
        "generate_audio_codes": True, "cfg_scale": 2.0,
        "temperature": 0.85, "top_p": 0.9, "top_k": 0, "min_p": 0}},
    "47":  {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["94", 0]}},  # 负向条件
    "3":   {"class_type": "KSampler", "inputs": {
        "model": ["78", 0], "positive": ["94", 0], "negative": ["47", 0],
        "latent_image": ["98", 0], "seed": 42,
        "steps": 8, "cfg": 1.0,                      # turbo 模型 cfg 固定 1
        "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
    "18":  {"class_type": "VAEDecodeAudio", "inputs": {"samples": ["3", 0], "vae": ["106", 0]}},
    "17":  {"class_type": "SaveAudio", "inputs": {"audio": ["18", 0], "filename_prefix": "bgm"}},
}

run(WF, r"D:\output\bgm.wav", media_key="audio", exts=(".wav",), timeout=600)
```

**参数**：
- `tags`：曲风描述（乐器/情绪/节奏/BPM/调式），逗号分隔标签式写法效果最好
- `duration`/`seconds`：曲长（秒），一次最长约 360 秒
- 耗时约 50-70 秒/分钟音频

> 音效生成用 Stable Audio 3 Medium（`stable_audio_3_medium.safetensors`），本次未整理工作流，需要时在 ComfyUI 网页里用模板拖一个。

---

## 七、常见问题

| 问题 | 原因/解决 |
|---|---|
| 连接拒绝 (10061) | ComfyUI 没启动或崩了，用第一节命令重启 |
| `REJECTED: ...` 返回 400 | 工作流 JSON 有错：节点名拼错/模型文件名不存在。对照 `GET /object_info` 检查 |
| 报 `NoneType has no attribute 'shape'` | AceStep 用了 v1.0 节点，必须 `TextEncodeAceStepAudio1.5` + `DualCLIPLoader` |
| 生图 API 节点 (ByteDanceSeedream 等) 报认证错误 | 那是云端 API 节点本地不可用，用本手册的本地工作流 |
| 生成结果在哪 | `H:\ComfyUI_windows_portable\ComfyUI\output\{前缀}_{序号}_.png`，多张时按序号挑 |
| ffmpeg 路径 | `C:\Users\zhn\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0-full_build\bin\ffmpeg.exe`（在 PATH 里，直接 `ffmpeg` 也行） |
| 想要更多参数说明 | `GET /object_info/{节点名}` 查看节点完整定义 |
| 网页操作参考 | 打开 `http://127.0.0.1:8188` 用浏览器直接拖工作流；模板在 `H:\ComfyUI_windows_portable\ComfyUI\blueprints\` |

---

*文档基于 2026-08 实测整理（Unity 2022.3 / RTX 5060 Ti 16G / ComfyUI 0.33.1）*

---

## 八、背景移除（rembg）

> 路径：H:\ComfyUI_windows_portable\python_embeded\python.exe  
> 版本：rembg 2.0.81（已随 ComfyUI 安装）

### 8.1 命令行用法

`powershell
# 基础抠图：输入图片 → 输出透明 PNG
&  H:\ComfyUI_windows_portable\python_embeded\python.exe -m rembg i input.png output.png

# 批量抠图：整个文件夹
& H:\ComfyUI_windows_portable\python_embeded\python.exe -m rembg p input_folder/ output_folder/

# 指定模型（可选）
& H:\ComfyUI_windows_portable\python_embeded\python.exe -m rembg i -m u2net input.png output.png
`

### 8.2 Python 代码调用

`python
import subprocess, os

PYTHON = rH:\ComfyUI_windows_portable\python_embeded\python.exe

def rembg_remove(input_path, output_path):
    调用 rembg 抠图，返回输出路径
 subprocess.run([PYTHON, -m, rembg, i, input_path, output_path], check=True)
 return output_path

# 示例
rembg_remove(cat_portrait.png, cat_portrait_alpha.png)
`

### 8.3 可用模型

| 模型 | 适用场景 | 速度 |
|------|---------|------|
| u2net | 通用（默认） | 快 |
| u2net_human_seg | 人物 | 快 |
| u2net_cloth_seg | 衣物 | 快 |
| isnet-general-use | 高精度通用 | 中 |
| isnet-anime | 动漫/插画 | 中 |
| bria-rmbg | 商业级 | 慢 |

> 💡 **推荐**：猫猫立绘用 isnet-anime 或默认 u2net，效果好速度快。

### 8.4 ComfyUI 节点调用

在 ComfyUI 工作流中使用 **BRIA-RMBG** 或 **rembg** 节点：
- 输入：LoadImage 节点
- 输出：带 Alpha 通道的图片
- 可与其他节点串联（如 色键抠绿 → rembg 精修）

### 8.5 注意事项

- 首次运行会自动下载模型（约 100-400MB），存放在 ~/.u2net/ 目录
- 处理速度：单张约 1-3 秒（RTX 5060 Ti）
- 支持格式：PNG、JPG、WEBP 输入 → PNG 输出
- 如果报错 CUDA out of memory，加 --disable-gpu 参数用 CPU 模式

---

## 九、火山引擎 Seedream 生图（云端 API）

> 适用于角色立绘、图标等需要高画质的素材生成。
> 对比本地 ComfyUI：画质更高，但需要 API Key 且按量付费。

### 9.1 API 配置

> **API Key 存放位置**：E:\UnityProject\wuziqi\.env
>
> 文件内容格式：
> `env
> SEEDREAM_API_KEY=your_api_key_here
> `
>
> 脚本读取方式：open(".env").read().split("=", 1)[1].strip()

| 项目 | 值 |
|------|-----|
| **Base URL** | https://ark.cn-beijing.volces.com/api/plan/v3/images/generations |
| **模型** | doubao-seedream-5.0-lite |
| **API Key** | 从 .env 文件读取，不要硬编码在代码里 |
### 9.2 Python 调用模板

`python
import json, base64, urllib.request, os

# 从 .env 读取 Key
API_KEY = open(".env").read().split("=", 1)[1].strip()
API_URL = "https://ark.cn-beijing.volces.com/api/plan/v3/images/generations"

def generate_image(prompt, save_path, reference_image=None, seed=42):
    """文生图或图生图，返回保存路径"""
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {API_KEY}"
    }
    body = {
        "model": "doubao-seedream-5.0-lite",
        "prompt": prompt,
        "size": "2k",
        "response_format": "b64_json",
        "watermark": False,
        "seed": seed
    }
    # 图生图：传参考图保持画风
    if reference_image:
        with open(reference_image, "rb") as f:
            img_b64 = base64.b64encode(f.read()).decode()
        ext = os.path.splitext(reference_image)[1].lower()
        mime = {"png": "image/png", "jpg": "image/jpeg", "jpeg": "image/jpeg"}.get(ext, "image/png")
        body["image"] = f"data:{mime};base64,{img_b64}"

    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(API_URL, data=data, headers=headers)
    with urllib.request.urlopen(req, timeout=120) as resp:
        result = json.loads(resp.read())

    for img_data in result.get("data", []):
        if "b64_json" in img_data:
            img_bytes = base64.b64decode(img_data["b64_json"])
            with open(save_path, "wb") as f:
                f.write(img_bytes)
            print(f"Saved: {save_path} ({len(img_bytes)//1024}KB)")
            return save_path

# 文生图
generate_image("水墨画风格的古琴图标", "output.png")

# 图生图（传参考图保持画风）
generate_image("思考的表情", "output.png", reference_image="ref_portrait.png")
`

### 9.3 本项目生图工作流

#### 角色立绘/表情（推荐流程）

1. **用大头照做参考图**（图生图保持画风一致）
2. **提示词模板**（以橘座为例）：

`
传统中国水墨工笔画风格，淡彩设色，宣纸笔墨质感。
一只胖嘟嘟的橘色虎斑猫，戴着红色项圈挂着金色铃铛。
[姿势/表情描述]。
橘色和奶油白色相间的虎斑花纹，白肚皮。
背景纯绿色幕布(#00FF00)，纯色无纹理无渐变无阴影。
笔触写意墨色浓淡变化。古风手绘可爱风。
猫占画面七成，居中构图。无文字无其他物件。
`

3. **后处理**：色键抠绿 → 缩放到 512×512 → 导入 Unity

#### 6 个情绪的姿势参考

| 情绪 | 姿势描述 |
|------|---------|
| idle（待机） | 坐姿闭眼睡觉或慵懒伸懒腰 |
| thinking（思考） | 坐姿，一只爪子抬到下巴处做出思考的动作，眼睛向上看 |
| smug（得意） | 二郎腿坏笑，得意洋洋的表情 |
| worried（担心） | 缩脖炸毛，紧张不安的表情 |
| celebrate（庆祝） | 跳起欢呼，开心的表情 |
| defeat（失败） | 趴伏摊滩，沮丧的表情 |

### 9.4 参数速查

| 参数 | 可选值 | 说明 |
|------|--------|------|
| model | doubao-seedream-5.0-lite | 当前使用的模型 |
| size | 2k / 3k / 4k | 图片分辨率 |
| response_format | url / 64_json | 返回格式，建议用 b64_json |
| watermark | 	rue / alse | 是否加水印 |
| seed | 整数 | 固定 seed 可复现 |
| image | Base64 或 URL | 参考图（图生图） |

### 9.5 本地 vs 云端选择

| 对比项 | 本地 ComfyUI | 火山引擎 Seedream |
|--------|-------------|-----------------|
| 费用 | 免费（电费） | 按量付费 |
| 画质 | 依赖模型 | 更高（5.0-lite） |
| 速度 | 取决于显卡 | 稳定 5-15 秒 |
| 适用场景 | 快速迭代、视频生成 | 高质量立绘、图标 |
| 网络 | 不需要 | 需要联网 |

> 💡 **建议**：日常迭代用本地 ComfyUI，最终成品用火山引擎出高画质版本。
