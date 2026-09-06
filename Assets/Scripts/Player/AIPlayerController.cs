using System.Collections;
using UnityEngine;
using Dapaolou.Game;
using Dapaolou.Marble;

namespace Dapaolou.Player
{
    /// <summary>
    /// AI 玩家控制器 - 非玩家角色自动打弹珠
    /// 手册"简单AI"方案：随机瞄准偏移 + 随机力度，优先攻击炮楼
    /// </summary>
    public class AIPlayerController : MonoBehaviour
    {
        [Header("AI 配置")]
        [SerializeField] private int playerId = 1;              // 控制的玩家ID
        [SerializeField] private Vector2 thinkDelayRange = new Vector2(3f, 8f); // 思考/找角度时间范围（秒）
        [SerializeField] private float aimYawError = 1.5f;      // 瞄准横向误差（度）
        [SerializeField] private Vector2 powerRange = new Vector2(0.75f, 1.0f); // 随机力度范围（0-1）

        private PlayerManager playerManager;
        private FirstPersonController fpsController;
        private bool turnActive = false;

        void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            fpsController = GetComponent<FirstPersonController>();
        }

        void Start()
        {
            // 保证 PlayerManager 的 ID 与 AI 配置一致
            if (playerManager != null && playerManager.GetPlayerId() != playerId)
            {
                playerManager.SetPlayerId(playerId);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerTurnStart += OnTurnStart;
                GameManager.Instance.OnPlayerTurnEnd += OnTurnEnd;
            }

            // 开局站定后面向人类玩家，站立朝向有意义
            StartCoroutine(FaceHumanWhenPlaying());
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerTurnStart -= OnTurnStart;
                GameManager.Instance.OnPlayerTurnEnd -= OnTurnEnd;
            }
        }

        private void OnTurnStart(int playerIndex)
        {
            if (playerIndex != playerId) return;
            turnActive = true;
            StartCoroutine(TakeTurn());
        }

        private void OnTurnEnd(int playerIndex)
        {
            if (playerIndex != playerId) return;
            turnActive = false;

            // 回合结束站定后面向人类玩家，站立朝向有意义
            Transform human = FindHumanPlayer();
            if (human != null) StartCoroutine(TurnToFace(human.position));
        }

        /// <summary>
        /// 找到人类玩家（AI 观察与面向的参照）
        /// </summary>
        private Transform FindHumanPlayer()
        {
            foreach (var pm in FindObjectsOfType<PlayerManager>())
            {
                if (pm.IsLocalHuman) return pm.transform;
            }
            return null;
        }

        /// <summary>
        /// 开局等位置落定后，先面向人类玩家站立
        /// </summary>
        private IEnumerator FaceHumanWhenPlaying()
        {
            var gm = GameManager.Instance;
            while (gm == null || gm.GetCurrentPhase() != GamePhase.Playing)
            {
                yield return new WaitForSeconds(0.5f);
                gm = GameManager.Instance;
            }
            yield return new WaitForSeconds(0.3f);

            Transform human = FindHumanPlayer();
            if (human != null)
            {
                Vector3 look = human.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look.normalized);
            }
        }

        /// <summary>
        /// 平滑转身面向目标点
        /// </summary>
        private IEnumerator TurnToFace(Vector3 targetPos)
        {
            Vector3 look = targetPos - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) yield break;

            Quaternion faceRot = Quaternion.LookRotation(look.normalized);
            float t = 0f;
            while (Quaternion.Angle(transform.rotation, faceRot) > 3f && t < 1.5f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, faceRot, Time.deltaTime * 4f);
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 挑选观察目标：首轮优先自家炮楼，之后在双方炮楼与存活小兵间随机
        /// </summary>
        private Vector3? PickObservationTarget(PlayerData enemy, PlayerData own, bool preferOwnTower)
        {
            var candidates = new System.Collections.Generic.List<Vector3>();
            if (own != null && !own.towerDestroyed)
            {
                candidates.Add(own.towerCenter);
            }
            if (preferOwnTower && candidates.Count > 0) return candidates[0];

            if (enemy != null && !enemy.towerDestroyed)
            {
                candidates.Add(enemy.towerCenter);
            }

            if (enemy != null)
            {
                foreach (var s in enemy.soldierMarbles)
                {
                    if (s != null && s.state != MarbleState.Destroyed) candidates.Add(s.transform.position);
                }
            }
            if (own != null)
            {
                foreach (var s in own.soldierMarbles)
                {
                    if (s != null && s.state != MarbleState.Destroyed) candidates.Add(s.transform.position);
                }
            }

            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// 挑一个不踩到炮楼/小兵的踱步落点（周围 0.8~1.5m），找不到就原地观察
        /// </summary>
        private Vector3? PickSafeWalkTarget(Vector3 from, PlayerData own, PlayerData enemy)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 rnd = Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.5f);
                Vector3 target = from + new Vector3(rnd.x, 0f, rnd.y);
                if (IsSpotClear(target, own, enemy)) return target;
            }
            return null;
        }

        /// <summary>
        /// 落点须与所有存活弹珠（炮楼+小兵）保持 0.35m 以上间距
        /// </summary>
        private bool IsSpotClear(Vector3 spot, PlayerData own, PlayerData enemy)
        {
            const float clearance = 0.35f;
            if (own != null)
            {
                foreach (var m in own.towerMarbles)
                    if (m != null && m.state != MarbleState.Destroyed && (m.transform.position - spot).magnitude < clearance) return false;
                foreach (var m in own.soldierMarbles)
                    if (m != null && m.state != MarbleState.Destroyed && (m.transform.position - spot).magnitude < clearance) return false;
            }
            if (enemy != null)
            {
                foreach (var m in enemy.towerMarbles)
                    if (m != null && m.state != MarbleState.Destroyed && (m.transform.position - spot).magnitude < clearance) return false;
                foreach (var m in enemy.soldierMarbles)
                    if (m != null && m.state != MarbleState.Destroyed && (m.transform.position - spot).magnitude < clearance) return false;
            }
            return true;
        }

        /// <summary>
        /// AI 回合流程：来回踱步观察（3~5轮：随机安全方向走一段→停下转身盯炮楼/小兵~1s）→ 选弹珠 → 走到弹珠后方 → 确定方向力度 → 发射 → 弹珠特写
        /// </summary>
        private IEnumerator TakeTurn()
        {
            // AI 不需要人类输入控制
            if (fpsController != null) fpsController.enabled = false;

            var gm = GameManager.Instance;
            if (gm == null || gm.GetCurrentPhase() != GamePhase.Playing) yield break;

            // 思考阶段：来回踱步找角度——不踩炮楼/小兵的方向随机走一段，停下转身面对目标观察
            float safetyCap = thinkDelayRange.y + 8f;   // 走位比纯思考耗时，放宽兜底
            float elapsed = 0f;
            int observeRounds = Random.Range(3, 6);     // 随机看个三五次
            Vector3 moveDir = Vector3.zero;             // 平滑移动方向（走位阶段复用）
            var cc = GetComponent<CharacterController>();
            var enemyData = gm.GetPlayer((playerId + 1) % 2);
            var ownData = gm.GetPlayer(playerId);

            for (int round = 0; round < observeRounds; round++)
            {
                if (!turnActive || gm.GetCurrentPhase() != GamePhase.Playing || elapsed > safetyCap) break;

                // 1) 选一个不踩到炮楼/小兵的方向，走一小段（面向行进方向）
                Vector3? walkTarget = PickSafeWalkTarget(transform.position, ownData, enemyData);
                if (walkTarget.HasValue)
                {
                    float walkT = 0f;
                    while (turnActive && gm.GetCurrentPhase() == GamePhase.Playing && elapsed <= safetyCap)
                    {
                        Vector3 flatDelta = walkTarget.Value - transform.position;
                        flatDelta.y = 0f;
                        if (flatDelta.magnitude <= 0.15f || walkT > 2.5f) break;

                        Vector3 desired = flatDelta.normalized;
                        moveDir = Vector3.Slerp(moveDir, desired, Time.deltaTime * 4f).normalized;
                        Vector3 step = moveDir * 1.1f * Time.deltaTime + Physics.gravity * Time.deltaTime;
                        if (cc != null) cc.Move(step);
                        else transform.position += step;
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(moveDir), Time.deltaTime * 6f);
                        walkT += Time.deltaTime;
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                }

                // 2) 停下转身面对观察目标（首轮优先自家炮楼，之后炮楼/双方小兵随机）
                Vector3? target = PickObservationTarget(enemyData, ownData, round == 0);
                if (target.HasValue)
                {
                    Vector3 lookFlat = target.Value - transform.position;
                    lookFlat.y = 0f;
                    if (lookFlat.sqrMagnitude > 0.01f)
                    {
                        Quaternion faceRot = Quaternion.LookRotation(lookFlat.normalized);
                        while (Quaternion.Angle(transform.rotation, faceRot) > 2f
                               && turnActive && gm.GetCurrentPhase() == GamePhase.Playing
                               && elapsed <= safetyCap)
                        {
                            transform.rotation = Quaternion.Slerp(transform.rotation, faceRot, Time.deltaTime * 5f);
                            elapsed += Time.deltaTime;
                            yield return null;
                        }
                    }

                    // 3) 盯着目标观察约 1 秒（原地待机）
                    float stare = Random.Range(0.8f, 1.2f);
                    while (stare > 0f && turnActive && gm.GetCurrentPhase() == GamePhase.Playing
                           && elapsed <= safetyCap)
                    {
                        stare -= Time.deltaTime;
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                }
            }
            if (!turnActive || gm.GetCurrentPhase() != GamePhase.Playing) yield break;

            // 选择弹珠（优先小兵）
            var marbles = gm.GetPlayer(playerId)?.GetAvailableMarbles();
            if (marbles == null || marbles.Count == 0)
            {
                Debug.Log($"[AI] Player {playerId}: no available marbles, skip");
                yield break;
            }
            var marble = marbles[0];
            var enemy = gm.GetPlayer((playerId + 1) % 2);
            if (enemy == null) yield break;

            // 走到要弹的弹珠正后方蹲点瞄准（弹哪个就停在哪个后面，不乱停）
            Vector3 aimFlat = enemy.towerCenter - marble.transform.position;
            aimFlat.y = 0f;
            Vector3 behindSpot = marble.transform.position - aimFlat.normalized * 0.9f;
            float wt = 0f;
            while ((transform.position - behindSpot).magnitude > 0.12f && wt < 3f && turnActive)
            {
                Vector3 flatDelta = behindSpot - transform.position;
                flatDelta.y = 0f;
                Vector3 desired = flatDelta.normalized;
                moveDir = Vector3.Slerp(moveDir, desired, Time.deltaTime * 4f).normalized;
                Vector3 step = moveDir * 1.1f * Time.deltaTime + Physics.gravity * Time.deltaTime;
                if (cc != null) cc.Move(step);
                else transform.position += step;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(moveDir), Time.deltaTime * 6f);
                wt += Time.deltaTime;
                yield return null;
            }
            // 站定后转身面向瞄准方向
            transform.rotation = Quaternion.LookRotation(aimFlat.normalized);
            yield return new WaitForSeconds(0.4f);
            if (!turnActive) yield break;

            // 随机力度
            float power = Random.Range(powerRange.x, powerRange.y);

            // 统一走 GameManager 接线的发射器（回合流程依赖其事件）
            var shooter = gm.ActiveShooter != null ? gm.ActiveShooter : GetComponent<MarbleShooter>();
            if (shooter == null)
            {
                Debug.LogError("[AI] no shooter available");
                yield break;
            }

            // 瞄准敌方炮楼：弹道仰角补偿重力下坠，并额外 +1.5m 距离补偿空气阻力造成的射程衰减
            Vector3 toTarget = enemy.towerCenter - marble.transform.position;
            float dist = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;
            float speed = shooter.GetLaunchSpeed(power);
            float g = Mathf.Abs(Physics.gravity.y);
            float dEff = dist + 1.5f;   // drag 补偿：真空射程需覆盖到目标后再远 1.5m
            float sinTheta = Mathf.Clamp(g * dEff / (2f * speed * speed), 0f, 0.9f);
            float theta = Mathf.Asin(sinTheta) * Mathf.Rad2Deg;
            Vector3 dir = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            dir.y = Mathf.Tan(theta * Mathf.Deg2Rad);
            dir.Normalize();
            float yawError = Random.Range(-aimYawError, aimYawError);
            dir = Quaternion.AngleAxis(yawError, Vector3.up) * dir;

            Debug.Log($"[AI] Player {playerId} fires {marble.name} power={power:F2}");
            shooter.FireMarble(marble, dir, power);

            // 弹珠特写：让人类玩家的镜头跟随这颗弹珠 3 秒，之后自动恢复第一人称
            foreach (var pm in FindObjectsOfType<PlayerManager>())
            {
                if (pm.IsLocalHuman)
                {
                    pm.PlayShotCloseup(marble, 3f);
                    break;
                }
            }
        }
    }
}
