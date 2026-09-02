using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Dapaolou.GameTime;

namespace Dapaolou.NPC
{
    /// <summary>
    /// 派活状态
    /// </summary>
    public enum TaskDelegateState
    {
        Idle,               // 空闲
        Delegating,         // 派活中
        WaitingForResponse, // 等待响应
        KickingAll,         // 踢所有人
        ForcingPlayer       // 强制玩家
    }

    /// <summary>
    /// 妈妈派活系统 - 管理妈妈派活和弟弟推脱逻辑
    /// </summary>
    public class MomTaskDelegate : MonoBehaviour
    {
        [Header("派活配置")]
        [SerializeField] private int maxDelegateRounds = 3;        // 最大派活轮数
        [SerializeField] private float delegateInterval = 5f;      // 派活间隔
        [SerializeField] private float responseTimeout = 10f;      // 响应超时
        
        [Header("引用")]
        [SerializeField] private MomNPC mom;
        [SerializeField] private BrotherNPC brother1;
        [SerializeField] private BrotherNPC brother2;
        
        [Header("音效")]
        [SerializeField] private AudioClip delegateSound;          // 派活音效
        [SerializeField] private AudioClip kickAllSound;           // 踢所有人音效
        [SerializeField] private AudioClip forcePlayerSound;       // 强制玩家音效
        
        // 派活状态
        private TaskDelegateState currentState = TaskDelegateState.Idle;
        private int currentDelegateRound = 0;
        private int currentTargetIndex = -1;                       // 当前被指派的人
        private string currentTaskDescription = "";
        private List<int> refusedPlayers = new List<int>();
        
        // 任务队列
        private Queue<string> taskQueue = new Queue<string>();
        
        // 事件
        public System.Action<int> OnTaskDelegated;                 // 派活给某人
        public System.Action<int> OnPlayerRefuses;                 // 某人推脱
        public System.Action OnAllRefused;                         // 所有人都推脱
        public System.Action OnPlayerForced;                       // 强制玩家
        public System.Action<string> OnShowMessage;
        
        void Start()
        {
            // 自动查找引用
            if (mom == null) mom = FindObjectOfType<MomNPC>();
            if (brother1 == null)
            {
                BrotherNPC[] brothers = FindObjectsOfType<BrotherNPC>();
                if (brothers.Length > 0) brother1 = brothers[0];
                if (brothers.Length > 1) brother2 = brothers[1];
            }
        }
        
        #region 派活流程
        
        /// <summary>
        /// 妈妈派活
        /// </summary>
        public void DelegateTask(string taskDescription)
        {
            if (currentState != TaskDelegateState.Idle)
            {
                taskQueue.Enqueue(taskDescription);
                return;
            }
            
            currentTaskDescription = taskDescription;
            currentDelegateRound = 0;
            refusedPlayers.Clear();
            
            Debug.Log($"妈妈要派活: {taskDescription}");
            ShowMessage($"妈妈: 谁去{taskDescription}？");
            
            // 开始派活流程
            StartCoroutine(DelegateTaskFlow());
        }
        
        /// <summary>
        /// 派活流程
        /// </summary>
        private IEnumerator DelegateTaskFlow()
        {
            currentState = TaskDelegateState.Delegating;
            
            // 播放派活音效
            if (delegateSound != null)
            {
                AudioSource.PlayClipAtPoint(delegateSound, Camera.main.transform.position);
            }
            
            // 随机选择第一个被指派的人
            currentTargetIndex = Random.Range(0, 3); // 0=玩家, 1=弟弟1, 2=弟弟2
            
            // 派活循环
            while (currentDelegateRound < maxDelegateRounds)
            {
                currentDelegateRound++;
                
                // 指派任务
                yield return StartCoroutine(DelegateToPerson(currentTargetIndex));
                
                // 等待响应
                yield return StartCoroutine(WaitForResponse());
                
                // 检查是否有人答应
                if (currentState != TaskDelegateState.WaitingForResponse)
                {
                    break;
                }
                
                // 切换到下一个人
                currentTargetIndex = GetNextTarget();
            }
            
            // 如果所有人都推脱了
            if (currentDelegateRound >= maxDelegateRounds)
            {
                yield return StartCoroutine(KickAllAndForcePlayer());
            }
            
            // 检查是否有更多任务
            if (taskQueue.Count > 0)
            {
                string nextTask = taskQueue.Dequeue();
                yield return new WaitForSeconds(2f);
                DelegateTask(nextTask);
            }
            else
            {
                currentState = TaskDelegateState.Idle;
            }
        }
        
