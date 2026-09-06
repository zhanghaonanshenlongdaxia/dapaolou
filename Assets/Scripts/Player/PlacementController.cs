using UnityEngine;
using Dapaolou.Game;
using Dapaolou.Marble;

namespace Dapaolou.Player
{
    /// <summary>
    /// 布防控制器（方舟生存进化式放置）：
    /// Placement 阶段启用——预览幻影跟随玩家前方，绿色=合法/红色=非法，按 F 放置。
    /// 顺序：炮楼 → 明兵×3 → 暗兵×3（H 跳过暗兵）。全部完成或跳过后通知 GameManager 开局。
    /// 约束：两家炮楼最小间距；明兵不能离己方炮楼太远；暗兵可离远些但不能进对手地盘。
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Transform aimSource;          // 预览方向的来源（玩家根）
        [SerializeField] private Camera playerCamera;
        [SerializeField] private TMPro.TextMeshProUGUI hintText;  // 复用 HintText（TMP）

        [Header("放置参数")]
        [SerializeField] private float previewDistance = 2.5f; // 幻影在玩家前方距离
        [SerializeField] private float groundY = 0.06f;        // 水泥地面高度
        [SerializeField] private float minTowerDistance = 6f;  // 两家炮楼最小间距
        [SerializeField] private float soldierMaxRadius = 3f;  // 明兵距己方炮楼最远
        [SerializeField] private float ambushMaxRadius = 5f;   // 暗兵距己方炮楼最远
        [SerializeField] private float ambushMinEnemyTowerDist = 6f; // 暗兵距敌方炮楼最小距离
        [SerializeField] private Vector2 placementBounds = new Vector2(10f, 8f);
        [SerializeField] private bool ambushModeEnabled = false;
        [SerializeField] private int ambushCountPerPlayer = 3;

        private enum Step { Tower, Soldier, Ambush, Done }
        private Step step = Step.Tower;
        private int soldierPlaced = 0;
        private int ambushPlaced = 0;

        private GameObject ghost;               // 预览幻影
        private Renderer ghostRenderer;
        private bool ghostLegal = false;

        private PlayerManager playerManager;
        private int myPlayerId = 0;
        private Color myColor = Color.green;

