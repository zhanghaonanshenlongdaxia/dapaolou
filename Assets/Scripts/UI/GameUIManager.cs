using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dapaolou.Game;
using Dapaolou.Marble;

namespace Dapaolou.UI
{
    /// <summary>
    /// 游戏UI管理器
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("玩家信息")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI playerScoreText;
        [SerializeField] private TextMeshProUGUI turnTimerText;
        [SerializeField] private Image playerColorIndicator;
        
        [Header("弹珠状态")]
        [SerializeField] private TextMeshProUGUI soldierCountText;
        [SerializeField] private TextMeshProUGUI towerCountText;
        [SerializeField] private TextMeshProUGUI marbleStateText;
        
        [Header("力度条")]
        [SerializeField] private Slider powerSlider;
        [SerializeField] private Image powerFill;
        [SerializeField] private TextMeshProUGUI powerText;
        [SerializeField] private Gradient powerGradient;
        
        [Header("手抖指示器")]
        [SerializeField] private RectTransform tremorIndicator;
        [SerializeField] private Image tremorCircle;
        [SerializeField] private float tremorVisualScale = 100f;
        
        [Header("提示信息")]
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private GameObject dismantleButton;
        [SerializeField] private GameObject skipTurnButton;
        
        [Header("游戏状态")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI winnerText;
        [SerializeField] private Button restartButton;

        [Header("排行榜")]
        [SerializeField] private TextMeshProUGUI rankingText;    // 弹珠排行榜文本
        
        // 内部状态
        private GameManager gameManager;
        private MarbleShooter marbleShooter;
        private HandTremorSystem tremorSystem;
        private float rankingRefreshTimer = 0f;
        
        void Awake()
        {
            gameManager = FindObjectOfType<GameManager>();
        }
        
        void Start()
        {
            // 订阅事件
            if (gameManager != null)
            {
                gameManager.OnPlayerTurnStart += OnTurnStart;
                gameManager.OnPlayerTurnEnd += OnTurnEnd;
                gameManager.OnGameOver += OnGameOver;
            }
            
            // 初始化UI
            InitializeUI();
        }
        
        void Update()
        {
            UpdateTurnTimer();
            UpdateTremorIndicator();
            UpdatePowerBar();
            UpdateRanking();
        }

        /// <summary>
        /// 刷新弹珠排行榜：按库存降序列出所有玩家
        /// </summary>
        private void UpdateRanking()
        {
            if (rankingText == null || gameManager == null) return;
            if (rankingRefreshTimer > 0f)
            {
                rankingRefreshTimer -= Time.deltaTime;
                return;
            }
            rankingRefreshTimer = 0.5f;

            var players = new System.Collections.Generic.List<PlayerData>();
            for (int i = 0; i < 2; i++)
            {
                var p = gameManager.GetPlayer(i);
                if (p != null) players.Add(p);
            }
            players.Sort((a, b) => b.marbleStock.CompareTo(a.marbleStock));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("弹珠排行榜");
            for (int i = 0; i < players.Count; i++)
            {
                sb.AppendLine($"{i + 1}. 玩家{players[i].playerId + 1}  {players[i].marbleStock}颗");
            }
            rankingText.text = sb.ToString();
        }
        
        #region 初始化
        
        private void InitializeUI()
        {
            // 隐藏游戏结束面板
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
            
            // 初始化力度条
            if (powerSlider != null)
            {
                powerSlider.minValue = 0f;
                powerSlider.maxValue = 1f;
                powerSlider.value = 0f;
            }
            
            // 创建默认颜色渐变
            if (powerGradient == null)
            {
                powerGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(Color.green, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.red, 1f)
                };
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                };
                powerGradient.SetKeys(colorKeys, alphaKeys);
            }
            
