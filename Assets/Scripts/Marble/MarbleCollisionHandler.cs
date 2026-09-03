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
        
        void Awake()
        {
            marbleData = GetComponent<MarbleData>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        void OnCollisionEnter(Collision collision)
        {
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
            // 确定攻击者和受害者
            MarbleData attacker = null;
            MarbleData victim = null;
            
            // 判断哪个是刚发射的弹珠（正在滚动的）
            if (marbleData.state == MarbleState.Rolling && otherMarble.state == MarbleState.Idle)
            {
                attacker = marbleData;
                victim = otherMarble;
            }
            else if (otherMarble.state == MarbleState.Rolling && marbleData.state == MarbleState.Idle)
            {
                attacker = otherMarble;
                victim = marbleData;
            }
            else
            {
                // 两个都在滚动，根据速度判断
                float mySpeed = marbleData.GetCurrentSpeed();
                float otherSpeed = otherMarble.GetCurrentSpeed();
                
                if (mySpeed > otherSpeed)
                {
                    attacker = marbleData;
                    victim = otherMarble;
                }
                else
                {
                    attacker = otherMarble;
                    victim = marbleData;
                }
            }
            
            // 有效攻击归属（连环碰撞：被撞飞的弹珠代表把它撞飞的玩家）
            int attackerId = attacker.GetEffectiveAttackerId();

            // 归属相同（自己人，或被自己弹珠撞飞的弹珠弹回）→ 忽略，避免误毁己方弹珠
            if (attackerId == victim.GetEffectiveAttackerId()) return;

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
                // 小兵弹珠，直接检查力度
                if (impactForce >= destroyThreshold)
                {
                    DestroyMarble(victim, impactForce);
                }
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

            // 延迟销毁对象
            Destroy(marble.gameObject, 0.5f);
        }

        /// <summary>
        /// 炮楼散架：被打掉一颗后，剩余弹珠的 FixedJoint 全部断开并施加散开冲击
        /// </summary>
        private void ScatterTower(MarbleData destroyed, float impactForce)
        {
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

                // 从被摧毁点向外弹开
                var rb = m.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (m.transform.position - epicenter).normalized + Vector3.up * 0.6f;
                    rb.AddForce(dir.normalized * scatter, ForceMode.VelocityChange);
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
            
            // 音效
            if (hitSound != null && audioSource != null)
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
            
            // 音效
            if (destroySound != null && audioSource != null)
            {
                audioSource.PlayOneShot(destroySound, 1f);
            }
        }
    }
}
