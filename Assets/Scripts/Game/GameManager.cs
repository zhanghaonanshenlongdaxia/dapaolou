using System.Collections.Generic;
using UnityEngine;
using Dapaolou.Marble;

namespace Dapaolou.Game
{
    /// <summary>
    /// 游戏阶段
    /// </summary>
    public enum GamePhase
    {
        Setup,          // 设置阶段
        Playing,        // 游戏进行中
        GameOver        // 游戏结束
    }

    /// <summary>
    /// 游戏管理器 - 管理游戏流程和规则
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("游戏配置")]
        [SerializeField] private GameConfig gameConfig;
        
        [Header("玩家配置")]
        [SerializeField] private int playerCount = 2;
        [SerializeField] private List<Color> playerColors = new List<Color>
        {
            Color.blue,
            Color.red,
            Color.green,
            Color.yellow
        };
        
        [Header("场景引用")]
        [SerializeField] private Transform[] playerSpawnPoints;
        [SerializeField] private Transform[] towerSpawnPoints;
        [SerializeField] private Transform[] soldierSpawnPoints;
        
        [Header("组件引用")]
        [SerializeField] private TowerBuilder towerBuilder;
        [SerializeField] private MarbleShooter marbleShooter;
        
        // 游戏状态
        private GamePhase currentPhase = GamePhase.Setup;
        private int currentPlayerIndex = 0;
        private float turnTimer = 0f;
        private bool isWaitingForMarbles = false;
        
        // 玩家数据
        private List<PlayerData> players = new List<PlayerData>();

        // 游戏顺序（由 GameStartManager 通过石头剪刀布决定，null 时按索引顺序）
        private int[] playOrder;
        
        // 弹珠追踪
        private List<MarbleData> allMarbles = new List<MarbleData>();
        private MarbleData lastShotMarble;
        
        // 单例
        public static GameManager Instance { get; private set; }
        
        // 事件
        public System.Action<int> OnPlayerTurnStart;
        public System.Action<int> OnPlayerTurnEnd;
        public System.Action<int> OnGameOver;
        public System.Action<MarbleData, MarbleData> OnMarbleDestroyed; // 被摧毁的弹珠, 被命中的弹珠
        
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
            InitializeGame();
        }
        
        void Update()
        {
            switch (currentPhase)
            {
                case GamePhase.Setup:
                    UpdateSetup();
                    break;
                case GamePhase.Playing:
                    UpdatePlaying();
                    break;
                case GamePhase.GameOver:
                    UpdateGameOver();
                    break;
            }
        }
        
        #region 游戏初始化
        
        private void InitializeGame()
        {
            Debug.Log("Initializing Dapaolou Game...");
            
            // 创建玩家数据
            for (int i = 0; i < playerCount; i++)
            {
                PlayerData player = new PlayerData
                {
                    playerId = i,
                    playerName = $"Player {i + 1}",
                    playerColor = playerColors[i % playerColors.Count],
                    maxSoldiers = gameConfig.soldierCountPerPlayer
                };
                players.Add(player);
            }
            
            // 设置炮楼建造器
            if (towerBuilder == null)
            {
                towerBuilder = FindObjectOfType<TowerBuilder>();
            }
            
            // 设置发射器
            if (marbleShooter == null)
            {
                marbleShooter = FindObjectOfType<MarbleShooter>();
            }
            
            // 订阅事件
            if (marbleShooter != null)
            {
                marbleShooter.OnMarbleShot += OnMarbleShot;
                marbleShooter.OnStateChanged += OnShootStateChanged;
            }
            
            // 生成炮楼和小兵
            SpawnAllTowers();
            SpawnAllSoldiers();
            
            currentPhase = GamePhase.Setup;
        }
        
        /// <summary>
        /// 生成所有玩家的炮楼
        /// </summary>
        private void SpawnAllTowers()
        {
            for (int i = 0; i < playerCount; i++)
            {
                Vector3 towerPos = towerSpawnPoints[i].position;
                List<MarbleData> towerMarbles = towerBuilder.BuildTower(towerPos, i, playerColors[i]);
                
                players[i].towerMarbles = towerMarbles;
                players[i].towerCenter = towerPos;
                
                allMarbles.AddRange(towerMarbles);
            }
        }
        
        /// <summary>
        /// 生成所有玩家的小兵
        /// </summary>
        private void SpawnAllSoldiers()
        {
            for (int i = 0; i < playerCount; i++)
            {
                Vector3 spawnCenter = soldierSpawnPoints[i].position;
                List<MarbleData> soldiers = SpawnSoldiersForPlayer(i, spawnCenter);
                
                players[i].soldierMarbles = soldiers;
                allMarbles.AddRange(soldiers);
            }
        }
        
        /// <summary>
        /// 为指定玩家生成小兵
        /// </summary>
        private List<MarbleData> SpawnSoldiersForPlayer(int playerId, Vector3 center)
        {
            List<MarbleData> soldiers = new List<MarbleData>();

            int soldierCount = gameConfig.soldierCountPerPlayer;

            // 横向一排（垂直于敌方方向）：避免队友挡在射击线上，
            // 扇形布阵会让前排队友挡住后排的发射路线（弹珠出门即撞友军弹飞 3-4 米）
            Vector3 enemyDir = playerId == 0 ? Vector3.right : Vector3.left;
            Vector3 perp = Vector3.Cross(Vector3.up, enemyDir);
            float spacing = 0.18f;   // 相邻小兵 18cm（弹珠直径 5cm，留足间隙）

            for (int i = 0; i < soldierCount; i++)
            {
                // 计算小兵位置（横向一排：左中右）
                float lateral = (i - (soldierCount - 1) / 2f) * spacing;
                Vector3 spawnPos = center + perp * lateral;

                // 创建小兵
                GameObject soldierObj = CreateSoldierMarble(spawnPos, playerId, i);
                MarbleData soldierData = soldierObj.GetComponent<MarbleData>();
                soldiers.Add(soldierData);
            }

            return soldiers;
        }
        
        /// <summary>
        /// 创建小兵弹珠
        /// </summary>
        private GameObject CreateSoldierMarble(Vector3 position, int playerId, int index)
        {
            // 创建弹珠对象
            GameObject marbleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marbleObj.transform.position = position;
            marbleObj.transform.localScale = Vector3.one * 0.05f; // 5cm直径
            
            // 添加物理组件
            Rigidbody rb = marbleObj.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            // 物理材质由 MarbleData.Awake 统一设置为玻璃材质

            // 添加弹珠数据
            MarbleData marbleData = marbleObj.AddComponent<MarbleData>();
            marbleData.marbleType = MarbleType.Soldier;
            marbleData.ownerPlayerId = playerId;
            marbleData.towerIndex = -1;
            marbleObj.AddComponent<MarbleCollisionHandler>();

            // 阵营标签：碰撞检测按阵营判定吃子
            marbleObj.tag = "Team" + playerId;
            
            // 玻璃质感 + 玩家色
            Renderer renderer = marbleObj.GetComponent<Renderer>();
            renderer.material = MarbleData.CreateGlassMaterial(playerColors[playerId]);
            
            // 设置名称
            marbleObj.name = $"Player{playerId}_Soldier_{index}";
            
            return marbleObj;
        }
        
        #endregion
        
        #region 游戏流程
        
        private void UpdateSetup()
        {
            // 等待所有弹珠静止
            if (AreAllMarblesStationary())
            {
                Debug.Log("All marbles settled. Starting game...");
                StartGame();
            }
        }
        
        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            currentPhase = GamePhase.Playing;

            // 开局：每人的当前局弹珠数重置为 7 颗入场（4 炮楼 + 3 小兵），总弹珠数不扣
            for (int i = 0; i < players.Count; i++)
            {
                players[i].roundMarbles = 7;
                Debug.Log($"Player {i} enters round with 7 marbles (total={players[i].totalMarbles})");
            }

            StartPlayerTurn(GetFirstPlayerInOrder());
        }

        /// <summary>
        /// 设置游戏顺序（由 GameStartManager 调用）
        /// </summary>
        public void SetPlayOrder(int[] order)
        {
            playOrder = order;
        }

        private int GetFirstPlayerInOrder()
        {
            return (playOrder != null && playOrder.Length > 0) ? playOrder[0] : 0;
        }

        private int GetNextPlayerInOrder()
        {
            if (playOrder == null || playOrder.Length == 0)
            {
                return (currentPlayerIndex + 1) % playerCount;
            }

            int pos = System.Array.IndexOf(playOrder, currentPlayerIndex);
            if (pos < 0) pos = 0;
            return playOrder[(pos + 1) % playOrder.Length];
        }
        
        private void UpdatePlaying()
        {
            // 更新回合计时器
            turnTimer -= Time.deltaTime;
            
            // 检查是否超时
            if (turnTimer <= 0)
            {
                EndCurrentTurn();
            }
            
            // 检查弹珠是否停止
            if (isWaitingForMarbles && lastShotMarble != null)
            {
                if (lastShotMarble.IsStationary())
                {
                    OnMarblesStopped();
                }
            }
        }
        
        /// <summary>
        /// 开始玩家回合
        /// </summary>
        private void StartPlayerTurn(int playerIndex)
        {
            currentPlayerIndex = playerIndex;
            turnTimer = gameConfig.turnTimeLimit;
            
            PlayerData currentPlayer = players[playerIndex];
            currentPlayer.ResetTurn();
            currentPlayer.state = PlayerState.Aiming;
            
            Debug.Log($"Player {playerIndex}'s turn started!");
            OnPlayerTurnStart?.Invoke(playerIndex);
            
            // 检查是否可以拆炮楼
            if (currentPlayer.CanDismantleTower())
            {
                // TODO: 显示拆炮楼选项UI
                Debug.Log($"Player {playerIndex} can dismantle tower!");
            }
            
            // 获取可用弹珠
            List<MarbleData> availableMarbles = currentPlayer.GetAvailableMarbles();
            if (availableMarbles.Count > 0)
            {
                // 自动选择第一个可用弹珠
                marbleShooter.SelectMarble(availableMarbles[0]);
            }
            else
            {
                Debug.LogWarning($"Player {playerIndex} has no available marbles!");
                EndCurrentTurn();
            }
        }
        
        /// <summary>
        /// 结束当前回合
        /// </summary>
        private void EndCurrentTurn()
        {
            PlayerData currentPlayer = players[currentPlayerIndex];
            currentPlayer.state = PlayerState.Waiting;
            
            Debug.Log($"Player {currentPlayerIndex}'s turn ended!");
            OnPlayerTurnEnd?.Invoke(currentPlayerIndex);
            
            // 检查胜利条件
            if (CheckVictoryCondition())
            {
                return;
            }
            
            // 切换到下一个玩家（按游戏顺序）
            int nextPlayerIndex = GetNextPlayerInOrder();

            // 跳过已失败的玩家
            while (players[nextPlayerIndex].IsDefeated() && nextPlayerIndex != currentPlayerIndex)
            {
                nextPlayerIndex = GetNextPlayerInOrder();
            }
            
            // 开始下一个玩家的回合
            StartPlayerTurn(nextPlayerIndex);
        }
        
        /// <summary>
        /// 弹珠发射回调
        /// </summary>
        private void OnMarbleShot(MarbleData marble, Vector3 direction, float force)
        {
            lastShotMarble = marble;
            isWaitingForMarbles = true;
            
            PlayerData currentPlayer = players[currentPlayerIndex];
            currentPlayer.shotsThisTurn++;
            
            Debug.Log($"Player {currentPlayerIndex} shot marble with force {force:F2}");
        }
        
        /// <summary>
        /// 发射状态改变回调
        /// </summary>
        private void OnShootStateChanged(ShootState newState)
        {
            if (newState == ShootState.Released)
            {
                // 弹珠已发射，等待停止
            }
        }
        
        /// <summary>
        /// 弹珠停止回调
        /// </summary>
        private void OnMarblesStopped()
        {
            isWaitingForMarbles = false;

            // 弹珠停止后恢复为 Idle，供后续回合重新选取
            if (lastShotMarble != null && lastShotMarble.state == MarbleState.Rolling)
            {
                lastShotMarble.state = MarbleState.Idle;
            }

            // 检查碰撞结果
            CheckCollisions();

            // 结束回合
            EndCurrentTurn();
        }
        
        #endregion
        
        #region 碰撞检测
        
        /// <summary>
        /// 检查碰撞结果
        /// </summary>
        private void CheckCollisions()
        {
            // 这里会在碰撞发生时由CollisionHandler调用
            // 简化版本：直接结束回合
        }
        
        /// <summary>
        /// 处理弹珠碰撞
        /// </summary>
        public void OnMarbleCollision(MarbleData attacker, MarbleData victim, float impactForce)
        {
            if (attacker == null || victim == null) return;

            // 有效攻击归属：连环碰撞中被撞飞的弹珠仍代表原始攻击者
            int attackerId = attacker.GetEffectiveAttackerId();

            // 真友军（同阵营）才忽略——双回调会先后写入/读取 lastAttackerId，
            // 用链 ID 判阵营会把敌方弹珠误判成己方（打中不消除的根因）
            if (attacker.ownerPlayerId == victim.ownerPlayerId)
            {
                // 不能打自己的弹珠（含被撞飞的己方弹珠弹回）
                return;
            }

            PlayerData attackerPlayer = players[attackerId];
            PlayerData victimPlayer = players[victim.ownerPlayerId];
            
            // 根据弹珠类型处理
            if (victim.marbleType == MarbleType.Tower)
            {
                HandleTowerHit(attackerPlayer, victimPlayer, victim, impactForce);
            }
            else if (victim.marbleType == MarbleType.Soldier)
            {
                HandleSoldierHit(attackerPlayer, victimPlayer, victim, impactForce);
            }
            
            OnMarbleDestroyed?.Invoke(attacker, victim);
        }
        
        /// <summary>
        /// 处理炮楼被击中
        /// </summary>
        private void HandleTowerHit(PlayerData attacker, PlayerData victim, MarbleData towerMarble, float force)
        {
            // 已摧毁的弹珠不重复判定
            if (towerMarble == null || towerMarble.state == MarbleState.Destroyed) return;

            // 检查是否需要二次打爆
            if (victim.NeedsSecondHit())
            {
                // 二次打爆判定
                if (force > 5f) // 需要足够的力度
                {
                    victim.DestroyTowerMarble(towerMarble);
                    attacker.score += 10;
                    // 弹珠转移：总弹珠易主，本局战况同步
                    victim.totalMarbles -= 4;
                    attacker.totalMarbles += 4;
                    victim.roundMarbles -= 4;
                    attacker.roundMarbles += 4;
                    Debug.Log($"Tower marble destroyed! Player {attacker.playerId} scores 10 points! (+4 marbles)");
                }
                else
                {
                    Debug.Log("Second hit failed - not enough force!");
                }
            }
            else
            {
                // 一次打爆：力度足够才摧毁，否则弹开（与手册判定表一致）
                if (force > 5f)
                {
                    victim.DestroyTowerMarble(towerMarble);
                    attacker.score += 5;
                    // 弹珠转移：总弹珠易主，本局战况同步
                    victim.totalMarbles -= 4;
                    attacker.totalMarbles += 4;
                    victim.roundMarbles -= 4;
                    attacker.roundMarbles += 4;
                    Debug.Log($"Tower marble destroyed! Player {attacker.playerId} scores 5 points! (+4 marbles)");
                }
                else
                {
                    Debug.Log("Hit too weak - marble bounced off!");
                }
            }
            
            // 检查炮楼是否完全摧毁
            if (victim.towerDestroyed)
            {
                attacker.score += 20;
                Debug.Log($"Tower completely destroyed! Player {attacker.playerId} scores 20 bonus points!");
            }
        }
        
        /// <summary>
        /// 处理小兵被击中
        /// </summary>
        private void HandleSoldierHit(PlayerData attacker, PlayerData victim, MarbleData soldierMarble, float force)
        {
            // 已摧毁的弹珠不重复判定
            if (soldierMarble == null || soldierMarble.state == MarbleState.Destroyed) return;

            victim.DestroySoldierMarble(soldierMarble);
            attacker.score += 3;
            attacker.soldiersDestroyed++;
            // 弹珠转移：总弹珠易主，本局战况同步
            victim.totalMarbles -= 1;
            attacker.totalMarbles += 1;
            victim.roundMarbles -= 1;
            attacker.roundMarbles += 1;
            
            Debug.Log($"Soldier destroyed! Player {attacker.playerId} scores 3 points! (+1 marble)");
            
            // 如果小兵全灭，检查是否可以拆炮楼
            if (victim.GetAliveSoldierCount() == 0)
            {
                Debug.Log($"Player {victim.playerId} lost all soldiers! Can dismantle tower.");
                // TODO: 通知UI显示拆炮楼选项
            }
        }
        
        #endregion
        
        #region 拆炮楼
        
        /// <summary>
        /// 尝试拆炮楼
        /// </summary>
        public bool TryDismantleTower(int playerId)
        {
            PlayerData player = players[playerId];
            
            if (!player.CanDismantleTower())
            {
                Debug.Log($"Player {playerId} cannot dismantle tower!");
                return false;
            }
            
            bool success = player.DismantleTower();
            
            if (success)
            {
                Debug.Log($"Player {playerId} dismantled a tower marble to get a soldier!");
                // TODO: 播放拆炮楼动画
                // TODO: 更新UI
            }
            
            return success;
        }
        
        #endregion
        
        #region 胜利条件
        
        /// <summary>
        /// 检查胜利条件
        /// </summary>
        private bool CheckVictoryCondition()
        {
            // 检查是否所有玩家都被击败
            int alivePlayers = 0;
            int winnerIndex = -1;
            
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].IsDefeated())
                {
                    alivePlayers++;
                    winnerIndex = i;
                }
            }
            
            // 只剩一个玩家
            if (alivePlayers <= 1)
            {
                if (winnerIndex >= 0)
                {
                    Debug.Log($"Player {winnerIndex} wins!");
                    OnGameOver?.Invoke(winnerIndex);
                    currentPhase = GamePhase.GameOver;
                    return true;
                }
            }
            
            // 检查得分胜利
            if (gameConfig.scoreToWin > 0)
            {
                foreach (var player in players)
                {
                    if (player.score >= gameConfig.scoreToWin)
                    {
                        Debug.Log($"Player {player.playerId} wins with {player.score} points!");
                        OnGameOver?.Invoke(player.playerId);
                        currentPhase = GamePhase.GameOver;
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        private void UpdateGameOver()
        {
            // 游戏结束状态
        }
        
        #endregion
        
        #region 辅助方法
        
        /// <summary>
        /// 检查所有弹珠是否静止
        /// </summary>
        private bool AreAllMarblesStationary()
        {
            foreach (var marble in allMarbles)
            {
                if (marble != null && !marble.IsStationary())
                {
                    return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// 获取当前玩家
        /// </summary>
        public PlayerData GetCurrentPlayer()
        {
            return players[currentPlayerIndex];
        }
        
        /// <summary>
        /// 获取指定玩家
        /// </summary>
        public PlayerData GetPlayer(int index)
        {
            if (index >= 0 && index < players.Count)
            {
                return players[index];
            }
            return null;
        }
        
        /// <summary>
        /// 获取游戏阶段
        /// </summary>
        public GamePhase GetCurrentPhase()
        {
            return currentPhase;
        }
        
        /// <summary>
        /// 获取当前玩家索引
        /// </summary>
        public int GetCurrentPlayerIndex()
        {
            return currentPlayerIndex;
        }

        /// <summary>
        /// 当前使用的发射器（供 AI / 系统化调用）
        /// </summary>
        public MarbleShooter ActiveShooter => marbleShooter;
        
        #endregion
        
        void OnDestroy()
        {
            // 取消订阅事件
            if (marbleShooter != null)
            {
                marbleShooter.OnMarbleShot -= OnMarbleShot;
                marbleShooter.OnStateChanged -= OnShootStateChanged;
            }
        }
    }
}
