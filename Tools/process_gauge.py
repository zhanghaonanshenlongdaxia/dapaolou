# -*- coding: utf-8 -*-
"""仪表盘贴图后处理：圆外透明 + 紧裁剪"""
from PIL import Image, ImageDraw, ImageFilter

SRC = r"E:\UnityProject\Dapaolou\TempAIGen\gauge_raw.png"
DST = r"E:\UnityProject\Dapaolou\Assets\UI\gauge_dial.png"

im = Image.open(SRC).convert("RGB")
w, h = im.size

# 找非白色像素的包围盒（圆盘边界）
px = im.load()
minx, miny, maxx, maxy = w, h, 0, 0
for y in range(0, h, 2):
    for x in range(0, w, 2):
        r, g, b = px[x, y]
        if not (r > 240 and g > 240 and b > 240):
            if x < minx: minx = x
            if x > maxx: maxx = x
            if y < miny: miny = y
            if y > maxy: maxy = y

cx, cy = (minx + maxx) // 2, (miny + maxy) // 2
radius = max(maxx - minx, maxy - miny) // 2
print(f"dial center=({cx},{cy}) radius={radius}")

# 圆形遮罩（边缘 3px 羽化）+ 裁剪到正方形
mask = Image.new("L", (w, h), 0)
ImageDraw.Draw(mask).ellipse([cx - radius, cy - radius, cx + radius, cy + radius], fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(2))

rgba = im.convert("RGBA")
rgba.putalpha(mask)

pad = 6
box = (max(0, cx - radius - pad), max(0, cy - radius - pad),
       min(w, cx + radius + pad), min(h, cy + radius + pad))
out = rgba.crop(box)

import os
os.makedirs(os.path.dirname(DST), exist_ok=True)
out.save(DST)
print(f"saved: {DST} {out.size}")
