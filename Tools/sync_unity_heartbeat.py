# -*- coding: utf-8 -*-
# Codely 桥接版本错位修复：把 Temp/.com-unity-codely.json（bridge 1.0.81+ 的真实心跳）
# 镜像到项目根 .com-unity-codely.json（旧 CLI 客户端只读根路径 + last_heartbeat 字段）。
# 用法：Unity 重启后若 Codely 连不上，跑一次本脚本再重试。
# 背景：2026-09-04 桥接包 1.0.80→1.0.81 自动升级后心跳文件迁到 Temp/ 且字段改名
#      (last_heartbeat→last_updated)，旧客户端读不到导致 "No valid Unity config found"。
# 根治：等 Cowork/CLI 升级到匹配 1.0.81 的版本后，本脚本与根目录垫片均可删除
#      （同时可移除用户环境变量 CODELY_UNITY_HEARTBEAT_MAX_AGE_S）。
import json
import os
import datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # 项目根
SRC = os.path.join(ROOT, "Temp", ".com-unity-codely.json")
DST = os.path.join(ROOT, ".com-unity-codely.json")


def main():
    if not os.path.exists(SRC):
        raise SystemExit(f"[sync_unity_heartbeat] 未找到 {SRC} —— Unity 编辑器没开或桥接未就绪")
    with open(SRC, encoding="utf-8") as f:
        src = json.load(f)
    if src.get("reason") != "ready" or not src.get("unity_port", 0) > 0:
        raise SystemExit(f"[sync_unity_heartbeat] 桥接未就绪: {src}")
    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"
    shim = {
        "unity_port": src["unity_port"],
        "unity_host": "localhost",
        "stream_port": src["unity_port"],
        "created_date": src.get("last_updated", now),
        "project_path": src.get("project_path", ROOT.replace("\\", "/") + "/Assets"),
        "reloading": src.get("reloading", False),
        "reason": src.get("reason", "ready"),
        "seq": int(datetime.datetime.now().timestamp()),
        "last_heartbeat": now,
    }
    with open(DST, "w", encoding="utf-8") as f:
        json.dump(shim, f, indent=2)
    print(f"[sync_unity_heartbeat] OK -> {DST}")
    print(json.dumps(shim, indent=2))


if __name__ == "__main__":
    main()
