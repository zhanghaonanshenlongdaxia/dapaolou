using UnityEngine;

namespace Dapaolou.Terrain
{
    /// <summary>
    /// 地形类型
    /// </summary>
    public enum TerrainType
    {
        Cement,         // 水泥地 - 正常
        Dirt,           // 泥土地 - 坑洼、上坡
        Grass,          // 草堆 - 减速
        LooseSand,      // 松散土地 - 上坡、容易陷住
        Puddle          // 水坑 - 大幅减速（新增）
    }

    /// <summary>
    /// 地形效果配置
    /// </summary>
    [System.Serializable]
    public class TerrainEffectConfig
    {
        public TerrainType terrainType;
        
        [Header("物理效果")]
        [Range(0f, 2f)] public float frictionMultiplier = 1f;      // 摩擦力倍率
        [Range(0f, 2f)] public float bouncinessMultiplier = 1f;    // 弹性倍率
        [Range(0f, 2f)] public float speedMultiplier = 1f;         // 速度倍率
        
        [Header("特殊效果")]
        public bool canStuck = false;                               // 是否会陷住
        [Range(0f, 1f)] public float stuckChance = 0f;             // 陷住概率
        public float stuckDuration = 2f;                            // 陷住持续时间
        
        [Header("视觉效果")]
        public GameObject terrainParticle;                          // 地形粒子效果
        public AudioClip rollSound;                                 // 滚动音效
    }

    /// <summary>
    /// 地形效果系统 - 管理不同地形对弹珠的影响
    /// </summary>
    public class TerrainEffectSystem : MonoBehaviour
    {
        [Header("地形配置")]
        [SerializeField] private TerrainEffectConfig cementConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.Cement,
            frictionMultiplier = 0.6f,       // 玻璃珠在水泥地上低摩擦
            bouncinessMultiplier = 1.3f,     // 弹性好
            speedMultiplier = 1.05f,         // 基本不减速
            canStuck = false,
            stuckChance = 0f
        };

