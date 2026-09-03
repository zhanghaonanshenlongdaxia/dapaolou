# -*- coding: utf-8 -*-
"""打炮楼 - 本地混元3D 批量生成基础模型
用法: python gen_models.py [--only name1,name2]
依赖: 已启动的 Hunyuan3D api_server (http://127.0.0.1:8081)
输出: Assets/Models/AIGen/{name}.glb
"""
import base64
import json
import os
import sys
import time
import urllib.request

COMFY = "http://127.0.0.1:8081"
PROJECT = r"E:\UnityProject\Dapaolou"
OUT_DIR = os.path.join(PROJECT, "Assets", "Models", "AIGen")

# 打炮楼基础建模清单：name -> (中文说明, 提示词)
MODELS = {
    "house":  ("农村平房", "low poly game asset, single-story chinese rural house with gray tiled roof, wooden door and window, weathered white walls, clean geometry, stylized game asset, full building, isolated on white background"),
    "wall":   ("院墙围墙段", "low poly game asset, old gray brick courtyard wall segment with coping on top, weathered bricks, clean geometry, stylized, isolated on white background"),
    "tree":   ("大树", "low poly game asset, large tree with thick brown trunk and big round green foliage, stylized game tree, clean geometry, isolated on white background"),
    "vat":    ("水缸", "low poly game asset, large ceramic water vat jar, dark brown glaze, wide round body, clean geometry, isolated on white background"),
    "doghouse": ("狗窝", "low poly game asset, small wooden dog house with sloped red roof and dark entrance, clean geometry, stylized, isolated on white background"),
    "fence":  ("牛圈栅栏段", "low poly game asset, wooden cattle pen fence segment, weathered wood posts and two horizontal rails, clean geometry, stylized, isolated on white background"),
    "mom":    ("妈妈NPC", "low poly 3d character, middle aged chinese rural woman wearing apron and headscarf, stylized game character, T-pose, full body, isolated on white background"),
    "cow":    ("牛", "low poly 3d character, yellow farm ox standing, stylized game asset, full body side view, four legs, isolated on white background"),
    "dog":    ("臭豆(狗)", "low poly 3d character, small yellow mongrel dog standing, stylized game asset, full body, four legs, isolated on white background"),
    "cat":    ("骚咪(猫)", "low poly 3d character, orange tabby cat standing, stylized game asset, full body, four legs, isolated on white background"),
}


def post(url, payload, timeout=30):
    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read())


def get(url, timeout=30):
    with urllib.request.urlopen(url, timeout=timeout) as r:
        return json.loads(r.read())


def send_and_wait(name, prompt, timeout=900):
    """提交文生3D任务并轮询，成功返回 GLB 字节"""
    resp = post(f"{COMFY}/send", {"text": prompt, "texture": True,
                                  "octree_resolution": 256,
                                  "num_inference_steps": 30})
    uid = resp["uid"]
    print(f"  [{name}] 提交 uid={uid}", flush=True)
    start = time.time()
    while True:
        time.sleep(5)
        try:
            st = get(f"{COMFY}/status/{uid}")
        except Exception as e:
            print(f"  [{name}] 轮询异常 {e}", flush=True)
            continue
        if st.get("status") == "completed":
            glb = base64.b64decode(st["model_base64"])
            out = os.path.join(OUT_DIR, f"{name}.glb")
            with open(out, "wb") as f:
                f.write(glb)
            print(f"  [{name}] 完成 -> {out} ({len(glb)//1024}KB)", flush=True)
            return out
        if time.time() - start > timeout:
            raise TimeoutError(f"[{name}] 超时")
        print(f"  [{name}] 生成中... {int(time.time()-start)}s", flush=True)


def main():
    only = None
    for a in sys.argv[1:]:
        if a.startswith("--only "):
            only = a.split(" ", 1)[1].split(",")
    os.makedirs(OUT_DIR, exist_ok=True)
    todo = {k: v for k, v in MODELS.items() if only is None or k in only}
    for name, (desc, prompt) in todo.items():
        out = os.path.join(OUT_DIR, f"{name}.glb")
        if os.path.exists(out):
            print(f"[{name}] 已存在，跳过", flush=True)
            continue
        print(f"[{name}] {desc} 开始生成", flush=True)
        try:
            send_and_wait(name, prompt)
        except Exception as e:
            print(f"[{name}] 失败: {e}", flush=True)
    print("全部任务结束", flush=True)


if __name__ == "__main__":
    main()