        /// <summary>
        /// 指派给某人
        /// </summary>
        private IEnumerator DelegateToPerson(int targetIndex)
        {
            string targetName = GetPersonName(targetIndex);
            
            Debug.Log($"妈妈: {targetName}，你去{currentTaskDescription}！");
            ShowMessage($"妈妈: {targetName}，你去{currentTaskDescription}！");
            
            OnTaskDelegated?.Invoke(targetIndex);
            
            // 播放派活动画
            if (mom != null && mom.animator != null)
            {
                mom.animator.SetTrigger("Delegate");
            }
            
            yield return new WaitForSeconds(delegateInterval);
        }
        
        /// <summary>
        /// 等待响应
        /// </summary>
        private IEnumerator WaitForResponse()
        {
            currentState = TaskDelegateState.WaitingForResponse;
            
            float timer = 0f;
            bool responded = false;
            
            while (timer < responseTimeout && !responded)
            {
                timer += Time.deltaTime;
                
                // 检查是否有人响应
                if (currentTargetIndex == 0)
                {
                    // 玩家响应
                    if (Input.GetKeyDown(KeyCode.Y))
                    {
                        OnPlayerAgrees(0);
                        responded = true;
                    }
                    else if (Input.GetKeyDown(KeyCode.N))
                    {
                        OnPlayerRefusesTask(0);
                        responded = true;
                    }
                }
                else
                {
                    // NPC响应
                    yield return new WaitForSeconds(Random.Range(2f, 4f));
                    
                    // 根据性格决定
                    BrotherNPC brother = GetBrother(currentTargetIndex);
                    if (brother != null)
                    {
                        float random = Random.value;
                        if (random < 0.3f) // 30%概率答应
                        {
                            OnPlayerAgrees(currentTargetIndex);
                        }
                        else
                        {
                            OnPlayerRefusesTask(currentTargetIndex);
                        }
                        responded = true;
                    }
                }
                
                yield return null;
            }
            
            // 超时视为推脱
            if (!responded)
            {
                OnPlayerRefusesTask(currentTargetIndex);
            }
        }
        
        /// <summary>
        /// 某人答应
        /// </summary>
        private void OnPlayerAgrees(int playerIndex)
        {
            string playerName = GetPersonName(playerIndex);
            Debug.Log($"{playerName}: 好吧，我去...");
            ShowMessage($"{playerName}: 好吧，我去...");
            
            // 通知对应的人
            if (playerIndex == 0)
            {
                // 玩家答应
                OnPlayerForced?.Invoke();
            }
            else
            {
                BrotherNPC brother = GetBrother(playerIndex);
                if (brother != null)
                {
                    brother.OnAgreesToTask();
                }
            }
            
            currentState = TaskDelegateState.Idle;
        }
        
        /// <summary>
        /// 某人推脱
        /// </summary>
        private void OnPlayerRefusesTask(int playerIndex)
        {
            string playerName = GetPersonName(playerIndex);
            refusedPlayers.Add(playerIndex);
            
            Debug.Log($"{playerName}: 我不想去...");
            ShowMessage($"{playerName}: 我不想去...");
            
            OnPlayerRefuses?.Invoke(playerIndex);
            
            // 通知对应的人
            if (playerIndex > 0)
            {
                BrotherNPC brother = GetBrother(playerIndex);
                if (brother != null)
                {
                    brother.OnRefusesTask();
                }
            }
            
            currentState = TaskDelegateState.Delegating;
        }
        
