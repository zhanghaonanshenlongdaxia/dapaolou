using UnityEngine;
using System.Collections.Generic;
using Dapaolou.GameTime;

namespace Dapaolou.Task
{
    /// <summary>
    /// 任务类型
    /// </summary>
    public enum TaskType
    {
        FetchWater,         // 打水
        LeadCowOut,         // 牵牛出门
        LeadCowIn,          // 牵牛回圈
        EatMeal,            // 吃饭
        ScareCat,           // 赶猫
        CatchDog,           // 抓狗
        PlayMarbles         // 打弹珠（默认）
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum TaskStatus
    {
        NotStarted,         // 未开始
        InProgress,         // 进行中
        Completed,          // 已完成
        Failed              // 已失败
    }

    /// <summary>
    /// 任务数据
    /// </summary>
    [System.Serializable]
    public class TaskData
    {
        public TaskType taskType;
        public string taskName;
        public string description;
        public TaskStatus status;
        public float timeLimit;              // 时间限制（秒）
        public float elapsedTime;            // 已用时间
        public bool isOptional;              // 是否可选
        public bool canBeIgnored;            // 是否可以忽略
        public int priority;                 // 优先级
    }

    /// <summary>
    /// 任务系统 - 管理游戏中的各种任务
    /// </summary>
    public class TaskSystem : MonoBehaviour
    {
        [Header("任务配置")]
        [SerializeField] private float waterFetchTime = 120f;      // 打水时间限制
        [SerializeField] private float cowLeadTime = 180f;         // 牵牛时间限制
        [SerializeField] private float mealTime = 300f;            // 吃饭时间限制
        
        [Header("UI")]
        [SerializeField] private GameObject taskPanelPrefab;
        [SerializeField] private Transform taskPanelPosition;
        
        // 当前任务
        private List<TaskData> activeTasks = new List<TaskData>();
        private TaskData currentMainTask = null;
        
        // 任务队列
        private Queue<TaskData> taskQueue = new Queue<TaskData>();
        
        // 单例
        public static TaskSystem Instance { get; private set; }
        
        // 事件
        public System.Action<TaskData> OnTaskStarted;
        public System.Action<TaskData> OnTaskCompleted;
        public System.Action<TaskData> OnTaskFailed;
        public System.Action<TaskType> OnTaskUpdated;
        
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
            // 订阅时间事件
            if (TimeSystem.Instance != null)
            {
                TimeSystem.Instance.OnTimeEvent += HandleTimeEvent;
            }
        }
        
        void Update()
        {
            // 更新任务计时
            UpdateTaskTimers();
        }
        
        #region 时间事件处理
        
        private void HandleTimeEvent(TimeEventType eventType)
        {
            switch (eventType)
            {
                case TimeEventType.MomLeavesForWork:
                    // 妈妈出门，牛已经在外面了
                    break;
                    
                case TimeEventType.CowNeedsWater:
                    StartTask(TaskType.FetchWater);
                    break;
                    
                case TimeEventType.CowNeedsReturn:
                    StartTask(TaskType.LeadCowIn);
                    break;
                    
                case TimeEventType.MomCallsForLunch:
                case TimeEventType.MomCallsForDinner:
                    StartTask(TaskType.EatMeal);
                    break;
                    
                case TimeEventType.CatDisturbs:
                    StartTask(TaskType.ScareCat);
                    break;
                    
                case TimeEventType.DogEscapes:
                    StartTask(TaskType.CatchDog);
                    break;
            }
        }
        
        #endregion
        
        #region 任务管理
        
        /// <summary>
        /// 开始任务
        /// </summary>
        public void StartTask(TaskType taskType)
        {
            // 检查是否已有相同任务
            foreach (var task in activeTasks)
            {
                if (task.taskType == taskType && task.status == TaskStatus.InProgress)
                {
                    return; // 任务已存在
                }
            }
            
            // 创建新任务
            TaskData newTask = CreateTask(taskType);
            
            if (newTask != null)
            {
                activeTasks.Add(newTask);
                
                // 设置为主任务（如果是高优先级）
                if (newTask.priority >= 2)
                {
                    currentMainTask = newTask;
                }
                
                OnTaskStarted?.Invoke(newTask);
                Debug.Log($"任务开始: {newTask.taskName}");
            }
        }
        
        /// <summary>
        /// 完成任务
        /// </summary>
        public void CompleteTask(TaskType taskType)
        {
            TaskData task = GetTask(taskType);
            
            if (task != null)
            {
                task.status = TaskStatus.Completed;
                
                OnTaskCompleted?.Invoke(task);
                Debug.Log($"任务完成: {task.taskName}");
                
                // 从活动任务中移除
                activeTasks.Remove(task);
                
                // 如果是主任务，清除
                if (currentMainTask == task)
                {
                    currentMainTask = null;
                }
            }
        }
        
        /// <summary>
        /// 任务失败
        /// </summary>
        public void FailTask(TaskType taskType)
        {
            TaskData task = GetTask(taskType);
            
            if (task != null)
            {
                task.status = TaskStatus.Failed;
                
                OnTaskFailed?.Invoke(task);
                Debug.Log($"任务失败: {task.taskName}");
                
                // 处理失败后果
                HandleTaskFailure(task);
                
                // 从活动任务中移除
                activeTasks.Remove(task);
                
                // 如果是主任务，清除
                if (currentMainTask == task)
                {
                    currentMainTask = null;
                }
            }
        }
        
