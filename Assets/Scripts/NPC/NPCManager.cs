using UnityEngine;
using System.Collections.Generic;
using Dapaolou.GameTime;

namespace Dapaolou.NPC
{
    /// <summary>
    /// NPC管理器 - 管理所有NPC和时间事件
    /// </summary>
    public class NPCManager : MonoBehaviour
    {
        [Header("NPC引用")]
        [SerializeField] private MomNPC mom;
        [SerializeField] private CowNPC cow;
        [SerializeField] private DogNPC dog;
        [SerializeField] private CatNPC cat;
        [SerializeField] private BrotherNPC brother1;
        [SerializeField] private BrotherNPC brother2;
        
        [Header("交互配置")]
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private LayerMask npcLayer;
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        
        [Header("UI提示")]
        [SerializeField] private GameObject interactionPromptPrefab;
        [SerializeField] private Transform promptPosition;
        
        // 单例
        public static NPCManager Instance { get; private set; }
        
        // 交互状态
        private NPCBase nearestNPC = null;
        private bool canInteract = false;
        
        // 事件
        public System.Action<NPCBase> OnNPCInteraction;
        public System.Action<string> OnShowMessage;
        
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
        
        void Start()
        {
            // 自动查找NPC
            if (mom == null) mom = FindObjectOfType<MomNPC>();
            if (cow == null) cow = FindObjectOfType<CowNPC>();
            if (dog == null) dog = FindObjectOfType<DogNPC>();
            if (cat == null) cat = FindObjectOfType<CatNPC>();
            if (brother1 == null)
            {
                BrotherNPC[] brothers = FindObjectsOfType<BrotherNPC>();
                if (brothers.Length > 0) brother1 = brothers[0];
                if (brothers.Length > 1) brother2 = brothers[1];
            }
            
            // 订阅时间事件
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.OnTimeEvent += HandleTimeEvent;
            }
            
            // 订阅NPC事件
            SubscribeNPCEvents();
        }
        
        void Update()
        {
            // 检查附近的NPC
            CheckNearbyNPCs();
            
            // 处理交互输入
            HandleInteractionInput();
        }
        
        #region NPC事件订阅
        
        private void SubscribeNPCEvents()
        {
            // 妈妈事件
            if (mom != null)
            {
                mom.OnMomCallsForMeal += OnMomCallsForMeal;
                mom.OnMomKicksPlayer += OnMomKicksPlayer;
            }
            
            // 牛事件
            if (cow != null)
            {
                cow.OnCowNeedsWater += OnCowNeedsWater;
                cow.OnCowReturned += OnCowReturned;
            }
            
            // 狗事件
            if (dog != null)
            {
                dog.OnDogEscapes += OnDogEscapes;
                dog.OnDogBarks += OnDogBarks;
            }
            
            // 猫事件
            if (cat != null)
            {
                cat.OnCatDisturbs += OnCatDisturbs;
            }
            
            // 弟弟事件
            if (brother1 != null)
            {
                brother1.OnBrotherAgrees += OnBrotherAgrees;
                brother1.OnBrotherRefuses += OnBrotherRefuses;
                brother1.OnBrotherKicked += OnBrotherKicked;
            }
            if (brother2 != null)
            {
                brother2.OnBrotherAgrees += OnBrotherAgrees;
                brother2.OnBrotherRefuses += OnBrotherRefuses;
                brother2.OnBrotherKicked += OnBrotherKicked;
            }
        }
        
        #endregion
        
        #region 时间事件处理
        
