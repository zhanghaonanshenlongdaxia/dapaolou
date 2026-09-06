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
        [Header("移动")]
        [Tooltip("要弹的弹珠离 AI 超过该距离（米）就不思考，直接跑过去")]
        [SerializeField] private float farMarbleDistance = 3.0f;
        [Tooltip("远距离直奔时的跑步速度（米/秒）")]
        [SerializeField] private float runSpeed = 2.6f;

        private PlayerManager playerManager;
        private FirstPersonController fpsController;
        private ThirdPersonModel thirdPersonModel;
        private bool turnActive = false;

        void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            fpsController = GetComponent<FirstPersonController>();
            thirdPersonModel = GetComponentInChildren<ThirdPersonModel>();
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
        /// 威胁评估：离我方炮楼最近的敌方存活小兵，3m 内视为威胁
        /// </summary>
        private MarbleData FindTowerThreat(PlayerData enemy, PlayerData own)
        {
            if (enemy == null || own == null || own.towerDestroyed) return null;
            MarbleData threat = null;
            float bestDist = float.MaxValue;
            foreach (var s in enemy.soldierMarbles)
            {
                if (s == null || s.state == MarbleState.Destroyed) continue;
                float d = FlatDist(s.transform.position, own.towerCenter);
                if (d < bestDist) { bestDist = d; threat = s; }
            }
            return bestDist < 3f ? threat : null;
        }

        private static float FlatDist(Vector3 a, Vector3 b)
        {
            Vector3 d = a - b; d.y = 0f;
            return d.magnitude;
        }

        /// <summary>
        /// 选弹决策：威胁小兵在 3m 内→用离它最近的我方小兵吃它（需 4.5m 内可直接弹到）；
        /// 否则用离敌方炮楼最近的我方小兵打炮楼
        /// </summary>
        private MarbleData PickFlickMarble(PlayerData own, PlayerData enemy, out Vector3 targetPos, out bool eating)
        {
            targetPos = enemy.towerCenter;
            eating = false;

            MarbleData threat = FindTowerThreat(enemy, own);
            if (threat != null)
            {
                MarbleData counter = null;
                float counterDist = float.MaxValue;
                foreach (var s in own.soldierMarbles)
                {
                    if (s == null || s.state == MarbleState.Destroyed) continue;
                    float d = FlatDist(s.transform.position, threat.transform.position);
                    if (d < counterDist) { counterDist = d; counter = s; }
                }
                if (counter != null && counterDist <= 4.5f)
                {
                    targetPos = threat.transform.position;
                    eating = true;
                    return counter;
                }
            }

            // 默认：离敌方炮楼最近的我方小兵打炮楼
            MarbleData best = null;
            float bestD = float.MaxValue;
            foreach (var s in own.soldierMarbles)
            {
                if (s == null || s.state == MarbleState.Destroyed) continue;
                float d = FlatDist(s.transform.position, enemy.towerCenter);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>
        /// AI 回合流程：决策先行——弹珠远（>3m）不思考直接跑过去；近则踱步观察（3~5轮）→
        /// 走到发射站位（正后方→斜后→侧边候选点）→ 转身半跪 → 发射 → 弹珠特写
        /// </summary>
        private IEnumerator TakeTurn()
        {
            // AI 不需要人类输入控制
            if (fpsController != null) fpsController.enabled = false;

            var gm = GameManager.Instance;
            if (gm == null || gm.GetCurrentPhase() != GamePhase.Playing) yield break;

            var cc = GetComponent<CharacterController>();
            var enemyData = gm.GetPlayer((playerId + 1) % 2);
            var ownData = gm.GetPlayer(playerId);
            if (enemyData == null) yield break;

            // ==== 选弹决策提前：远弹珠直奔，近弹珠才踱步思考 ====
            Vector3 targetPos;
            bool eating;
            var marble = PickFlickMarble(ownData, enemyData, out targetPos, out eating);
            if (marble == null)
            {
                // 无小兵可用时兜底走旧接口（如只剩炮楼弹珠可拆）
                var marbles = gm.GetPlayer(playerId)?.GetAvailableMarbles();
                if (marbles == null || marbles.Count == 0)
                {
                    Debug.Log($"[AI] Player {playerId}: no available marbles, skip");
                    yield break;
                }
                marble = marbles[0];
                targetPos = enemyData.towerCenter;
                eating = false;
            }

            Vector3 aimFlat = targetPos - marble.transform.position;
            aimFlat.y = 0f;
            float distToMarble = FlatDist(transform.position, marble.transform.position);
            bool farMarble = distToMarble > farMarbleDistance;   // 远：不思考直接跑
            float approachSpeed = farMarble ? runSpeed : 1.1f;
            Debug.Log($"[AI] plan: marble={marble.name} dist={distToMarble:F1}m far={farMarble} eat={eating} run={approachSpeed:F1}m/s");

            if (!farMarble)
            {
                // ==== 思考阶段：来回踱步找角度（弹珠在近处才慢悠悠思考） ====
                float safetyCap = thinkDelayRange.y + 8f;
                float elapsed = 0f;
                int observeRounds = Random.Range(3, 6);
                Vector3 moveDir = Vector3.zero;             // 平滑移动方向（走位阶段复用）
                MarbleData threat = FindTowerThreat(enemyData, ownData);   // 首轮观察优先盯威胁

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

                    // 2) 停下转身面对观察目标（首轮盯威胁小兵或自家炮楼，之后炮楼/双方小兵随机）
                    Vector3? target = round == 0 && threat != null
                        ? threat.transform.position
                        : PickObservationTarget(enemyData, ownData, round == 0);
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
            }
            if (!turnActive || gm.GetCurrentPhase() != GamePhase.Playing) yield break;

            // ==== 发射站位：正后方→斜后→侧边候选点；被高障碍挡住走不到就换下一个 ====
            // （矮障碍物 CharacterController 的 stepOffset 会自动踩上去）
            bool inPosition = false;
            Vector3 usedSpot = Vector3.zero;
            Vector3 moveDir2 = Vector3.zero;
            float[] spotAngles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f };
            foreach (var ang in spotAngles)
            {
                if (!turnActive) break;
                Vector3 spotDir = Quaternion.AngleAxis(ang, Vector3.up) * aimFlat.normalized;
                Vector3 spot = marble.transform.position - spotDir * 0.95f;
                float walkT = 0f;
                float maxWalk = farMarble ? 8f : 4f;
                while (FlatDist(transform.position, spot) > 0.2f && walkT < maxWalk && turnActive)
                {
                    Vector3 flatDelta = spot - transform.position;
                    flatDelta.y = 0f;
                    Vector3 desired = flatDelta.normalized;
                    moveDir2 = Vector3.Slerp(moveDir2, desired, Time.deltaTime * 5f).normalized;
                    Vector3 step = moveDir2 * approachSpeed * Time.deltaTime + Physics.gravity * Time.deltaTime;
                    if (cc != null) cc.Move(step);
                    else transform.position += step;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(moveDir2), Time.deltaTime * 6f);
                    walkT += Time.deltaTime;
                    yield return null;
                }
                if (FlatDist(transform.position, spot) <= 0.28f)
                {
                    inPosition = true;
                    usedSpot = spot;
                    Debug.Log($"[AI] spot: angle {ang} reached in {walkT:F1}s");
                    break;
                }
                if (walkT >= maxWalk) Debug.Log($"[AI] spot: angle {ang} BLOCKED, trying next");
                // 走不到（被墙/房子挡住）→ 换下一个角度
            }
            if (!turnActive) yield break;
            if (!inPosition)
            {
                // 全部候选点都到不了（罕见）：仍从原地发射，防止回合卡死
                Debug.Log($"[AI] Player {playerId}: no firing spot reached, firing anyway");
            }

            // 站定后平滑转身正对发射方向，停顿后再弹
            Quaternion aimRot = Quaternion.LookRotation(aimFlat.normalized);
            float faceT = 0f;
            while (Quaternion.Angle(transform.rotation, aimRot) > 2f && faceT < 1f && turnActive)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, aimRot, Time.deltaTime * 6f);
                faceT += Time.deltaTime;
                yield return null;
            }
            // 半蹲蓄力后再弹
            if (thirdPersonModel != null) thirdPersonModel.SetCrouching(true);
            yield return new WaitForSeconds(0.7f);
            if (!turnActive)
            {
                if (thirdPersonModel != null) thirdPersonModel.SetCrouching(false);
                yield break;
            }

            // 吃子要力度足；打炮楼用常规范围
            float power = eating ? Random.Range(0.9f, 1.0f) : Random.Range(powerRange.x, powerRange.y);

            // 统一走 GameManager 接线的发射器（回合流程依赖其事件）
            var shooter = gm.ActiveShooter != null ? gm.ActiveShooter : GetComponent<MarbleShooter>();
            if (shooter == null)
            {
                Debug.LogError("[AI] no shooter available");
                yield break;
            }

            // 瞄准目标（敌方炮楼或威胁小兵）：弹道仰角补偿重力下坠，并额外 +1.5m 距离补偿空气阻力造成的射程衰减
            Vector3 toTarget = targetPos - marble.transform.position;
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

            Debug.Log($"[AI] Player {playerId} flicks {marble.name} -> {(eating ? "吃威胁小兵@" + targetPos.ToString("F1") : "敌方炮楼")} power={power:F2}");
            shooter.FireMarble(marble, dir, power);
            if (thirdPersonModel != null) thirdPersonModel.SetCrouching(false);

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
