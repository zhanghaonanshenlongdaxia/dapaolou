using UnityEngine;
using Dapaolou.Player;
using Dapaolou.Marble;

namespace Dapaolou.Game
{
    /// <summary>
    /// 角色类型
    /// </summary>
    public enum CharacterType
    {
        Eldest,     // 大哥（玩家默认）
        Second,     // 二哥（臭豆）
        Third       // 三弟（骚咪）
    }

    /// <summary>
    /// 角色配置 - 存储每个角色的属性
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterConfig", menuName = "Dapaolou/Character Config")]
    public class CharacterConfig : ScriptableObject
    {
        [Header("基本信息")]
        public CharacterType characterType;
        public string characterName = "角色";
        public string displayName = "角色";
        public string description = "角色描述";
        public Color characterColor = Color.white;
        
        [Header("弹珠属性")]
        public int initialMarbleCount = 21;         // 初始弹珠数量
        
        [Header("力度属性")]
        public float minShootForce = 2f;            // 最小弹射力度
        public float maxShootForce = 15f;           // 最大弹射力度
        public float chargeSpeed = 1f;              // 蓄力速度
        public float forceMultiplier = 1f;          // 力度倍率
        
        [Header("手抖属性")]
        public float baseStability = 0.8f;          // 基础稳定性
        public float tremorAmount = 0.02f;          // 基础抖动幅度
        public float nervousnessMultiplier = 1f;    // 紧张度倍率
        public float aimDriftRadius = 0.5f;         // 瞄准漂移半径
        
        [Header("移动属性")]
        public float walkSpeed = 3f;                // 行走速度
        public float crouchSpeed = 1.5f;            // 蹲下速度
        public float mouseSensitivity = 2f;         // 鼠标灵敏度
        
        [Header("特殊能力")]
        public bool canDoubleShot = false;          // 能否双发
        public bool canSteadyAim = false;           // 能否稳定瞄准
        public float criticalChance = 0.1f;         // 暴击概率
        public float criticalMultiplier = 1.5f;     // 暴击倍率
        
        [Header("视觉")]
        public Sprite portrait;                     // 头像
        public RuntimeAnimatorController animatorController; // 动画控制器
    }

    /// <summary>
    /// 角色管理器 - 管理所有角色配置
    /// </summary>
    public class CharacterManager : MonoBehaviour
    {
        [Header("角色配置")]
        [SerializeField] private CharacterConfig eldestConfig;  // 大哥配置
        [SerializeField] private CharacterConfig secondConfig;  // 二哥配置
        [SerializeField] private CharacterConfig thirdConfig;   // 三弟配置
        
        // 当前选择的角色
        private CharacterConfig currentCharacter;
        private CharacterType selectedType = CharacterType.Eldest;
        
        // 单例
        public static CharacterManager Instance { get; private set; }
        
        // 事件
        public System.Action<CharacterConfig> OnCharacterSelected;
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 获取指定角色配置
        /// </summary>
        public CharacterConfig GetCharacterConfig(CharacterType type)
        {
            switch (type)
            {
                case CharacterType.Eldest:
                    return eldestConfig;
                case CharacterType.Second:
                    return secondConfig;
                case CharacterType.Third:
                    return thirdConfig;
                default:
                    return eldestConfig;
            }
        }
        
        /// <summary>
        /// 获取所有角色配置
        /// </summary>
        public CharacterConfig[] GetAllCharacters()
        {
            return new CharacterConfig[] { eldestConfig, secondConfig, thirdConfig };
        }
        
        /// <summary>
        /// 选择角色
        /// </summary>
        public void SelectCharacter(CharacterType type)
        {
            selectedType = type;
            currentCharacter = GetCharacterConfig(type);
            
            Debug.Log($"选择了角色: {currentCharacter.characterName}");
            OnCharacterSelected?.Invoke(currentCharacter);
        }
        
        /// <summary>
        /// 获取当前角色配置
        /// </summary>
        public CharacterConfig GetCurrentCharacter()
        {
            if (currentCharacter == null)
            {
                currentCharacter = eldestConfig; // 默认大哥
            }
            return currentCharacter;
        }
        
        /// <summary>
        /// 获取当前角色类型
        /// </summary>
        public CharacterType GetSelectedType()
        {
            return selectedType;
        }
        
        /// <summary>
        /// 应用角色属性到玩家
        /// </summary>
        public void ApplyCharacterToPlayer(PlayerManager playerManager)
        {
            if (playerManager == null || currentCharacter == null) return;
            
            // 应用力度属性
            Marble.MarbleShooter shooter = playerManager.GetComponent<Marble.MarbleShooter>();
            if (shooter != null)
            {
                // 这里需要MarbleShooter支持外部配置
                // shooter.SetForceRange(currentCharacter.minShootForce, currentCharacter.maxShootForce);
                // shooter.SetChargeSpeed(currentCharacter.chargeSpeed);
            }
            
            // 应用手抖属性
            Marble.HandTremorSystem tremor = playerManager.GetComponent<Marble.HandTremorSystem>();
            if (tremor != null)
            {
                tremor.SetBaseStability(currentCharacter.baseStability);
            }
            
            // 应用移动属性
            Player.FirstPersonController fps = playerManager.GetComponent<Player.FirstPersonController>();
            if (fps != null)
            {
                // 这里需要FirstPersonController支持外部配置
                // fps.SetWalkSpeed(currentCharacter.walkSpeed);
                // fps.SetMouseSensitivity(currentCharacter.mouseSensitivity);
            }
        }
    }
}
