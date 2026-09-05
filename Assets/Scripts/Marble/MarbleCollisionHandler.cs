using UnityEngine;
using Dapaolou.Game;

namespace Dapaolou.Marble
{
    /// <summary>
    /// 弹珠碰撞处理器 - 检测和处理弹珠之间的碰撞
    /// </summary>
    public class MarbleCollisionHandler : MonoBehaviour
    {
        [Header("碰撞参数")]
        [SerializeField] private float minImpactForce = 1f;        // 最小碰撞力度
        [SerializeField] private float destroyThreshold = 5f;      // 摧毁阈值
        [SerializeField] private float secondHitThreshold = 3f;    // 二次打爆力度阈值
        
        [Header("特效")]
        [SerializeField] private GameObject hitParticlePrefab;     // 命中粒子特效
        [SerializeField] private AudioClip hitSound;               // 命中音效
        [SerializeField] private AudioClip destroySound;           // 摧毁音效
        
        private MarbleData marbleData;
        private AudioSource audioSource;
        private Vector3 lastSweepPos;           // 上一物理步位置（高速扫掠检测用）
        private bool sweepPrevValid = false;

        void Awake()
        {
            marbleData = GetComponent<MarbleData>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            lastSweepPos = transform.position;
        }

        void FixedUpdate()
        {
            // 高速弹珠扫掠检测：离散步进在 10+ m/s 时会穿透 5cm 目标（27cm/步），
            // 用球形扫掠补上物理引擎漏掉的接触
            if (marbleData == null || marbleData.state != MarbleState.Rolling) { sweepPrevValid = false; return; }
            var rb = marbleData.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) { sweepPrevValid = false; return; }

            Vector3 cur = transform.position;
            if (!sweepPrevValid) { lastSweepPos = cur; sweepPrevValid = true; return; }

            Vector3 move = cur - lastSweepPos;
            float dist = move.magnitude;
            if (dist > 0.02f)
            {
                var hits = Physics.SphereCastAll(lastSweepPos, 0.03f, move.normalized, dist + 0.02f);
                foreach (var h in hits)
                {
                    var otherData = h.collider.GetComponent<MarbleData>();
                    if (otherData == null || otherData == marbleData) continue;
                    if (otherData.state == MarbleState.Destroyed) continue;
                    float impactForce = (rb.velocity - otherData.GetComponent<Rigidbody>().velocity).magnitude;
                    HandleCollision(otherData, impactForce, h.point);
                    break;   // 每步只处理一次命中
                }
            }
            lastSweepPos = cur;
        }

        void OnCollisionEnter(Collision collision)
        {
            Debug.Log($"[COLLISION] {gameObject.name} x {collision.gameObject.name} relV={collision.relativeVelocity.magnitude:F2} layers={gameObject.layer}/{collision.gameObject.layer}");
            // 获取碰撞的弹珠
            MarbleData otherMarble = collision.gameObject.GetComponent<MarbleData>();
            if (otherMarble == null) return;

            // 计算碰撞力度
            float impactForce = collision.relativeVelocity.magnitude;

            // 忽略太轻的碰撞
            if (impactForce < minImpactForce) return;

            // 处理碰撞
            HandleCollision(otherMarble, impactForce, collision.contacts[0].point);
        }
        
        /// <summary>
        /// 处理碰撞
        /// </summary>
        private void HandleCollision(MarbleData otherMarble, float impactForce, Vector3 contactPoint)
        {
            // 已摧毁的弹珠不参与任何计分/摧毁判定
            if (marbleData.state == MarbleState.Destroyed || otherMarble.state == MarbleState.Destroyed) return;

            // 确定攻击者和受害者：刚发射的弹珠（shotSequence 大）=攻击方。
            // 必须用发射戳——回合/state/速度在"地形颠簸刹停+回合切换"下会让两个回调
            // 得出相反结论（实测：射击弹珠被刹停→回合切走→己方弹珠反被敌方"吃掉"）。
            // 双方都没被发射过（seq 相等，互相碰撞）→ 不产生吃子。
            if (marbleData.shotSequence == otherMarble.shotSequence)
            {
                Debug.Log($"[HC] {gameObject.name}: 非发射接触跳过 {marbleData.gameObject.name} x {otherMarble.gameObject.name}");
                return;
            }

            MarbleData attacker, victim;
            if (marbleData.shotSequence > otherMarble.shotSequence)
            {
                attacker = marbleData;
                victim = otherMarble;
            }
            else
            {
                attacker = otherMarble;
                victim = marbleData;
            }

            // 攻击方已停下的旧弹珠蹭到人不产生吃子（发射戳是永久的，只有滚动中的
            // 发射弹珠才算"这一击"）
            if (attacker.state == MarbleState.Idle)
            {
                Debug.Log($"[HC] {gameObject.name}: 攻击方已静止跳过 {attacker.gameObject.name} x {victim.gameObject.name}");
                return;
            }
            
            // 有效攻击归属（连环碰撞：被撞飞的弹珠代表把它撞飞的玩家）
            int attackerId = attacker.GetEffectiveAttackerId();

            // 真友军（同阵营）才忽略。不能用 GetEffectiveAttackerId 判阵营：
            // 双方都会收到碰撞回调，受害方回调先写 lastAttackerId，攻击方回调随后
            // 就会把刚写入的链 ID 误判成"同归属"（实测打中敌方小兵永不消除的根因）
            if (attacker.ownerPlayerId == victim.ownerPlayerId)
            {
                Debug.Log($"[HC] {gameObject.name}: 同阵营忽略 attacker={attacker.gameObject.name}({attacker.ownerPlayerId}) victim={victim.gameObject.name}({victim.ownerPlayerId})");
                return;
            }

            // 传递攻击链：被撞的弹珠如果再撞坏别人的弹珠，仍算本攻击者的功劳
            victim.lastAttackerId = attackerId;

            // 双方弹珠都会收到碰撞回调，仅由攻击方处理一次，避免重复计分
            if (marbleData != attacker) return;
            
            // 播放碰撞特效
            PlayHitEffects(contactPoint, impactForce);
            
            // 通知GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMarbleCollision(attacker, victim, impactForce);
            }
            
