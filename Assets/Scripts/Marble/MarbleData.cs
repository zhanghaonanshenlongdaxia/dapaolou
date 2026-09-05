using UnityEngine;
using Dapaolou.Game;

namespace Dapaolou.Marble
{
    /// <summary>
    /// 弹珠类型
    /// </summary>
    public enum MarbleType
    {
        Tower,      // 炮楼弹珠
        Soldier     // 小兵弹珠
    }

    /// <summary>
    /// 弹珠状态
    /// </summary>
    public enum MarbleState
    {
        Idle,       // 静止
        Rolling,    // 滚动中
        Destroyed   // 被打爆
    }

    /// <summary>
    /// 弹珠数据组件 - 挂载在每个弹珠上
    /// </summary>
    public class MarbleData : MonoBehaviour
    {
        [Header("弹珠属性")]
        public MarbleType marbleType = MarbleType.Soldier;
        public int ownerPlayerId = 0;           // 所属玩家ID
        public int towerIndex = -1;             // 炮楼中的位置索引（-1表示小兵）
        public int lastAttackerId = -1;         // 攻击链归属：被撞飞后代表哪个玩家（-1=无）
        public bool pendingCleanup = false;    // 待清理标记（炮楼散架后滚动停止即销毁）

        /// <summary>发射序号：每次发射盖唯一递增戳（0=未被发射过）。
        /// 攻守判定用——回合/state/速度在双回调+地形刹停下都会出现两回调结论相左</summary>
        public long shotSequence;
        public static long s_shotCounter;

        /// <summary>
        /// 有效攻击归属：被撞飞的弹珠代表把它撞飞的玩家（连环碰撞算原攻击者）
        /// </summary>
        public int GetEffectiveAttackerId()
        {
            return lastAttackerId >= 0 ? lastAttackerId : ownerPlayerId;
        }
        
        [Header("当前状态")]
        public MarbleState state = MarbleState.Idle;
        public bool isInvincible = false;       // 无敌状态（刚被打爆保护）
        
        [Header("物理参数")]
        public float baseMass = 0.1f;           // 基础质量（kg）
        public float bounciness = 0.3f;         // 弹性
        public float rollingFriction = 0.05f;   // 滚动摩擦

        [Header("坑洼检测")]
        [SerializeField] private float bumpCheckInterval = 0.05f;   // 坑洼检测频率（秒）
        [SerializeField] private float bumpHeightThreshold = 0.015f; // 坑洼高度阈值（米）
        [SerializeField] private float bumpSpeedLoss = 0.12f;       // 每次碰到坑洼的速度损失

        private Rigidbody rb;
        private Vector3 initialPosition;
        private float terrainCheckTimer = 0f;
        private float bumpCheckTimer = 0f;
        private float lastGroundHeight = 0f;
        private Terrain.TerrainType currentTerrain = Terrain.TerrainType.Cement;
        private float destroyedAt = -1f;        // 被摧毁的时刻（滚停后移除用）

        private bool removing = false;          // 正在播放消失动画
        private float removeStartAt = 0f;
        private Vector3 removeInitialScale;
        private float removeInitialAlpha;
        private const float RemoveDuration = 0.25f;  // 缩放+渐隐时长

        private Vector3 prevPhysPos;            // 上一物理步位置（水泥缝穿越检测用）
        private bool prevPhysPosValid = false;
        
        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            // 设置物理参数
            rb.mass = baseMass;
            rb.drag = 0f;                  // 空气阻力忽略；滚动阻力走恒定减速度模型（FixedUpdate）
            rb.angularDrag = 0.2f;         // 旋转阻力调低，保留玻璃的滚动感
            // 扫掠式 CCD：对静态墙地精确防穿透。动态×弹珠的穿透由 MarbleCollisionHandler
            // 的等半径球形扫掠兜底——Speculative 会按"速度×步长"膨胀碰撞圈（9.8m/s 时
            // 比弹珠本体大 4 倍），擦身而过也触发命中（实测从两小兵缝隙穿过却判双吃）
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0f;        // 永不睡眠：睡眠刚体会被高速弹珠穿透（CCD 不检测睡眠对）

