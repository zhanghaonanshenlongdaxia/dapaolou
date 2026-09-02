using UnityEngine;
using System.Collections;
using Dapaolou.GameTime;

namespace Dapaolou.NPC
{
    /// <summary>
    /// 妈妈行为状态
    /// </summary>
    public enum MomBehavior
    {
        AtHome,             // 在家
        GoneToWork,         // 出门干活
        Cooking,            // 做饭中
        CallingForMeal,     // 喊吃饭中
        KickingPlayer,      // 踢玩家中
        LeadingCowOut,      // 牵牛出门
        LeadingCowIn        // 牵牛回圈
    }

    /// <summary>
    /// 妈妈NPC - 管理妈妈的所有行为
    /// </summary>
    public class MomNPC : NPCBase
    {
        [Header("妈妈配置")]
        [SerializeField] private Transform homePosition;           // 家的位置
        [SerializeField] private Transform workPosition;           // 干活位置
        [SerializeField] private Transform kitchenPosition;        // 厨房位置
        [SerializeField] private Transform cowPenPosition;         // 牛圈位置
        [SerializeField] private Transform gatePosition;           // 大门位置
        
        [Header("喊饭配置")]
        [SerializeField] private int maxCallCount = 4;             // 最大喊饭次数
        [SerializeField] private float callInterval = 60f;         // 喊饭间隔（游戏内秒）
        [SerializeField] private AudioClip[] callForMealSounds;    // 喊饭音效
        [SerializeField] private AudioClip kickSound;              // 踢人音效
        
        [Header("UI提示")]
        [SerializeField] private GameObject callBubblePrefab;      // 喊话气泡预制体
        [SerializeField] private Transform bubblePosition;         // 气泡位置
        
        // 妈妈状态
        private MomBehavior currentBehavior = MomBehavior.AtHome;
        private int currentCallCount = 0;
        private bool isKicking = false;
        private bool hasCalledForMeal = false;
        
        // 协程引用
        private Coroutine kickCoroutine;
        private Coroutine callCoroutine;
        
        // 事件
        public System.Action OnMomCallsForMeal;
        public System.Action OnMomKicksPlayer;
        public System.Action OnMomReturnsHome;
        public System.Action OnMomLeavesHome;
        
        protected override void InitializeNPC()
        {
            base.InitializeNPC();
            
            // 订阅时间事件
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.OnTimeEvent += HandleTimeEvent;
            }
            
            // 初始位置
            if (homePosition != null)
            {
                transform.position = homePosition.position;
            }
        }
        
        protected override void UpdateNPC()
        {
            base.UpdateNPC();
            
            // 更新行为
            UpdateBehavior();
        }
        
        #region 时间事件处理
        
        private void HandleTimeEvent(TimeEventType eventType)
        {
            switch (eventType)
            {
                case TimeEventType.MomLeavesForWork:
                    OnLeavesForWork();
                    break;
                    
                case TimeEventType.MomReturnsForLunch:
                    OnReturnsForLunch();
                    break;
                    
                case TimeEventType.MomCallsForLunch:
                    OnCallsForMeal();
                    break;
                    
                case TimeEventType.MomKicksPlayer:
                    OnKicksPlayer();
                    break;
                    
                case TimeEventType.MomLeavesAfterLunch:
                    OnLeavesAfterLunch();
                    break;
                    
                case TimeEventType.MomReturnsForDinner:
                    OnReturnsForDinner();
                    break;
                    
                case TimeEventType.MomCallsForDinner:
                    OnCallsForMeal();
                    break;
                    
                case TimeEventType.MomKicksPlayerDinner:
                    OnKicksPlayer();
                    break;
                    
                case TimeEventType.CowNeedsReturn:
                    OnCowNeedsReturn();
                    break;
            }
        }
        
        #endregion
        
        #region 妈妈行为
        
        /// <summary>
        /// 妈妈出门干活（早上7:00）
        /// </summary>
        private void OnLeavesForWork()
        {
            Debug.Log("妈妈出门干活了");
            currentBehavior = MomBehavior.GoneToWork;
            
            // 先牵牛出门
            StartCoroutine(LeadCowOutAndLeave());
            
            OnMomLeavesHome?.Invoke();
        }
        