        private void HandleTimeEvent(TimeEventType eventType)
        {
            switch (eventType)
            {
                // 妈妈相关
                case TimeEventType.MomLeavesForWork:
                    Debug.Log("妈妈出门干活了，今天又是个好天气");
                    break;
                    
                case TimeEventType.MomReturnsForLunch:
                    Debug.Log("妈妈回来了，开始做午饭");
                    ShowMessage("妈妈回来做午饭了");
                    break;
                    
                case TimeEventType.MomCallsForLunch:
                    int lunchCount = TimeSystem.Instance?.GetLunchCallCount() ?? 0;
                    Debug.Log($"妈妈喊吃饭了！第{lunchCount}次");
                    ShowMessage($"吃饭啦！（{lunchCount}/4）");
                    break;
                    
                case TimeEventType.MomKicksPlayer:
                    Debug.Log("妈妈生气了，踢了你一脚！");
                    ShowMessage("妈妈踢了你一脚！快去吃饭！");
                    HandleMomKick();
                    break;
                    
                case TimeEventType.MomLeavesAfterLunch:
                    Debug.Log("妈妈下午继续出门干活");
                    ShowMessage("妈妈出门了");
                    break;
                    
                case TimeEventType.MomReturnsForDinner:
                    Debug.Log("妈妈回来做晚饭了");
                    ShowMessage("妈妈回来做晚饭了");
                    break;
                    
                case TimeEventType.MomCallsForDinner:
                    int dinnerCount = TimeSystem.Instance?.GetDinnerCallCount() ?? 0;
                    Debug.Log($"妈妈喊吃晚饭了！第{dinnerCount}次");
                    ShowMessage($"吃晚饭啦！（{dinnerCount}/4）");
                    break;
                    
                case TimeEventType.MomKicksPlayerDinner:
                    Debug.Log("妈妈生气了，踢了你一脚！");
                    ShowMessage("妈妈踢了你一脚！快去吃饭！");
                    HandleMomKick();
                    break;
                    
                // 牛相关
                case TimeEventType.CowNeedsWater:
                    Debug.Log("牛渴了，需要喝水");
                    ShowMessage("牛渴了，去打桶水给牛喝");
                    break;
                    
                case TimeEventType.CowNeedsReturn:
                    Debug.Log("天快黑了，要把牛牵回牛圈");
                    ShowMessage("把牛牵回牛圈");
                    break;
                    
                // 狗相关
                case TimeEventType.DogEscapes:
                    Debug.Log("臭豆跑出来了！");
                    ShowMessage("臭豆跑出来了！");
                    break;
                    
                // 猫相关
                case TimeEventType.CatDisturbs:
                    Debug.Log("骚咪在捣乱！");
                    ShowMessage("骚咪在骚扰弹珠！");
                    break;
                    
                // 时间相关
                case TimeEventType.NightFalls:
                    Debug.Log("天黑了，光线变暗");
                    ShowMessage("天黑了...");
                    break;
                    
                case TimeEventType.GameEnd:
                    Debug.Log("晚上8点了，游戏结束！");
                    ShowMessage("天黑了，该回家了！");
                    HandleGameEnd();
                    break;
            }
        }
        
        #endregion
        
        #region NPC事件处理
        
        private void OnMomCallsForMeal()
        {
            // 妈妈喊吃饭的处理
            // 可以播放特定音效或动画
        }
        
        private void OnMomKicksPlayer()
        {
            // 妈妈踢玩家的处理
            HandleMomKick();
        }
        
        private void OnCowNeedsWater()
        {
            // 牛需要喝水的处理
            ShowMessage("牛渴了，去打桶水给牛喝");
        }
        
        private void OnCowReturned()
        {
            // 牛回到牛圈的处理
            ShowMessage("牛已回到牛圈");
        }
        
        private void OnDogEscapes()
        {
            // 狗逃跑的处理
            ShowMessage("臭豆跑出来了！");
        }
        
        private void OnDogBarks()
        {
            // 狗叫的处理
            // 可以播放狗叫音效
        }
        
        private void OnCatDisturbs()
        {
            // 猫骚扰的处理
            ShowMessage("骚咪在骚扰弹珠！快赶走它！");
        }
        
        private void OnBrotherAgrees(int brotherIndex)
        {
            // 弟弟答应干活
            BrotherNPC brother = brotherIndex == 1 ? brother1 : brother2;
            string name = brother != null ? brother.GetBrotherName() : $"弟弟{brotherIndex}";
            ShowMessage($"{name}: 好吧，我去...");
        }
        
        private void OnBrotherRefuses(int brotherIndex)
        {
            // 弟弟推脱
            BrotherNPC brother = brotherIndex == 1 ? brother1 : brother2;
            string name = brother != null ? brother.GetBrotherName() : $"弟弟{brotherIndex}";
            ShowMessage($"{name}: 我不想去...");
        }
        
        private void OnBrotherKicked(int brotherIndex)
        {
            // 弟弟被踢
            BrotherNPC brother = brotherIndex == 1 ? brother1 : brother2;
            string name = brother != null ? brother.GetBrotherName() : $"弟弟{brotherIndex}";
            ShowMessage($"{name}被妈妈踢了一脚！");
        }
        