            // 玻璃物理材质：高反弹 + 低摩擦（单一事实来源，覆盖各创建路径的默认材质）
            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.material = CreateGlassPhysicMaterial();
            }
        }

        /// <summary>
        /// 玻璃物理材质：Average 配对各表面材质（PM_Cement/PM_Dirt/PM_Wall 等）——
        /// bounce 按实测校准：玻璃-玻璃 COR 0.9（Physics Factbook），配对后水泥面 0.8、泥土 0.475
        /// </summary>
        public static PhysicMaterial CreateGlassPhysicMaterial()
        {
            PhysicMaterial mat = new PhysicMaterial("GlassMarble");
            mat.dynamicFriction = 0.04f;    // 玻璃珠表面极低摩擦（滑动顺滑）
            mat.staticFriction = 0.06f;     // 静止启动摩擦也很低
            mat.bounciness = 0.9f;          // 玻璃-玻璃实测 COR 0.90-0.95
            mat.frictionCombine = PhysicMaterialCombine.Average;   // 与表面材质取平均，让不同表面有不同手感
            mat.bounceCombine = PhysicMaterialCombine.Average;     // 同上：泥土吸能、水泥高弹
            return mat;
        }
        
        void Start()
        {
            initialPosition = transform.position;
        }

        void Update()
        {
            // 消失动画：缩放+透明渐隐，播完才真正销毁（避免弹珠凭空消失的突兀感）
            if (removing)
            {
                float t = (Time.time - removeStartAt) / RemoveDuration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                float k = 1f - t;
                transform.localScale = removeInitialScale * k;
                Renderer r = GetComponent<Renderer>();
                if (r != null)
                {
                    Color c = r.material.GetColor("_BaseColor");
                    c.a = removeInitialAlpha * k;
                    r.material.SetColor("_BaseColor", c);
                }
                return;   // 动画期间冻结其余逻辑
            }

            // 待清理（炮楼散架）：滚停即移除；滚出场地坠落也直接移除（速度>0.3 否则会悬空永不回收）
            if (pendingCleanup && rb != null)
            {
                if (transform.position.y < -2f)
                {
                    Destroy(gameObject);
                    return;
                }
                if (rb.velocity.magnitude < 0.3f)
                {
                    BeginRemoval();
                    return;
                }
            }

            // 已摧毁的弹珠：保持物理滚动/弹跳，滚停后播放消失动画再移除（超时 5s 强制）
            if (state == MarbleState.Destroyed && rb != null)
            {
                if (rb.velocity.magnitude < 0.3f || Time.time - destroyedAt > 5f)
                {
                    BeginRemoval();
                }
                return;
            }

            // 蠕动清零：被撞开的弹珠物理上在动但 state 仍是 Idle，不经过下面的 Rolling
            // 刹停分支——接地且速度/角速度低到不可见就彻底清零，能量归零不再蠕动
            if (!removing && state != MarbleState.Destroyed && rb != null
                && rb.velocity.magnitude < 0.05f && rb.angularVelocity.magnitude < 0.05f
                && Physics.Raycast(transform.position, Vector3.down, 0.06f))
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                if (state == MarbleState.Rolling)
                {
                    state = MarbleState.Idle;
                    lastAttackerId = -1;     // 停下后攻击链结束
                }
            }

            // 滚动中的弹珠：低速直接刹停，避免长时间蠕动导致回合等待过久
            if (state == MarbleState.Rolling && rb != null)
            {
                if (rb.velocity.magnitude < 0.3f)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    state = MarbleState.Idle;
                    lastAttackerId = -1;     // 停下后攻击链结束
                }
                else if (transform.position.y < -2f)
                {
                    // 滚出场地坠落：回收到出生点，避免回合永久卡死
                    ResetToInitial();
                }
                else
                {
                    // 地形检测（0.15s 一次）：记录脚下地形供恒定减速度模型使用；入水瞬间骤减
                    terrainCheckTimer -= Time.deltaTime;
                    if (terrainCheckTimer <= 0f && Terrain.TerrainEffectSystem.Instance != null)
                    {
                        terrainCheckTimer = 0.15f;
                        var t = Terrain.TerrainEffectSystem.Instance.GetTerrainType(transform.position);
                        if (t != currentTerrain)
                        {
                            if (t == Terrain.TerrainType.Puddle)
                            {
                                rb.velocity *= 0.5f;    // 入水骤减
                                if (Audio.AudioManager.Instance != null)
                                    Audio.AudioManager.Instance.PlayWaterSplash();
                            }
                            currentTerrain = t;
                        }
                    }

                    // 坑洼检测（仅泥土地/松土/草堆）：检测地面微小凹凸，反复减速模拟颠簸
                    if (currentTerrain == Terrain.TerrainType.Dirt ||
                        currentTerrain == Terrain.TerrainType.LooseSand ||
                        currentTerrain == Terrain.TerrainType.Grass)
                    {
                        bumpCheckTimer -= Time.deltaTime;
                        if (bumpCheckTimer <= 0f)
                        {
                            bumpCheckTimer = bumpCheckInterval;
                            CheckForBumps();
                        }
                    }
                }
            }
        }
        
        void FixedUpdate()
        {
            if (rb == null || rb.isKinematic) return;

            // 水泥缝改向：物理步间穿越缝隙线时施加轻微随机偏转（桥接盒已消除物理尖峰）
            if (!removing && !rb.IsSleeping() && prevPhysPosValid &&
                Terrain.TerrainEffectSystem.Instance != null &&
                Terrain.TerrainEffectSystem.TryGetSeamDeflection(prevPhysPos, transform.position, out float deflectDeg))
            {
                Vector3 hvDef = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
                if (hvDef.magnitude > 0.3f)
                {
                    float ang = deflectDeg * Mathf.Deg2Rad;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    rb.velocity = new Vector3(hvDef.x * cs - hvDef.z * sn, rb.velocity.y, hvDef.x * sn + hvDef.z * cs);
                }
            }
            prevPhysPos = transform.position;
            prevPhysPosValid = true;

            // 恒定滚动减速度模型（台球式）：a=Crr·g·k，方向恒反向水平速度。
            // 替代 rb.drag 指数衰减——真实滚动阻力是恒力，与速度无关（Engineering Toolbox 实测表）。
            // 旧版在 0.05m/s 以下不施力：配合永不睡眠（sleepThreshold=0），被撞开的 Idle 弹珠
            // 会以 ~4cm/s 蠕动数个回合不停——现在本步能刹停就直接清零，能量单调衰减到 0
            if (removing || rb.IsSleeping()) return;

            Vector3 hv = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (hv.magnitude <= 0.0001f) return;
            if (!Physics.Raycast(transform.position, Vector3.down, 0.06f)) return;   // 离地（弹跳/坠落）不受滚动阻力

            float decel = Terrain.TerrainEffectSystem.GetRollingDeceleration(currentTerrain);
            float dv = decel * Time.fixedDeltaTime;
            if (dv >= hv.magnitude)
            {
                // 本步足以刹停：直接清零水平速度，避免反向力在 0 附近抖动
                rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                rb.AddForce(-hv.normalized * (decel * rb.mass), ForceMode.Force);
            }
        }

        /// <summary>
        /// 开始消失动画：冻结物理、记录初始缩放/透明度，缩放+渐隐 0.25s 后销毁
        /// </summary>
        private void BeginRemoval()
        {
            if (removing) return;
            removing = true;
            removeStartAt = Time.time;
            removeInitialScale = transform.localScale;
            Renderer r = GetComponent<Renderer>();
            removeInitialAlpha = r != null ? r.material.GetColor("_BaseColor").a : 1f;
            if (rb != null) rb.isKinematic = true;   // 冻结物理，避免渐隐期间还参与碰撞
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        /// <summary>
        /// 坑洼检测：检测地面微小凹凸，模拟泥土地颠簸感
        /// </summary>
        private void CheckForBumps()
        {
            if (rb == null || rb.velocity.magnitude < 0.3f) return;

            // 获取滚动方向
            Vector3 moveDir = rb.velocity.normalized;
            moveDir.y = 0;
            if (moveDir.sqrMagnitude < 0.001f) return;

            // 检测当前位置地面高度
            float currentHeight;
            if (!GetGroundHeight(transform.position, out currentHeight)) return;

            // 检测前方 10cm 处地面高度
            Vector3 checkPos = transform.position + moveDir * 0.1f;
            float forwardHeight;
            if (!GetGroundHeight(checkPos, out forwardHeight)) return;

            // 计算高度差
            float heightDiff = forwardHeight - currentHeight;

            // 检测是否为凹凸（高度差超过阈值）
            if (Mathf.Abs(heightDiff) > bumpHeightThreshold)
            {
                // 凸起（上坡）：明显减速
                if (heightDiff > 0)
                {
                    rb.velocity *= (1f - bumpSpeedLoss * 1.5f);
                }
                // 凹陷（下坡）：轻微减速（颠簸感）
                else
                {
                    rb.velocity *= (1f - bumpSpeedLoss);
                }

                // 播放颠簸音效（如果有）
                // TODO: PlayBumpSound()
            }
        }

        /// <summary>
        /// 获取指定位置的地面高度
        /// </summary>
        private bool GetGroundHeight(Vector3 position, out float height)
        {
            height = 0;
            Ray ray = new Ray(position + Vector3.up * 0.5f, Vector3.down);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1f, LayerMask.GetMask("Terrain")))
            {
                height = hit.point.y;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查弹珠是否静止
        /// </summary>
        public bool IsStationary()
        {
            if (rb == null) return true;
            return rb.velocity.magnitude < 0.05f && rb.angularVelocity.magnitude < 0.05f;
        }
        
        /// <summary>
        /// 获取当前速度
        /// </summary>
        public float GetCurrentSpeed()
        {
            return rb != null ? rb.velocity.magnitude : 0f;
        }
        
        /// <summary>
        /// 完全停止弹珠
        /// </summary>
        public void StopMarble()
        {
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            state = MarbleState.Idle;
        }
        
        /// <summary>
        /// 被打爆
        /// </summary>
        public void OnDestroyed()
        {
            state = MarbleState.Destroyed;
            destroyedAt = Time.time;

            // 玻璃弹珠散架：断开自身关节，同炮楼的剩余弹珠也断开并标记待清理
            // （覆盖所有摧毁路径：碰撞/打爆炮楼/拆炮楼）
            foreach (var j in GetComponents<FixedJoint>())
                Destroy(j);

            var owner = GameManager.Instance != null
                ? GameManager.Instance.GetPlayer(ownerPlayerId)
                : null;
            if (owner != null && marbleType == MarbleType.Tower)
            {
                foreach (var m in owner.towerMarbles)
                {
                    if (m == null || m == this) continue;
                    foreach (var j in m.GetComponents<FixedJoint>())
                        Destroy(j);
                    m.pendingCleanup = true;   // 滚动停止后移除
                    m.destroyedAt = Time.time;  // 散架弹珠的超时计时起点
                }

                // 打掉一颗 = 整塔报废（打散了四颗都要消失）
                owner.towerDestroyed = true;
            }

            // TODO: 播放破碎特效
        }
        
        /// <summary>
        /// 创建玻璃质感弹珠材质（半透明 + 高光滑度）
        /// </summary>
        public static Material CreateGlassMaterial(Color tint)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Color c = tint;
            c.a = 0.65f;
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Metallic", 0.05f);
            mat.SetFloat("_Smoothness", 0.92f);
            // 半透明表面
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        /// <summary>
        /// 重置到初始位置
        /// </summary>
        public void ResetToInitial()
        {
            transform.position = initialPosition;
            StopMarble();
            state = MarbleState.Idle;
        }
        
        void OnCollisionEnter(Collision collision)
        {
            // 碰撞音效
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce > 0.5f)
            {
                // TODO: 播放碰撞音效，音量根据力度调整
            }

            // 撞到地形障碍物时根据碰撞力度减速
            // 适用于：土堆、石头、战壕边缘等地形凸起
            if (collision.gameObject.layer == LayerMask.NameToLayer("Terrain"))
            {
                // 计算碰撞方向的法线
                if (collision.contacts.Length > 0)
                {
                    Vector3 normal = collision.contacts[0].normal;
                    // 只有撞到侧上方（角度 > 45°）才减速，避免在平地上也减速
                    if (normal.y < 0.7f)
                    {
                        // 碰撞力度越大，减速越明显
                        float speedLoss = Mathf.Clamp01(impactForce * 0.3f);
                        rb.velocity *= (1f - speedLoss);

                        // 根据地形类型应用额外减速
                        if (currentTerrain == Terrain.TerrainType.Dirt ||
                            currentTerrain == Terrain.TerrainType.LooseSand)
                        {
                            // 泥土地/松土：碰撞减速更明显（坑洼感）
                            rb.velocity *= 0.85f;
                        }
                        else if (currentTerrain == Terrain.TerrainType.Grass)
                        {
                            // 草堆：碰撞减速中等
                            rb.velocity *= 0.9f;
                        }
                    }
                }
            }
        }
    }
}
