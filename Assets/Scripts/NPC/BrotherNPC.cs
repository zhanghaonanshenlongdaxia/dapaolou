using UnityEngine;
using System.Collections;

namespace Dapaolou.NPC
{
    /// <summary>
    /// 弟弟性格
    /// </summary>
    public enum BrotherPersonality
    {
        Lazy,           // 懒惰（爱推脱）
        Obedient,       // 听话（容易答应）
        Naughty         // 调皮（爱捣乱）
    }

    /// <summary>
    /// 弟弟NPC - 主角的弟弟
    /// </summary>
    public class BrotherNPC : NPCBase
    {
        [Header("弟弟配置")]
        [SerializeField] private int brotherIndex = 0;             // 弟弟编号（1或2）
        [SerializeField] private BrotherPersonality personality = BrotherPersonality.Lazy;
        [SerializeField] private string brotherName = "弟弟";
        
        [Header("行为配置")]
        [SerializeField] private float agreeChance = 0.3f;         // 答应干活的概率
        [SerializeField] private float refuseChance = 0.5f;        // 推脱的概率
        [SerializeField] private float pushToOtherChance = 0.4f;   // 推给别人的概率
        
        [Header("游戏配置")]
        [SerializeField] private Transform marblePlayPosition;     // 打弹珠位置
        [SerializeField] private Transform restPosition;           // 休息位置
        
        [Header("音效")]
        [SerializeField] private AudioClip[] agreeSounds;          // 答应音效
        [SerializeField] private AudioClip[] refuseSounds;         // 推脱音效
        [SerializeField] private AudioClip[] complainSounds;       // 抱怨音效
        [SerializeField] private AudioClip[] kickSounds;           // 被踢音效
        
        // 弟弟状态
        private bool isPlayingMarbles = false;
        private bool isDoingChores = false;
        private bool hasRefusedTask = false;
        private int refuseCount = 0;
        
        // 事件
        public System.Action<int> OnBrotherAgrees;                 // 答应干活
        public System.Action<int> OnBrotherRefuses;                // 推脱
        public System.Action<int> OnBrotherPushesToOther;          // 推给别人
        public System.Action<int> OnBrotherKicked;                 // 被踢
        
        protected override void InitializeNPC()
        {
            base.InitializeNPC();
            
            // 根据性格设置初始概率
            switch (personality)
            {
                case BrotherPersonality.Lazy:
                    agreeChance = 0.2f;
                    refuseChance = 0.6f;
                    pushToOtherChance = 0.5f;
                    break;
                case BrotherPersonality.Obedient:
                    agreeChance = 0.6f;
                    refuseChance = 0.2f;
                    pushToOtherChance = 0.2f;
                    break;
                case BrotherPersonality.Naughty:
                    agreeChance = 0.4f;
                    refuseChance = 0.3f;
                    pushToOtherChance = 0.4f;
                    break;
            }
        }
        
        #region 任务响应
        
        /// <summary>
        /// 收到干活任务
        /// </summary>
        public void OnReceiveTask(string taskDescription)
        {
            Debug.Log($"{brotherName}收到任务: {taskDescription}");
            
            // 根据性格决定反应
            float random = Random.value;
            
            if (random < agreeChance)
            {
                // 答应干活
                OnAgreesToTask();
            }
            else if (random < agreeChance + refuseChance)
            {
                // 推脱
                OnRefusesTask();
            }
            else
            {
                // 推给别人
                OnPushesTaskToOther();
            }
        }
        
        /// <summary>
        /// 答应干活
        /// </summary>
        public void OnAgreesToTask()
        {
            Debug.Log($"{brotherName}: 好吧，我去...");
            isDoingChores = true;
            hasRefusedTask = false;
            refuseCount = 0;
            
            // 播放答应音效
            PlayRandomSound(agreeSounds);
            
            // 触发事件
            OnBrotherAgrees?.Invoke(brotherIndex);
            
            // 开始干活动画
            if (animator != null)
            {
                animator.SetTrigger("Agree");
            }
        }
        
        /// <summary>
        /// 推脱
        /// </summary>
        public void OnRefusesTask()
        {
            refuseCount++;
            hasRefusedTask = true;
            
            // 随机推脱理由
            string[] refuseReasons = new string[]
            {
                "我肚子疼...",
                "我要打弹珠！",
                "让哥哥去！",
                "我刚干完活！",
                "我不想去...",
                "让弟弟去！"
            };
            
            string reason = refuseReasons[Random.Range(0, refuseReasons.Length)];
            Debug.Log($"{brotherName}: {reason}");
            
            // 播放推脱音效
            PlayRandomSound(refuseSounds);
            
            // 触发事件
            OnBrotherRefuses?.Invoke(brotherIndex);
            
            // 推脱动画
            if (animator != null)
            {
                animator.SetTrigger("Refuse");
            }
        }
        
