using UnityEngine;
using UnityEngine.AI;

namespace Dapaolou.NPC
{
    /// <summary>
    /// NPC状态
    /// </summary>
    public enum NPCState
    {
        Idle,           // 空闲
        Walking,        // 行走中
        Working,        // 工作中
        Talking,        // 说话中
        Interacting,    // 交互中
        Sleeping        // 睡觉
    }

    /// <summary>
    /// NPC基类 - 所有NPC的父类
    /// </summary>
    public abstract class NPCBase : MonoBehaviour
    {
        [Header("NPC基本信息")]
        [SerializeField] protected string npcName = "NPC";
        [SerializeField] protected string displayName = "NPC";
        [SerializeField] protected Color nameColor = Color.white;
        
        [Header("移动配置")]
        [SerializeField] protected float walkSpeed = 2f;
        [SerializeField] protected float runSpeed = 5f;
        [SerializeField] protected float rotationSpeed = 5f;
        
        [Header("交互配置")]
        [SerializeField] protected float interactionRange = 2f;
        [SerializeField] protected LayerMask playerLayer;
        
        [Header("动画")]
        public Animator animator;
        [SerializeField] protected RuntimeAnimatorController animatorController;
        
        [Header("音效")]
        [SerializeField] protected AudioSource audioSource;
        [SerializeField] protected AudioClip[] idleSounds;
        [SerializeField] protected AudioClip[] talkSounds;
        
        // 组件引用
        protected NavMeshAgent navAgent;
        protected Transform currentTarget;
        
        // 状态
        protected NPCState currentState = NPCState.Idle;
        protected bool isActive = true;
        