        #endregion
        
        #region 交互系统
        
        private void CheckNearbyNPCs()
        {
            // 找到最近的NPC
            Collider[] npcs = Physics.OverlapSphere(transform.position, interactionRange, npcLayer);
            
            nearestNPC = null;
            float minDistance = float.MaxValue;
            
            foreach (var npcCollider in npcs)
            {
                NPCBase npc = npcCollider.GetComponent<NPCBase>();
                if (npc != null && npc.IsActive())
                {
                    float distance = Vector3.Distance(transform.position, npc.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestNPC = npc;
                    }
                }
            }
            
            // 显示交互提示
            canInteract = nearestNPC != null;
            UpdateInteractionPrompt();
        }
        
        private void HandleInteractionInput()
        {
            if (!canInteract || nearestNPC == null) return;
            
            if (Input.GetKeyDown(interactKey))
            {
                InteractWithNPC(nearestNPC);
            }
        }
        
        private void InteractWithNPC(NPCBase npc)
        {
            if (npc == null) return;
            
            Debug.Log($"与 {npc.GetDisplayName()} 交互");
            
            // 调用NPC的交互方法
            npc.Interact();
            
            // 触发事件
            OnNPCInteraction?.Invoke(npc);
            
            // 根据NPC类型显示不同消息
            if (npc is MomNPC)
            {
                HandleMomInteraction();
            }
            else if (npc is CowNPC)
            {
                HandleCowInteraction();
            }
            else if (npc is DogNPC)
            {
                HandleDogInteraction();
            }
            else if (npc is CatNPC)
            {
                HandleCatInteraction();
            }
        }
        
        private void HandleMomInteraction()
        {
            if (mom == null) return;
            
            MomBehavior behavior = mom.GetCurrentBehavior();
            
            switch (behavior)
            {
                case MomBehavior.CallingForMeal:
                    // 响应吃饭
                    mom.OnPlayerRespondsToMeal();
                    ShowMessage("好的妈妈，我来吃饭了");
                    break;
                    
                case MomBehavior.LeadingCowIn:
                    // 答应去牵牛
                    ShowMessage("好的妈妈，我去牵牛");
                    break;
                    
                default:
                    ShowMessage("妈妈在忙...");
                    break;
            }
        }
        
        private void HandleCowInteraction()
        {
            if (cow == null) return;
            
            CowState state = cow.GetCowState();
            
            switch (state)
            {
                case CowState.Outside:
                    if (cow.NeedsWater())
                    {
                        // 给牛喝水
                        cow.OnPlayerGivesWater();
                        ShowMessage("给牛喝水");
                    }
                    else if (!cow.IsFollowingPlayer())
                    {
                        // 牵牛
                        cow.StartFollowing(transform);
                        ShowMessage("牵着牛");
                    }
                    break;
                    
                case CowState.Following:
                    // 检查是否在牛圈附近
                    // 这里需要检查牛圈位置
                    ShowMessage("牛跟着你");
                    break;
                    
                default:
                    ShowMessage("牛在休息...");
                    break;
            }
        }
        
        private void HandleDogInteraction()
        {
            if (dog == null) return;
            
            DogState state = dog.GetDogState();
            
            switch (state)
            {
                case DogState.Tied:
                    ShowMessage("摸摸臭豆");
                    break;
                    
                case DogState.Watching:
                    ShowMessage("臭豆在看你玩耍");
                    break;
                    
                case DogState.Wandering:
                    ShowMessage("赶走臭豆");
                    break;
                    
                default:
                    break;
            }
        }
        
        private void HandleCatInteraction()
        {
            if (cat == null) return;
            
            CatState state = cat.GetCatState();
            
            if (cat.IsDisturbing())
            {
                // 赶走猫
                cat.ScareAway();
                ShowMessage("走开，骚咪！");
            }
            else if (cat.IsLying() || cat.IsSitting())
            {
                ShowMessage("摸摸骚咪");
            }
            else
            {
                ShowMessage("骚咪在闲逛");
            }
        }
        
        private void UpdateInteractionPrompt()
        {
            // 这里可以显示UI提示
            // 例如："按E与妈妈对话"
        }
        
        #endregion
        
        #region 特殊事件处理
        