            // 隐藏拆炮楼按钮
            if (dismantleButton != null)
            {
                dismantleButton.SetActive(false);
            }
        }
        
        #endregion
        
        #region UI更新
        
        private void UpdateTurnTimer()
        {
            if (turnTimerText == null || gameManager == null) return;
            
            // 获取当前玩家的回合时间
            PlayerData currentPlayer = gameManager.GetCurrentPlayer();
            if (currentPlayer != null)
            {
                // 这里需要GameManager提供时间，简化处理
                turnTimerText.text = $"回合时间: --";
            }
        }
        
        private void UpdateTremorIndicator()
        {
            if (tremorIndicator == null) return;
            
            // 获取手抖强度
            float tremorIntensity = 0f;
            if (tremorSystem != null)
            {
                tremorIntensity = tremorSystem.GetCurrentTremorIntensity();
            }
            
            // 更新手抖指示器大小
            float scale = 1f + tremorIntensity * 2f;
            tremorIndicator.localScale = Vector3.one * scale;
            
            // 更新颜色
            if (tremorCircle != null)
            {
                Color color = Color.Lerp(Color.green, Color.red, tremorIntensity);
                tremorCircle.color = color;
            }
        }
        
        private void UpdatePowerBar()
        {
            if (powerSlider == null || marbleShooter == null) return;
            
            // 更新力度条
            float power = marbleShooter.GetCurrentPower();
            powerSlider.value = power;
            
            // 更新颜色
            if (powerFill != null)
            {
                powerFill.color = powerGradient.Evaluate(power);
            }
            
            // 更新文本
            if (powerText != null)
            {
                powerText.text = $"力度: {Mathf.RoundToInt(power * 100)}%";
            }
        }
        
        #endregion
        
        #region 事件回调
        
        private void OnTurnStart(int playerIndex)
        {
            PlayerData player = gameManager.GetPlayer(playerIndex);
            if (player == null) return;
            
            // 更新玩家信息
            if (playerNameText != null)
            {
                playerNameText.text = player.playerName;
            }
            
            if (playerScoreText != null)
            {
                playerScoreText.text = $"得分: {player.score}";
            }
            
            if (playerColorIndicator != null)
            {
                playerColorIndicator.color = player.playerColor;
            }
            
            // 更新弹珠状态
            UpdateMarbleStatus(player);
            
            // 显示提示
            ShowHint($"玩家 {playerIndex + 1} 的回合");
            
            // 检查是否可以拆炮楼
            if (dismantleButton != null)
            {
                dismantleButton.SetActive(player.CanDismantleTower());
            }
        }
        
        private void OnTurnEnd(int playerIndex)
        {
            // 清理UI
            if (hintText != null)
            {
                hintText.text = "";
            }
            
            if (dismantleButton != null)
            {
                dismantleButton.SetActive(false);
            }
        }
        
        private void OnGameOver(int winnerIndex)
        {
            // 显示游戏结束面板
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
            
            if (winnerText != null)
            {
                PlayerData winner = gameManager.GetPlayer(winnerIndex);
                winnerText.text = $"玩家 {winnerIndex + 1} 获胜！\n得分: {winner?.score ?? 0}";
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        private void UpdateMarbleStatus(PlayerData player)
        {
            if (soldierCountText != null)
            {
                soldierCountText.text = $"小兵: {player.GetAliveSoldierCount()}/{player.maxSoldiers}";
            }
            
            if (towerCountText != null)
            {
                int towerCount = player.GetAliveTowerCount();
                towerCountText.text = $"炮楼: {towerCount}/4";
            }
            
            if (marbleStateText != null)
            {
                string state = player.NeedsSecondHit() ? "需要二次打爆" : "正常";
                marbleStateText.text = $"状态: {state}";
            }
        }
        
        private void ShowHint(string message)
        {
            if (hintText != null)
            {
                hintText.text = message;
            }
        }
        
        #endregion
        
        #region 按钮回调
        
        /// <summary>
        /// 拆炮楼按钮点击
        /// </summary>
        public void OnDismantleButtonClicked()
        {
            if (gameManager == null) return;
            
            int currentPlayerIndex = gameManager.GetCurrentPlayerIndex();
            bool success = gameManager.TryDismantleTower(currentPlayerIndex);
            
            if (success)
            {
                ShowHint("拆炮楼成功！获得1个小兵");
                
                // 更新UI
                PlayerData player = gameManager.GetCurrentPlayer();
                if (player != null)
                {
                    UpdateMarbleStatus(player);
                }
            }
            else
            {
                ShowHint("无法拆炮楼");
            }
        }
        
        /// <summary>
        /// 跳过回合按钮点击
        /// </summary>
        public void OnSkipTurnButtonClicked()
        {
            // 这里需要通知GameManager跳过回合
            ShowHint("跳过回合");
        }
        
        /// <summary>
        /// 重新开始按钮点击
        /// </summary>
        public void OnRestartButtonClicked()
        {
            // 重新加载场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
        
        #endregion
        
        void OnDestroy()
        {
            // 取消订阅事件
            if (gameManager != null)
            {
                gameManager.OnPlayerTurnStart -= OnTurnStart;
                gameManager.OnPlayerTurnEnd -= OnTurnEnd;
                gameManager.OnGameOver -= OnGameOver;
            }
        }
    }
}
