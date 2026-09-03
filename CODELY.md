

## Codely Structured Memories

### User
- [2026-09-03 04:02:48] 用户本地部署了混元(Hunyuan)3D 大模型，Unity 项目需要 3D 模型资产时可直接用本地混元生成（generate_3d_model provider=hunyuan 或 TJGenerators），无需询问是否用付费云端服务。
- [2026-09-03 13:29:09] 用户是试玩驱动型开发者：每轮改动后会实际 Play 试玩，反馈具体手感/视觉问题并附截图（.codely/clipboard）。**How to apply:** 涉及 UI/手感/镜头的改动，完成后主动提示用户试玩验证；处理反馈时先看截图再分析代码。

### Feedback
- [2026-09-03 05:03:28] Git 工作流：只负责 commit，push 由用户自己执行（"你提交就行推送我来"）。**Why:** 用户想自己控制推送到 GitHub 的时机（需要开梯子）。**How to apply:** 完成变更后 commit 即可，提醒用户推送，不要主动 git push。
- [2026-09-03 05:39:56] 打炮楼游戏手感偏好：①辅助线要台球式贴地（不要空中悬浮线）②镜头保持第一人称沉浸感，对手回合也不自动运镜，让玩家移动走位自由观看 ③AI 要有拟人节奏（瞄准准备时间、发射后弹珠特写），不要瞬间完成。**Why:** 用户明确要求"像打台球""镜头切到主角身上""要有瞄准准备时间"，重视代入感和节奏感。**How to apply:** 后续做镜头、UI 提示、手感相关的功能时优先考虑沉浸感与节奏，避免突兀的自动运镜。

### Project
- [2026-09-03 12:04:47] Unity 技术陷阱：本项目场景中的组件（如 Player2 的 AIPlayerController）带序列化字段，修改代码里的字段默认值对已存在场景实例不生效（如 thinkDelay、powerRange 都踩过此坑）。**How to apply:** 改 [SerializeField] 字段默认值后，必须用 exec_editor_script 通过 SerializedObject 更新场景实例的值再保存，不能只改代码。
- [2026-09-03 13:29:09] Unity Play 验证陷阱：exec_runtime_script 结束后 Play 会话会继续运行（不自动退出），连续验证会互相污染状态（弹珠被消耗、排行榜旧值、回合漂移）。**How to apply:** 需要全新会话的验证前先 unity_editor stop 再跑脚本；或在脚本开头检查状态是否延续（如库存/可用弹珠数）并主动重开。
- [2026-09-03 17:13:10] 本地混元3D部署状态（未完成，暂停中）：部署在 D:\AI-3D\Hunyuan3D\Hunyuan3D-2（官方仓库，含 api_server.py，POST /send + GET /status/{uid}，默认端口 8081，返回 GLB base64）。用 E:\AI-3D\miniconda3\envs\triposr 的 python 启动（HF_HOME=E:\AI-3D\cache\huggingface，tencent/Hunyuan3D-2 权重已缓存）。**已修复依赖**：onnxruntime/trimesh/omegaconf；**仍缺**：diffusers/transformers/gradio 等（安装进行到一半被中止）。批量生成脚本已写好：Tools/gen_models.py（10个基础模型清单：房子/院墙/树/水缸/狗窝/栅栏/妈妈/牛/狗/猫，输出 Assets/Models/AIGen/）。**继续方式**：补装 diffusers transformers opencv-python tqdm accelerate gradio pymeshlab pygltflib xatlas 后启动 api_server.py --port 8081 --enable_tex，然后跑 gen_models.py。

### Reference
- [2026-09-03 03:44:36] Dapaolou(打炮楼) 3D物理弹珠对战游戏 — 完整开发手册在项目根目录「打炮楼游戏开发手册.md」，含玩法规则、时间系统(6:00-20:00)、NPC行为时间表(妈妈/牛/狗臭豆/猫骚咪/弟弟)、手抖/力度/地形系统详解与Unity设置指南；修改游戏系统前先查手册。
