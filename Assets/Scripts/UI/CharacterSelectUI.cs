using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dapaolou.Game;

namespace Dapaolou.UI
{
    /// <summary>
    /// 角色选择UI - 让玩家选择扮演哪个兄弟
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private GameObject characterSelectPanel;
        [SerializeField] private Button[] characterButtons;         // 角色选择按钮
        [SerializeField] private Image[] characterPortraits;        // 角色头像
        [SerializeField] private TextMeshProUGUI[] characterNames;  // 角色名称
        [SerializeField] private TextMeshProUGUI[] characterDescs;  // 角色描述
        
        [Header("详情面板")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailPortrait;
        [SerializeField] private TextMeshProUGUI detailName;
        [SerializeField] private TextMeshProUGUI detailDescription;
        [SerializeField] private TextMeshProUGUI detailStats;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;
        
        [Header("属性显示")]
        [SerializeField] private Slider forceSlider;                // 力度条
        [SerializeField] private Slider stabilitySlider;            // 稳定性条
        [SerializeField] private Slider speedSlider;                // 速度条
        [SerializeField] private TextMeshProUGUI marbleCountText;   // 弹珠数量
        
        [Header("音效")]
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip confirmSound;
        [SerializeField] private AudioClip hoverSound;
        
        // 内部状态
        private CharacterConfig selectedCharacter;
        private CharacterType selectedType;
        private int currentIndex = 0;
        
        // 事件
        public System.Action<CharacterType> OnCharacterConfirmed;
        
        void Start()
        {
            InitializeUI();
        }
        
        /// <summary>
        /// 初始化UI
        /// </summary>
        private void InitializeUI()
        {
            // 获取角色配置
            CharacterManager charManager = CharacterManager.Instance;
            if (charManager == null)
            {
                Debug.LogError("CharacterManager not found!");
                return;
            }
            
            CharacterConfig[] characters = charManager.GetAllCharacters();
            
            // 设置按钮事件
            for (int i = 0; i < characterButtons.Length && i < characters.Length; i++)
            {
                int index = i; // 闭包引用
                characterButtons[i].onClick.AddListener(() => OnCharacterClicked(index));
                
                // 设置角色信息
                if (characterPortraits.Length > i && characters[i].portrait != null)
                {
                    characterPortraits[i].sprite = characters[i].portrait;
                }
                
                if (characterNames.Length > i)
                {
                    characterNames[i].text = characters[i].displayName;
                }
                
                if (characterDescs.Length > i)
                {
                    characterDescs[i].text = characters[i].description;
                }
            }
            
            // 设置确认和返回按钮
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
            
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }
            
            // 默认选择大哥
            SelectCharacter(0);
        }
        
        /// <summary>
        /// 角色按钮点击
        /// </summary>
        private void OnCharacterClicked(int index)
        {
            SelectCharacter(index);
            
            // 播放选择音效
            PlaySound(selectSound);
        }
        
        /// <summary>
        /// 选择角色
        /// </summary>
        private void SelectCharacter(int index)
        {
            CharacterManager charManager = CharacterManager.Instance;
            if (charManager == null) return;
            
            CharacterConfig[] characters = charManager.GetAllCharacters();
            if (index < 0 || index >= characters.Length) return;
            
            currentIndex = index;
            selectedCharacter = characters[index];
            selectedType = selectedCharacter.characterType;
            
            // 更新详情面板
            UpdateDetailPanel(selectedCharacter);
            
            // 高亮选中的按钮
            UpdateButtonHighlights(index);
        }
        
        /// <summary>
        /// 更新详情面板
        /// </summary>
        private void UpdateDetailPanel(CharacterConfig config)
        {
            if (detailPanel == null) return;
            
            // 显示详情面板
            detailPanel.SetActive(true);
            
            // 设置头像
            if (detailPortrait != null && config.portrait != null)
            {
                detailPortrait.sprite = config.portrait;
            }
            
            // 设置名称
            if (detailName != null)
            {
                detailName.text = config.displayName;
            }
            
            // 设置描述
            if (detailDescription != null)
            {
                detailDescription.text = config.description;
            }
            
            // 设置属性条
            if (forceSlider != null)
            {
                // 归一化力度值 (2-15 -> 0-1)
                forceSlider.value = (config.maxShootForce - 2f) / 13f;
            }
            
            if (stabilitySlider != null)
            {
                stabilitySlider.value = config.baseStability;
            }
            
            if (speedSlider != null)
            {
                // 归一化速度值 (1.5-5 -> 0-1)
                speedSlider.value = (config.walkSpeed - 1.5f) / 3.5f;
            }
            
            // 设置弹珠数量
            if (marbleCountText != null)
            {
                marbleCountText.text = $"弹珠: {config.initialMarbleCount}";
            }
            
            // 设置详细属性文本
            if (detailStats != null)
            {
                string stats = $"<b>属性详情</b>\n\n";
                stats += $"力度范围: {config.minShootForce:F1} - {config.maxShootForce:F1}\n";
                stats += $"蓄力速度: {config.chargeSpeed:F2}x\n";
                stats += $"基础稳定性: {config.baseStability * 100:F0}%\n";
                stats += $"手抖幅度: {config.tremorAmount:F3}\n";
                stats += $"瞄准漂移: {config.aimDriftRadius:F2}\n";
                stats += $"行走速度: {config.walkSpeed:F1}\n";
                stats += $"暴击概率: {config.criticalChance * 100:F0}%\n";
                
                if (config.canDoubleShot)
                {
                    stats += $"\n<color=yellow>★ 可以双发</color>";
                }
                if (config.canSteadyAim)
                {
                    stats += $"\n<color=yellow>★ 稳定瞄准</color>";
                }
                
                detailStats.text = stats;
            }
        }
        
        /// <summary>
        /// 更新按钮高亮
        /// </summary>
        private void UpdateButtonHighlights(int selectedIndex)
        {
            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] != null)
                {
                    // 设置选中状态
                    ColorBlock colors = characterButtons[i].colors;
                    colors.normalColor = (i == selectedIndex) ? Color.yellow : Color.white;
                    characterButtons[i].colors = colors;
                }
            }
        }
        
        /// <summary>
        /// 确认按钮点击
        /// </summary>
        private void OnConfirmClicked()
        {
            if (selectedCharacter == null) return;
            
            Debug.Log($"确认选择: {selectedCharacter.characterName}");
            
            // 播放确认音效
            PlaySound(confirmSound);
            
            // 通知角色管理器
            CharacterManager charManager = CharacterManager.Instance;
            if (charManager != null)
            {
                charManager.SelectCharacter(selectedType);
            }
            
            // 触发事件
            OnCharacterConfirmed?.Invoke(selectedType);
            
            // 隐藏面板
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 返回按钮点击
        /// </summary>
        private void OnBackClicked()
        {
            // 返回上一步
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 显示角色选择界面
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            SelectCharacter(0); // 默认选择大哥
        }
        
        /// <summary>
        /// 隐藏角色选择界面
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// 播放音效
        /// </summary>
        private void PlaySound(AudioClip clip)
        {
            if (clip != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
            }
        }
    }
}
