using UnityEngine;
using System.Collections;

namespace Dapaolou.NPC
{
    /// <summary>
    /// 猫的状态
    /// </summary>
    public enum CatState
    {
        Idle,           // 空闲
        Wandering,      // 闲逛中
        Lying,          // 躺着
        Sitting,        // 坐着
        Disturbing,     // 骚扰玩家中
        Fleeing         // 逃跑中
    }

    /// <summary>
    /// 宠物猫NPC - 骚咪
    /// </summary>
    public class CatNPC : NPCBase
    {
        [Header("猫配置")]
        [SerializeField] private Transform[] restPositions;        // 休息位置（台阶、土墙）
        [SerializeField] private Transform[] wanderPoints;         // 闲逛点
        [SerializeField] private Transform[] lieDownPositions;     // 躺下的位置
        
        [Header("行为配置")]
        [SerializeField] private float wanderChance = 0.3f;        // 闲逛概率
        [SerializeField] private float lieDownChance = 0.2f;       // 躺下概率
        [SerializeField] private float disturbChance = 0.15f;      // 骚扰概率
        [SerializeField] private float actionInterval = 30f;       // 行为切换间隔
        
        [Header("骚扰配置")]
        [SerializeField] private float disturbRange = 3f;          // 骚扰范围
        [SerializeField] private float disturbDuration = 20f;      // 骚扰持续时间
        [SerializeField] private LayerMask marbleLayer;            // 弹珠层级
        
        [Header("音效")]
        [SerializeField] private AudioClip[] meowSounds;          // 猫叫声
        [SerializeField] private AudioClip[] purrSounds;          // 呼噜声
        [SerializeField] private AudioClip[] hissSounds;          // 嘶嘶声
        
        [Header("动画")]
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip lieDownClip;
        [SerializeField] private AnimationClip sitClip;
        [SerializeField] private AnimationClip playClip;
        
        // 猫的状态
        private CatState catState = CatState.Idle;
        private float actionTimer = 0f;
        private int currentRestPosition = 0;
        private int currentWanderPoint = 0;
        
        // 骚扰相关
        private bool isDisturbing = false;
        private float disturbTimer = 0f;
        private GameObject targetMarble = null;
        
        // 事件
        public System.Action OnCatDisturbs;
        public System.Action OnCatFlees;
        
        protected override void InitializeNPC()
        {
            base.InitializeNPC();
            
            // 初始在某个休息位置
            if (restPositions != null && restPositions.Length > 0)
            {
                currentRestPosition = Random.Range(0, restPositions.Length);
                transform.position = restPositions[currentRestPosition].position;
            }
            
            // 猫的移动速度
            if (navAgent != null)
            {
                navAgent.speed = walkSpeed * 0.5f;
            }
        }
        
        protected override void UpdateNPC()
        {
            base.UpdateNPC();
            
            // 更新行为计时
            UpdateActionTimer();
            
            // 更新猫的行为
            UpdateCatBehavior();
            
            // 更新骚扰逻辑
            UpdateDisturb();
        }
        
        #region 行为更新
        
        private void UpdateActionTimer()
        {
            actionTimer += Time.deltaTime;
            
            if (actionTimer >= actionInterval)
            {
                actionTimer = 0f;
                DecideNextAction();
            }
        }
        
        /// <summary>
        /// 决定下一个行为
        /// </summary>
        private void DecideNextAction()
        {
            if (isDisturbing) return;
            
            float random = Random.value;
            
            if (random < wanderChance)
            {
                StartWandering();
            }
            else if (random < wanderChance + lieDownChance)
            {
                StartLyingDown();
            }
            else if (random < wanderChance + lieDownChance + disturbChance)
            {
                TryDisturb();
            }
            else
            {
                StartSitting();
            }
        }
        
        private void UpdateCatBehavior()
        {
            switch (catState)
            {
                case CatState.Idle:
                    UpdateIdle();
                    break;
                case CatState.Wandering:
                    UpdateWandering();
                    break;
                case CatState.Lying:
                    UpdateLying();
                    break;
                case CatState.Sitting:
                    UpdateSitting();
                    break;
                case CatState.Disturbing:
                    // 骚扰逻辑在UpdateDisturb中处理
                    break;
                case CatState.Fleeing:
                    UpdateFleeing();
                    break;
            }
        }
        
        private void UpdateIdle()
        {
            // 空闲时偶尔叫一声
            if (Random.value < 0.001f)
            {
                PlayRandomMeow();
            }
        }
        
