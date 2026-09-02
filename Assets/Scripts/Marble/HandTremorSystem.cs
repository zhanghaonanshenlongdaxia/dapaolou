using UnityEngine;

namespace Dapaolou.Marble
{
    /// <summary>
    /// 手抖动因素
    /// </summary>
    [System.Serializable]
    public class TremorFactors
    {
        [Range(0f, 1f)] public float baseStability = 0.8f;     // 基础稳定性
        [Range(0f, 1f)] public float nervousness = 0f;          // 紧张程度
        [Range(0f, 1f)] public float fatigue = 0f;              // 疲劳程度
        [Range(0f, 1f)] public float terrainEffect = 0f;        // 地形影响
        [Range(0f, 1f)] public float windEffect = 0f;           // 风力影响
    }

    /// <summary>
    /// 手抖系统 - 类似荒野的召唤的手抖效果
    /// 影响弹珠发射方向，增加操作技巧性
    /// </summary>
    public class HandTremorSystem : MonoBehaviour
    {
        [Header("基础抖动参数")]
        [SerializeField] private float baseTremorSpeed = 2f;        // 基础抖动速度
        [SerializeField] private float baseTremorAmount = 0.02f;    // 基础抖动幅度
        [SerializeField] private float maxTremorAmount = 0.15f;     // 最大抖动幅度
        
        [Header("准星/瞄准点")]
        [SerializeField] private Transform aimPoint;                // 瞄准点（准星）
        [SerializeField] private float aimDriftRadius = 0.5f;       // 瞄准漂移半径
        [SerializeField] private float aimDriftSpeed = 1f;          // 瞄准漂移速度
        
        [Header("Perlin噪声参数")]
        [SerializeField] private float noiseScaleX = 1.2f;
        [SerializeField] private float noiseScaleY = 0.8f;
        [SerializeField] private float noiseScaleZ = 1.0f;
        
        [Header("当前抖动因素")]
        [SerializeField] private TremorFactors tremorFactors = new TremorFactors();
        
        // 内部状态
        private float noiseOffsetX;
        private float noiseOffsetY;
        private float noiseOffsetZ;
        private Vector3 currentTremorOffset;
        private Vector3 aimDriftOffset;
        private float currentStability;     // 当前稳定性（0-1，1为完全稳定）
        
        // 缓存
        private Camera playerCamera;
        private Vector3 originalAimPosition;
        
        void Awake()
        {
            playerCamera = GetComponentInChildren<Camera>();
            noiseOffsetX = Random.Range(0f, 1000f);
            noiseOffsetY = Random.Range(0f, 1000f);
            noiseOffsetZ = Random.Range(0f, 1000f);
        }
        
        void Start()
        {
            if (aimPoint != null)
            {
                originalAimPosition = aimPoint.localPosition;
            }
        }
        
        void Update()
        {
            UpdateTremor();
            UpdateAimDrift();
        }
        
        /// <summary>
        /// 更新手抖效果
        /// </summary>
        private void UpdateTremor()
        {
            // 计算当前稳定性
            currentStability = tremorFactors.baseStability;
            currentStability -= tremorFactors.nervousness * 0.4f;
            currentStability -= tremorFactors.fatigue * 0.3f;
            currentStability -= tremorFactors.terrainEffect * 0.2f;
            currentStability -= tremorFactors.windEffect * 0.15f;
            currentStability = Mathf.Clamp01(currentStability);
            
            // 计算抖动幅度（稳定性越低，抖动越大）
            float tremorMultiplier = 1f - currentStability;
            float currentAmount = Mathf.Lerp(baseTremorAmount, maxTremorAmount, tremorMultiplier);
            
            // 使用Perlin噪声生成平滑的抖动
            float time = Time.time * baseTremorSpeed;
            float noiseX = Mathf.PerlinNoise(time * noiseScaleX + noiseOffsetX, 0f) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(0f, time * noiseScaleY + noiseOffsetY) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(time * noiseScaleZ, time * noiseScaleZ + noiseOffsetZ) * 2f - 1f;
            
            // 应用抖动
            currentTremorOffset = new Vector3(
                noiseX * currentAmount,
                noiseY * currentAmount * 0.5f,  // 垂直抖动减半
                noiseZ * currentAmount * 0.3f     // 前后抖动更小
            );
        }
        