        /// <summary>
        /// 创建任务
        /// </summary>
        private TaskData CreateTask(TaskType taskType)
        {
            TaskData task = new TaskData();
            task.taskType = taskType;
            task.status = TaskStatus.InProgress;
            
            switch (taskType)
            {
                case TaskType.FetchWater:
                    task.taskName = "打水给牛喝";
                    task.description = "去井里打桶水，给牛喝";
                    task.timeLimit = waterFetchTime;
                    task.isOptional = false;
                    task.canBeIgnored = false;
                    task.priority = 3;
                    break;
                    
                case TaskType.LeadCowIn:
                    task.taskName = "牵牛回牛圈";
                    task.description = "把牛牵回牛圈";
                    task.timeLimit = cowLeadTime;
                    task.isOptional = false;
                    task.canBeIgnored = false;
                    task.priority = 3;
                    break;
                    
                case TaskType.EatMeal:
                    task.taskName = "吃饭";
                    task.description = "妈妈叫吃饭了，快去";
                    task.timeLimit = mealTime;
                    task.isOptional = false;
                    task.canBeIgnored = false;
                    task.priority = 4; // 最高优先级
                    break;
                    
                case TaskType.ScareCat:
                    task.taskName = "赶走骚咪";
                    task.description = "骚咪在骚扰弹珠，赶走它";
                    task.timeLimit = 60f;
                    task.isOptional = true;
                    task.canBeIgnored = true;
                    task.priority = 1;
                    break;
                    
                case TaskType.CatchDog:
                    task.taskName = "抓回臭豆";
                    task.description = "臭豆跑出来了，抓住它";
                    task.timeLimit = 120f;
                    task.isOptional = true;
                    task.canBeIgnored = true;
                    task.priority = 1;
                    break;
                    
                default:
                    return null;
            }
            
            task.elapsedTime = 0f;
            return task;
        }
        
        /// <summary>
        /// 获取任务
        /// </summary>
        private TaskData GetTask(TaskType taskType)
        {
            foreach (var task in activeTasks)
            {
                if (task.taskType == taskType)
                {
                    return task;
                }
            }
            return null;
        }
        
        #endregion
        
        #region 任务更新
        
        private void UpdateTaskTimers()
        {
            List<TaskData> failedTasks = new List<TaskData>();
            
            foreach (var task in activeTasks)
            {
                if (task.status != TaskStatus.InProgress) continue;
                
                // 更新计时
                task.elapsedTime += Time.deltaTime;
                
                // 检查是否超时
                if (task.elapsedTime >= task.timeLimit)
                {
                    failedTasks.Add(task);
                }
            }
            
            // 处理超时任务
            foreach (var task in failedTasks)
            {
                FailTask(task.taskType);
            }
        }
        
        /// <summary>
        /// 处理任务失败
        /// </summary>
        private void HandleTaskFailure(TaskData task)
        {
            switch (task.taskType)
            {
                case TaskType.FetchWater:
                    // 没打水，妈妈会踢
                    Debug.Log("没给牛打水，妈妈生气了！");
                    NPC.MomNPC mom = FindObjectOfType<NPC.MomNPC>();
                    if (mom != null)
                    {
                        // 触发妈妈踢人
                    }
                    break;
                    
                case TaskType.LeadCowIn:
                    // 没牵牛，妈妈会踢
                    Debug.Log("没把牛牵回来，妈妈生气了！");
                    break;
                    
                case TaskType.EatMeal:
                    // 没吃饭，妈妈会踢
                    Debug.Log("没去吃饭，妈妈生气了！");
                    break;
                    
                case TaskType.ScareCat:
                    // 没赶猫，弹珠可能被骚扰
                    Debug.Log("没赶走骚咪，弹珠被骚扰了");
                    break;
                    
                case TaskType.CatchDog:
                    // 没抓狗，狗会继续捣乱
                    Debug.Log("没抓住臭豆，它继续捣乱");
                    break;
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取当前主任务
        /// </summary>
        public TaskData GetCurrentMainTask()
        {
            return currentMainTask;
        }
        
        /// <summary>
        /// 获取所有活动任务
        /// </summary>
        public List<TaskData> GetActiveTasks()
        {
            return activeTasks;
        }
        
        /// <summary>
        /// 检查是否有指定类型的任务
        /// </summary>
        public bool HasTask(TaskType taskType)
        {
            return GetTask(taskType) != null;
        }
        
        /// <summary>
        /// 获取任务剩余时间
        /// </summary>
        public float GetTaskRemainingTime(TaskType taskType)
        {
            TaskData task = GetTask(taskType);
            if (task != null)
            {
                return Mathf.Max(0, task.timeLimit - task.elapsedTime);
            }
            return 0f;
        }
        
        /// <summary>
        /// 获取任务进度 (0-1)
        /// </summary>
        public float GetTaskProgress(TaskType taskType)
        {
            TaskData task = GetTask(taskType);
            if (task != null && task.timeLimit > 0)
            {
                return Mathf.Clamp01(task.elapsedTime / task.timeLimit);
            }
            return 0f;
        }
        
        /// <summary>
        /// 检查是否可以忽略任务
        /// </summary>
        public bool CanIgnoreTask(TaskType taskType)
        {
            TaskData task = GetTask(taskType);
            return task != null && task.canBeIgnored;
        }
        
        /// <summary>
        /// 获取任务优先级描述
        /// </summary>
        public string GetTaskPriorityDescription(TaskType taskType)
        {
            TaskData task = GetTask(taskType);
            if (task == null) return "";
            
            if (task.priority >= 4)
                return "紧急！";
            else if (task.priority >= 3)
                return "重要";
            else if (task.priority >= 2)
                return "一般";
            else
                return "可选";
        }
        
        #endregion
    }
}
