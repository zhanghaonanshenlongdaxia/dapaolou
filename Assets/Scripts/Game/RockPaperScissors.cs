using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Dapaolou.NPC;

namespace Dapaolou.Game
{
    /// <summary>
    /// 石头剪刀布选择
    /// </summary>
    public enum RPSChoice
    {
        Rock = 0,       // 石头
        Scissors = 1,   // 剪刀
        Paper = 2       // 布
    }

    /// <summary>
    /// 石头剪刀布结果
    /// </summary>
    public enum RPSResult
    {
        Win,            // 赢
        Lose,           // 输
        Draw            // 平局
    }

    /// <summary>
    /// 参与者数据
    /// </summary>
    [System.Serializable]
    public class RPSPlayer
    {
        public string playerName;
        public int playerIndex;
        public RPSChoice choice;
        public RPSResult lastResult;
        public bool isOut = false;              // 是否淘汰
        public int winCount = 0;
    }

    /// <summary>
    /// 石头剪刀布系统 - 决定游戏开始顺序
    /// </summary>
    public class RockPaperScissors : MonoBehaviour
    {
        [Header("UI配置")]
        [SerializeField] private GameObject rpsPanelPrefab;
        [SerializeField] private Transform rpsPanelPosition;
        [SerializeField] private float choiceDisplayTime = 2f;
        [SerializeField] private float resultDisplayTime = 1.5f;
        
        [Header("音效")]
        [SerializeField] private AudioClip countdownSound;
        [SerializeField] private AudioClip revealSound;
        [SerializeField] private AudioClip winSound;
        [SerializeField] private AudioClip loseSound;
        [SerializeField] private AudioClip drawSound;
        
        // 参与者
        private List<RPSPlayer> players = new List<RPSPlayer>();
        private int currentPlayerIndex = 0;
        
        // 游戏状态
        private bool isGameActive = false;
        private bool isWaitingForChoice = false;
        private RPSPlayer winner = null;
        
        // UI引用
        private GameObject currentPanel;
        
        // 事件
        public System.Action<RPSPlayer> OnPlayerChooses;
        public System.Action<RPSResult> OnRoundResult;
        public System.Action<RPSPlayer> OnGameEnd;
        
        /// <summary>
        /// 开始石头剪刀布
        /// </summary>
        public void StartRPS(List<string> playerNames)
        {
            // 初始化玩家
            players.Clear();
            for (int i = 0; i < playerNames.Count; i++)
            {
                players.Add(new RPSPlayer
                {
                    playerName = playerNames[i],
                    playerIndex = i,
                    isOut = false,
                    winCount = 0
                });
            }
            
            // 开始游戏
            StartCoroutine(RPSGameLoop());
        }
        
        /// <summary>
        /// 游戏循环
        /// </summary>
        private IEnumerator RPSGameLoop()
        {
            isGameActive = true;
            winner = null;
            
            Debug.Log("石头剪刀布开始！");
            
            // 循环直到决出胜负
            while (isGameActive)
            {
                // 重置选择
                foreach (var player in players)
                {
                    if (!player.isOut)
                    {
                        player.choice = RPSChoice.Rock; // 默认值
                    }
                }
                
                // 倒计时
                yield return StartCoroutine(Countdown());
                
                // 收集选择
                yield return StartCoroutine(CollectChoices());
                
                // 显示结果
                yield return StartCoroutine(ShowResults());
                
                // 检查是否结束
                if (CheckGameEnd())
                {
                    isGameActive = false;
                }
            }
            
            // 游戏结束
            Debug.Log($"石头剪刀布结束！{winner.playerName}获胜！");
            OnGameEnd?.Invoke(winner);
        }
        
