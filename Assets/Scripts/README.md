# 打炮楼游戏 - 脚本说明

## 项目结构

```
Assets/Scripts/
├── Game/                    # 游戏核心逻辑
│   ├── GameManager.cs       # 游戏管理器
│   ├── GameConfig.cs        # 游戏配置
│   ├── PlayerData.cs        # 玩家数据
│   └── TowerBuilder.cs      # 炮楼建造器
├── Marble/                  # 弹珠系统
│   ├── MarbleData.cs        # 弹珠数据
│   ├── MarbleShooter.cs     # 弹珠发射器
│   ├── MarbleCollisionHandler.cs  # 碰撞处理
│   └── HandTremorSystem.cs  # 手抖系统（类似荒野的召唤）
├── Player/                  # 玩家系统
│   ├── FirstPersonController.cs  # 第一人称控制器
│   ├── ThirdPersonModel.cs  # 第三人称模型
│   └── PlayerManager.cs     # 玩家管理器
├── Terrain/                 # 地形系统
│   └── TerrainEffectSystem.cs  # 地形效果
└── UI/                      # UI系统
    └── GameUIManager.cs     # 游戏UI管理器
```

## 核心玩法

### 炮楼结构
- **底部**：3个弹珠呈三角形排列
- **顶部**：1个弹珠放在底部上方
- **总弹珠数**：4个

### 小兵系统
- 每个玩家3个小兵弹珠
- 小兵用于攻击对方炮楼和小兵

### 攻击规则
1. **弹射**：类似高尔夫，按住鼠标蓄力，松开发射
2. **手抖**：类似《荒野的召唤》，手会抖动影响方向
3. **打爆判定**：
   - 一次打爆：力度足够大
   - 二次打爆：炮楼剩1-3个弹珠时需要二次攻击
4. **拆炮楼**：小兵全灭后，可以拆自己炮楼补充小兵

### 地形效果
- **水泥地**：正常，速度最快
- **泥土地**：坑洼、上坡，速度减慢
- **草堆**：减速效果明显
- **松散土地**：容易陷住弹珠

## Unity设置步骤

### 1. 创建场景层级
```
场景层级：
├── --- 游戏管理 ---
│   ├── GameManager
│   ├── TerrainEffectSystem
│   └── GameUIManager
├── --- 玩家 ---
│   ├── Player1
│   │   ├── FirstPersonController
│   │   ├── MarbleShooter
│   │   ├── HandTremorSystem
│   │   ├── PlayerManager
│   │   ├── Camera
│   │   └── ThirdPersonModel (子对象)
│   └── Player2
│       └── ... (同上)
├── --- 场景 ---
│   ├── Ground (地形)
│   │   ├── CementArea (水泥地)
│   │   ├── DirtArea (泥土地)
│   │   ├── GrassArea (草堆)
│   │   └── SandArea (松散土地)
│   ├── SpawnPoints
│   │   ├── Player1Spawn
│   │   ├── Player2Spawn
│   │   ├── Tower1Spawn
│   │   ├── Tower2Spawn
│   │   ├── Soldier1Spawn
│   │   └── Soldier2Spawn
│   └── Environment (场景装饰)
└── --- UI ---
    └── Canvas
        ├── PlayerInfo
        ├── PowerBar
        ├── TremorIndicator
        ├── Hints
        └── GameOverPanel
```

### 2. 创建预制体

#### 弹珠预制体 (MarblePrefab)
```
创建步骤：
1. 创建Sphere对象
2. 设置Scale为(0.05, 0.05, 0.05) - 5cm直径
3. 添加组件：
   - Rigidbody (mass: 0.1)
   - SphereCollider
   - MarbleData
   - MarbleCollisionHandler
4. 创建PhysicMaterial:
   - Dynamic Friction: 0.4
   - Static Friction: 0.4
   - Bounciness: 0.3
5. 保存为预制体
```

### 3. 设置地形

#### 创建地形标签
```
1. 创建空对象，命名为地形区域
2. 添加TerrainTag组件
3. 设置地形类型
4. 添加BoxCollider作为触发器
5. 设置Layer为"Terrain"
```

#### 地形材质示例
```
水泥地：灰色材质，光滑
泥土地：棕色材质，有凹凸贴图
草堆：绿色材质，带草地纹理
松散土地：浅棕色，沙地纹理
```

### 4. 设置玩家

#### 第一人称玩家
```
1. 创建空对象，命名为Player1
2. 添加CharacterController
3. 添加FirstPersonController脚本
4. 添加MarbleShooter脚本
5. 添加HandTremorSystem脚本
6. 添加PlayerManager脚本
7. 创建子对象Camera，设置为Main Camera
8. 创建子对象ThirdPersonModel
```

#### 第三人称模型
```
1. 在Player下创建空对象，命名为ThirdPersonModel
2. 添加ThirdPersonModel脚本
3. 运行时会自动生成基本模型
4. 或手动创建：
   - Body (Cube)
   - Head (Sphere)
   - Arms (Cube)
   - Legs (Cube)
```

