using UnityEngine;

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
        
        [Header("当前状态")]
        public MarbleState state = MarbleState.Idle;
        public bool isInvincible = false;       // 无敌状态（刚被打爆保护）
        
        [Header("物理参数")]
        public float baseMass = 0.1f;           // 基础质量（kg）
        public float bounciness = 0.3f;         // 弹性
        public float rollingFriction = 0.05f;   // 滚动摩擦
        
        private Rigidbody rb;
        private Vector3 initialPosition;
        
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
            // 滚动中的弹珠速度低于阈值时直接刹停，避免长时间蠕动导致回合等待过久
            if (state == MarbleState.Rolling && rb != null && rb.velocity.magnitude < 0.3f)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                state = MarbleState.Idle;
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
            // TODO: 播放破碎特效
            // TODO: 通知GameManager
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