        /// <summary>
        /// 踢所有人并强制玩家
        /// </summary>
        private IEnumerator KickAllAndForcePlayer()
        {
            currentState = TaskDelegateState.KickingAll;
            
            Debug.Log("妈妈生气了，踢了所有人一脚！");
            ShowMessage("妈妈生气了！");
            
            // 播放踢人音效
            if (kickAllSound != null)
            {
                AudioSource.PlayClipAtPoint(kickAllSound, Camera.main.transform.position);
            }
            
            // 踢弟弟们
            if (brother1 != null)
            {
                brother1.OnKickedByMom();
            }
            if (brother2 != null)
            {
                brother2.OnKickedByMom();
            }
            
            // 等待动画
            yield return new WaitForSeconds(2f);
            
            // 强制玩家去干活
            Debug.Log("妈妈: 你去！不去别想玩了！");
            ShowMessage("妈妈: 你去！不去别想玩了！");
            
            currentState = TaskDelegateState.ForcingPlayer;
            
            // 播放强制音效
            if (forcePlayerSound != null)
            {
                AudioSource.PlayClipAtPoint(forcePlayerSound, Camera.main.transform.position);
            }
            
            OnAllRefused?.Invoke();
            OnPlayerForced?.Invoke();
            
            // 等待玩家响应
            yield return new WaitForSeconds(3f);
            
            currentState = TaskDelegateState.Idle;
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 获取下一个目标
        /// </summary>
        private int GetNextTarget()
        {
            // 轮流指派
            int next = (currentTargetIndex + 1) % 3;
            
            // 跳过已经拒绝的人
            while (refusedPlayers.Contains(next) && refusedPlayers.Count < 3)
            {
                next = (next + 1) % 3;
            }
            
            return next;
        }
        
        /// <summary>
        /// 获取人名
        /// </summary>
        private string GetPersonName(int index)
        {
            switch (index)
            {
                case 0: return "你";
                case 1: return brother1 != null ? brother1.GetBrotherName() : "弟弟1";
                case 2: return brother2 != null ? brother2.GetBrotherName() : "弟弟2";
                default: return "未知";
            }
        }
        
        /// <summary>
        /// 获取弟弟
        /// </summary>
        private BrotherNPC GetBrother(int index)
        {
            if (index == 1) return brother1;
            if (index == 2) return brother2;
            return null;
        }
        
        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message)
        {
            Debug.Log($"[派活] {message}");
            OnShowMessage?.Invoke(message);
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取当前状态
        /// </summary>
        public TaskDelegateState GetCurrentState()
        {
            return currentState;
        }
        
        /// <summary>
        /// 是否在派活
        /// </summary>
        public bool IsDelegating()
        {
            return currentState != TaskDelegateState.Idle;
        }
        
        /// <summary>
        /// 获取当前任务
        /// </summary>
        public string GetCurrentTask()
        {
            return currentTaskDescription;
        }
        
        /// <summary>
        /// 获取当前轮数
        /// </summary>
        public int GetCurrentRound()
        {
            return currentDelegateRound;
        }
        
        /// <summary>
        /// 玩家答应干活
        /// </summary>
        public void PlayerAgrees()
        {
            if (currentState == TaskDelegateState.WaitingForResponse && currentTargetIndex == 0)
            {
                OnPlayerAgrees(0);
            }
        }
        
        /// <summary>
        /// 玩家推脱
        /// </summary>
        public void PlayerRefuses()
        {
            if (currentState == TaskDelegateState.WaitingForResponse && currentTargetIndex == 0)
            {
                OnPlayerRefusesTask(0);
            }
        }
        
        #endregion
    }
}
