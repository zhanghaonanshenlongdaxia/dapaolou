using UnityEngine;
using System.Collections;

namespace Dapaolou.NPC
{
    /// <summary>
    /// 牛的状态
    /// </summary>
    public enum CowState
    {
        InPen,          // 在牛圈里
        Outside,        // 在院子外
        Drinking,       // 喝水中
        Following,      // 跟随玩家
        Returned        // 已回到牛圈
    }

    /// <summary>
    /// 牛NPC
    /// </summary>
    public class CowNPC : NPCBase
    {
        [Header("牛配置")]
        [SerializeField] private Transform penPosition;            // 牛圈位置
        [SerializeField] private Transform outsidePosition;        // 院子外位置
        [SerializeField] private Transform waterPosition;          // 水桶位置
        
        [Header("音效")]
        [SerializeField] private AudioClip[] moosSounds;           // 牛叫声
        [SerializeField] private float mooInterval = 30f;          // 叫声间隔
        [SerializeField] private float mooRandomRange = 10f;       // 随机范围
        
        [Header("交互")]
        [SerializeField] private float followDistance = 2f;        // 跟随距离
        [SerializeField] private GameObject leadRopePrefab;        // 牵绳预制体
        [SerializeField] private Transform ropeAttachPoint;        // 绳子连接点
        
        // 牛的状态
        private CowState cowState = CowState.InPen;
        private bool isFollowingPlayer = false;
        private Transform followingPlayer = null;
        private bool needsWater = false;
        private bool hasDrunk = false;
        
        // 叫声计时
        private float mooTimer = 0f;
        private float nextMooTime = 0f;
        
        // 事件
        public System.Action OnCowNeedsWater;
        public System.Action OnCowReturned;
        public System.Action OnCowFollowsPlayer;
        
        protected override void InitializeNPC()
        {
            base.InitializeNPC();
            
            // 初始在牛圈
            if (penPosition != null)
            {
                transform.position = penPosition.position;
            }
            
            // 设置第一次叫的时间
            nextMooTime = Random.Range(mooInterval - mooRandomRange, mooInterval + mooRandomRange);
        }
        
        protected override void UpdateNPC()
        {
            base.UpdateNPC();
            
            // 更新牛叫声
            UpdateMooTimer();
            
            // 更新跟随逻辑
            UpdateFollowing();
            
            // 更新状态
            UpdateCowState();
        }
        
        #region 牛叫声
        
        private void UpdateMooTimer()
        {
            if (cowState == CowState.InPen || cowState == CowState.Returned) return;
            
            mooTimer += Time.deltaTime;
            
            if (mooTimer >= nextMooTime)
            {
                mooTimer = 0f;
                nextMooTime = Random.Range(mooInterval - mooRandomRange, mooInterval + mooRandomRange);
                
                PlayRandomMoo();
            }
        }
        
        private void PlayRandomMoo()
        {
            if (moosSounds != null && moosSounds.Length > 0)
            {
                AudioClip clip = moosSounds[Random.Range(0, moosSounds.Length)];
                PlaySound(clip, 0.8f);
                Debug.Log("哞~~~");
            }
        }
        
        #endregion
        
        #region 跟随逻辑
        
        private void UpdateFollowing()
        {
            if (!isFollowingPlayer || followingPlayer == null) return;
            
            // 检查距离
            float distance = Vector3.Distance(transform.position, followingPlayer.position);
            
            if (distance > followDistance + 1f)
            {
                // 跟上玩家
                MoveTo(followingPlayer.position);
            }
            else if (distance < followDistance * 0.5f)
            {
                // 太近了，停下
                StopMoving();
            }
        }
        
        /// <summary>
        /// 开始跟随玩家
        /// </summary>
        public void StartFollowing(Transform player)
        {
            if (player == null) return;
            
            followingPlayer = player;
            isFollowingPlayer = true;
            cowState = CowState.Following;
            
            // 显示牵绳
            if (leadRopePrefab != null && ropeAttachPoint != null)
            {
                GameObject rope = Instantiate(leadRopePrefab, ropeAttachPoint);
                rope.transform.localPosition = Vector3.zero;
            }
            
            OnCowFollowsPlayer?.Invoke();
            Debug.Log("牛开始跟随玩家");
        }
        
        /// <summary>
        /// 停止跟随
        /// </summary>
        public void StopFollowing()
        {
            followingPlayer = null;
            isFollowingPlayer = false;
            StopMoving();
            
            // 隐藏牵绳
            if (ropeAttachPoint != null)
            {
                foreach (Transform child in ropeAttachPoint)
                {
                    Destroy(child.gameObject);
                }
            }
        }
        
        #endregion
        
        #region 状态管理
        
        private void UpdateCowState()
        {
            switch (cowState)
            {
                case CowState.InPen:
                    // 在牛圈里，随机走动
                    if (HasReachedDestination() && Random.value < 0.005f)
                    {
                        Vector3 randomPos = penPosition.position + Random.insideUnitSphere * 2f;
                        randomPos.y = penPosition.position.y;
                        MoveTo(randomPos);
                    }
                    break;
                    
                case CowState.Outside:
                    // 在院子外，随机走动
                    if (HasReachedDestination() && Random.value < 0.01f)
                    {
                        Vector3 randomPos = outsidePosition.position + Random.insideUnitSphere * 5f;
                        randomPos.y = outsidePosition.position.y;
                        MoveTo(randomPos);
                    }
                    break;
                    
                case CowState.Drinking:
                    // 喝水中
                    break;
                    
                case CowState.Following:
                    // 跟随玩家中
                    break;
            }
        }
        