        /// <summary>
        /// 倒计时
        /// </summary>
        private IEnumerator Countdown()
        {
            Debug.Log("石头剪刀布！");
            
            // 显示倒计时UI
            ShowRPSUI("石头剪刀布...");
            
            // 播放倒计时音效
            if (countdownSound != null)
            {
                AudioSource.PlayClipAtPoint(countdownSound, Camera.main.transform.position);
            }
            
            yield return new WaitForSeconds(1f);
            
            Debug.Log("布！");
        }
        
        /// <summary>
        /// 收集选择
        /// </summary>
        private IEnumerator CollectChoices()
        {
            isWaitingForChoice = true;
            
            // 让每个玩家做出选择
            foreach (var player in players)
            {
                if (player.isOut) continue;
                
                // 如果是玩家（人类），等待输入
                if (player.playerIndex == 0)
                {
                    yield return StartCoroutine(WaitForPlayerChoice(player));
                }
                else
                {
                    // NPC自动选择
                    yield return StartCoroutine(NPCMakesChoice(player));
                }
            }
            
            isWaitingForChoice = false;
        }
        
        /// <summary>
        /// 等待玩家选择
        /// </summary>
        private IEnumerator WaitForPlayerChoice(RPSPlayer player)
        {
            ShowRPSUI("按1=石头 2=剪刀 3=布");
            
            bool hasChosen = false;
            
            while (!hasChosen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                {
                    player.choice = RPSChoice.Rock;
                    hasChosen = true;
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                {
                    player.choice = RPSChoice.Scissors;
                    hasChosen = true;
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                {
                    player.choice = RPSChoice.Paper;
                    hasChosen = true;
                }
                
                yield return null;
            }
            
            Debug.Log($"{player.playerName}选择了: {GetChoiceName(player.choice)}");
            OnPlayerChooses?.Invoke(player);
        }
        
        /// <summary>
        /// NPC做出选择
        /// </summary>
        private IEnumerator NPCMakesChoice(RPSPlayer player)
        {
            // NPC随机选择
            player.choice = (RPSChoice)Random.Range(0, 3);
            
            // 模拟思考时间
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
            
            Debug.Log($"{player.playerName}选择了: {GetChoiceName(player.choice)}");
            OnPlayerChooses?.Invoke(player);
        }
        
        /// <summary>
        /// 显示结果
        /// </summary>
        private IEnumerator ShowResults()
        {
            // 显示所有选择
            string resultText = "结果：\n";
            foreach (var player in players)
            {
                if (!player.isOut)
                {
                    resultText += $"{player.playerName}: {GetChoiceName(player.choice)}\n";
                }
            }
            
            ShowRPSUI(resultText);
            
            // 播放揭示音效
            if (revealSound != null)
            {
                AudioSource.PlayClipAtPoint(revealSound, Camera.main.transform.position);
            }
            
            yield return new WaitForSeconds(resultDisplayTime);
            
            // 计算结果
            CalculateResults();
        }
        
        /// <summary>
        /// 计算结果
        /// </summary>
        private void CalculateResults()
        {
            List<RPSPlayer> activePlayers = GetActivePlayers();
            
            if (activePlayers.Count < 2) return;
            
            // 检查是否有平局
            bool isDraw = false;
            RPSChoice? winningChoice = null;
            
            // 检查所有人的选择是否相同
            RPSChoice firstChoice = activePlayers[0].choice;
            bool allSame = true;
            foreach (var player in activePlayers)
            {
                if (player.choice != firstChoice)
                {
                    allSame = false;
                    break;
                }
            }
            
            if (allSame)
            {
                // 全部相同，平局
                isDraw = true;
                foreach (var player in activePlayers)
                {
                    player.lastResult = RPSResult.Draw;
                }
                Debug.Log("平局！重新来过！");
            }
            else
            {
                // 计算胜负
                foreach (var player in activePlayers)
                {
                    player.lastResult = CalculatePlayerResult(player, activePlayers);
                }
                
                // 淘汰输的玩家
                foreach (var player in activePlayers)
                {
                    if (player.lastResult == RPSResult.Lose)
                    {
                        player.isOut = true;
                        Debug.Log($"{player.playerName}淘汰！");
                    }
                    else if (player.lastResult == RPSResult.Win)
                    {
                        player.winCount++;
                    }
                }
            }
            
            // 播放结果音效
            PlayResultSound(isDraw);
            
            // 通知结果
            if (isDraw)
            {
                OnRoundResult?.Invoke(RPSResult.Draw);
            }
            else
            {
                OnRoundResult?.Invoke(RPSResult.Win);
            }
        }
        
        /// <summary>
        /// 计算单个玩家结果
        /// </summary>
        private RPSResult CalculatePlayerResult(RPSPlayer player, List<RPSPlayer> opponents)
        {
            foreach (var opponent in opponents)
            {
                if (opponent == player || opponent.isOut) continue;
                
                RPSResult result = CompareChoices(player.choice, opponent.choice);
                
                if (result == RPSResult.Lose)
                {
                    return RPSResult.Lose;
                }
            }
            
            return RPSResult.Win;
        }
        
        /// <summary>
        /// 比较两个选择
        /// </summary>
        private RPSResult CompareChoices(RPSChoice choice1, RPSChoice choice2)
        {
            if (choice1 == choice2)
            {
                return RPSResult.Draw;
            }
            
            // 石头赢剪刀，剪刀赢布，布赢石头
            switch (choice1)
            {
                case RPSChoice.Rock:
                    return choice2 == RPSChoice.Scissors ? RPSResult.Win : RPSResult.Lose;
                case RPSChoice.Scissors:
                    return choice2 == RPSChoice.Paper ? RPSResult.Win : RPSResult.Lose;
                case RPSChoice.Paper:
                    return choice2 == RPSChoice.Rock ? RPSResult.Win : RPSResult.Lose;
                default:
                    return RPSResult.Draw;
            }
        }
        
        /// <summary>
        /// 检查游戏是否结束
        /// </summary>
        private bool CheckGameEnd()
        {
            List<RPSPlayer> activePlayers = GetActivePlayers();
            
            // 只剩一个玩家
            if (activePlayers.Count == 1)
            {
                winner = activePlayers[0];
                return true;
            }
            
            // 检查是否有玩家连续赢
            foreach (var player in activePlayers)
            {
                if (player.winCount >= 2)
                {
                    winner = player;
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取活跃玩家
        /// </summary>
        private List<RPSPlayer> GetActivePlayers()
        {
            List<RPSPlayer> active = new List<RPSPlayer>();
            foreach (var player in players)
            {
                if (!player.isOut)
                {
                    active.Add(player);
                }
            }
            return active;
        }
        
        #region UI
        
        private void ShowRPSUI(string message)
        {
            Debug.Log($"[石头剪刀布] {message}");
            // 这里可以实例化UI预制体
        }
        
        #endregion
        
        #region 音效
        
        private void PlayResultSound(bool isDraw)
        {
            AudioClip clip = isDraw ? drawSound : winSound;
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
            }
        }
        
        #endregion
        
        #region 辅助方法
        
        private string GetChoiceName(RPSChoice choice)
        {
            switch (choice)
            {
                case RPSChoice.Rock: return "石头";
                case RPSChoice.Scissors: return "剪刀";
                case RPSChoice.Paper: return "布";
                default: return "未知";
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取获胜者
        /// </summary>
        public RPSPlayer GetWinner()
        {
            return winner;
        }
        
        /// <summary>
        /// 获取获胜者索引
        /// </summary>
        public int GetWinnerIndex()
        {
            return winner != null ? winner.playerIndex : -1;
        }
        
        /// <summary>
        /// 游戏是否进行中
        /// </summary>
        public bool IsGameActive()
        {
            return isGameActive;
        }
        
        /// <summary>
        /// 获取玩家列表
        /// </summary>
        public List<RPSPlayer> GetPlayers()
        {
            return players;
        }
        
        #endregion
    }
}