        private void HandleMomKick()
        {
            // 妈妈踢玩家的处理
            // 1. 暂停游戏
            // 2. 播放踢人动画
            // 3. 强制玩家吃饭
            // 4. 恢复游戏
            
            Debug.Log("被妈妈踢了一脚，乖乖去吃饭");
            
            // 通知游戏管理器
            Game.GameManager gameManager = FindObjectOfType<Game.GameManager>();
            if (gameManager != null)
            {
                // 暂停游戏
            }
        }
        
        private void HandleGameEnd()
        {
            // 游戏结束处理
            Debug.Log("游戏结束！晚上8点了！");
            
            // 通知游戏管理器
            Game.GameManager gameManager = FindObjectOfType<Game.GameManager>();
            if (gameManager != null)
            {
                // 触发游戏结束
            }
        }
        
        #endregion
        
        #region UI提示
        
        private void ShowMessage(string message)
        {
            Debug.Log($"[NPC] {message}");
            OnShowMessage?.Invoke(message);
            
            // 显示UI消息
            // 这里可以实例化消息预制体
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取妈妈NPC
        /// </summary>
        public MomNPC GetMom()
        {
            return mom;
        }
        
        /// <summary>
        /// 获取牛NPC
        /// </summary>
        public CowNPC GetCow()
        {
            return cow;
        }
        
        /// <summary>
        /// 获取狗NPC
        /// </summary>
        public DogNPC GetDog()
        {
            return dog;
        }
        
        /// <summary>
        /// 获取猫NPC
        /// </summary>
        public CatNPC GetCat()
        {
            return cat;
        }
        
        /// <summary>
        /// 获取弟弟1
        /// </summary>
        public BrotherNPC GetBrother1()
        {
            return brother1;
        }
        
        /// <summary>
        /// 获取弟弟2
        /// </summary>
        public BrotherNPC GetBrother2()
        {
            return brother2;
        }
        
        /// <summary>
        /// 获取所有弟弟
        /// </summary>
        public List<BrotherNPC> GetBrothers()
        {
            List<BrotherNPC> brothers = new List<BrotherNPC>();
            if (brother1 != null) brothers.Add(brother1);
            if (brother2 != null) brothers.Add(brother2);
            return brothers;
        }
        
        /// <summary>
        /// 获取所有玩家（包括主角和弟弟）
        /// </summary>
        public List<string> GetAllPlayerNames()
        {
            List<string> names = new List<string>();
            names.Add("我"); // 主角
            if (brother1 != null) names.Add(brother1.GetBrotherName());
            if (brother2 != null) names.Add(brother2.GetBrotherName());
            return names;
        }
        
        /// <summary>
        /// 是否可以交互
        /// </summary>
        public bool CanInteract()
        {
            return canInteract;
        }
        
        /// <summary>
        /// 获取最近的NPC
        /// </summary>
        public NPCBase GetNearestNPC()
        {
            return nearestNPC;
        }
        
        #endregion
        
        #region Gizmos
        
        void OnDrawGizmosSelected()
        {
            // 绘制交互范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
        
        #endregion
        
        void OnDestroy()
        {
            // 取消订阅事件
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.OnTimeEvent -= HandleTimeEvent;
            }
            
            if (mom != null)
            {
                mom.OnMomCallsForMeal -= OnMomCallsForMeal;
                mom.OnMomKicksPlayer -= OnMomKicksPlayer;
            }
            
            if (cow != null)
            {
                cow.OnCowNeedsWater -= OnCowNeedsWater;
                cow.OnCowReturned -= OnCowReturned;
            }
            
            if (dog != null)
            {
                dog.OnDogEscapes -= OnDogEscapes;
                dog.OnDogBarks -= OnDogBarks;
            }
            
            if (cat != null)
            {
                cat.OnCatDisturbs -= OnCatDisturbs;
            }
            
            if (brother1 != null)
            {
                brother1.OnBrotherAgrees -= OnBrotherAgrees;
                brother1.OnBrotherRefuses -= OnBrotherRefuses;
                brother1.OnBrotherKicked -= OnBrotherKicked;
            }
            
            if (brother2 != null)
            {
                brother2.OnBrotherAgrees -= OnBrotherAgrees;
                brother2.OnBrotherRefuses -= OnBrotherRefuses;
                brother2.OnBrotherKicked -= OnBrotherKicked;
            }
        }
    }
}
