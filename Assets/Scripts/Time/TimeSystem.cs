using UnityEngine;
using System;

namespace Dapaolou.GameTime
{
    /// <summary>
    /// 游戏时间段
    /// </summary>
    public enum TimeOfDay
    {
        EarlyMorning,   // 清晨 (6:00-7:00)
        Morning,        // 上午 (7:00-11:00)
        Noon,           // 中午 (11:00-13:00)
        Afternoon,      // 下午 (13:00-17:00)
        Evening,        // 傍晚 (17:00-19:00)
        Night           // 晚上 (19:00-20:00)
    }

    /// <summary>
    /// 时间事件类型
    /// </summary>
    public enum TimeEventType
    {
        MomLeavesForWork,       // 妈妈出门干活
        MomReturnsForLunch,     // 妈妈回来做午饭
        MomCallsForLunch,       // 妈妈喊吃午饭
        MomKicksPlayer,         // 妈妈踢玩家（午饭）
        MomLeavesAfterLunch,    // 妈妈午饭后出门
        MomReturnsForDinner,    // 妈妈回来做晚饭
        MomCallsForDinner,      // 妈妈喊吃晚饭
        MomKicksPlayerDinner,   // 妈妈踢玩家（晚饭）
        CowNeedsWater,          // 牛需要喝水
        CowNeedsReturn,         // 牛需要牵回牛圈
        DogEscapes,             // 狗跑出来
        CatDisturbs,            // 猫骚扰玩家
        NightFalls,             // 天黑
        GameEnd                 // 游戏结束
    }

    /// <summary>
    /// 时间系统 - 管理游戏内时间流逝和事件触发
    /// </summary>
    public class TimeSystem : MonoBehaviour
    {
        [Header("时间配置")]
        [SerializeField] private float timeScale = 60f;            // 时间缩放（1秒=游戏内1分钟）
        [SerializeField] private int startHour = 6;                // 开始时间（小时）
        [SerializeField] private int startMinute = 0;              // 开始时间（分钟）
        [SerializeField] private int endHour = 20;                 // 结束时间（小时）
        
        [Header("光照控制")]
        [SerializeField] private Light directionalLight;           // 主光源
        [SerializeField] private Gradient lightColorGradient;      // 光照颜色渐变
        [SerializeField] private AnimationCurve lightIntensityCurve; // 光照强度曲线
        [SerializeField] private float minLightIntensity = 0.1f;   // 最小光照强度
        [SerializeField] private float maxLightIntensity = 1f;     // 最大光照强度
        
        [Header("天空盒")]
        [SerializeField] private Material skyboxMaterial;          // 天空盒材质
        [SerializeField] private Color daySkyColor = new Color(0.5f, 0.8f, 1f);
        [SerializeField] private Color nightSkyColor = new Color(0.05f, 0.05f, 0.1f);
        
        // 当前时间
        private int currentHour;
        private int currentMinute;
        private float timeAccumulator = 0f;
        
        // 时间状态
        private TimeOfDay currentTimeOfDay;
        private bool isGamePaused = false;
        private bool isGameEnded = false;
        
        // 事件计数
        private int lunchCallCount = 0;
        private int dinnerCallCount = 0;
        
        // 单例
        public static TimeSystem Instance { get; private set; }
        
        // 事件
        public Action<TimeEventType> OnTimeEvent;
        public Action<TimeOfDay> OnTimeOfDayChanged;
        public Action<int, int> OnTimeUpdated;  // hour, minute
        
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
            // 初始化时间
            currentHour = startHour;
            currentMinute = startMinute;
            UpdateTimeOfDay();
            
            // 初始化光照渐变
            InitializeLightGradient();
        }
        
        void Update()
        {
            if (isGamePaused || isGameEnded) return;
            
            // 更新时间
            UpdateTime();
            
            // 更新光照
            UpdateLighting();
            
            // 检查事件
            CheckTimeEvents();
        }
        
        #region 时间更新
        
