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
            frictionMultiplier = 0.8f,
            bouncinessMultiplier = 1.2f,
            speedMultiplier = 1.1f,
            canStuck = false,
            stuckChance = 0f
        };
        
        [SerializeField] private TerrainEffectConfig dirtConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.Dirt,
            frictionMultiplier = 1.3f,
            bouncinessMultiplier = 0.7f,
            speedMultiplier = 0.9f,
            canStuck = false,
            stuckChance = 0f
        };
        
        [SerializeField] private TerrainEffectConfig grassConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.Grass,
            frictionMultiplier = 1.8f,
            bouncinessMultiplier = 0.5f,
            speedMultiplier = 0.7f,
            canStuck = false,
            stuckChance = 0f
        };
        
        [SerializeField] private TerrainEffectConfig looseSandConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.LooseSand,
            frictionMultiplier = 2.0f,
            bouncinessMultiplier = 0.3f,
            speedMultiplier = 0.5f,
            canStuck = true,
            stuckChance = 0.3f,
            stuckDuration = 3f
        };

        [SerializeField] private TerrainEffectConfig puddleConfig = new TerrainEffectConfig
        {
            terrainType = TerrainType.Puddle,
            frictionMultiplier = 2.4f,
            bouncinessMultiplier = 0.2f,
            speedMultiplier = 0.45f,
            canStuck = false,
            stuckChance = 0f
        };

        /// <summary>
        /// 各地形的滚动阻力（rb.drag），供弹珠状态机查询
        /// </summary>
        public static float GetDragFor(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Cement: return 0.5f;
                case TerrainType.Dirt: return 0.8f;
                case TerrainType.Grass: return 1.6f;
                case TerrainType.LooseSand: return 2.6f;
                case TerrainType.Puddle: return 3.2f;
                default: return 0.5f;
            }
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