### 5. 设置UI

#### 创建Canvas
```
1. 创建Canvas
2. 设置Canvas Scaler为Scale With Screen Size
3. 参考分辨率：1920x1080
```

#### UI元素
```
Canvas:
├── PlayerInfoPanel (左上角)
│   ├── PlayerNameText
│   ├── PlayerScoreText
│   ├── SoldierCountText
│   └── TowerCountText
├── PowerBarPanel (底部中央)
│   ├── PowerSlider
│   └── PowerText
├── TremorIndicator (准星位置)
│   └── TremorCircle (Image)
├── HintText (中央)
├── TurnTimerText (右上角)
└── GameOverPanel (中央，默认隐藏)
    ├── WinnerText
    └── RestartButton
```

### 6. 配置GameManager

```
1. 创建空对象，命名为GameManager
2. 添加GameManager脚本
3. 配置参数：
   - Player Count: 2
   - Player Colors: [蓝色, 红色]
   - Tower Spawn Points: [Tower1Spawn, Tower2Spawn]
   - Soldier Spawn Points: [Soldier1Spawn, Soldier2Spawn]
4. 创建GameConfig ScriptableObject
5. 拖拽到GameManager的配置槽
```

## 手抖系统详解

### 类似《荒野的召唤》的效果
- **基础稳定性**：角色固有的稳定性
- **紧张程度**：瞄准时、被瞄准时增加
- **疲劳程度**：游戏时间越长越疲劳
- **地形影响**：不同地形影响稳定性
- **风力影响**：可选，模拟风力

### 调试手抖
```
在HandTremorSystem组件上：
- Base Stability: 0.8 (基础稳定性)
- Nervousness: 0-1 (紧张程度)
- Fatigue: 0-1 (疲劳程度)
- Terrain Effect: 0-1 (地形影响)
```

## 力度系统详解

### 高尔夫式蓄力
- 按住鼠标左键开始蓄力
- 力度条往返移动（类似高尔夫游戏）
- 松开鼠标发射
- 力度决定弹珠速度

### 力度条UI
```
颜色渐变：
- 绿色 (0-33%): 轻力度
- 黄色 (34-66%): 中力度
- 红色 (67-100%): 重力度
```

## 调试技巧

### 1. 测试弹珠物理
```csharp
// 在Console中测试
GameManager.Instance.GetCurrentPlayer().soldierMarbles[0].GetComponent<Rigidbody>().AddForce(Vector3.forward * 10f, ForceMode.Impulse);
```

### 2. 测试手抖
```csharp
// 调整手抖参数
HandTremorSystem tremor = FindObjectOfType<HandTremorSystem>();
tremor.SetNervousness(0.8f);  // 高紧张度
tremor.SetFatigue(0.5f);      // 中等疲劳
```

### 3. 测试地形效果
```csharp
// 在TerrainTag对象上调整地形类型
TerrainTag tag = FindObjectOfType<TerrainTag>();
tag.terrainType = TerrainType.LooseSand;  // 改为松散土地
```

## 扩展功能

### 1. 添加音效
- 弹珠碰撞音效
- 弹射音效
- 地形滚动音效
- 背景音乐

### 2. 添加特效
- 碰撞火花
- 弹珠轨迹
- 地形粒子
- 胜利特效

### 3. 网络多人
- 使用Mirror或Photon
- 同步弹珠位置
- 同步回合状态

### 4. AI对手
- 简单的瞄准AI
- 力度控制AI
- 策略AI（选择目标）

## 常见问题

### Q: 弹珠穿过了地面？
A: 确保：
1. 地面有Collider组件
2. 弹珠的Collision Detection设置为Continuous Dynamic
3. 地面Layer设置正确

### Q: 炮楼倒塌了？
A: 调整：
1. TowerBuilder的Stability Force参数
2. 弹珠的Mass参数
3. FixedJoint的Break Force

### Q: 手抖效果不明显？
A: 调整：
1. HandTremorSystem的参数
2. 确保启用了enableTremor
3. 检查是否有其他脚本覆盖了方向

### Q: 力度条不显示？
A: 检查：
1. MarbleShooter的Power Slider引用
2. UI Canvas是否激活
3. Slider组件是否正确配置

## 下一步开发

1. **美术资源**
   - 替换默认几何体为低多边形模型
   - 创建材质和纹理
   - 添加环境装饰

2. **音效系统**
   - 添加碰撞音效
   - 添加环境音效
   - 添加UI音效

3. **特效系统**
   - 碰撞粒子特效
   - 弹珠轨迹特效
   - 地形交互特效

4. **网络同步**
   - 选择网络方案（Mirror/Photon）
   - 同步核心游戏状态
   - 处理延迟和预测

5. **AI系统**
   - 简单的AI对手
   - 不同难度级别
   - 策略选择

---

**祝开发顺利！** 🎮