        private void UpdateTime()
        {
            timeAccumulator += Time.deltaTime * timeScale;
            
            while (timeAccumulator >= 60f)
            {
                timeAccumulator -= 60f;
                currentMinute++;
                
                if (currentMinute >= 60)
                {
                    currentMinute = 0;
                    currentHour++;
                    
                    // 检查是否游戏结束
                    if (currentHour >= endHour)
                    {
                        TriggerGameEnd();
                        return;
                    }
                }
                
                // 更新时间段
                TimeOfDay previousTimeOfDay = currentTimeOfDay;
                UpdateTimeOfDay();
                
                if (previousTimeOfDay != currentTimeOfDay)
                {
                    OnTimeOfDayChanged?.Invoke(currentTimeOfDay);
                }
                
                // 通知时间更新
                OnTimeUpdated?.Invoke(currentHour, currentMinute);
            }
        }
        
        private void UpdateTimeOfDay()
        {
            if (currentHour >= 6 && currentHour < 7)
            {
                currentTimeOfDay = TimeOfDay.EarlyMorning;
            }
            else if (currentHour >= 7 && currentHour < 11)
            {
                currentTimeOfDay = TimeOfDay.Morning;
            }
            else if (currentHour >= 11 && currentHour < 13)
            {
                currentTimeOfDay = TimeOfDay.Noon;
            }
            else if (currentHour >= 13 && currentHour < 17)
            {
                currentTimeOfDay = TimeOfDay.Afternoon;
            }
            else if (currentHour >= 17 && currentHour < 19)
            {
                currentTimeOfDay = TimeOfDay.Evening;
            }
            else if (currentHour >= 19 && currentHour < 20)
            {
                currentTimeOfDay = TimeOfDay.Night;
            }
        }
        
        #endregion
        
        #region 光照控制
        
