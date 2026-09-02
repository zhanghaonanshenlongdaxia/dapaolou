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
            if (playerIndex == playerId) turnActive = false;
        }

        /// <summary>
        /// AI 回合流程：来回走位反复找角度（3~8s）→ 选弹珠 → 确定方向力度 → 发射 → 弹珠特写
        /// </summary>
        private IEnumerator TakeTurn()
        {
            // AI 不需要人类输入控制
            if (fpsController != null) fpsController.enabled = false;

            var gm = GameManager.Instance;
            if (gm == null || gm.GetCurrentPhase() != GamePhase.Playing) yield break;

            // 思考阶段：来回移动踱步、左右张望，模拟反复找角度
            float thinkTime = Random.Range(thinkDelayRange.x, thinkDelayRange.y);
            float elapsed = 0f;
            Vector3 wanderTarget = transform.position;
            var cc = GetComponent<CharacterController>();
            var enemyPreview = gm.GetPlayer((playerId + 1) % 2);
            Vector3 aimPreviewDir = enemyPreview != null
                ? (enemyPreview.towerCenter - transform.position).normalized
                : transform.forward;

            while (elapsed < thinkTime && turnActive && gm.GetCurrentPhase() == GamePhase.Playing)
            {
                // 走位：到达附近随机点后换下一个
                Vector3 flatDelta = wanderTarget - transform.position;
                flatDelta.y = 0f;
                if (flatDelta.magnitude < 0.15f)
                {
                    wanderTarget = transform.position +
                        new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-0.6f, 0.6f));
                }

                if (flatDelta.magnitude > 0.05f)
                {
                    Vector3 step = flatDelta.normalized * 1.0f * Time.deltaTime;
                    if (cc != null) cc.Move(step);
                    else transform.position += step;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(flatDelta.normalized), Time.deltaTime * 4f);
                }
                else
                {
                    // 原地左右张望（反复找角度）
                    float sway = Mathf.Sin(elapsed * 2.5f) * 12f;
                    Vector3 lookDir = Quaternion.AngleAxis(sway, Vector3.up) * aimPreviewDir;
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(lookDir), Time.deltaTime * 5f);
                }

                elapsed += Time.deltaTime;
                yield return null;
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
            var enemy = gm.GetPlayer((playerId + 1) % 2);
            if (enemy == null) yield break;
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
