using System.Collections.Generic;
using UnityEngine;
using Dapaolou.Marble;

namespace Dapaolou.Game
{
    /// <summary>
    /// 玩家状态
    /// </summary>
    public enum PlayerState
    {
        Waiting,        // 等待回合
        Aiming,         // 瞄准中
        Shooting,       // 射击中
        Finished        // 游戏结束
    }

    /// <summary>
    /// 玩家数据 - 管理玩家的炮楼和小兵
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        public int playerId;
        public string playerName;
        public Color playerColor;
        
        [Header("状态")]
        public PlayerState state = PlayerState.Waiting;
        public int score = 0;
        public int totalMarbles = 21;       // 总弹珠数（跨局身家：摧毁时易主，开局不扣）
        public int roundMarbles = 0;         // 当前局弹珠数（开局重置为 7 入场，本局摧毁实时增减）
        
        [Header("炮楼")]
        public List<MarbleData> towerMarbles = new List<MarbleData>();
        public Vector3 towerCenter;
        public bool towerDestroyed = false;
        
        [Header("小兵")]
        public List<MarbleData> soldierMarbles = new List<MarbleData>();
        public int maxSoldiers = 3;
        
        [Header("回合统计")]
        public int shotsThisTurn = 0;
        public int soldiersDestroyed = 0;
        public int towersDestroyed = 0;
        
        /// <summary>
        /// 获取存活的小兵数量
        /// </summary>
        public int GetAliveSoldierCount()
        {
            int count = 0;
            foreach (var soldier in soldierMarbles)
            {
                if (soldier != null && soldier.state != MarbleState.Destroyed)
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// 获取存活的炮楼弹珠数量
        /// </summary>
        public int GetAliveTowerCount()
        {
            int count = 0;
            foreach (var tower in towerMarbles)
            {
                if (tower != null && tower.state != MarbleState.Destroyed)
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// 检查炮楼是否需要二次打爆
        /// </summary>
        public bool NeedsSecondHit()
        {
            int aliveCount = GetAliveTowerCount();
            return aliveCount > 0 && aliveCount <= 3;
        }
        
        /// <summary>
        /// 检查是否可以拆炮楼
        /// </summary>
        public bool CanDismantleTower()
        {
            return GetAliveSoldierCount() == 0 && GetAliveTowerCount() > 0;
        }
        
        /// <summary>
        /// 获取可拆的炮楼弹珠
        /// </summary>
        public MarbleData GetDismantleableTower()
        {
            foreach (var tower in towerMarbles)
            {
                if (tower != null && tower.state != MarbleState.Destroyed)
                {
                    return tower;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 拆炮楼补充小兵
        /// </summary>
        public bool DismantleTower()
        {
            if (!CanDismantleTower()) return false;
            
            MarbleData towerToDismantle = GetDismantleableTower();
            if (towerToDismantle == null) return false;
            
            // 将炮楼弹珠转为小兵
            towerToDismantle.marbleType = MarbleType.Soldier;
            towerToDismantle.towerIndex = -1;
            soldierMarbles.Add(towerToDismantle);
            towerMarbles.Remove(towerToDismantle);
            
            Debug.Log($"Player {playerId} dismantled a tower marble to get a soldier!");
            return true;
        }
        
        /// <summary>
        /// 摧毁炮楼弹珠
        /// </summary>
        public void DestroyTowerMarble(MarbleData marble)
        {
            if (towerMarbles.Contains(marble))
            {
                marble.OnDestroyed();
                towerMarbles.Remove(marble);
                CleanupMarbleObject(marble);

                if (towerMarbles.Count == 0)
                {
                    towerDestroyed = true;
                }
            }
        }
        
        /// <summary>
        /// 摧毁小兵弹珠
        /// </summary>
        public void DestroySoldierMarble(MarbleData marble)
        {
            if (soldierMarbles.Contains(marble))
            {
                marble.OnDestroyed();
                soldierMarbles.Remove(marble);
                soldiersDestroyed++;
                CleanupMarbleObject(marble);
            }
        }

        /// <summary>
        /// 清理被摧毁的弹珠物体：断开关节并延迟销毁（给散架特效留时间）
        /// </summary>
        private void CleanupMarbleObject(MarbleData marble)
        {
            if (marble == null) return;
            var joint = marble.GetComponent<FixedJoint>();
            if (joint != null) UnityEngine.Object.Destroy(joint);
            UnityEngine.Object.Destroy(marble.gameObject, 0.5f);
        }
        
        /// <summary>
        /// 检查玩家是否失败
        /// </summary>
        public bool IsDefeated()
        {
            return towerDestroyed && GetAliveSoldierCount() == 0;
        }
        
        /// <summary>
        /// 获取所有可用的弹珠（未被摧毁且静止）
        /// </summary>
        public List<MarbleData> GetAvailableMarbles()
        {
            List<MarbleData> available = new List<MarbleData>();
            
            // 优先返回小兵
            foreach (var soldier in soldierMarbles)
            {
                if (soldier != null && soldier.state == MarbleState.Idle)
                {
                    available.Add(soldier);
                }
            }
            
            // 如果没有可用小兵，返回炮楼（用于拆炮楼）
            if (available.Count == 0 && CanDismantleTower())
            {
                foreach (var tower in towerMarbles)
                {
                    if (tower != null && tower.state == MarbleState.Idle)
                    {
                        available.Add(tower);
                    }
                }
            }
            
            return available;
        }
        
        /// <summary>
        /// 重置回合状态
        /// </summary>
        public void ResetTurn()
        {
            shotsThisTurn = 0;
            state = PlayerState.Waiting;
        }
    }
}
