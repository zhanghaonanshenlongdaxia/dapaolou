using UnityEngine;
using System.Collections;

namespace Dapaolou.NPC
{
    /// <summary>
    /// 狗的状态
    /// </summary>
    public enum DogState
    {
        Tied,           // 拴着
        Wandering,      // 闲逛中
        Watching,       // 看玩家玩耍
        Barking,        // 叫唤中
        Returning       // 返回狗窝
    }

    /// <summary>
    /// 宠物狗NPC - 臭豆
    /// </summary>
    public class DogNPC : NPCBase
    {
        [Header("狗配置")]
        [SerializeField] private Transform dogHousePosition;       // 狗窝位置
        [SerializeField] private Transform tiePosition;            // 拴绳位置
        [SerializeField] private Transform[] wanderPoints;         // 闲逛点
        [SerializeField] private Transform watchPosition;          // 观看位置
        
        [Header("行为配置")]
        [SerializeField] private float escapeChance = 0.1f;        // 逃跑概率（每分钟）
        [SerializeField] private float wanderDuration = 60f;       // 闲逛时长（秒）
        [SerializeField] private float watchDuration = 30f;        // 观看时长（秒）
        [SerializeField] private float returnDelay = 10f;          // 返回延迟
        
        [Header("音效")]
        [SerializeField] private AudioClip[] barkSounds;           // 叫声
        [SerializeField] private AudioClip[] whineSounds;          // 呜咽声
        [SerializeField] private AudioClip[] happySounds;          // 开心声
        
        [Header("动画")]
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip barkClip;
        [SerializeField] private AnimationClip tailWagClip;
        
        // 狗的状态
        private DogState dogState = DogState.Tied;
        private bool isEscaped = false;
        private float wanderTimer = 0f;
        private float watchTimer = 0f;
        private int currentWanderPoint = 0;
        
        // 逃跑计时
        private float escapeCheckTimer = 0f;
        private float escapeCheckInterval = 60f; // 每分钟检查一次
        
        // 事件
        public System.Action OnDogEscapes;
        public System.Action OnDogReturns;
        public System.Action OnDogBarks;
        
        protected override void InitializeNPC()
        {
            base.InitializeNPC();
            
            // 初始在狗窝
            if (dogHousePosition != null)
            {
                transform.position = dogHousePosition.position;
            }
            
            // 设置NavMeshAgent
            if (navAgent != null)
            {
                navAgent.speed = walkSpeed * 0.7f; // 狗走得慢一点
            }
        }
        
        protected override void UpdateNPC()
        {
            base.UpdateNPC();
            
            // 更新逃跑检查
            UpdateEscapeCheck();
            
            // 更新狗的状态
            UpdateDogBehavior();
        }
        
        #region 逃跑逻辑
        
        private void UpdateEscapeCheck()
        {
            if (isEscaped) return;
            
            escapeCheckTimer += Time.deltaTime;
            
            if (escapeCheckTimer >= escapeCheckInterval)
            {
                escapeCheckTimer = 0f;
                
                // 随机检查是否逃跑
                if (Random.value < escapeChance)
                {
                    Escape();
                }
            }
        }
        
        /// <summary>
        /// 狗逃跑
        /// </summary>
        private void Escape()
        {
            if (isEscaped) return;
            
            Debug.Log("臭豆跑出来了！");
            isEscaped = true;
            dogState = DogState.Wandering;
            
            // 播放叫声
            PlayRandomBark();
            
            OnDogEscapes?.Invoke();
            
            // 开始闲逛
            StartCoroutine(WanderSequence());
        }
        
        private IEnumerator WanderSequence()
        {
            wanderTimer = 0f;
            
            // 随机选择闲逛点
            if (wanderPoints != null && wanderPoints.Length > 0)
            {
                currentWanderPoint = Random.Range(0, wanderPoints.Length);
                MoveTo(wanderPoints[currentWanderPoint].position);
            }
            
            // 闲逛一段时间
            while (wanderTimer < wanderDuration)
            {
                wanderTimer += Time.deltaTime;
                
                // 到达闲逛点后，选择下一个
                if (HasReachedDestination())
                {
                    // 随机决定：继续闲逛还是去看玩家
                    if (Random.value < 0.3f)
                    {
                        // 去看玩家玩耍
                        yield return StartCoroutine(WatchPlayersSequence());
                    }
                    else
                    {
                        // 继续闲逛
                        if (wanderPoints != null && wanderPoints.Length > 0)
                        {
                            currentWanderPoint = Random.Range(0, wanderPoints.Length);
                            MoveTo(wanderPoints[currentWanderPoint].position);
                        }
                    }
                }
                
                yield return null;
            }
            
            // 闲逛结束，返回狗窝
            yield return StartCoroutine(ReturnToHouseSequence());
        }
        