            // 检查是否需要销毁
            CheckDestroy(attacker, victim, impactForce);
        }
        
        /// <summary>
        /// 检查是否需要销毁弹珠
        /// </summary>
        private void CheckDestroy(MarbleData attacker, MarbleData victim, float impactForce)
        {
            // 检查是否是炮楼弹珠
            if (victim.marbleType == MarbleType.Tower)
            {
                // 检查是否需要二次打爆
                if (victim.state == MarbleState.Idle)
                {
                    // 第一次被击中
                    if (impactForce >= destroyThreshold)
                        {
                            // 直接摧毁
                            DestroyMarble(victim, impactForce);
                        }
                        else
                        {
                            // 标记为已击中（需要二次打爆）
                            victim.isInvincible = false;
                            // TODO: 显示已击中状态
                        }
                }
                else if (!victim.isInvincible)
                {
                    // 二次打爆
                    if (impactForce >= secondHitThreshold)
                    {
                        DestroyMarble(victim, impactForce);
                    }
                }
            }
            else
            {
                // 小兵弹珠：打中即被吃掉（传统打弹珠规则），无力度门槛——
                // 变 Destroyed 保持滚动，滚停后渐隐消失（MarbleData 负责）
                DestroyMarble(victim, impactForce);
            }
        }
        
        /// <summary>
        /// 摧毁弹珠
        /// </summary>
        private void DestroyMarble(MarbleData marble, float impactForce)
        {
            // 播放摧毁特效
            PlayDestroyEffects(marble.transform.position, impactForce);
            
            // 通知GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMarbleDestroyed?.Invoke(null, marble);
            }
            
            // 标记为销毁
            marble.OnDestroyed();

            // 玻璃弹珠散架：断开整座炮楼的 FixedJoint 并从爆点向外散开
            ScatterTower(marble, impactForce);

            // 移除由 MarbleData.Update 管理：保持物理滚动，滚停后自动销毁
        }

        /// <summary>
        /// 炮楼散架：被打掉一颗后，剩余弹珠的 FixedJoint 全部断开并施加散开冲击
        /// </summary>
        private void ScatterTower(MarbleData destroyed, float impactForce)
        {
            // 仅炮楼弹珠触发散架：小兵被吃不应波及同队炮楼
            if (destroyed.marbleType != MarbleType.Tower) return;
            if (GameManager.Instance == null) return;
            var owner = GameManager.Instance.GetPlayer(destroyed.ownerPlayerId);
            if (owner == null || owner.towerMarbles == null) return;

            Vector3 epicenter = destroyed.transform.position;
            float scatter = Mathf.Clamp(impactForce * 0.12f, 0.8f, 2.5f);

            foreach (var m in owner.towerMarbles)
            {
                if (m == null || m == destroyed || m.state == MarbleState.Destroyed) continue;

                // 断开剩余弹珠之间的关节
                var joint = m.GetComponent<FixedJoint>();
                if (joint != null) Destroy(joint);

                // 从被摧毁点向外弹开（直接赋速度：AddForce 要等下次物理步生效，
                // 期间 pendingCleanup 检查会因速度<0.3 把几乎没动的弹珠原地瞬消）
                var rb = m.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (m.transform.position - epicenter).normalized + Vector3.up * 0.6f;
                    rb.WakeUp();
                    rb.velocity = dir.normalized * scatter;
                }
            }

            // 被摧毁弹珠自身的关节也断开
            var ownJoint = destroyed.GetComponent<FixedJoint>();
            if (ownJoint != null) Destroy(ownJoint);
        }
        
        /// <summary>
        /// 播放命中特效
        /// </summary>
        private void PlayHitEffects(Vector3 position, float force)
        {
            // 粒子特效
            if (hitParticlePrefab != null)
            {
                GameObject particle = Instantiate(hitParticlePrefab, position, Quaternion.identity);
                particle.transform.localScale = Vector3.one * (force / 10f);
                Destroy(particle, 2f);
            }
            
            // 音效：优先走全局 AudioManager，未接入时退回本地音源
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayMarbleHit(Mathf.Clamp01(force / 10f));
            }
            else if (hitSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hitSound, Mathf.Clamp01(force / 10f));
            }
        }
        
        /// <summary>
        /// 播放摧毁特效
        /// </summary>
        private void PlayDestroyEffects(Vector3 position, float force)
        {
            // 粒子特效
            if (hitParticlePrefab != null)
            {
                GameObject particle = Instantiate(hitParticlePrefab, position, Quaternion.identity);
                particle.transform.localScale = Vector3.one * (force / 5f);
                Destroy(particle, 3f);
            }
            
            // 音效：优先走全局 AudioManager，未接入时退回本地音源
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayGlassShatter();
            }
            else if (destroySound != null && audioSource != null)
            {
                audioSource.PlayOneShot(destroySound, 1f);
            }
        }
    }
}