        [SerializeField] private TerrainEffectConfig dirtConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.Dirt,
            frictionMultiplier = 1.0f,       // 泥土地：中等摩擦
            bouncinessMultiplier = 0.85f,    // 弹性稍减
            speedMultiplier = 0.95f,         // 轻微减速（5%）
            canStuck = false,
            stuckChance = 0f
        };

        [SerializeField] private TerrainEffectConfig grassConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.Grass,
            frictionMultiplier = 1.4f,       // 草堆：明显摩擦
            bouncinessMultiplier = 0.6f,     // 弹性大幅降低
            speedMultiplier = 0.8f,          // 减速20%
            canStuck = false,
            stuckChance = 0f
        };

        [SerializeField] private TerrainEffectConfig looseSandConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.LooseSand,
            frictionMultiplier = 1.8f,       // 松土：高摩擦
            bouncinessMultiplier = 0.4f,     // 弹性很低
            speedMultiplier = 0.65f,         // 减速35%
            canStuck = true,
            stuckChance = 0.2f,              // 20%概率陷住
            stuckDuration = 2.5f
        };

        [SerializeField] private TerrainEffectConfig puddleConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.Puddle,
            frictionMultiplier = 2.0f,       // 水坑：最高摩擦
            bouncinessMultiplier = 0.3f,     // 弹性最低
            speedMultiplier = 0.55f,         // 减速45%
            canStuck = false,
            stuckChance = 0f
        };

        [Header("滚动减速度（恒定减速度模型，台球式）")]
        [SerializeField] private float rollDecelMultiplier = 3f;   // 节奏系数：1=纯物理实测值，>1 加快回合节奏

        /// <summary>
        /// 各地形的恒定滚动减速度 a=Crr·g·k（m/s²，方向恒反向水平速度）。
        /// Crr 抄工程实测表（Engineering Toolbox）：水泥 0.01 / 土路 0.05 / 草地 0.15 / 沙 0.25 / 水坑 0.3。
        /// 替代旧 rb.drag 指数衰减——玻璃珠滚动阻力是恒力，不是按速度比例衰减。
        /// </summary>
        public static float GetRollingDeceleration(TerrainType type)
        {
            float crr;
            switch (type)
            {
                case TerrainType.Cement: crr = 0.01f; break;
                case TerrainType.Dirt: crr = 0.05f; break;
                case TerrainType.Grass: crr = 0.15f; break;
                case TerrainType.LooseSand: crr = 0.25f; break;
                case TerrainType.Puddle: crr = 0.3f; break;
                default: crr = 0.01f; break;
            }
            float k = Instance != null ? Instance.rollDecelMultiplier : 3f;
            return crr * 9.81f * k;
        }

        [Header("水泥缝改向")]
        [SerializeField] private float seamDeflectMinDeg = 1.5f;   // 过缝最小偏转角
        [SerializeField] private float seamDeflectMaxDeg = 4.5f;   // 过缝最大偏转角

        /// <summary>水泥缝线：axisX=true 表示缝沿 X=coord（纵向缝），min/max 为另一轴延展范围</summary>
        public struct CementSeam
        {
            public bool axisX;
            public float coord;
            public float min;
            public float max;
        }

        private readonly System.Collections.Generic.List<CementSeam> cementSeams = new System.Collections.Generic.List<CementSeam>();

        void Start()
        {
            // 注册水泥缝：沟壕无碰撞体（1.2cm 缝由弹珠天然跨过），改向由程序在穿越瞬间施加
            cementSeams.Clear();
            var yard = GameObject.Find("YardTerrain");
            if (yard == null) return;
            foreach (Transform child in yard.transform)
            {
                if (!child.name.StartsWith("CementTrench")) continue;
                var rend = child.GetComponent<Renderer>();
                if (rend == null) continue;
                var b = rend.bounds;
                var seam = new CementSeam();
                if (b.size.x <= b.size.z)
                {
                    seam.axisX = true;
                    seam.coord = b.center.x;
                    seam.min = b.center.z - b.size.z * 0.5f;
                    seam.max = b.center.z + b.size.z * 0.5f;
                }
                else
                {
                    seam.axisX = false;
                    seam.coord = b.center.z;
                    seam.min = b.center.x - b.size.x * 0.5f;
                    seam.max = b.center.x + b.size.x * 0.5f;
                }
                cementSeams.Add(seam);
            }
        }

        /// <summary>
        /// 检测本物理步内弹珠是否穿越了水泥缝；穿越则返回随机偏转角（度，正负随机）
        /// </summary>
        public static bool TryGetSeamDeflection(Vector3 from, Vector3 to, out float deflectDeg)
        {
            deflectDeg = 0f;
            if (Instance == null) return false;
            foreach (var seam in Instance.cementSeams)
            {
                float a = seam.axisX ? from.x : from.z;
                float b = seam.axisX ? to.x : to.z;
                float along = seam.axisX ? from.z : from.x;
                if ((a - seam.coord) * (b - seam.coord) < 0f &&
                    along > seam.min - 0.05f && along < seam.max + 0.05f)
                {
                    deflectDeg = Random.Range(Instance.seamDeflectMinDeg, Instance.seamDeflectMaxDeg)
                                 * (Random.value < 0.5f ? -1f : 1f);
                    return true;
                }
            }
            return false;
        }
        
        [Header("地形检测")]
        [SerializeField] private LayerMask terrainLayer;
        [SerializeField] private float checkHeight = 0.5f;
        
        // 单例
        public static TerrainEffectSystem Instance { get; private set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 获取指定位置的地形类型
        /// </summary>
        public TerrainType GetTerrainType(Vector3 position)
        {
            // 向下发射射线检测地形（长度覆盖高处调用，如从 1m 高俯视检测）
            Ray ray = new Ray(position + Vector3.up * checkHeight, Vector3.down);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, checkHeight * 2 + 1.5f, terrainLayer))
            {
                // 根据地形Tag或组件判断类型
                TerrainTag terrainTag = hit.collider.GetComponent<TerrainTag>();
                if (terrainTag != null)
                {
                    return terrainTag.terrainType;
                }
                
                // 根据材质名称判断（备用方案）
                Renderer renderer = hit.collider.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return GetTerrainTypeFromMaterial(renderer.material);
                }
            }
            
            // 默认水泥地
            return TerrainType.Cement;
        }
        
        /// <summary>
        /// 获取地形效果配置
        /// </summary>
        public TerrainEffectConfig GetTerrainConfig(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Cement:
                    return cementConfig;
                case TerrainType.Dirt:
                    return dirtConfig;
                case TerrainType.Grass:
                    return grassConfig;
                case TerrainType.LooseSand:
                    return looseSandConfig;
                case TerrainType.Puddle:
                    return puddleConfig;
                default:
                    return cementConfig;
            }
        }
        
        /// <summary>
        /// 应用地形效果到弹珠
        /// </summary>
        public void ApplyTerrainEffect(Marble.MarbleData marble, TerrainType terrainType)
        {
            TerrainEffectConfig config = GetTerrainConfig(terrainType);
            Rigidbody rb = marble.GetComponent<Rigidbody>();
            
            if (rb == null) return;
            
            // 应用摩擦力
            rb.drag *= config.frictionMultiplier;
            
            // 应用速度衰减
            rb.velocity *= config.speedMultiplier;
            
            // 检查是否陷住
            if (config.canStuck && Random.value < config.stuckChance)
            {
                ApplyStuckEffect(marble, config.stuckDuration);
            }
            
            // 播放粒子效果
            if (config.terrainParticle != null)
            {
                Instantiate(config.terrainParticle, marble.transform.position, Quaternion.identity);
            }
        }
        
        /// <summary>
        /// 应用陷住效果
        /// </summary>
        private void ApplyStuckEffect(Marble.MarbleData marble, float duration)
        {
            Debug.Log($"Marble stuck in terrain for {duration}s!");
            
            Rigidbody rb = marble.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 减速到几乎停止
                rb.velocity *= 0.1f;
                rb.angularVelocity *= 0.1f;
                
                // 增加阻力
                rb.drag = 10f;
                rb.angularDrag = 10f;
                
                // 延迟恢复
                StartCoroutine(RecoverFromStuck(rb, duration));
            }
        }
        
        private System.Collections.IEnumerator RecoverFromStuck(Rigidbody rb, float duration)
        {
            yield return new WaitForSeconds(duration);
            
            if (rb != null)
            {
                rb.drag = 0.5f;
                rb.angularDrag = 0.8f;
            }
        }
        
        /// <summary>
        /// 根据材质名称判断地形类型（备用方案）
        /// </summary>
        private TerrainType GetTerrainTypeFromMaterial(Material material)
        {
            string name = material.name.ToLower();
            
            if (name.Contains("cement") || name.Contains("concrete") || name.Contains("stone"))
            {
                return TerrainType.Cement;
            }
            else if (name.Contains("dirt") || name.Contains("soil") || name.Contains("mud"))
            {
                return TerrainType.Dirt;
            }
            else if (name.Contains("grass") || name.Contains("lawn"))
            {
                return TerrainType.Grass;
            }
            else if (name.Contains("sand") || name.Contains("loose"))
            {
                return TerrainType.LooseSand;
            }
            
            return TerrainType.Cement;
        }
    }

    /// <summary>
    /// 地形标签组件 - 挂载在地形对象上标记地形类型
    /// </summary>
    public class TerrainTag : MonoBehaviour
    {
        public TerrainType terrainType = TerrainType.Cement;
        
        [Header("可视化")]
        public bool showDebugGizmos = true;
        public Color gizmoColor = Color.white;
        
        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            Gizmos.color = gizmoColor;
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }
    }
}
