using UnityEngine;
using System.Collections.Generic;

namespace Dapaolou.Game
{
    /// <summary>
    /// 弹珠库存 - 管理每个玩家的弹珠数量
    /// </summary>
    public class MarbleInventory : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private int initialMarbleCount = 21;      // 初始弹珠数量
        [SerializeField] private int maxMarbleCount = 30;          // 最大弹珠数量
        
        [Header("弹珠预制体")]
        [SerializeField] private GameObject marblePrefab;          // 弹珠预制体
        
        // 玩家弹珠数据
        private Dictionary<int, PlayerMarbleData> playerMarbles = new Dictionary<int, PlayerMarbleData>();
        
        // 单例
        public static MarbleInventory Instance { get; private set; }
        
        // 事件
        public System.Action<int, int> OnMarbleCountChanged;       // playerId, count
        public System.Action<int> OnPlayerEliminated;              // playerId
        
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
        
        /// <summary>
        /// 玩家弹珠数据
        /// </summary>
        [System.Serializable]
        public class PlayerMarbleData
        {
            public int playerId;
            public string playerName;
            public int totalMarbles;           // 总弹珠数
            public int towerMarbles;           // 炮楼弹珠数
            public int soldierMarbles;         // 小兵弹珠数
            public int capturedMarbles;        // 缴获的弹珠数
            public List<Marble.MarbleData> activeMarbles = new List<Marble.MarbleData>();
        }
        
        /// <summary>
        /// 初始化玩家弹珠
        /// </summary>
        public void InitializePlayer(int playerId, string playerName, int marbleCount = -1)
        {
            if (marbleCount < 0)
            {
                marbleCount = initialMarbleCount;
            }
            
            PlayerMarbleData data = new PlayerMarbleData
            {
                playerId = playerId,
                playerName = playerName,
                totalMarbles = marbleCount,
                towerMarbles = 4,    // 炮楼4个弹珠
                soldierMarbles = 3,  // 小兵3个弹珠
                capturedMarbles = 0,
                activeMarbles = new List<Marble.MarbleData>()
            };
            
            // 剩余弹珠 = 总数 - 炮楼(4) - 小兵(3) = 14个备用
            // 备用弹珠可以用来拆炮楼补充
            
            playerMarbles[playerId] = data;
            
            Debug.Log($"玩家 {playerName} 初始化: {marbleCount}个弹珠 (炮楼:{data.towerMarbles}, 小兵:{data.soldierMarbles}, 备用:{marbleCount - 7})");
            
            OnMarbleCountChanged?.Invoke(playerId, data.totalMarbles);
        }
        
        /// <summary>
        /// 使用角色配置初始化
        /// </summary>
        public void InitializeWithCharacter(int playerId, CharacterConfig character)
        {
            if (character == null)
            {
                InitializePlayer(playerId, "玩家");
                return;
            }
            
            InitializePlayer(playerId, character.characterName, character.initialMarbleCount);
        }
        
        /// <summary>
        /// 获取玩家弹珠数据
        /// </summary>
        public PlayerMarbleData GetPlayerData(int playerId)
        {
            if (playerMarbles.ContainsKey(playerId))
            {
                return playerMarbles[playerId];
            }
            return null;
        }
        