        void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
        }

        void Start()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnPlacementStart += OnPlacementStart;
            // 布防参数从 GameConfig 同步（Inspector 默认仅兜底）
            var cfg = GameManager.Instance.Config;
            if (cfg != null)
            {
                minTowerDistance = cfg.minTowerDistance;
                soldierMaxRadius = cfg.soldierMaxRadius;
                ambushMaxRadius = cfg.ambushMaxRadius;
                ambushMinEnemyTowerDist = cfg.ambushMinEnemyTowerDist;
                placementBounds = cfg.placementBounds;
                ambushModeEnabled = cfg.ambushModeEnabled;
                ambushCountPerPlayer = cfg.ambushCountPerPlayer;
            }
            // 事件可能已错过（InitializeGame 在 GameManager.Start 里先跑）：主动初始化
            if (GameManager.Instance.GetCurrentPhase() == GamePhase.Placement && step == Step.Done)
            {
                OnPlacementStart(0);
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnPlacementStart -= OnPlacementStart;
        }

        private void OnPlacementStart(int firstPlayerId)
        {
            // 只有人类玩家（isLocalHuman）才自己放；AI 玩家由 GameManager 自动布防
            if (playerManager == null || !playerManager.IsLocalHuman) return;
            myPlayerId = playerManager.GetPlayerId();
            var pd = GameManager.Instance.GetPlayer(myPlayerId);
            myColor = pd?.playerColor ?? Color.green;
            step = Step.Tower;
            soldierPlaced = 0;
            ambushPlaced = 0;
            CreateGhost();
            UpdateHint();
        }

        void Update()
        {
            if (step == Step.Done || GameManager.Instance == null
                || GameManager.Instance.GetCurrentPhase() != GamePhase.Placement) return;

            UpdateGhost();
            HandleInput();
        }

        // ===== 幻影 =====

        private void CreateGhost()
        {
            if (ghost != null) Destroy(ghost);
            ghost = GameObject.CreatePrimitive(step == Step.Tower ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
            ghost.name = "PlacementGhost_" + step;
            var col = ghost.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ghostRenderer = ghost.GetComponent<Renderer>();
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            ghostRenderer.sharedMaterial = m;
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private Vector3 GetPreviewPos()
        {
            Transform src = aimSource != null ? aimSource : transform;
            Vector3 fwd = src.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            return src.position + fwd.normalized * previewDistance;
        }

        private void UpdateGhost()
        {
            if (ghost == null) return;
            Vector3 pos = GetPreviewPos();
            pos.y = groundY + (step == Step.Tower ? 0.15f : 0.03f);
            ghost.transform.position = pos;
            ghostLegal = IsPlacementLegal(pos, out string reason);
            lastRejectReason = reason;
            if (ghostRenderer != null)
            {
                var mat = ghostRenderer.sharedMaterial;
                mat.SetColor("_BaseColor", ghostLegal ? new Color(myColor.r, myColor.g, myColor.b, 0.55f)
                                                      : new Color(1f, 0.15f, 0.1f, 0.55f));
            }
            UpdateHint();
        }

        // ===== 输入 =====

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.F)) TryPlace();
            if (Input.GetKeyDown(KeyCode.H) && step == Step.Ambush) SkipAmbush();
        }

        private void TryPlace()
        {
            // 只在布防阶段有效（防外部误用/会话残留）
            if (GameManager.Instance == null
                || GameManager.Instance.GetCurrentPhase() != GamePhase.Placement) return;

            Vector3 pos = GetPreviewPos();
            pos.y = groundY;
            if (!IsPlacementLegal(pos, out string reason))
            {
                SetHint(reason);
                return;   // 非法：F 无效
            }

            var gm = GameManager.Instance;
            var pd = gm.GetPlayer(myPlayerId);
            var towerBuilder = Object.FindObjectOfType<TowerBuilder>();

            if (step == Step.Tower)
            {
                var tower = towerBuilder.BuildTower(pos, myPlayerId, myColor);
                pd.towerMarbles = tower;
                pd.towerCenter = pos;
                foreach (var t in tower) gm.RegisterPlacementMarble(t);
                step = Step.Soldier;
                CreateGhost();
            }
            else if (step == Step.Soldier)
            {
                var soldierObj = gm.CreatePlacementSoldier(pos, myPlayerId, soldierPlaced);
                pd.soldierMarbles.Add(soldierObj.GetComponent<MarbleData>());
                soldierPlaced++;
                if (soldierPlaced >= 3)
                {
                    step = ambushModeEnabled ? Step.Ambush : Step.Done;
                    if (step == Step.Ambush) CreateGhost();
                }
            }
            else if (step == Step.Ambush)
            {
                var soldierObj = gm.CreatePlacementSoldier(pos, myPlayerId, 100 + ambushPlaced);
                var data = soldierObj.GetComponent<MarbleData>();
                data.BuryAsAmbush();
                pd.ambushMarbles.Add(data);
                ambushPlaced++;
                if (ambushPlaced >= ambushCountPerPlayer)
                {
                    step = Step.Done;
                }
            }

            if (step == Step.Done) FinishPlacement();
            else UpdateHint();
        }

        private void SkipAmbush()
        {
            if (step != Step.Ambush) return;
            step = Step.Done;
            FinishPlacement();
        }

        private void FinishPlacement()
        {
            if (ghost != null) Destroy(ghost);
            SetHint("");
            GameManager.Instance.NotifyPlayerPlacementDone(myPlayerId);
        }

        // ===== 约束校验 =====

        private string lastRejectReason = "";

        private bool IsPlacementLegal(Vector3 pos, out string reason)
        {
            reason = "";
            var gm = GameManager.Instance;
            var enemyTower = gm.GetPlayer(myPlayerId == 0 ? 1 : 0)?.towerCenter ?? Vector3.zero;

            if (Mathf.Abs(pos.x) > placementBounds.x || Mathf.Abs(pos.z) > placementBounds.y)
            {
                reason = "超出布防区域！"; return false;
            }

            // 与所有存活弹珠保持间距（炮楼/小兵不能重叠摆放）
            foreach (var m in Object.FindObjectsOfType<MarbleData>())
            {
                if (m.state == MarbleState.Destroyed) continue;
                if (Vector3.Distance(m.transform.position, pos) < 0.25f)
                {
                    reason = "离其他弹珠太近！"; return false;
                }
            }

            if (step == Step.Tower)
            {
                // 两家炮楼最小间距（对手还没放时只查边界）
                if (gm.GetPlayer(myPlayerId == 0 ? 1 : 0).towerMarbles.Count > 0
                    && Vector3.Distance(pos, enemyTower) < minTowerDistance)
                {
                    reason = $"离敌方炮楼太近！至少 {minTowerDistance:F0}m"; return false;
                }
            }
            else if (step == Step.Soldier)
            {
                var ownTower = gm.GetPlayer(myPlayerId).towerCenter;
                if (Vector3.Distance(pos, ownTower) > soldierMaxRadius)
                {
                    reason = "小兵不能离炮楼太远！"; return false;
                }
            }
            else if (step == Step.Ambush)
            {
                var ownTower = gm.GetPlayer(myPlayerId).towerCenter;
                float dOwn = Vector3.Distance(pos, ownTower);
                var enemyPd = gm.GetPlayer(myPlayerId == 0 ? 1 : 0);
                // 敌方还没放炮楼时跳过该距离校验（AI 炮楼落位时会与人类炮楼及所有暗兵保持最小间距）
                if (enemyPd != null && enemyPd.towerMarbles.Count > 0)
                {
                    float dEnemy = Vector3.Distance(pos, enemyPd.towerCenter);
                    if (dEnemy < ambushMinEnemyTowerDist)
                    {
                        reason = "不能埋到对手地盘！"; return false;
                    }
                }
                if (dOwn > ambushMaxRadius)
                {
                    reason = "暗兵不能离炮楼太远！"; return false;
                }
            }
            return true;
        }

        // ===== HUD =====

        private void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        private void UpdateHint()
        {
            string what = step == Step.Tower ? "炮楼" : step == Step.Soldier ? $"小兵 ({soldierPlaced}/3)"
                : $"暗兵 ({ambushPlaced}/{ambushCountPerPlayer})  [H 跳过]";
            string status = ghostLegal ? "按 F 放置" : lastRejectReason;
            SetHint($"布防：放置{what}\n{status}");
        }
    }
}