        private IEnumerator LeadCowOutAndLeave()
        {
            // 移动到牛圈
            if (cowPenPosition != null)
            {
                MoveTo(cowPenPosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 牵牛出门
            currentBehavior = MomBehavior.LeadingCowOut;
            
            // 通知CowNPC
            CowNPC cow = FindObjectOfType<CowNPC>();
            if (cow != null)
            {
                cow.OnMomLeadsOut();
            }
            
            // 移动到大门
            if (gatePosition != null)
            {
                MoveTo(gatePosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 等待一下
            yield return new WaitForSeconds(2f);
            
            // 去干活位置
            if (workPosition != null)
            {
                MoveTo(workPosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 离开场景
            Deactivate();
        }
        
        /// <summary>
        /// 妈妈回来做午饭（中午11:30）
        /// </summary>
        private void OnReturnsForLunch()
        {
            Debug.Log("妈妈回来做午饭了");
            currentBehavior = MomBehavior.Cooking;
            currentCallCount = 0;
            hasCalledForMeal = false;
            
            // 回到家
            Activate();
            StartCoroutine(ReturnHomeAndCook());
            
            OnMomReturnsHome?.Invoke();
        }
        
        private IEnumerator ReturnHomeAndCook()
        {
            // 移动到家门口
            if (gatePosition != null)
            {
                MoveTo(gatePosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 移动到厨房
            if (kitchenPosition != null)
            {
                SetState(NPCState.Walking);
                MoveTo(kitchenPosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 开始做饭
            SetState(NPCState.Working);
            Debug.Log("妈妈开始做饭...");
        }
        
        /// <summary>
        /// 妈妈喊吃饭
        /// </summary>
        private void OnCallsForMeal()
        {
            if (currentBehavior == MomBehavior.Cooking || 
                currentBehavior == MomBehavior.CallingForMeal)
            {
                currentBehavior = MomBehavior.CallingForMeal;
                currentCallCount++;
                
                Debug.Log($"妈妈第{currentCallCount}次喊吃饭！");
                
                // 播放喊饭音效
                if (callForMealSounds != null && callForMealSounds.Length > 0)
                {
                    AudioClip clip = callForMealSounds[Random.Range(0, callForMealSounds.Length)];
                    PlaySound(clip);
                }
                
                // 显示喊话气泡
                ShowCallBubble($"吃饭啦！（{currentCallCount}/{maxCallCount}）");
                
                // 通知UI
                OnMomCallsForMeal?.Invoke();
                
                // 通知时间系统
                if (TimeSystem.Instance != null)
                {
                    TimeSystem.Instance.OnPlayerEating();
                }
            }
        }
        
        /// <summary>
        /// 妈妈踢玩家（喊了4遍后）
        /// </summary>
        private void OnKicksPlayer()
        {
            if (isKicking) return;
            
            Debug.Log("妈妈生气了，要踢玩家！");
            currentBehavior = MomBehavior.KickingPlayer;
            
            // 找到最近的玩家并踢
            if (kickCoroutine != null)
            {
                StopCoroutine(kickCoroutine);
            }
            kickCoroutine = StartCoroutine(KickPlayerSequence());
        }
        
        private IEnumerator KickPlayerSequence()
        {
            isKicking = true;
            
            // 暂停游戏时间
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.PauseTime();
            }
            
            // 找到最近的玩家
            Transform player = GetNearestPlayer();
            if (player != null)
            {
                // 移动到玩家身边
                MoveTo(player.position);
                yield return new WaitUntil(() => HasReachedDestination());
                
                // 面向玩家
                LookAtTarget(player);
                
                // 播放踢人动画
                if (animator != null)
                {
                    animator.SetTrigger("Kick");
                }
                
                // 播放踢人音效
                PlaySound(kickSound);
                
                // 等待动画
                yield return new WaitForSeconds(1f);
                
                // 通知游戏管理器
                OnMomKicksPlayer?.Invoke();
                
                // 强制玩家吃饭（显示吃饭UI）
                ShowCallBubble("快去吃饭！");
                
                // 等待玩家吃饭（模拟）
                yield return new WaitForSeconds(3f);
            }
            
            // 恢复游戏
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.ResumeTime();
            }
            
            isKicking = false;
            currentBehavior = MomBehavior.AtHome;
        }
        
        /// <summary>
        /// 妈妈午饭后出门（下午13:00）
        /// </summary>
        private void OnLeavesAfterLunch()
        {
            Debug.Log("妈妈下午继续出门干活");
            currentBehavior = MomBehavior.GoneToWork;
            currentCallCount = 0;
            
            StartCoroutine(LeaveForWork());
        }
        
        private IEnumerator LeaveForWork()
        {
            // 移动到门口
            if (gatePosition != null)
            {
                MoveTo(gatePosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 去干活
            if (workPosition != null)
            {
                MoveTo(workPosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 离开场景
            Deactivate();
            
            OnMomLeavesHome?.Invoke();
        }
        
        /// <summary>
        /// 妈妈回来做晚饭（傍晚17:30）
        /// </summary>
        private void OnReturnsForDinner()
        {
            Debug.Log("妈妈回来做晚饭了");
            currentBehavior = MomBehavior.Cooking;
            currentCallCount = 0;
            
            Activate();
            StartCoroutine(ReturnHomeAndCook());
            
            OnMomReturnsHome?.Invoke();
        }
        
        /// <summary>
        /// 牛需要牵回牛圈（傍晚19:00）
        /// </summary>
        private void OnCowNeedsReturn()
        {
            Debug.Log("妈妈让玩家把牛牵回牛圈");
            currentBehavior = MomBehavior.LeadingCowIn;
            
            // 这里应该通知玩家去牵牛
            // 如果玩家不去，妈妈会踢
            StartCoroutine(CowReturnSequence());
        }
        
        private IEnumerator CowReturnSequence()
        {
            // 等待玩家响应
            float waitTime = 0f;
            bool playerResponded = false;
            
            while (waitTime < 300f && !playerResponded) // 等待5分钟（游戏内）
            {
                // 检查玩家是否去牵牛了
                CowNPC cow = FindObjectOfType<CowNPC>();
                if (cow != null && cow.IsReturned())
                {
                    playerResponded = true;
                }
                
                waitTime += Time.deltaTime * TimeSystem.Instance?.GetProgress() ?? 1f;
                yield return null;
            }
            
            // 如果玩家没去，妈妈踢人
            if (!playerResponded)
            {
                OnKicksPlayer();
            }
        }
        
        #endregion
        
        #region 行为更新
        
        private void UpdateBehavior()
        {
            switch (currentBehavior)
            {
                case MomBehavior.AtHome:
                    UpdateAtHome();
                    break;
                case MomBehavior.Cooking:
                    UpdateCooking();
                    break;
                case MomBehavior.CallingForMeal:
                    UpdateCallingForMeal();
                    break;
            }
        }
        
        private void UpdateAtHome()
        {
            // 在家时随机走动或休息
            if (HasReachedDestination() && Random.value < 0.01f)
            {
                // 随机移动到家附近
                Vector3 randomPos = homePosition.position + Random.insideUnitSphere * 3f;
                randomPos.y = homePosition.position.y;
                MoveTo(randomPos);
            }
        }
        
        private void UpdateCooking()
        {
            // 做饭动画
            if (animator != null)
            {
                animator.SetBool("IsCooking", true);
            }
        }
        
        private void UpdateCallingForMeal()
        {
            // 持续喊饭
            if (!hasCalledForMeal)
            {
                hasCalledForMeal = true;
                // 喊饭逻辑在时间事件中处理
            }
        }
        
        #endregion
        
        #region UI提示
        
        private void ShowCallBubble(string message)
        {
            if (callBubblePrefab != null && bubblePosition != null)
            {
                GameObject bubble = Instantiate(callBubblePrefab, bubblePosition.position, Quaternion.identity);
                bubble.transform.SetParent(bubblePosition);
                
                // 设置气泡文字
                TMPro.TextMeshPro tmp = bubble.GetComponentInChildren<TMPro.TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = message;
                }
                
                // 自动销毁
                Destroy(bubble, 3f);
            }
            
            Debug.Log($"[妈妈] {message}");
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取当前行为状态
        /// </summary>
        public MomBehavior GetCurrentBehavior()
        {
            return currentBehavior;
        }
        
        /// <summary>
        /// 获取喊饭次数
        /// </summary>
        public int GetCurrentCallCount()
        {
            return currentCallCount;
        }
        
        /// <summary>
        /// 是否在家
        /// </summary>
        public bool IsAtHome()
        {
            return currentBehavior != MomBehavior.GoneToWork;
        }
        
        /// <summary>
        /// 是否在做饭
        /// </summary>
        public bool IsCooking()
        {
            return currentBehavior == MomBehavior.Cooking;
        }
        
        /// <summary>
        /// 响应吃饭
        /// </summary>
        public void OnPlayerRespondsToMeal()
        {
            currentCallCount = 0;
            currentBehavior = MomBehavior.AtHome;
            
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.OnPlayerEating();
            }
        }
        
        #endregion
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // 绘制家的位置
            if (homePosition != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(homePosition.position, 1f);
            }
            
            // 绘制工作位置
            if (workPosition != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(workPosition.position, 1f);
            }
            
            // 绘制厨房位置
            if (kitchenPosition != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(kitchenPosition.position, 1f);
            }
        }
        
        void OnDestroy()
        {
            // 取消订阅事件
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.OnTimeEvent -= HandleTimeEvent;
            }
        }
    }
}