        /// <summary>
        /// 更新瞄准漂移（准星移动）
        /// </summary>
        private void UpdateAimDrift()
        {
            if (aimPoint == null) return;
            
            float driftMultiplier = 1f - currentStability;
            float currentDriftRadius = aimDriftRadius * driftMultiplier;
            
            // 8字形漂移轨迹
            float time = Time.time * aimDriftSpeed;
            float driftX = Mathf.Sin(time * 1.1f) * currentDriftRadius;
            float driftY = Mathf.Sin(time * 0.9f + 1.5f) * currentDriftRadius * 0.6f;
            
            // 加上一些随机扰动
            driftX += Mathf.PerlinNoise(time * 2f, 0f) * currentDriftRadius * 0.3f - currentDriftRadius * 0.15f;
            driftY += Mathf.PerlinNoise(0f, time * 2f) * currentDriftRadius * 0.3f - currentDriftRadius * 0.15f;
            
            aimDriftOffset = new Vector3(driftX, driftY, 0f);
            aimPoint.localPosition = originalAimPosition + aimDriftOffset;
        }
        
        /// <summary>
        /// 获取弹珠发射方向偏移（用于MarbleShooter）
        /// </summary>
        /// <param name="baseDirection">基础发射方向</param>
        /// <returns>应用手抖后的实际方向</returns>
        public Vector3 GetAimDirectionWithTremor(Vector3 baseDirection)
        {
            // 将抖动偏移转换为方向偏移
            Vector3 right = playerCamera != null ? playerCamera.transform.right : Vector3.right;
            Vector3 up = playerCamera != null ? playerCamera.transform.up : Vector3.up;
            
            // 计算方向偏移（抖动越大，偏移越大）
            float tremorMultiplier = 1f - currentStability;
            Vector3 directionOffset = 
                right * currentTremorOffset.x * tremorMultiplier * 2f +
                up * currentTremorOffset.y * tremorMultiplier * 2f;
            
            Vector3 finalDirection = (baseDirection + directionOffset).normalized;
            return finalDirection;
        }
        
        /// <summary>
        /// 获取当前抖动强度（0-1）
        /// </summary>
        public float GetCurrentTremorIntensity()
        {
            return 1f - currentStability;
        }
        
        /// <summary>
        /// 获取当前稳定性（0-1）
        /// </summary>
        public float GetCurrentStability()
        {
            return currentStability;
        }
        
        // ============ 外部设置抖动因素 ============
        
        /// <summary>
        /// 设置紧张程度（被瞄准、关键时刻等）
        /// </summary>
        public void SetNervousness(float value)
        {
            tremorFactors.nervousness = Mathf.Clamp01(value);
        }
        
        /// <summary>
        /// 设置疲劳程度（游戏时间越长越疲劳）
        /// </summary>
        public void SetFatigue(float value)
        {
            tremorFactors.fatigue = Mathf.Clamp01(value);
        }
        
        /// <summary>
        /// 设置地形影响
        /// </summary>
        public void SetTerrainEffect(float value)
        {
            tremorFactors.terrainEffect = Mathf.Clamp01(value);
        }
        
        /// <summary>
        /// 设置风力影响
        /// </summary>
        public void SetWindEffect(float value)
        {
            tremorFactors.windEffect = Mathf.Clamp01(value);
        }
        
        /// <summary>
        /// 设置基础稳定性
        /// </summary>
        public void SetBaseStability(float value)
        {
            tremorFactors.baseStability = Mathf.Clamp01(value);
        }
        
        /// <summary>
        /// 站立时减少手抖
        /// </summary>
        public void OnStanceChanged(bool isCrouching)
        {
            if (isCrouching)
            {
                tremorFactors.baseStability = 0.9f;  // 蹲下更稳定
            }
            else
            {
                tremorFactors.baseStability = 0.7f;  // 站立稳定性降低
            }
        }
        
        /// <summary>
        /// 屏息功能（临时提高稳定性）
        /// </summary>
        public void OnBreathHold(bool isHolding)
        {
            if (isHolding)
            {
                tremorFactors.baseStability = Mathf.Min(1f, tremorFactors.baseStability + 0.3f);
            }
        }
        
        void OnDrawGizmosSelected()
        {
            // 在编辑器中显示抖动范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, maxTremorAmount);
            
            // 显示当前抖动偏移
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, currentTremorOffset * 10f);
        }
    }
}