        /// <summary>
        /// 推给别人
        /// </summary>
        private void OnPushesTaskToOther()
        {
            Debug.Log($"{brotherName}: 让哥哥/弟弟去吧！");
            
            // 播放推脱音效
            PlayRandomSound(refuseSounds);
            
            // 触发事件
            OnBrotherPushesToOther?.Invoke(brotherIndex);
            
            // 推脱动画
            if (animator != null)
            {
                animator.SetTrigger("PushToOther");
            }
        }
        
        /// <summary>
        /// 被妈妈踢
        /// </summary>
        public void OnKickedByMom()
        {
            Debug.Log($"{brotherName}被妈妈踢了一脚！");
            
            // 播放被踢音效
            PlayRandomSound(kickSounds);
            
            // 触发事件
            OnBrotherKicked?.Invoke(brotherIndex);
            
            // 被踢动画
            if (animator != null)
            {
                animator.SetTrigger("Kicked");
            }
            
            // 抱怨
            StartCoroutine(ComplainAfterKick());
        }
        
        private IEnumerator ComplainAfterKick()
        {
            yield return new WaitForSeconds(1f);
            
            string[] complains = new string[]
            {
                "哎呦！",
                "妈妈别打了！",
                "我去我去！",
                "疼死了！",
                "知道了！"
            };
            
            string complain = complains[Random.Range(0, complains.Length)];
            Debug.Log($"{brotherName}: {complain}");
            
            PlayRandomSound(complainSounds);
        }
        
        #endregion
        
        #region 游戏状态
        
        /// <summary>
        /// 开始打弹珠
        /// </summary>
        public void StartPlayingMarbles()
        {
            isPlayingMarbles = true;
            isDoingChores = false;
            
            // 移动到打弹珠位置
            if (marblePlayPosition != null)
            {
                MoveTo(marblePlayPosition.position);
            }
        }
        
        /// <summary>
        /// 停止打弹珠
        /// </summary>
        public void StopPlayingMarbles()
        {
            isPlayingMarbles = false;
            StopMoving();
        }
        
        /// <summary>
        /// 开始干活
        /// </summary>
        public void StartDoingChores()
        {
            isDoingChores = true;
            isPlayingMarbles = false;
        }
        
        /// <summary>
        /// 完成干活
        /// </summary>
        public void FinishChores()
        {
            isDoingChores = false;
            
            // 回到休息位置
            if (restPosition != null)
            {
                MoveTo(restPosition.position);
            }
        }
        
        #endregion
        
        #region 石头剪刀布
        
        /// <summary>
        /// 出拳（石头剪刀布）
        /// </summary>
        public int MakeRPSChoice()
        {
            // 0=石头, 1=剪刀, 2=布
            int choice = Random.Range(0, 3);
            
            Debug.Log($"{brotherName}出了: {GetRPSName(choice)}");
            
            // 播放出拳动画
            if (animator != null)
            {
                animator.SetInteger("RPSChoice", choice);
                animator.SetTrigger("RPS");
            }
            
            return choice;
        }
        
        private string GetRPSName(int choice)
        {
            switch (choice)
            {
                case 0: return "石头";
                case 1: return "剪刀";
                case 2: return "布";
                default: return "未知";
            }
        }
        
        #endregion
        
        #region 音效
        
        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips != null && clips.Length > 0)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                PlaySound(clip, 0.7f);
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取弟弟编号
        /// </summary>
        public int GetBrotherIndex()
        {
            return brotherIndex;
        }
        
        /// <summary>
        /// 获取弟弟名称
        /// </summary>
        public string GetBrotherName()
        {
            return brotherName;
        }
        
        /// <summary>
        /// 获取性格
        /// </summary>
        public BrotherPersonality GetPersonality()
        {
            return personality;
        }
        
        /// <summary>
        /// 是否在打弹珠
        /// </summary>
        public bool IsPlayingMarbles()
        {
            return isPlayingMarbles;
        }
        
        /// <summary>
        /// 是否在干活
        /// </summary>
        public bool IsDoingChores()
        {
            return isDoingChores;
        }
        
        /// <summary>
        /// 是否推脱过任务
        /// </summary>
        public bool HasRefusedTask()
        {
            return hasRefusedTask;
        }
        
        /// <summary>
        /// 获取推脱次数
        /// </summary>
        public int GetRefuseCount()
        {
            return refuseCount;
        }
        
        /// <summary>
        /// 重置推脱状态
        /// </summary>
        public void ResetRefuseState()
        {
            hasRefusedTask = false;
            refuseCount = 0;
        }
        
        #endregion
        
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            
            // 绘制打弹珠位置
            if (marblePlayPosition != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(marblePlayPosition.position, 0.5f);
            }
            
            // 绘制休息位置
            if (restPosition != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(restPosition.position, 0.5f);
            }
        }
    }
}
