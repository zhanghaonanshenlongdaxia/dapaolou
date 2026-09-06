using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Dapaolou.NPC;

namespace Dapaolou.Game
{
    /// <summary>
    /// 游戏开始阶段
    /// </summary>
    public enum GameStartPhase
    {
        Intro,              // 介绍
        CharacterSelect,    // 角色选择
        SettingUpTowers,    // 摆炮楼
        RockPaperScissors,  // 石头剪刀布
        DecidingOrder,      // 决定顺序
        GameStart           // 游戏开始
    }

    /// <summary>
    /// 游戏开始管理器 - 处理游戏开始前的准备工作
    /// </summary>
    public class GameStartManager : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private RockPaperScissors rpsSystem;
        [SerializeField] private NPCManager npcManager;
        
        [Header("配置")]
        [SerializeField] private float introDuration = 5f;
        [SerializeField] private float setupDuration = 30f;
        
        [Header("音效")]
        [SerializeField] private AudioClip introMusic;
        [SerializeField] private AudioClip setupSound;
        [SerializeField] private AudioClip gameStartSound;
        
        [Header("UI")]
        [SerializeField] private GameObject introPanel;
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private GameObject setupPanel;
        [SerializeField] private GameObject rpsPanel;
        
        // 游戏开始阶段
        private GameStartPhase currentPhase = GameStartPhase.Intro;
        private bool isSetupComplete = false;
        private int[] playOrder = new int[3]; // 游戏顺序
        
        // 事件
        public System.Action<GameStartPhase> OnPhaseChanged;
        public System.Action<int[]> OnPlayOrderDecided;
        public System.Action OnGameReady;
        
        void Start()
        {
            // 开始游戏流程
            StartCoroutine(GameStartFlow());
        }
        
        /// <summary>
        /// 游戏开始流程
        /// </summary>
        private IEnumerator GameStartFlow()
        {
            // 阶段1：介绍
            yield return StartCoroutine(IntroPhase());
            
            // 阶段2：角色选择
            yield return StartCoroutine(CharacterSelectPhase());
            
            // 阶段3：摆炮楼
            yield return StartCoroutine(SetupPhase());
            
            // 阶段4：石头剪刀布
            yield return StartCoroutine(RPSPhase());
            
            // 阶段5：决定顺序
            yield return StartCoroutine(DecideOrderPhase());
            
            // 阶段6：游戏开始
            yield return StartCoroutine(GameStartPhaseRoutine());
        }
        
        #region 阶段1：介绍
        
        private IEnumerator IntroPhase()
        {
            currentPhase = GameStartPhase.Intro;
            OnPhaseChanged?.Invoke(currentPhase);
            
            Debug.Log("游戏开始！今天是个好天气，三兄弟要在院子里打弹珠！");
            ShowMessage("游戏开始！今天是个好天气，三兄弟要在院子里打弹珠！");
            
            // 播放介绍音乐
            if (introMusic != null)
            {
                AudioSource.PlayClipAtPoint(introMusic, Camera.main.transform.position);
            }
            
            // 显示介绍UI
            if (introPanel != null)
            {
                introPanel.SetActive(true);
            }
            
            // 等待介绍结束
            yield return new WaitForSeconds(introDuration);
            
            // 隐藏介绍UI
            if (introPanel != null)
            {
                introPanel.SetActive(false);
            }
        }
        
        #endregion
        
        #region 阶段2：角色选择
        