        protected virtual void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null)
            {
                navAgent = gameObject.AddComponent<NavMeshAgent>();
            }
            
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            
            // 配置NavMeshAgent
            navAgent.speed = walkSpeed;
            navAgent.angularSpeed = rotationSpeed * 100f;
            navAgent.acceleration = 8f;
            navAgent.stoppingDistance = 0.5f;
        }
        
        protected virtual void Start()
        {
            InitializeNPC();
        }
        
        protected virtual void Update()
        {
            if (!isActive) return;
            
            UpdateNPC();
            UpdateAnimations();
        }
        
        #region 初始化
        
        protected virtual void InitializeNPC()
        {
            // 子类重写初始化逻辑
        }
        
        #endregion
        
        #region 更新逻辑
        
        protected virtual void UpdateNPC()
        {
            // 子类重写更新逻辑
        }
        
        protected virtual void UpdateAnimations()
        {
            if (animator == null) return;
            
            // 更新移动动画
            if (navAgent != null && navAgent.enabled)
            {
                float speed = navAgent.velocity.magnitude;
                animator.SetFloat("Speed", speed);
                animator.SetBool("IsMoving", speed > 0.1f);
            }
            
            animator.SetInteger("State", (int)currentState);
        }
        
        #endregion
        
        #region 移动控制
        
        /// <summary>
        /// 移动到目标位置
        /// </summary>
        public virtual void MoveTo(Vector3 destination)
        {
            if (navAgent == null || !navAgent.enabled) return;
            
            navAgent.SetDestination(destination);
            SetState(NPCState.Walking);
        }
        
        /// <summary>
        /// 移动到目标对象
        /// </summary>
        public virtual void MoveToTarget(Transform target)
        {
            if (target == null) return;
            
            currentTarget = target;
            MoveTo(target.position);
        }
        
        /// <summary>
        /// 停止移动
        /// </summary>
        public virtual void StopMoving()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.ResetPath();
            }
            SetState(NPCState.Idle);
        }
        
        /// <summary>
        /// 跟随目标
        /// </summary>
        public virtual void FollowTarget(Transform target, float distance = 2f)
        {
            if (target == null) return;
            
            currentTarget = target;
            Vector3 direction = (transform.position - target.position).normalized;
            Vector3 destination = target.position + direction * distance;
            
            MoveTo(destination);
        }
        
        /// <summary>
        /// 是否到达目标
        /// </summary>
        public virtual bool HasReachedDestination()
        {
            if (navAgent == null || !navAgent.enabled) return true;
            
            return !navAgent.pathPending && 
                   navAgent.remainingDistance <= navAgent.stoppingDistance &&
                   (!navAgent.hasPath || navAgent.velocity.sqrMagnitude == 0f);
        }
        
        #endregion
        
        #region 状态控制
        
        /// <summary>
        /// 设置NPC状态
        /// </summary>
        public virtual void SetState(NPCState newState)
        {
            if (currentState == newState) return;
            
            OnStateExit(currentState);
            currentState = newState;
            OnStateEnter(newState);
        }
        
        /// <summary>
        /// 进入状态
        /// </summary>
        protected virtual void OnStateEnter(NPCState state)
        {
            // 子类重写
        }
        
        /// <summary>
        /// 退出状态
        /// </summary>
        protected virtual void OnStateExit(NPCState state)
        {
            // 子类重写
        }
        
        /// <summary>
        /// 获取当前状态
        /// </summary>
        public NPCState GetCurrentState()
        {
            return currentState;
        }
        
        #endregion
        
        #region 交互
        
        /// <summary>
        /// 与玩家交互
        /// </summary>
        public virtual void Interact()
        {
            // 子类重写交互逻辑
        }
        
        /// <summary>
        /// 检查玩家是否在交互范围内
        /// </summary>
        protected virtual bool IsPlayerInRange()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, playerLayer);
            return colliders.Length > 0;
        }
        
        /// <summary>
        /// 获取最近的玩家
        /// </summary>
        protected virtual Transform GetNearestPlayer()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, playerLayer);
            
            Transform nearest = null;
            float minDistance = float.MaxValue;
            
            foreach (var col in colliders)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = col.transform;
                }
            }
            
            return nearest;
        }
        
        #endregion
        
        #region 音效
        
        /// <summary>
        /// 播放音效
        /// </summary>
        public virtual void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
        
        /// <summary>
        /// 播放随机空闲音效
        /// </summary>
        protected virtual void PlayRandomIdleSound()
        {
            if (idleSounds != null && idleSounds.Length > 0)
            {
                AudioClip clip = idleSounds[Random.Range(0, idleSounds.Length)];
                PlaySound(clip);
            }
        }
        
        /// <summary>
        /// 播放随机说话音效
        /// </summary>
        protected virtual void PlayRandomTalkSound()
        {
            if (talkSounds != null && talkSounds.Length > 0)
            {
                AudioClip clip = talkSounds[Random.Range(0, talkSounds.Length)];
                PlaySound(clip);
            }
        }
        
        #endregion
        
        #region 激活/禁用
        
        /// <summary>
        /// 激活NPC
        /// </summary>
        public virtual void Activate()
        {
            isActive = true;
            gameObject.SetActive(true);
        }
        
        /// <summary>
        /// 禁用NPC
        /// </summary>
        public virtual void Deactivate()
        {
            isActive = false;
            StopMoving();
        }
        
        /// <summary>
        /// 是否激活
        /// </summary>
        public bool IsActive()
        {
            return isActive;
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 获取NPC名称
        /// </summary>
        public string GetNPCName()
        {
            return npcName;
        }
        
        /// <summary>
        /// 获取显示名称
        /// </summary>
        public string GetDisplayName()
        {
            return displayName;
        }
        
        /// <summary>
        /// 面向目标
        /// </summary>
        protected virtual void LookAt(Vector3 target)
        {
            Vector3 direction = (target - transform.position).normalized;
            direction.y = 0; // 保持水平
            
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        
        /// <summary>
        /// 面向目标
        /// </summary>
        protected virtual void LookAtTarget(Transform target)
        {
            if (target != null)
            {
                LookAt(target.position);
            }
        }
        
        #endregion
        
        #region Gizmos
        
        protected virtual void OnDrawGizmosSelected()
        {
            // 交互范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // 移动目标
            if (navAgent != null && navAgent.hasPath)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, navAgent.destination);
                Gizmos.DrawWireSphere(navAgent.destination, 0.3f);
            }
        }
        
        #endregion
    }
}
