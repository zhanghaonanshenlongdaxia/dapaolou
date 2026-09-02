using UnityEngine;

namespace Dapaolou.Game
{
    /// <summary>
    /// 角色预设 - 创建三个兄弟的默认配置
    /// </summary>
    public static class CharacterPresets
    {
        /// <summary>
        /// 创建大哥配置
        /// </summary>
        public static CharacterConfig CreateEldestConfig()
        {
            CharacterConfig config = ScriptableObject.CreateInstance<CharacterConfig>();
            
            config.characterType = CharacterType.Eldest;
            config.characterName = "大哥";
            config.displayName = "大哥（你）";
            config.description = "稳重可靠的大哥，力度适中，稳定性最高。\n\n特点：\n- 力度均衡\n- 手抖最小\n- 适合新手";
            config.characterColor = new Color(0.2f, 0.6f, 1f); // 蓝色
            
            // 弹珠属性
            config.initialMarbleCount = 21;
            
            // 力度属性 - 均衡型
            config.minShootForce = 3f;
            config.maxShootForce = 12f;
            config.chargeSpeed = 1f;
            config.forceMultiplier = 1f;
            
            // 手抖属性 - 最稳定
            config.baseStability = 0.9f;
            config.tremorAmount = 0.015f;
            config.nervousnessMultiplier = 0.8f;
            config.aimDriftRadius = 0.3f;
            
            // 移动属性
            config.walkSpeed = 3f;
            config.crouchSpeed = 1.5f;
            config.mouseSensitivity = 2f;
            
            // 特殊能力
            config.canDoubleShot = false;
            config.canSteadyAim = true;     // 大哥可以稳定瞄准
            config.criticalChance = 0.1f;
            config.criticalMultiplier = 1.5f;
            
            return config;
        }
        
        /// <summary>
        /// 创建二弟配置
        /// </summary>
        public static CharacterConfig CreateSecondConfig()
        {
            CharacterConfig config = ScriptableObject.CreateInstance<CharacterConfig>();
            
            config.characterType = CharacterType.Second;
            config.characterName = "二弟";
            config.displayName = "二弟";
            config.description = "力气最大的二弟，但稳定性较差。\n\n特点：\n- 力度最大\n- 手抖较大\n- 暴击率高";
            config.characterColor = new Color(1f, 0.4f, 0.2f); // 橙色
            
            // 弹珠属性
            config.initialMarbleCount = 21;
            
            // 力度属性 - 力量型
            config.minShootForce = 5f;
            config.maxShootForce = 18f;     // 力度最大
            config.chargeSpeed = 0.9f;      // 蓄力稍慢
            config.forceMultiplier = 1.2f;
            
            // 手抖属性 - 较不稳定
            config.baseStability = 0.7f;
            config.tremorAmount = 0.03f;    // 抖动较大
            config.nervousnessMultiplier = 1.2f;
            config.aimDriftRadius = 0.7f;   // 漂移较大
            
            // 移动属性
            config.walkSpeed = 3.5f;        // 移动较快
            config.crouchSpeed = 1.8f;
            config.mouseSensitivity = 2.5f;
            
            // 特殊能力
            config.canDoubleShot = false;
            config.canSteadyAim = false;
            config.criticalChance = 0.2f;   // 暴击率高
            config.criticalMultiplier = 1.8f;
            
            return config;
        }
        
        /// <summary>
        /// 创建三弟配置
        /// </summary>
        public static CharacterConfig CreateThirdConfig()
        {
            CharacterConfig config = ScriptableObject.CreateInstance<CharacterConfig>();
            
            config.characterType = CharacterType.Third;
            config.characterName = "三弟";
            config.displayName = "三弟";
            config.description = "灵活的三弟，可以双发弹珠。\n\n特点：\n- 可以双发\n- 力度较小\n- 灵活机动";
            config.characterColor = new Color(0.2f, 0.9f, 0.3f); // 绿色
            
            // 弹珠属性
            config.initialMarbleCount = 21;
            
            // 力度属性 - 技巧型
            config.minShootForce = 2f;
            config.maxShootForce = 10f;     // 力度较小
            config.chargeSpeed = 1.3f;      // 蓄力快
            config.forceMultiplier = 0.9f;
            
            // 手抖属性 - 中等稳定
            config.baseStability = 0.8f;
            config.tremorAmount = 0.02f;
            config.nervousnessMultiplier = 1f;
            config.aimDriftRadius = 0.5f;
            
            // 移动属性 - 最快
            config.walkSpeed = 4f;          // 移动最快
            config.crouchSpeed = 2f;
            config.mouseSensitivity = 3f;
            
            // 特殊能力
            config.canDoubleShot = true;    // 可以双发！
            config.canSteadyAim = false;
            config.criticalChance = 0.15f;
            config.criticalMultiplier = 1.5f;
            
            return config;
        }
        
        /// <summary>
        /// 获取角色预设描述
        /// </summary>
        public static string GetCharacterSummary(CharacterType type)
        {
            switch (type)
            {
                case CharacterType.Eldest:
                    return "【大哥】均衡型\n力度: ★★★☆☆\n稳定: ★★★★★\n速度: ★★★☆☆\n特殊: 稳定瞄准";
                    
                case CharacterType.Second:
                    return "【二弟】力量型\n力度: ★★★★★\n稳定: ★★☆☆☆\n速度: ★★★★☆\n特殊: 高暴击";
                    
                case CharacterType.Third:
                    return "【三弟】技巧型\n力度: ★★☆☆☆\n稳定: ★★★★☆\n速度: ★★★★★\n特殊: 双发弹珠";
                    
                default:
                    return "未知角色";
            }
        }
    }
}
