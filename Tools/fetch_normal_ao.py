# -*- coding: utf-8 -*-
"""下载 Poly Haven 资产的法线(nor_gl)与 AO 贴图（1k jpg）"""
import io
import json
import os
import sys
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

API = "https://api.polyhaven.com"
SAVE_DIR = r"E:\UnityProject\Dapaolou\Assets\Textures\Terrain"

ASSETS = {
    "concrete": "concrete_floor_worn_001",
    "dirt": "brown_mud_leaves_01",
    "grass": "aerial_grass_rock",
    "sand": "sand_01",
    "brick": "brick_4",
}


def get_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())


def download(url, save_to):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=120) as r:
        data = r.read()
    with open(save_to, "wb") as f:
        f.write(data)
    return len(data)


def pick(entry):
    """从贴图条目里选 1k jpg"""
    if entry is None:
        return None
    res = entry.get("1k") or entry.get("2k")
    return res.get("jpg") if res else None


def main():
    os.makedirs(SAVE_DIR, exist_ok=True)
    for theme, asset in ASSETS.items():
        try:
            files = get_json(f"{API}/files/{asset}")
            for map_key, suffix in (("nor_gl", "normal"), ("AO", "ao")):
                out = os.path.join(SAVE_DIR, f"{theme}_{suffix}.jpg")
                if os.path.exists(out):
                    print(f"[{theme}.{suffix}] 已存在，跳过", flush=True)
                    continue
                jpg = pick(files.get(map_key))
                if jpg is None:
                    print(f"[{theme}.{suffix}] 无此贴图", flush=True)
                    continue
                size = download(jpg["url"], out)
                print(f"[{theme}.{suffix}] {size // 1024}KB", flush=True)
        except Exception as e:
            print(f"[{theme}] 失败: {e}", flush=True)
    print("完成", flush=True)


if __name__ == "__main__":
    main()
