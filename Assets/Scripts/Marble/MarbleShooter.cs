using UnityEngine;
using UnityEngine.UI;
using Dapaolou.Game;
using Dapaolou.Player;

namespace Dapaolou.Marble
{
    /// <summary>
    /// 弹射状态
    /// </summary>
    public enum ShootState
    {
        Idle,           // 空闲
        Aiming,         // 瞄准中
        Charging,       // 蓄力中
        Released,       // 已释放，弹珠飞行中
        Waiting         // 等待弹珠停止
    }

    /// <summary>
    /// 弹珠发射器 - 高尔夫式弹射 + 荒野召唤式手抖
    /// </summary>
    public class MarbleShooter : MonoBehaviour
    {
        [Header("弹射参数")]
        [SerializeField] private float maxShootForce = 15f;         // 最大弹射力度
        [SerializeField] private float minShootForce = 2f;          // 最小弹射力度
        [SerializeField] private float chargeSpeed = 1f;            // 蓄力速度
        [SerializeField] private float maxChargeTime = 2f;          // 最大蓄力时间
        [SerializeField] private float aimDistance = 10f;            // 瞄准射线距离
        
        [Header("高尔夫式力度条")]
        [SerializeField] private Slider powerSlider;                // 力度条UI
        [SerializeField] private Image powerFill;                   // 力度条填充
        [SerializeField] private Gradient powerGradient;            // 力度条颜色渐变
        [SerializeField] private float powerBarBounceSpeed = 2f;    // 力度条往返速度
        [SerializeField] private bool useGolfStyleBounce = true;    // 使用高尔夫式往返蓄力
        
        [Header("瞄准系统")]
        [SerializeField] private LineRenderer aimLine;              // 瞄准线
        [SerializeField] private Transform aimTarget;               // 瞄准目标点
        [SerializeField] private LayerMask groundLayer;             // 地面层级
        [SerializeField] private LayerMask marbleLayer;             // 弹珠层级
        [SerializeField] private float aimLineWidth = 0.02f;
        [SerializeField] private Color aimLineColor = Color.white;
        
        [Header("手抖系统")]
        [SerializeField] private HandTremorSystem tremorSystem;     // 手抖系统引用
        [SerializeField] private bool enableTremor = true;          // 是否启用手抖
        
        [Header("UI提示")]
        [SerializeField] private GameObject aimDotPrefab;           // 瞄准点预制体
        [SerializeField] private GameObject tremorIndicator;        // 手抖指示器

        [Header("蓄力仪表盘")]
        [SerializeField] private GameObject powerGauge;             // 蓄力扇形仪表盘（油门盘）
        [SerializeField] private Image powerGaugeFill;              // 蓄力填充扇形

        // 内部状态
        private ShootState currentState = ShootState.Idle;
        private MarbleData currentMarble;                           // 当前要发射的弹珠
        private MarbleData aimedMarble;                             // 准星指向的己方弹珠（瞄准哪个弹哪个）
        private MarbleData highlightedMarble;                       // 当前高亮的弹珠
        private float currentChargeTime = 0f;
        private float currentPower = 0f;                            // 当前力度 (0-1)
        private Vector3 aimDirection;                               // 瞄准方向
        private Vector3 aimHitPoint;                                // 瞄准命中点
        private bool isChargingForward = true;                      // 力度条方向
        
        // 缓存
        private Camera playerCamera;
        private GameObject aimDotInstance;
        
        // 事件
        public System.Action<MarbleData, Vector3, float> OnMarbleShot;  // 弹珠发射事件
        public System.Action<ShootState> OnStateChanged;                 // 状态改变事件
        
        void Awake()
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (tremorSystem == null)
            {
                tremorSystem = GetComponent<HandTremorSystem>();
            }
        }
        
        void Start()
        {
            InitializeAimLine();
            InitializePowerBar();
        }
        
        void Update()
        {
            switch (currentState)
            {
                case ShootState.Idle:
                    UpdateIdle();
                    break;
                case ShootState.Aiming:
                    UpdateAiming();
                    break;
                case ShootState.Charging:
                    UpdateCharging();
                    break;
                case ShootState.Released:
                    UpdateReleased();
                    break;
                case ShootState.Waiting:
                    UpdateWaiting();
                    break;
            }
        }
        
        #region 初始化
        
        private void InitializeAimLine()
        {
            if (aimLine == null)
            {
                GameObject lineObj = new GameObject("AimLine");
                lineObj.transform.SetParent(transform);
                aimLine = lineObj.AddComponent<LineRenderer>();
            }
            
            aimLine.startWidth = aimLineWidth;
            aimLine.endWidth = aimLineWidth;
            aimLine.material = new Material(Shader.Find("Sprites/Default"));
            aimLine.startColor = aimLineColor;
            aimLine.endColor = new Color(aimLineColor.r, aimLineColor.g, aimLineColor.b, 0.3f);
            aimLine.positionCount = 2;
            aimLine.enabled = false;
        }
        
