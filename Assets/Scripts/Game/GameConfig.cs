using UnityEngine;

namespace Dapaolou.Game
{
    /// <summary>
    /// 游戏配置 - 存储游戏规则参数
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Dapaolou/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("炮楼配置")]
        public int towerBaseCount = 3;          // 炮楼底部弹珠数
        public int towerTopCount = 1;           // 炮楼顶部弹珠数
        public float towerSpacing = 0.05f;      // 炮楼弹珠间距
        public float towerBaseHeight = 0.025f;  // 炮楼底部高度（弹珠半径）
        
        [Header("小兵配置")]
        public int soldierCountPerPlayer = 3;   // 每个玩家的小兵数
        public float soldierSpawnRadius = 0.5f; // 小兵生成半径
        public float soldierSpawnAngle = 45f;   // 小兵生成角度范围

        [Header("布防配置（开局自选位置）")]
        public Vector2 placementBounds = new Vector2(10f, 8f);  // 布防合法区（半宽半深，以场地中心为原点）
        public float minTowerDistance = 6f;     // 两家炮楼最小间距
        public float soldierMaxRadius = 3f;     // 明兵距己方炮楼最远距离

        [Header("暗兵配置")]
        public bool ambushModeEnabled = false;  // 暗兵模式开关（联机开局可选）
        public int ambushCountPerPlayer = 3;    // 每个玩家的暗兵数（基础 3，联机房主可调上限）
        public float ambushMaxRadius = 5f;      // 暗兵距己方炮楼最远距离
        public float ambushMinEnemyTowerDist = 6f; // 暗兵距敌方炮楼最小距离（不能埋到对手地盘）
        
        [Header("二次打爆配置")]
        public int secondHitThreshold = 3;      // 二次打爆阈值（剩余多少需要二次打爆）
        public float secondHitRadius = 0.15f;   // 二次打爆判定半径
        public float secondHitTimeWindow = 5f;  // 二次打爆时间窗口（秒）
        
        [Header("拆炮楼配置")]
        public bool allowDismantleTower = true; // 是否允许拆炮楼
        public int maxDismantlePerTurn = 1;     // 每回合最大拆炮楼数
        
        [Header("回合配置")]
        public float turnTimeLimit = 30f;       // 回合时间限制
        public float shootCooldown = 1f;        // 射击冷却时间
        public float marbleStopThreshold = 0.1f;// 弹珠停止阈值
        
        [Header("胜利条件")]
        public bool destroyAllTowers = true;    // 摧毁所有炮楼获胜
        public bool destroyAllSoldiers = false; // 消灭所有小兵获胜
        public int scoreToWin = 0;              // 得分获胜（0表示禁用）
    }
}