        private IEnumerator CharacterSelectPhase()
        {
            currentPhase = GameStartPhase.CharacterSelect;
            OnPhaseChanged?.Invoke(currentPhase);
            
            Debug.Log("选择你要扮演的兄弟！");
            ShowMessage("选择你要扮演的兄弟！");
            
            // 显示角色选择UI
            if (characterSelectPanel != null)
            {
                characterSelectPanel.SetActive(true);
            }
            
            // 等待玩家选择
            bool characterSelected = false;
            
            // 监听角色选择事件
            CharacterManager charManager = CharacterManager.Instance;
            if (charManager != null)
            {
                charManager.OnCharacterSelected += (config) =>
                {
                    characterSelected = true;
                };
            }
            
            // 等待选择完成
            while (!characterSelected)
            {
                // 也可以检测键盘快捷键
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    charManager?.SelectCharacter(CharacterType.Eldest);
                    characterSelected = true;
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    charManager?.SelectCharacter(CharacterType.Second);
                    characterSelected = true;
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    charManager?.SelectCharacter(CharacterType.Third);
                    characterSelected = true;
                }
                
                yield return null;
            }
            
            // 隐藏角色选择UI
            if (characterSelectPanel != null)
            {
                characterSelectPanel.SetActive(false);
            }
            
            // 获取选择的角色
            CharacterConfig selectedChar = charManager?.GetCurrentCharacter();
            if (selectedChar != null)
            {
                Debug.Log($"你选择了: {selectedChar.characterName}");
                ShowMessage($"你选择了: {selectedChar.characterName}！");
            }
            
