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
        
        private Rigidbody rb;
        private Vector3 initialPosition;
        private float terrainCheckTimer = 0f;
        private Terrain.TerrainType currentTerrain = Terrain.TerrainType.Cement;
        private float destroyedAt = -1f;        // 被摧毁的时刻（滚停后移除用）
        
        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            
            // 设置物理参数
            rb.mass = baseMass;
            rb.drag = 0.5f;                // 空气阻力
            rb.angularDrag = 0.8f;         // 旋转阻力
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        
        void Start()
        {
            initialPosition = transform.position;
        }

        void Update()
        {
            // 待清理（炮楼散架）：速度低于阈值即从场上移除（无论 state 是 Idle 还是蠕动中的低速滑动）
            if (pendingCleanup && rb != null && rb.velocity.magnitude < 0.3f)
            {
                Destroy(gameObject);
                return;
            }

            // 已摧毁的弹珠：保持物理滚动/弹跳，滚停后从场上移除（超时 5s 强制）
            if (state == MarbleState.Destroyed && rb != null)
            {
                if (rb.velocity.magnitude < 0.3f || Time.time - destroyedAt > 5f)
                {
                    Destroy(gameObject);
                }
                return;
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
                    // 地形检测（0.15s 一次）：按脚下地形设置滚动阻力；入水瞬间骤减
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
                            rb.drag = Terrain.TerrainEffectSystem.GetDragFor(t);
                        }
                    }
                }
            }
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
        }
    }
}