        /// <summary>
        /// 观看玩家玩耍
        /// </summary>
        private IEnumerator WatchPlayersSequence()
        {
            Debug.Log("臭豆在看玩家们玩耍");
            dogState = DogState.Watching;
            
            // 移动到观看位置
            if (watchPosition != null)
            {
                MoveTo(watchPosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 停下来观看
            StopMoving();
            
            // 随机摇尾巴
            if (animator != null)
            {
                animator.SetBool("IsWatching", true);
            }
            
            // 观看一段时间
            watchTimer = 0f;
            while (watchTimer < watchDuration)
            {
                watchTimer += Time.deltaTime;
                
                // 随机叫几声
                if (Random.value < 0.01f)
                {
                    PlayRandomBark();
                }
                
                yield return null;
            }
            
            // 停止观看
            if (animator != null)
            {
                animator.SetBool("IsWatching", false);
            }
            
            dogState = DogState.Wandering;
        }
        
        /// <summary>
        /// 返回狗窝
        /// </summary>
        private IEnumerator ReturnToHouseSequence()
        {
            Debug.Log("臭豆要回狗窝了");
            dogState = DogState.Returning;
            
            // 等待一下
            yield return new WaitForSeconds(returnDelay);
            
            // 移动到狗窝
            if (dogHousePosition != null)
            {
                MoveTo(dogHousePosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 回到拴绳位置
            if (tiePosition != null)
            {
                transform.position = tiePosition.position;
            }
            
            // 状态重置
            isEscaped = false;
            dogState = DogState.Tied;
            
            OnDogReturns?.Invoke();
            Debug.Log("臭豆回到狗窝了");
        }
        
        #endregion
        
        #region 行为更新
        
        private void UpdateDogBehavior()
        {
            switch (dogState)
            {
                case DogState.Tied:
                    UpdateTied();
                    break;
                case DogState.Wandering:
                    UpdateWandering();
                    break;
                case DogState.Watching:
                    UpdateWatching();
                    break;
            }
        }
        
        private void UpdateTied()
        {
            // 拴着时偶尔呜咽
            if (Random.value < 0.001f)
            {
                PlayRandomWhine();
            }
            
            // 偶尔摇尾巴
            if (animator != null)
            {
                animator.SetBool("IsTied", true);
            }
        }
        
        private void UpdateWandering()
        {
            // 闲逛时偶尔叫唤
            if (Random.value < 0.005f)
            {
                PlayRandomBark();
            }
        }
        
        private void UpdateWatching()
        {
            // 观看时摇尾巴
            if (animator != null)
            {
                animator.SetBool("IsWagging", true);
            }
            
            // 偶尔叫唤（表示开心）
            if (Random.value < 0.01f)
            {
                PlayRandomHappy();
            }
        }
        
        #endregion
        
        #region 音效
        
        private void PlayRandomBark()
        {
            if (barkSounds != null && barkSounds.Length > 0)
            {
                AudioClip clip = barkSounds[Random.Range(0, barkSounds.Length)];
                PlaySound(clip, 0.6f);
                Debug.Log("汪汪！");
                OnDogBarks?.Invoke();
            }
        }
        
        private void PlayRandomWhine()
        {
            if (whineSounds != null && whineSounds.Length > 0)
            {
                AudioClip clip = whineSounds[Random.Range(0, whineSounds.Length)];
                PlaySound(clip, 0.4f);
                Debug.Log("呜~~");
            }
        }
        
        private void PlayRandomHappy()
        {
            if (happySounds != null && happySounds.Length > 0)
            {
                AudioClip clip = happySounds[Random.Range(0, happySounds.Length)];
                PlaySound(clip, 0.5f);
            }
        }
        
        #endregion
        
        #region 交互
        
        /// <summary>
        /// 与狗交互
        /// </summary>
        public override void Interact()
        {
            base.Interact();
            
            // 摸狗
            if (dogState == DogState.Tied || dogState == DogState.Watching)
            {
                Debug.Log("摸摸臭豆");
                PlayRandomHappy();
                
                if (animator != null)
                {
                    animator.SetTrigger("Pet");
                }
            }
            // 赶狗
            else if (dogState == DogState.Wandering)
            {
                Debug.Log("赶走臭豆");
                PlayRandomWhine();
                
                // 狗会跑开
                StartCoroutine(ScareAway());
            }
        }
        
        private IEnumerator ScareAway()
        {
            // 跑到远离玩家的位置
            Transform player = GetNearestPlayer();
            if (player != null)
            {
                Vector3 runDirection = (transform.position - player.position).normalized;
                Vector3 runTarget = transform.position + runDirection * 10f;
                
                navAgent.speed = runSpeed;
                MoveTo(runTarget);
                
                yield return new WaitForSeconds(3f);
                
                navAgent.speed = walkSpeed * 0.7f;
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取狗的状态
        /// </summary>
        public DogState GetDogState()
        {
            return dogState;
        }
        
        /// <summary>
        /// 是否逃跑
        /// </summary>
        public bool IsEscaped()
        {
            return isEscaped;
        }
        
        /// <summary>
        /// 是否拴着
        /// </summary>
        public bool IsTied()
        {
            return dogState == DogState.Tied;
        }
        
        /// <summary>
        /// 是否在观看
        /// </summary>
        public bool IsWatching()
        {
            return dogState == DogState.Watching;
        }
        
        /// <summary>
        /// 强制返回狗窝
        /// </summary>
        public void ForceReturn()
        {
            if (isEscaped)
            {
                StopAllCoroutines();
                StartCoroutine(ReturnToHouseSequence());
            }
        }
        
        #endregion
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // 绘制狗窝位置
            if (dogHousePosition != null)
            {
                Gizmos.color = new Color(0.55f, 0.27f, 0.07f); // 棕色
                Gizmos.DrawWireCube(dogHousePosition.position, new Vector3(1.5f, 1f, 1.5f));
            }
            
            // 绘制拴绳位置
            if (tiePosition != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(tiePosition.position, 0.3f);
            }
            
            // 绘制闲逛点
            if (wanderPoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var point in wanderPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, 0.5f);
                    }
                }
            }
            
            // 绘制观看位置
            if (watchPosition != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(watchPosition.position, 0.5f);
            }
        }
    }
}