        private void UpdateWandering()
        {
            // 到达目标后停下
            if (HasReachedDestination())
            {
                // 随机决定：继续闲逛还是停下
                if (Random.value < 0.3f)
                {
                    catState = CatState.Idle;
                    StopMoving();
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
        }
        
        private void UpdateLying()
        {
            // 躺着时偶尔翻身或叫唤
            if (Random.value < 0.002f)
            {
                if (animator != null)
                {
                    animator.SetTrigger("Roll");
                }
            }
            
            if (Random.value < 0.001f)
            {
                PlayRandomPurr();
            }
        }
        
        private void UpdateSitting()
        {
            // 坐着时观察四周
            if (Random.value < 0.005f)
            {
                // 随机转头
                float randomAngle = Random.Range(-45f, 45f);
                transform.Rotate(0, randomAngle, 0);
            }
        }
        
        private void UpdateFleeing()
        {
            // 逃跑时播放嘶嘶声
            if (Random.value < 0.01f)
            {
                PlayRandomHiss();
            }
        }
        
        #endregion
        
        #region 闲逛
        
        private void StartWandering()
        {
            if (wanderPoints == null || wanderPoints.Length == 0) return;
            
            catState = CatState.Wandering;
            currentWanderPoint = Random.Range(0, wanderPoints.Length);
            MoveTo(wanderPoints[currentWanderPoint].position);
            
            if (animator != null)
            {
                animator.SetBool("IsWalking", true);
            }
        }
        
        #endregion
        
        #region 躺下
        
        private void StartLyingDown()
        {
            if (lieDownPositions == null || lieDownPositions.Length == 0) return;
            
            catState = CatState.Lying;
            
            // 移动到躺下位置
            int lieIndex = Random.Range(0, lieDownPositions.Length);
            MoveTo(lieDownPositions[lieIndex].position);
            
            StartCoroutine(LieDownSequence());
        }
        
        private IEnumerator LieDownSequence()
        {
            yield return new WaitUntil(() => HasReachedDestination());
            
            StopMoving();
            
            if (animator != null)
            {
                animator.SetBool("IsLying", true);
            }
            
            // 躺一段时间
            float lieDuration = Random.Range(30f, 120f);
            yield return new WaitForSeconds(lieDuration);
            
            // 起来
            if (animator != null)
            {
                animator.SetBool("IsLying", false);
            }
            
            catState = CatState.Idle;
        }
        
        #endregion
        
        #region 坐下
        
        private void StartSitting()
        {
            catState = CatState.Sitting;
            StopMoving();
            
            if (animator != null)
            {
                animator.SetBool("IsSitting", true);
            }
            
            // 坐一段时间
            StartCoroutine(SitSequence());
        }
        
        private IEnumerator SitSequence()
        {
            float sitDuration = Random.Range(20f, 60f);
            yield return new WaitForSeconds(sitDuration);
            
            if (animator != null)
            {
                animator.SetBool("IsSitting", false);
            }
            
            catState = CatState.Idle;
        }
        
        #endregion
        
        #region 骚扰
        
        /// <summary>
        /// 尝试骚扰玩家
        /// </summary>
        private void TryDisturb()
        {
            // 检查附近是否有弹珠
            Collider[] marbles = Physics.OverlapSphere(transform.position, disturbRange, marbleLayer);
            
            if (marbles.Length > 0)
            {
                // 找到最近的弹珠
                targetMarble = FindNearestMarble(marbles);
                
                if (targetMarble != null)
                {
                    StartDisturbing();
                }
            }
        }
        
        private GameObject FindNearestMarble(Collider[] marbles)
        {
            GameObject nearest = null;
            float minDistance = float.MaxValue;
            
            foreach (var marble in marbles)
            {
                float distance = Vector3.Distance(transform.position, marble.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = marble.gameObject;
                }
            }
            
            return nearest;
        }
        
        private void StartDisturbing()
        {
            if (targetMarble == null) return;
            
            Debug.Log("骚咪要去骚扰弹珠了！");
            isDisturbing = true;
            catState = CatState.Disturbing;
            disturbTimer = 0f;
            
            OnCatDisturbs?.Invoke();
            
            // 移动到弹珠旁边
            MoveTo(targetMarble.transform.position);
            
            if (animator != null)
            {
                animator.SetBool("IsDisturbing", true);
            }
            
            // 播放叫声
            PlayRandomMeow();
        }
        
        private void UpdateDisturb()
        {
            if (!isDisturbing) return;
            
            disturbTimer += Time.deltaTime;
            
            // 检查弹珠是否还在
            if (targetMarble == null)
            {
                StopDisturbing();
                return;
            }
            
            // 到达弹珠位置后，拨弄弹珠
            if (HasReachedDestination())
            {
                // 随机拨弄弹珠
                if (Random.value < 0.1f)
                {
                    Rigidbody rb = targetMarble.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 pushDirection = Random.insideUnitSphere;
                        pushDirection.y = 0;
                        rb.AddForce(pushDirection * 2f, ForceMode.Impulse);
                    }
                    
                    PlayRandomMeow();
                }
                
                // 随机移动到弹珠旁边
                if (Random.value < 0.05f)
                {
                    Vector3 offset = Random.insideUnitSphere * 0.5f;
                    offset.y = 0;
                    MoveTo(targetMarble.transform.position + offset);
                }
            }
            
            // 骚扰时间结束或被赶走
            if (disturbTimer >= disturbDuration)
            {
                StopDisturbing();
            }
        }
        
        private void StopDisturbing()
        {
            isDisturbing = false;
            targetMarble = null;
            catState = CatState.Idle;
            StopMoving();
            
            if (animator != null)
            {
                animator.SetBool("IsDisturbing", false);
            }
        }
        
        #endregion
        
        #region 逃跑
        
        /// <summary>
        /// 赶走猫
        /// </summary>
        public void ScareAway()
        {
            if (catState == CatState.Fleeing) return;
            
            Debug.Log("赶走骚咪！");
            catState = CatState.Fleeing;
            
            // 停止骚扰
            if (isDisturbing)
            {
                StopDisturbing();
            }
            
            OnCatFlees?.Invoke();
            
            // 播放嘶嘶声
            PlayRandomHiss();
            
            // 跑开
            StartCoroutine(FleeSequence());
        }
        
        private IEnumerator FleeSequence()
        {
            // 找到远离玩家的方向
            Transform player = GetNearestPlayer();
            Vector3 fleeDirection;
            
            if (player != null)
            {
                fleeDirection = (transform.position - player.position).normalized;
            }
            else
            {
                fleeDirection = Random.insideUnitSphere;
                fleeDirection.y = 0;
            }
            
            // 跑到远处
            Vector3 fleeTarget = transform.position + fleeDirection * 15f;
            
            navAgent.speed = runSpeed;
            MoveTo(fleeTarget);
            
            // 播放逃跑动画
            if (animator != null)
            {
                animator.SetBool("IsFleeing", true);
            }
            
            // 等待逃跑
            yield return new WaitForSeconds(5f);
            
            // 恢复正常
            navAgent.speed = walkSpeed * 0.5f;
            
            if (animator != null)
            {
                animator.SetBool("IsFleeing", false);
            }
            
            catState = CatState.Idle;
            
            // 移动到休息位置
            if (restPositions != null && restPositions.Length > 0)
            {
                currentRestPosition = Random.Range(0, restPositions.Length);
                MoveTo(restPositions[currentRestPosition].position);
            }
        }
        
        #endregion
        
        #region 音效
        
        private void PlayRandomMeow()
        {
            if (meowSounds != null && meowSounds.Length > 0)
            {
                AudioClip clip = meowSounds[Random.Range(0, meowSounds.Length)];
                PlaySound(clip, 0.5f);
                Debug.Log("喵~");
            }
        }
        
        private void PlayRandomPurr()
        {
            if (purrSounds != null && purrSounds.Length > 0)
            {
                AudioClip clip = purrSounds[Random.Range(0, purrSounds.Length)];
                PlaySound(clip, 0.3f);
            }
        }
        
        private void PlayRandomHiss()
        {
            if (hissSounds != null && hissSounds.Length > 0)
            {
                AudioClip clip = hissSounds[Random.Range(0, hissSounds.Length)];
                PlaySound(clip, 0.7f);
                Debug.Log("嘶~~~");
            }
        }
        
        #endregion
        
        #region 交互
        
        /// <summary>
        /// 与猫交互
        /// </summary>
        public override void Interact()
        {
            base.Interact();
            
            if (isDisturbing)
            {
                // 赶走猫
                ScareAway();
            }
            else if (catState == CatState.Lying || catState == CatState.Sitting)
            {
                // 摸猫
                Debug.Log("摸摸骚咪");
                PlayRandomPurr();
                
                if (animator != null)
                {
                    animator.SetTrigger("Pet");
                }
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取猫的状态
        /// </summary>
        public CatState GetCatState()
        {
            return catState;
        }
        
        /// <summary>
        /// 是否在骚扰
        /// </summary>
        public bool IsDisturbing()
        {
            return isDisturbing;
        }
        
        /// <summary>
        /// 是否躺着
        /// </summary>
        public bool IsLying()
        {
            return catState == CatState.Lying;
        }
        
        /// <summary>
        /// 是否坐着
        /// </summary>
        public bool IsSitting()
        {
            return catState == CatState.Sitting;
        }
        
        /// <summary>
        /// 在院子里（可以被赶走）
        /// </summary>
        public bool IsInYard()
        {
            return catState != CatState.Fleeing;
        }
        
        #endregion
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // 绘制休息位置
            if (restPositions != null)
            {
                Gizmos.color = Color.green;
                foreach (var pos in restPositions)
                {
                    if (pos != null)
                    {
                        Gizmos.DrawWireCube(pos.position, new Vector3(1f, 0.5f, 1f));
                    }
                }
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
            
            // 绘制躺下位置
            if (lieDownPositions != null)
            {
                Gizmos.color = Color.cyan;
                foreach (var pos in lieDownPositions)
                {
                    if (pos != null)
                    {
                        Gizmos.DrawWireSphere(pos.position, 0.8f);
                    }
                }
            }
            
            // 绘制骚扰范围
            if (isDisturbing)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, disturbRange);
            }
        }
    }
}
