

## Codely Structured Memories

### User
- [2026-09-03 04:02:48] 用户本地部署了混元(Hunyuan)3D 大模型，Unity 项目需要 3D 模型资产时可直接用本地混元生成（generate_3d_model provider=hunyuan 或 TJGenerators），无需询问是否用付费云端服务。
- [2026-09-04 03:31:10] 用户是试玩驱动型开发者：每轮改动后会实际 Play 试玩，反馈具体手感/视觉问题并附截图（.codely/clipboard），且会自己在编辑器里手动调整场景/UI（布局、贴图、删对象）。**How to apply:** 涉及 UI/手感/镜头的改动，完成后主动提示用户试玩验证；处理反馈时先看截图再分析代码；**批量重建/修改场景对象前必须确认不会覆盖用户的手动编辑**（用户明确说过"你别还原"）。


### Feedback
- [2026-09-03 05:03:28] Git 工作流：只负责 commit，push 由用户自己执行（"你提交就行推送我来"）。**Why:** 用户想自己控制推送到 GitHub 的时机（需要开梯子）。**How to apply:** 完成变更后 commit 即可，提醒用户推送，不要主动 git push。
- [2026-09-03 05:39:56] 打炮楼游戏手感偏好：①辅助线要台球式贴地（不要空中悬浮线）②镜头保持第一人称沉浸感，对手回合也不自动运镜，让玩家移动走位自由观看 ③AI 要有拟人节奏（瞄准准备时间、发射后弹珠特写），不要瞬间完成。**Why:** 用户明确要求"像打台球""镜头切到主角身上""要有瞄准准备时间"，重视代入感和节奏感。**How to apply:** 后续做镜头、UI 提示、手感相关的功能时优先考虑沉浸感与节奏，避免突兀的自动运镜。
- [2026-09-04 03:31:29] 复制外部项目的文档/脚本进仓库前，先检查是否含 API Key 等敏感信息——本次复制五子棋的操作手册时把火山引擎 API Key 明文带入仓库，GitHub push protection 拦截，被迫 filter-branch 重写历史。**How to apply:** git add 前对外来文件 grep 一遍 ark-/sk-/key 等模式；确认含 Key 时先打码或排除。

### Project
- [2026-09-03 12:04:47] Unity 技术陷阱：本项目场景中的组件（如 Player2 的 AIPlayerController）带序列化字段，修改代码里的字段默认值对已存在场景实例不生效（如 thinkDelay、powerRange 都踩过此坑）。**How to apply:** 改 [SerializeField] 字段默认值后，必须用 exec_editor_script 通过 SerializedObject 更新场景实例的值再保存，不能只改代码。
- [2026-09-03 13:29:09] Unity Play 验证陷阱：exec_runtime_script 结束后 Play 会话会继续运行（不自动退出），连续验证会互相污染状态（弹珠被消耗、排行榜旧值、回合漂移）。**How to apply:** 需要全新会话的验证前先 unity_editor stop 再跑脚本；或在脚本开头检查状态是否延续（如库存/可用弹珠数）并主动重开。
- [2026-09-03 18:00:17] 本地混元3D部署状态（未完成，暂停中）：部署在 D:\AI-3D\Hunyuan3D\Hunyuan3D-2（官方仓库，含 api_server.py，POST /send + GET /status/{uid}，默认端口 8081，返回 GLB base64）。用 E:\AI-3D\miniconda3\envs\triposr 的 python 启动（HF_HOME=E:\AI-3D\cache\huggingface，tencent/Hunyuan3D-2 权重已缓存）。**已修复依赖**：onnxruntime/trimesh/omegaconf；**仍缺**：diffusers/transformers/gradio 等（安装进行到一半被中止）。批量生成脚本已写好：Tools/gen_models.py（10个基础模型清单：房子/院墙/树/水缸/狗窝/栅栏/妈妈/牛/狗/猫，输出 Assets/Models/AIGen/）。**继续方式**：补装 diffusers transformers opencv-python tqdm accelerate gradio pymeshlab pygltflib xatlas 后启动 api_server.py --port 8081 --enable_tex，然后跑 gen_models.py。另：本地 ComfyUI 生图管线（Qwen-Image 4步，Tools/gen_gauge.py 为模板）已实测可用，油门盘贴图即此管线产出（Tools/process_gauge.py 做圆形抠图）。
- [2026-09-04 15:13:20] Unity 编辑器脚本陷阱：exec_editor_script 中通过代码创建的 GameObject 不会自动标记场景为 dirty（isDirty 保持 False），SaveOpenScenes 会跳过写入返回 True 但实际什么都没保存。**How to apply:** 所有 exec_editor_script 修改场景后，必须先调 EditorSceneManager.MarkSceneDirty(scene) 再 SaveOpenScenes，然后用 Python grep 磁盘文件确认对象名确实写入（不能用 isDirty 判断）。
- [2026-09-04 16:48:18] 音频生成（Job 15 已完成）：本地 ComfyUI AceStep 1.5 实际能完成推理——120s BGM 约 8-10 分钟、每条 SFX 约 2 分钟，只是比脚本客户端超时长。**关键陷阱**：SaveAudio 节点默认输出 .flac，而 Tools/gen_bgm.py、gen_sfx.py 只认 .wav，导致客户端误判 timeout（exit 1）但 ComfyUI 端其实 success——先查 /history/{prompt_id} 再重跑。已产出：Assets/Audio/bgm_countryside.wav（120s BGM）+ Assets/Audio/SFX/ 6 条（marble_hit/roll/flick、glass_shatter、water_splash、towerscatter 备用）。AudioManager 已挂场景并接线（Job 16 完成），调用点：MarbleShooter.ShootMarble（弹射）、MarbleCollisionHandler.PlayHit/PlayDestroyEffects（撞击/碎裂）、MarbleData.Update 地形切换入水（水声）。towerscatter 暂无字段未接线。纹理方案已转向 PolyHaven 下载（Tools/fetch_polyhaven.py + fetch_normal_ao.py）；本地 ComfyUI 生纹理（Tools/gen_textures.py）实测效果不佳，不要再用。


### Reference
- [2026-09-04 02:06:59] Dapaolou(打炮楼) 3D物理弹珠对战游戏 — 完整开发手册在项目根目录「打炮楼游戏开发手册.md」，含玩法规则、时间系统(6:00-20:00)、NPC行为时间表(妈妈/牛/狗臭豆/猫骚咪/弟弟)、手抖/力度/地形系统详解与Unity设置指南；修改游戏系统前先查手册。⚠️ 手册部分内容已过时：地形系统已升级 v3（TerrainType 5 种含 Puddle 水坑、水泥地分块+1.2cm 沟壕曲面填充、泥地 Perlin 网格嵌水坑盆、墙角三件套），以 TerrainEffectSystem.cs 与场景 YardTerrain 实际实现为准。