            yield return new WaitForSeconds(1f);
        }
        
        #endregion
        
        #region 阶段3：摆炮楼
        
        private IEnumerator SetupPhase()
        {
            currentPhase = GameStartPhase.SettingUpTowers;
            OnPhaseChanged?.Invoke(currentPhase);
            
            Debug.Log("开始摆炮楼...");
            ShowMessage("摆好炮楼，准备开始！");
            
            // 播放摆炮楼音效
            if (setupSound != null)
            {
                AudioSource.PlayClipAtPoint(setupSound, Camera.main.transform.position);
            }
            
            // 显示摆炮楼UI
            if (setupPanel != null)
            {
                setupPanel.SetActive(true);
            }
            
            // 等待玩家摆好炮楼
            // 这里可以添加玩家操作逻辑
            float timer = 0f;
            while (!isSetupComplete && timer < setupDuration)
            {
                timer += Time.deltaTime;
                
                // 检查是否摆好炮楼
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    isSetupComplete = true;
                }
                
                yield return null;
            }
            
            // 隐藏摆炮楼UI
            if (setupPanel != null)
            {
                setupPanel.SetActive(false);
            }
            
            Debug.Log("炮楼摆好了！");
            ShowMessage("炮楼摆好了！来决定谁先开始！");
        }
        
        #endregion
        
        #region 阶段4：石头剪刀布
        
        private IEnumerator RPSPhase()
        {
            currentPhase = GameStartPhase.RockPaperScissors;
            OnPhaseChanged?.Invoke(currentPhase);
            
            Debug.Log("石头剪刀布！决定谁先开始！");
            ShowMessage("石头剪刀布！决定谁先开始！");
            
            // 显示石头剪刀布UI
            if (rpsPanel != null)
            {
                rpsPanel.SetActive(true);
            }
            
            // 获取所有玩家名称
            List<string> playerNames = new List<string>();
            playerNames.Add("我"); // 主角
            
            if (npcManager != null)
            {
                BrotherNPC brother1 = npcManager.GetBrother1();
                BrotherNPC brother2 = npcManager.GetBrother2();
                
                if (brother1 != null) playerNames.Add(brother1.GetBrotherName());
                if (brother2 != null) playerNames.Add(brother2.GetBrotherName());
            }
            
            // 开始石头剪刀布
            bool rpsComplete = false;
            RPSPlayer winner = null;
            
            if (rpsSystem != null)
            {
                rpsSystem.OnGameEnd += (w) =>
                {
                    winner = w;
                    rpsComplete = true;
                };
                
                rpsSystem.StartRPS(playerNames);
                
                // 等待石头剪刀布结束
                while (!rpsComplete)
                {
                    yield return null;
                }
            }
            else
            {
                // 如果没有石头剪刀布系统，随机决定顺序
                yield return new WaitForSeconds(3f);
            }
            
            // 隐藏石头剪刀布UI
            if (rpsPanel != null)
            {
                rpsPanel.SetActive(false);
            }
        }
        
        #endregion
        
        #region 阶段5：决定顺序
        
        private IEnumerator DecideOrderPhase()
        {
            currentPhase = GameStartPhase.DecidingOrder;
            OnPhaseChanged?.Invoke(currentPhase);
            
            // 决定游戏顺序
            // 这里简化处理，实际应该根据石头剪刀布结果
            playOrder = new int[] { 0, 1, 2 }; // 默认顺序
            
            // 如果有石头剪刀布获胜者，让他先开始
            if (rpsSystem != null && rpsSystem.GetWinner() != null)
            {
                int winnerIndex = rpsSystem.GetWinner().playerIndex;
                
                // 重新排列顺序
                List<int> order = new List<int>();
                order.Add(winnerIndex);
                
                for (int i = 0; i < 3; i++)
                {
                    if (i != winnerIndex)
                    {
                        order.Add(i);
                    }
                }
                
                playOrder = order.ToArray();
            }
            
            // 显示顺序
            string orderText = "游戏顺序：\n";
            for (int i = 0; i < playOrder.Length; i++)
            {
                string name = GetPlayerName(playOrder[i]);
                orderText += $"{i + 1}. {name}\n";
            }
            
            Debug.Log(orderText);
            ShowMessage(orderText);
            
            // 通知顺序决定
            OnPlayOrderDecided?.Invoke(playOrder);
            
            yield return new WaitForSeconds(3f);
        }
        
        #endregion
        
        #region 阶段6：游戏开始
        
        private IEnumerator GameStartPhaseRoutine()
        {
            currentPhase = GameStartPhase.GameStart;
            OnPhaseChanged?.Invoke(currentPhase);
            
            Debug.Log("游戏开始！");
            ShowMessage("游戏开始！按顺序打弹珠！");
            
            // 播放游戏开始音效
            if (gameStartSound != null)
            {
                AudioSource.PlayClipAtPoint(gameStartSound, Camera.main.transform.position);
            }
            
            // 通知游戏管理器
            if (gameManager != null)
            {
                // 设置游戏顺序
                gameManager.SetPlayOrder(playOrder);

                // 布防模式下先走布防阶段（人类放炮楼/小兵/暗兵），布防完成由 GameManager 自动开局
                if (gameManager.GetCurrentPhase() == GamePhase.Placement)
                {
                    Debug.Log("GameStartManager: 布防模式，等待玩家完成布防…");
                }
                else
                {
                    // 开始游戏
                    gameManager.StartGame();
                }
            }
            
            // 通知游戏准备就绪
            OnGameReady?.Invoke();
            
            yield return null;
        }
        
        #endregion
        
        #region 辅助方法
        
        private string GetPlayerName(int index)
        {
            switch (index)
            {
                case 0: return "我";
                case 1:
                    BrotherNPC brother1 = npcManager?.GetBrother1();
                    return brother1 != null ? brother1.GetBrotherName() : "弟弟1";
                case 2:
                    BrotherNPC brother2 = npcManager?.GetBrother2();
                    return brother2 != null ? brother2.GetBrotherName() : "弟弟2";
                default: return "未知";
            }
        }
        
        private void ShowMessage(string message)
        {
            Debug.Log($"[游戏开始] {message}");
            // 这里可以显示UI消息
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取当前阶段
        /// </summary>
        public GameStartPhase GetCurrentPhase()
        {
            return currentPhase;
        }
        
        /// <summary>
        /// 获取游戏顺序
        /// </summary>
        public int[] GetPlayOrder()
        {
            return playOrder;
        }
        
        /// <summary>
        /// 完成摆炮楼
        /// </summary>
        public void CompleteSetup()
        {
            isSetupComplete = true;
        }
        
        /// <summary>
        /// 跳过介绍
        /// </summary>
        public void SkipIntro()
        {
            StopAllCoroutines();
            StartCoroutine(SetupPhase());
        }
        
        #endregion
    }
}
