# -*- coding: utf-8 -*-
"""从 Poly Haven 下载 CC0 地形纹理（diffuse 1k/2k）"""
import io
import json
import os
import sys
import urllib.request

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

API = "https://api.polyhaven.com"
SAVE_DIR = r"E:\UnityProject\Dapaolou\Assets\Textures\Terrain"

# 目标资产名（Poly Haven 已知资产，按主题挑选）
WANTED = {
    "concrete": "concrete_floor_worn_001",   # 水泥地面
    "dirt": "brown_mud_leaves_01",           # 泥地
    "grass": "grass_medium_01",              # 草地（fallback: forest_ground_01）
    "sand": "sand_01",                       # 沙土
    "brick": "brick_wall_014",               # 砖墙
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


def main():
    os.makedirs(SAVE_DIR, exist_ok=True)

    # 拉取全部纹理资产清单
    print("获取 Poly Haven 资产清单...", flush=True)
    assets = get_json(f"{API}/assets?type=textures")
    print(f"共 {len(assets)} 个纹理资产", flush=True)

    results = {}
    for theme, want in WANTED.items():
        if want in assets:
            results[theme] = want
            print(f"[{theme}] {want} OK", flush=True)
            continue
        # fallback：按关键词在资产名里找
        keys = {"concrete": ["concrete"], "dirt": ["mud", "dirt"], "grass": ["grass"],
                "sand": ["sand"], "brick": ["brick"]}
        found = None
        for name in sorted(assets.keys()):
            if any(k in name for k in keys[theme]):
                found = name
                break
        if found:
            results[theme] = found
            print(f"[{theme}] fallback -> {found}", flush=True)
        else:
            print(f"[{theme}] 未找到匹配资产", flush=True)

    # 下载 Diffuse 1k jpg
    print("---下载---", flush=True)
    for theme, asset in results.items():
        try:
            files = get_json(f"{API}/files/{asset}")
            entry = files.get("Diffuse", {})
            # 优先 1k
            res = entry.get("1k") or entry.get("2k") or (list(entry.values())[0] if entry else None)
            if res is None:
                print(f"[{theme}] 无 Diffuse 贴图", flush=True)
                continue
            jpg = res.get("jpg")
            if jpg is None:
                continue
            url = jpg["url"]
            out = os.path.join(SAVE_DIR, f"{theme}.jpg")
            size = download(url, out)
            print(f"[{theme}] {asset} -> {out} ({size // 1024}KB)", flush=True)
        except Exception as e:
            print(f"[{theme}] 下载失败: {e}", flush=True)

    print("完成", flush=True)


if __name__ == "__main__":
    main()