        #endregion
        
        #region 时间事件
        
        /// <summary>
        /// 妈妈牵牛出门（早上7:00）
        /// </summary>
        public void OnMomLeadsOut()
        {
            Debug.Log("妈妈把牛牵出牛圈");
            cowState = CowState.Outside;
            
            // 移动到院子外
            if (outsidePosition != null)
            {
                MoveTo(outsidePosition.position);
            }
        }
        
        /// <summary>
        /// 牛需要喝水（下午15:00）
        /// </summary>
        public void OnNeedsWater()
        {
            if (cowState != CowState.Outside) return;
            
            Debug.Log("牛需要喝水了");
            needsWater = true;
            hasDrunk = false;
            
            OnCowNeedsWater?.Invoke();
            
            // 播放叫声提醒
            PlayRandomMoo();
        }
        
        /// <summary>
        /// 玩家给牛喝水
        /// </summary>
        public void OnPlayerGivesWater()
        {
            if (!needsWater) return;
            
            Debug.Log("玩家给牛喝水了");
            cowState = CowState.Drinking;
            
            StartCoroutine(DrinkingSequence());
        }
        
        private IEnumerator DrinkingSequence()
        {
            // 移动到水桶位置
            if (waterPosition != null)
            {
                MoveTo(waterPosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 喝水动画
            if (animator != null)
            {
                animator.SetTrigger("Drink");
            }
            
            // 等待喝水
            yield return new WaitForSeconds(5f);
            
            // 喝完了
            needsWater = false;
            hasDrunk = true;
            cowState = CowState.Outside;
            
            Debug.Log("牛喝完水了");
        }
        
        /// <summary>
        /// 牛需要回到牛圈（傍晚19:00）
        /// </summary>
        public void OnNeedsReturn()
        {
            if (cowState == CowState.InPen || cowState == CowState.Returned) return;
            
            Debug.Log("牛需要回到牛圈");
            // 等待玩家来牵牛
        }
        
        /// <summary>
        /// 玩家牵牛回牛圈
        /// </summary>
        public void OnPlayerLeadsReturn()
        {
            Debug.Log("玩家牵牛回牛圈");
            cowState = CowState.Returned;
            StopFollowing();
            
            StartCoroutine(ReturnToPenSequence());
        }
        
        private IEnumerator ReturnToPenSequence()
        {
            // 移动到牛圈
            if (penPosition != null)
            {
                MoveTo(penPosition.position);
                yield return new WaitUntil(() => HasReachedDestination());
            }
            
            // 回到牛圈
            cowState = CowState.InPen;
            OnCowReturned?.Invoke();
            
            Debug.Log("牛已回到牛圈");
        }
        
        #endregion
        
        #region 交互
        
        /// <summary>
        /// 与牛交互（牵牛）
        /// </summary>
        public override void Interact()
        {
            base.Interact();
            
            if (cowState == CowState.Outside && !isFollowingPlayer)
            {
                // 玩家来牵牛
                Transform player = GetNearestPlayer();
                if (player != null)
                {
                    StartFollowing(player);
                }
            }
            else if (isFollowingPlayer)
            {
                // 检查是否在牛圈附近
                if (penPosition != null)
                {
                    float distanceToPen = Vector3.Distance(transform.position, penPosition.position);
                    if (distanceToPen < 3f)
                    {
                        // 牵回牛圈
                        OnPlayerLeadsReturn();
                    }
                    else
                    {
                        // 放下绳子
                        StopFollowing();
                    }
                }
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取牛的状态
        /// </summary>
        public CowState GetCowState()
        {
            return cowState;
        }
        
        /// <summary>
        /// 是否需要喝水
        /// </summary>
        public bool NeedsWater()
        {
            return needsWater;
        }
        
        /// <summary>
        /// 是否已回到牛圈
        /// </summary>
        public bool IsReturned()
        {
            return cowState == CowState.InPen || cowState == CowState.Returned;
        }
        
        /// <summary>
        /// 是否在跟随玩家
        /// </summary>
        public bool IsFollowingPlayer()
        {
            return isFollowingPlayer;
        }
        
        /// <summary>
        /// 是否在院子外
        /// </summary>
        public bool IsOutside()
        {
            return cowState == CowState.Outside || cowState == CowState.Following;
        }
        
        #endregion
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // 绘制牛圈位置
            if (penPosition != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(penPosition.position, new Vector3(3, 2, 3));
            }
            
            // 绘制院子外位置
            if (outsidePosition != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(outsidePosition.position, 5f);
            }
            
            // 绘制水桶位置
            if (waterPosition != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(waterPosition.position, 0.5f);
            }
            
            // 绘制跟随距离
            if (isFollowingPlayer && followingPlayer != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, followingPlayer.position);
                Gizmos.DrawWireSphere(followingPlayer.position, followDistance);
            }
        }
    }
}
