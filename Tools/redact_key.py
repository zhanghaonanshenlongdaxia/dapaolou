# -*- coding: utf-8 -*-
"""tree-filter 用：替换手册中的 API Key 明文（幂等）"""
import os
import re
import sys

NAME = "本地AI生成操作手册.md"
if not os.path.exists(NAME):
    sys.exit(0)

s = open(NAME, encoding="utf-8").read()
s2 = re.sub(r"ark-[A-Za-z0-9\-]{20,}", "ark-****（Key 在本地 .env，勿写入文档）", s)
if s != s2:
    open(NAME, "w", encoding="utf-8").write(s2)
    print("redacted:", NAME)