        private void InitializePowerBar()
        {
            if (powerSlider != null)
            {
                powerSlider.minValue = 0f;
                powerSlider.maxValue = 1f;
                powerSlider.value = 0f;
                powerSlider.gameObject.SetActive(false);
            }
            
            // 创建默认颜色渐变
            if (powerGradient == null || powerGradient.colorKeys.Length == 0)
            {
                powerGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(Color.green, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.red, 1f)
                };
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                };
                powerGradient.SetKeys(colorKeys, alphaKeys);
            }
        }
        
        #endregion
        
        #region 状态更新
        
        private void UpdateIdle()
        {
            // 空闲状态，等待玩家选择弹珠
        }
        
        private void UpdateAiming()
        {
            UpdateAimDirection();
            UpdateAimVisuals();
            SelectAimedMarble();
            
            // 按下鼠标左键开始蓄力
            if (Input.GetMouseButtonDown(0))
            {
                StartCharging();
            }
            
            // 右键取消
            if (Input.GetMouseButtonDown(1))
            {
                CancelAiming();
            }
        }

        /// <summary>
        /// 准星选弹：瞄准线指向的己方弹珠即为要弹的弹珠，并点亮高光
        /// </summary>
        private void SelectAimedMarble()
        {
            var gm = GameManager.Instance;
            if (gm == null) { aimedMarble = null; }
            else
            {
                // 仅当前回合玩家的发射器做准星选弹（AI 回合时该发射器归 AI，不做准星高亮）
                var pm = GetComponent<PlayerManager>();
                if (pm != null && pm.GetPlayerId() != gm.GetCurrentPlayerIndex() || playerCamera == null)
                {
                    aimedMarble = null;
                }
                else
                {
                    var player = gm.GetCurrentPlayer();
                    var available = player.GetAvailableMarbles();
                    // 准星粗射线物理判定：SphereCast（半径 6cm）真实碰到哪颗己方弹珠就选哪颗，
                    // 左右/前后天然准确——指到弹珠上才算命中，不受距离/屏幕投影失真影响
                    Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    var hits = Physics.SphereCastAll(ray, 0.06f, 15f);
                    MarbleData best = null;
                    float bestDist = float.MaxValue;
                    foreach (var h in hits)
                    {
                        var md = h.collider.GetComponent<MarbleData>();
                        if (md == null || !available.Contains(md)) continue;
                        if (h.distance < bestDist)
                        {
                            best = md;
                            bestDist = h.distance;
                        }
                    }
                    aimedMarble = best;
                }
            }

            // 高亮切换（瞄准哪个就弹哪个）
            if (highlightedMarble != aimedMarble)
            {
                SetHighlight(highlightedMarble, false);
                SetHighlight(aimedMarble, true);
                highlightedMarble = aimedMarble;
                if (aimedMarble != null) currentMarble = aimedMarble;
            }
        }

        /// <summary>
        /// 弹珠高光开关（URP 自发光）
        /// </summary>
        private void SetHighlight(MarbleData marble, bool on)
        {
            if (marble == null) return;
            var r = marble.GetComponent<Renderer>();
            if (r == null) return;
            var mat = r.material;
            if (on)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 0.9f);
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
        }
        
        private void UpdateCharging()
        {
            // 继续更新瞄准方向（手抖会影响）
            UpdateAimDirection();
            UpdateAimVisuals();
            
            // 高尔夫式蓄力
            if (useGolfStyleBounce)
            {
                UpdateGolfStyleCharging();
            }
            else
            {
                UpdateNormalCharging();
            }
            
            // 更新力度条UI
            UpdatePowerBarUI();
            
            // 松开鼠标发射
            if (Input.GetMouseButtonUp(0))
            {
                ReleaseShot();
            }
        }
        
        /// <summary>
        /// 高尔夫式蓄力 - 力度条往返
        /// </summary>
        private void UpdateGolfStyleCharging()
        {
            if (isChargingForward)
            {
                currentChargeTime += Time.deltaTime * chargeSpeed;
                if (currentChargeTime >= maxChargeTime)
                {
                    currentChargeTime = maxChargeTime;
                    isChargingForward = false;
                }
            }
            else
            {
                currentChargeTime -= Time.deltaTime * chargeSpeed;
                if (currentChargeTime <= 0f)
                {
                    currentChargeTime = 0f;
                    isChargingForward = true;
                }
            }
            
            currentPower = currentChargeTime / maxChargeTime;
        }
        
        /// <summary>
        /// 普通蓄力 - 按住越久力度越大
        /// </summary>
        private void UpdateNormalCharging()
        {
            currentChargeTime += Time.deltaTime * chargeSpeed;
            currentPower = Mathf.Clamp01(currentChargeTime / maxChargeTime);
        }
        
        private void UpdateReleased()
        {
            // 等待弹珠停止滚动
            if (currentMarble != null && currentMarble.IsStationary())
            {
                ChangeState(ShootState.Waiting);
            }
        }
        
        private void UpdateWaiting()
        {
            // 等待一段时间后切换到下一个玩家
            // 这里由GameManager控制
        }
        
        #endregion
        
        #region 瞄准系统
        
        /// <summary>
        /// 更新瞄准方向和命中点
        /// </summary>
        private void UpdateAimDirection()
        {
            if (playerCamera == null) return;
            
            // 从摄像机中心发射射线
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            
            // 射线检测
            int layerMask = groundLayer | marbleLayer;
            
            if (Physics.Raycast(ray, out hit, aimDistance, layerMask))
            {
                aimHitPoint = hit.point;
                aimDirection = (hit.point - GetShootPosition()).normalized;
            }
            else
            {
                aimHitPoint = ray.GetPoint(aimDistance);
                aimDirection = ray.direction;
            }
            
            // 应用手抖效果
            if (enableTremor && tremorSystem != null)
            {
                aimDirection = tremorSystem.GetAimDirectionWithTremor(aimDirection);
            }
        }
        
        /// <summary>
        /// 更新瞄准视觉效果
        /// </summary>
        private void UpdateAimVisuals()
        {
            // 更新瞄准线：射线驱动——从要弹的弹珠出发，指向准星射线落点，贴地延伸、到落点即停
            if (aimLine != null)
            {
                // 起点优先用当前瞄准/选中的弹珠，弹珠从它自己身上出发
                Vector3 startPos = currentMarble != null
                    ? currentMarble.transform.position
                    : GetShootPosition();

                // 方向与长度：弹珠 → 准星射线落点（aimHitPoint 由 UpdateAimDirection 射线检测得出）
                Vector3 toHit = aimHitPoint - startPos;
                Vector3 flatDir = new Vector3(toHit.x, 0f, toHit.z).normalized;
                if (flatDir.sqrMagnitude < 0.0001f)
                    flatDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

                float length = new Vector3(toHit.x, 0f, toHit.z).magnitude;
                aimLine.enabled = length > 0.4f;
                if (!aimLine.enabled) return;

                length = Mathf.Clamp(length, 0.5f, aimDistance);

                // 贴地采样：沿弹珠→落点方向逐步投影到地面
                Vector3 cursor = startPos;
                if (Physics.Raycast(startPos, Vector3.down, out var startHit, 2f, groundLayer))
                    cursor = startHit.point + Vector3.up * 0.03f;

                const int segments = 12;
                aimLine.positionCount = segments + 1;
                float curY = cursor.y;
                for (int i = 0; i <= segments; i++)
                {
                    Vector3 p = cursor + flatDir * (length * i / segments);
                    if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var gh, 3f, groundLayer))
                        curY = gh.point.y + 0.03f;
                    p.y = curY;
                    aimLine.SetPosition(i, p);
                }
            }
            
            // 更新瞄准点
            if (aimTarget != null)
            {
                aimTarget.position = aimHitPoint;
                aimTarget.gameObject.SetActive(true);
            }
        }
        
        #endregion
        
        #region 发射系统
        
        /// <summary>
        /// 开始蓄力
        /// </summary>
        private void StartCharging()
        {
            currentChargeTime = 0f;
            currentPower = 0f;
            isChargingForward = true;
            
            ChangeState(ShootState.Charging);
            
            // 显示力度条与蓄力仪表盘
            if (powerSlider != null)
            {
                powerSlider.gameObject.SetActive(true);
            }
            if (powerGauge != null)
            {
                powerGauge.SetActive(true);
            }
        }
        
        /// <summary>
        /// 释放弹珠
        /// </summary>
        private void ReleaseShot()
        {
            if (currentMarble == null)
            {
                Debug.LogWarning("No marble selected to shoot!");
                return;
            }
            
            // 计算最终力度
            float finalForce = Mathf.Lerp(minShootForce, maxShootForce, currentPower);
            
            // 发射弹珠
            ShootMarble(currentMarble, aimDirection, finalForce);
            
            // 隐藏UI
            HideAimVisuals();
            
            // 触发事件
            OnMarbleShot?.Invoke(currentMarble, aimDirection, finalForce);
            
            // 切换状态
            ChangeState(ShootState.Released);
        }
        
        /// <summary>
        /// 发射弹珠
        /// </summary>
        private void ShootMarble(MarbleData marble, Vector3 direction, float force)
        {
            Rigidbody rb = marble.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                // VelocityChange：力度即出膛速度(m/s)，与手册"力度>5 打爆"判定一致，
                // 避免 Impulse 在轻质量弹珠上产生数百 m/s 的荒谬速度
                rb.AddForce(direction * force, ForceMode.VelocityChange);

                // 添加一点随机旋转
                rb.AddTorque(Random.insideUnitSphere * force * 0.05f, ForceMode.VelocityChange);
            }

            marble.state = MarbleState.Rolling;
        }
        
        /// <summary>
        /// 取消瞄准
        /// </summary>
        private void CancelAiming()
        {
            HideAimVisuals();
            ChangeState(ShootState.Idle);
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 选择要发射的弹珠
        /// </summary>
        public void SelectMarble(MarbleData marble)
        {
            if (marble.state != MarbleState.Idle)
            {
                Debug.LogWarning("Cannot select a moving marble!");
                return;
            }
            
            currentMarble = marble;
            ChangeState(ShootState.Aiming);
        }
        
        /// <summary>
        /// 获取当前状态
        /// </summary>
        public ShootState GetCurrentState()
        {
            return currentState;
        }
        
        /// <summary>
        /// 获取当前选中的弹珠
        /// </summary>
        public MarbleData GetCurrentMarble()
        {
            return currentMarble;
        }
        
        /// <summary>
        /// 获取当前准星指向的己方弹珠（瞄准哪个弹哪个）
        /// </summary>
        public MarbleData GetAimedMarble()
        {
            return aimedMarble;
        }

        /// <summary>
        /// 获取当前力度
        /// </summary>
        public float GetCurrentPower()
        {
            return currentPower;
        }
        
        /// <summary>
        /// 程序化发射（AI/自动化用）：等效完成选弹→释放全流程
        /// </summary>
        public void FireMarble(MarbleData marble, Vector3 direction, float power01)
        {
            if (marble == null || marble.state != MarbleState.Idle)
            {
                Debug.LogWarning("FireMarble: marble unavailable!");
                return;
            }

            currentMarble = marble;
            float finalForce = Mathf.Lerp(minShootForce, maxShootForce, Mathf.Clamp01(power01));
            ShootMarble(marble, direction, finalForce);
            OnMarbleShot?.Invoke(marble, direction, finalForce);
            ChangeState(ShootState.Released);
        }

        /// <summary>
        /// 获取指定力度档位的出膛速度（m/s），供 AI 弹道计算
        /// </summary>
        public float GetLaunchSpeed(float power01)
        {
            return Mathf.Lerp(minShootForce, maxShootForce, Mathf.Clamp01(power01));
        }

        /// <summary>
        /// 重置发射器
        /// </summary>
        public void ResetShooter()
        {
            currentMarble = null;
            currentChargeTime = 0f;
            currentPower = 0f;
            HideAimVisuals();
            ChangeState(ShootState.Idle);
        }
        
        #endregion
        
        #region 辅助方法
        
        private Vector3 GetShootPosition()
        {
            // 弹珠发射位置（玩家手部位置）
            return transform.position + transform.forward * 0.5f + Vector3.down * 0.3f;
        }
        
        private void UpdatePowerBarUI()
        {
            if (powerSlider != null)
            {
                powerSlider.value = currentPower;
            }
            
            if (powerFill != null)
            {
                powerFill.color = powerGradient.Evaluate(currentPower);
            }

            if (powerGaugeFill != null)
            {
                powerGaugeFill.fillAmount = currentPower;
                powerGaugeFill.color = powerGradient.Evaluate(currentPower);
            }
        }
        
        private void HideAimVisuals()
        {
            if (aimLine != null)
            {
                aimLine.enabled = false;
            }
            
            if (aimTarget != null)
            {
                aimTarget.gameObject.SetActive(false);
            }
            
            if (powerSlider != null)
            {
                powerSlider.gameObject.SetActive(false);
            }

            if (powerGauge != null)
            {
                powerGauge.SetActive(false);
            }

            // 清除准星高亮
            SetHighlight(highlightedMarble, false);
            highlightedMarble = null;
            aimedMarble = null;
        }
        
        private void ChangeState(ShootState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(newState);
            Debug.Log($"ShootState changed to: {newState}");
        }
        
        #endregion
        
        #region Gizmos
        
        void OnDrawGizmosSelected()
        {
            // 绘制发射方向
            Gizmos.color = Color.red;
            Gizmos.DrawRay(GetShootPosition(), aimDirection * aimDistance);
            
            // 绘制瞄准点
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(aimHitPoint, 0.1f);
            
            // 绘制力度范围
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Gizmos.DrawWireSphere(GetShootPosition(), maxShootForce * 0.1f);
        }
        
        #endregion
    }
}