        private void InitializeLightGradient()
        {
            if (lightColorGradient == null || lightColorGradient.colorKeys.Length == 0)
            {
                lightColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.8f, 0.6f), 0f),      // 清晨
                    new GradientColorKey(Color.white, 0.2f),                    // 上午
                    new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0.4f),    // 中午
                    new GradientColorKey(new Color(1f, 0.9f, 0.7f), 0.6f),     // 下午
                    new GradientColorKey(new Color(1f, 0.6f, 0.3f), 0.8f),     // 傍晚
                    new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1f)      // 晚上
                };
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                };
                lightColorGradient.SetKeys(colorKeys, alphaKeys);
            }
            
            if (lightIntensityCurve == null || lightIntensityCurve.keys.Length == 0)
            {
                lightIntensityCurve = new AnimationCurve(
                    new Keyframe(0f, 0.6f),      // 清晨
                    new Keyframe(0.2f, 1f),      // 上午
                    new Keyframe(0.4f, 1f),      // 中午
                    new Keyframe(0.6f, 0.9f),    // 下午
                    new Keyframe(0.8f, 0.5f),    // 傍晚
                    new Keyframe(1f, 0.1f)       // 晚上
                );
            }
        }
        
        private void UpdateLighting()
        {
            if (directionalLight == null) return;
            
            // 计算时间进度 (0-1)
            float timeProgress = GetTimeProgress();
            
            // 更新光照颜色
            directionalLight.color = lightColorGradient.Evaluate(timeProgress);
            
            // 更新光照强度
            float intensity = lightIntensityCurve.Evaluate(timeProgress);
            directionalLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, intensity);
            
            // 更新光照角度（模拟太阳移动）
            float sunAngle = Mathf.Lerp(-30f, 210f, timeProgress);
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
            
            // 更新天空盒
            if (skyboxMaterial != null)
            {
                Color skyColor = Color.Lerp(daySkyColor, nightSkyColor, timeProgress);
                skyboxMaterial.SetColor("_SkyTint", skyColor);
            }
        }
        
        private float GetTimeProgress()
        {
            // 将当前时间转换为0-1的进度
            int totalMinutes = (currentHour - startHour) * 60 + currentMinute;
            int maxMinutes = (endHour - startHour) * 60;
            return Mathf.Clamp01((float)totalMinutes / maxMinutes);
        }
        
        #endregion
        
        #region 事件检查
        
        private void CheckTimeEvents()
        {
            // 早上 7:00 - 妈妈出门干活
            if (currentHour == 7 && currentMinute == 0)
            {
                TriggerEvent(TimeEventType.MomLeavesForWork);
            }
            
            // 中午 11:30 - 妈妈回来做饭
            if (currentHour == 11 && currentMinute == 30)
            {
                TriggerEvent(TimeEventType.MomReturnsForLunch);
            }
            
            // 中午 12:00 - 妈妈第一次喊吃饭
            if (currentHour == 12 && currentMinute == 0 && lunchCallCount == 0)
            {
                lunchCallCount++;
                TriggerEvent(TimeEventType.MomCallsForLunch);
            }
            
            // 中午 12:15 - 妈妈第二次喊吃饭
            if (currentHour == 12 && currentMinute == 15 && lunchCallCount == 1)
            {
                lunchCallCount++;
                TriggerEvent(TimeEventType.MomCallsForLunch);
            }
            
            // 中午 12:30 - 妈妈第三次喊吃饭
            if (currentHour == 12 && currentMinute == 30 && lunchCallCount == 2)
            {
                lunchCallCount++;
                TriggerEvent(TimeEventType.MomCallsForLunch);
            }
            
            // 中午 12:45 - 妈妈第四次喊吃饭，准备踢人
            if (currentHour == 12 && currentMinute == 45 && lunchCallCount == 3)
            {
                lunchCallCount++;
                TriggerEvent(TimeEventType.MomCallsForLunch);
            }
            
            // 中午 12:50 - 妈妈踢玩家
            if (currentHour == 12 && currentMinute == 50 && lunchCallCount >= 4)
            {
                TriggerEvent(TimeEventType.MomKicksPlayer);
            }
            
            // 下午 13:00 - 妈妈出门干活
            if (currentHour == 13 && currentMinute == 0)
            {
                TriggerEvent(TimeEventType.MomLeavesAfterLunch);
                lunchCallCount = 0;  // 重置计数
            }
            
            // 下午 15:00 - 牛需要喝水
            if (currentHour == 15 && currentMinute == 0)
            {
                TriggerEvent(TimeEventType.CowNeedsWater);
            }
            
            // 傍晚 17:30 - 妈妈回来做晚饭
            if (currentHour == 17 && currentMinute == 30)
            {
                TriggerEvent(TimeEventType.MomReturnsForDinner);
            }
            
            // 傍晚 18:00 - 妈妈第一次喊吃晚饭
            if (currentHour == 18 && currentMinute == 0 && dinnerCallCount == 0)
            {
                dinnerCallCount++;
                TriggerEvent(TimeEventType.MomCallsForDinner);
            }
            
            // 傍晚 18:15 - 妈妈第二次喊吃晚饭
            if (currentHour == 18 && currentMinute == 15 && dinnerCallCount == 1)
            {
                dinnerCallCount++;
                TriggerEvent(TimeEventType.MomCallsForDinner);
            }
            
            // 傍晚 18:30 - 妈妈第三次喊吃晚饭
            if (currentHour == 18 && currentMinute == 30 && dinnerCallCount == 2)
            {
                dinnerCallCount++;
                TriggerEvent(TimeEventType.MomCallsForDinner);
            }
            
            // 傍晚 18:45 - 妈妈第四次喊吃晚饭
            if (currentHour == 18 && currentMinute == 45 && dinnerCallCount == 3)
            {
                dinnerCallCount++;
                TriggerEvent(TimeEventType.MomCallsForDinner);
            }
            
            // 傍晚 18:50 - 妈妈踢玩家
            if (currentHour == 18 && currentMinute == 50 && dinnerCallCount >= 4)
            {
                TriggerEvent(TimeEventType.MomKicksPlayerDinner);
            }
            
            // 傍晚 19:00 - 牛需要牵回牛圈
            if (currentHour == 19 && currentMinute == 0)
            {
                TriggerEvent(TimeEventType.CowNeedsReturn);
            }
            
            // 晚上 19:30 - 天黑
            if (currentHour == 19 && currentMinute == 30)
            {
                TriggerEvent(TimeEventType.NightFalls);
            }
            
            // 晚上 20:00 - 游戏结束
            if (currentHour == 20 && currentMinute == 0)
            {
                TriggerGameEnd();
            }
        }
        
        private void TriggerEvent(TimeEventType eventType)
        {
            Debug.Log($"[TimeSystem] Event triggered: {eventType} at {currentHour:D2}:{currentMinute:D2}");
            OnTimeEvent?.Invoke(eventType);
        }
        
        private void TriggerGameEnd()
        {
            if (isGameEnded) return;
            
            isGameEnded = true;
            TriggerEvent(TimeEventType.GameEnd);
            Debug.Log("游戏结束！已经晚上8点了！");
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取当前时间字符串
        /// </summary>
        public string GetCurrentTimeString()
        {
            return $"{currentHour:D2}:{currentMinute:D2}";
        }
        
        /// <summary>
        /// 获取当前小时
        /// </summary>
        public int GetCurrentHour()
        {
            return currentHour;
        }
        
        /// <summary>
        /// 获取当前分钟
        /// </summary>
        public int GetCurrentMinute()
        {
            return currentMinute;
        }
        
        /// <summary>
        /// 获取当前时间段
        /// </summary>
        public TimeOfDay GetCurrentTimeOfDay()
        {
            return currentTimeOfDay;
        }
        
        /// <summary>
        /// 获取时间段中文描述
        /// </summary>
        public string GetTimeOfDayDescription()
        {
            switch (currentTimeOfDay)
            {
                case TimeOfDay.EarlyMorning:
                    return "清晨";
                case TimeOfDay.Morning:
                    return "上午";
                case TimeOfDay.Noon:
                    return "中午";
                case TimeOfDay.Afternoon:
                    return "下午";
                case TimeOfDay.Evening:
                    return "傍晚";
                case TimeOfDay.Night:
                    return "晚上";
                default:
                    return "未知";
            }
        }
        
        /// <summary>
        /// 暂停时间
        /// </summary>
        public void PauseTime()
        {
            isGamePaused = true;
        }
        
        /// <summary>
        /// 恢复时间
        /// </summary>
        public void ResumeTime()
        {
            isGamePaused = false;
        }
        
        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused()
        {
            return isGamePaused;
        }
        
        /// <summary>
        /// 是否游戏结束
        /// </summary>
        public bool IsGameEnded()
        {
            return isGameEnded;
        }
        
        /// <summary>
        /// 设置时间流速
        /// </summary>
        public void SetTimeScale(float scale)
        {
            timeScale = Mathf.Max(0f, scale);
        }
        
        /// <summary>
        /// 获取时间进度 (0-1)
        /// </summary>
        public float GetProgress()
        {
            return GetTimeProgress();
        }
        
        /// <summary>
        /// 响应吃饭（重置喊饭计数）
        /// </summary>
        public void OnPlayerEating()
        {
            if (currentTimeOfDay == TimeOfDay.Noon)
            {
                lunchCallCount = 0;
            }
            else if (currentTimeOfDay == TimeOfDay.Evening)
            {
                dinnerCallCount = 0;
            }
        }
        
        /// <summary>
        /// 获取午饭喊叫次数
        /// </summary>
        public int GetLunchCallCount()
        {
            return lunchCallCount;
        }
        
        /// <summary>
        /// 获取晚饭喊叫次数
        /// </summary>
        public int GetDinnerCallCount()
        {
            return dinnerCallCount;
        }
        
        #endregion
    }
}