        /// <summary>
        /// 获取玩家总弹珠数
        /// </summary>
        public int GetTotalMarbles(int playerId)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            return data != null ? data.totalMarbles : 0;
        }
        
        /// <summary>
        /// 获取玩家可用弹珠数（未在场上的）
        /// </summary>
        public int GetAvailableMarbles(int playerId)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            if (data == null) return 0;
            
            return data.totalMarbles - data.towerMarbles - data.soldierMarbles;
        }
        
        /// <summary>
        /// 消耗弹珠（被打爆）
        /// </summary>
        public bool ConsumeMarble(int playerId, bool isTower)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            if (data == null) return false;
            
            if (isTower)
            {
                if (data.towerMarbles <= 0) return false;
                data.towerMarbles--;
            }
            else
            {
                if (data.soldierMarbles <= 0) return false;
                data.soldierMarbles--;
            }
            
            data.totalMarbles--;
            
            Debug.Log($"玩家 {data.playerName} 失去1个弹珠，剩余: {data.totalMarbles}");
            
            OnMarbleCountChanged?.Invoke(playerId, data.totalMarbles);
            
            // 检查是否被淘汰
            if (data.totalMarbles <= 0)
            {
                OnPlayerEliminated?.Invoke(playerId);
                Debug.Log($"玩家 {data.playerName} 被淘汰了！");
            }
            
            return true;
        }
        
        /// <summary>
        /// 获得弹珠（缴获）
        /// </summary>
        public void AddMarbles(int playerId, int count)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            if (data == null) return;
            
            data.totalMarbles += count;
            data.capturedMarbles += count;
            
            // 限制最大数量
            if (data.totalMarbles > maxMarbleCount)
            {
                data.totalMarbles = maxMarbleCount;
            }
            
            Debug.Log($"玩家 {data.playerName} 获得 {count} 个弹珠，总数: {data.totalMarbles}");
            
            OnMarbleCountChanged?.Invoke(playerId, data.totalMarbles);
        }
        
        /// <summary>
        /// 拆炮楼补充小兵
        /// </summary>
        public bool DismantleTower(int playerId)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            if (data == null) return false;
            
            // 检查是否可以拆炮楼
            if (data.towerMarbles <= 0 || data.soldierMarbles > 0)
            {
                return false;
            }
            
            // 拆一个炮楼弹珠变成小兵
            data.towerMarbles--;
            data.soldierMarbles++;
            
            Debug.Log($"玩家 {data.playerName} 拆炮楼补充小兵");
            
            return true;
        }
        
        /// <summary>
        /// 从备用弹珠补充小兵
        /// </summary>
        public bool RecruitSoldier(int playerId)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            if (data == null) return false;
            
            // 计算可用备用弹珠
            int available = data.totalMarbles - data.towerMarbles - data.soldierMarbles;
            
            if (available <= 0)
            {
                return false;
            }
            
            // 补充一个小兵
            data.soldierMarbles++;
            
            Debug.Log($"玩家 {data.playerName} 补充1个小兵，小兵数: {data.soldierMarbles}");
            
            return true;
        }
        
        /// <summary>
        /// 检查玩家是否还有炮楼
        /// </summary>
        public bool HasTower(int playerId)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            return data != null && data.towerMarbles > 0;
        }
        
        /// <summary>
        /// 检查玩家是否还有小兵
        /// </summary>
        public bool HasSoldiers(int playerId)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            return data != null && data.soldierMarbles > 0;
        }
        
        /// <summary>
        /// 检查玩家是否被淘汰
        /// </summary>
        public bool IsPlayerEliminated(int playerId)
        {
            PlayerMarbleData data = GetPlayerData(playerId);
            return data == null || data.totalMarbles <= 0;
        }
        
        /// <summary>
        /// 获取所有玩家排名
        /// </summary>
        public List<int> GetPlayerRankings()
        {
            List<KeyValuePair<int, int>> playerScores = new List<KeyValuePair<int, int>>();
            
            foreach (var kvp in playerMarbles)
            {
                playerScores.Add(new KeyValuePair<int, int>(kvp.Key, kvp.Value.totalMarbles));
            }
            
            // 按弹珠数量排序（从多到少）
            playerScores.Sort((a, b) => b.Value.CompareTo(a.Value));
            
            List<int> rankings = new List<int>();
            foreach (var kvp in playerScores)
            {
                rankings.Add(kvp.Key);
            }
            
            return rankings;
        }
        
        /// <summary>
        /// 获取排名文本
        /// </summary>
        public string GetRankingText()
        {
            List<int> rankings = GetPlayerRankings();
            
            string text = "排名：\n";
            for (int i = 0; i < rankings.Count; i++)
            {
                PlayerMarbleData data = GetPlayerData(rankings[i]);
                if (data != null)
                {
                    string status = IsPlayerEliminated(rankings[i]) ? "(淘汰)" : "";
                    text += $"{i + 1}. {data.playerName}: {data.totalMarbles}个弹珠 {status}\n";
                }
            }
            
            return text;
        }
        
        /// <summary>
        /// 重置所有数据
        /// </summary>
        public void ResetAll()
        {
            playerMarbles.Clear();
        }
    }
}
