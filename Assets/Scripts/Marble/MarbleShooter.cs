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
        [SerializeField] private float maxShootDistance = 1.5f;     // 最大发射距离（玩家与弹珠）

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
        [SerializeField] private TMPro.TextMeshProUGUI distanceHintText;  // 距离提示文本
        [SerializeField] private float hintDisplayTime = 1.5f;      // 提示显示时间
        private string lastHintMessage;                             // 去重日志用
        private Coroutine hideHintCoroutine;                        // 隐藏提示的协程引用

        [Header("蓄力仪表盘")]
        [SerializeField] private GameObject powerGauge;             // 蓄力扇形仪表盘（油门盘）
        [SerializeField] private Image powerGaugeFill;              // 蓄力填充扇形
        [SerializeField] private float pitchAdjustSpeed = 45f;      // 仰角调整速度（度/秒）

        // 内部状态
        private ShootState currentState = ShootState.Idle;
        private MarbleData currentMarble;                           // 当前要发射的弹珠
        private MarbleData aimedMarble;                             // 准星指向的己方弹珠（瞄准哪个弹哪个）
        private MarbleData highlightedMarble;                       // 当前高亮的弹珠
        private float manualSelectTime = -10f;                      // 滚轮手动选择时间戳
        private float currentChargeTime = 0f;
        private float currentPower = 0f;                            // 当前力度 (0-1)
        private float launchPitch = 0f;                             // 起飞仰角（度，0=平射）
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
            // 敌人回合时本发射器不参与瞄准（辅助线/高亮/输入全部关闭）
            if (!IsMyTurnToAim()) return;

            UpdateAimDirection();
            UpdateAimVisuals();
            SelectAimedMarble();

            // 检查距离并显示提示
            if (currentMarble != null)
            {
                float distance = Vector3.Distance(transform.position, currentMarble.transform.position);
                if (distance > maxShootDistance)
                {
                    ShowDistanceHint("距离过远，靠近弹珠！");
                }
                else
                {
                    HideDistanceHint();
                }
            }
            else
            {
                HideDistanceHint();
            }

            // 滚轮上下切换要弹的小兵
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                CycleMarble(scroll > 0f ? 1 : -1);
            }

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
        /// 滚轮循环切换当前要弹的弹珠，并在短时间内抑制准星自动选择
        /// </summary>
        private void CycleMarble(int dir)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var available = gm.GetCurrentPlayer().GetAvailableMarbles();
            if (available == null || available.Count == 0) return;

            int idx = available.IndexOf(currentMarble);
            idx = (idx + dir + available.Count) % available.Count;
            currentMarble = available[idx];
            manualSelectTime = Time.time;

            // 高亮与瞄准目标同步到新弹珠
            aimedMarble = currentMarble;
            SetHighlight(highlightedMarble, false);
            SetHighlight(currentMarble, true);
            highlightedMarble = currentMarble;
            Debug.Log($"[MarbleShooter] 滚轮切换弹珠 -> {currentMarble.name}");
        }

        /// <summary>
        /// 本发射器所属玩家是否当前回合（用于控制辅助线/选弹只在己方回合生效）
        /// </summary>
        private bool IsMyTurnToAim()
        {
            var gm = GameManager.Instance;
            var pm = GetComponent<PlayerManager>();
            return gm != null && pm != null && pm.GetPlayerId() == gm.GetCurrentPlayerIndex();
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
                else if (Time.time - manualSelectTime < 1.2f)
                {
                    // 滚轮手动选择后短暂抑制准星自动选择，避免高亮被抢走
                    aimedMarble = currentMarble;
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
            // 蓄力时 W/S 调整起飞仰角（0~85°，向上抛射过障碍）
            if (Input.GetKey(KeyCode.W))
                launchPitch = Mathf.Min(launchPitch + pitchAdjustSpeed * Time.deltaTime, 85f);
            if (Input.GetKey(KeyCode.S))
                launchPitch = Mathf.Max(launchPitch - pitchAdjustSpeed * Time.deltaTime, 0f);

            // 继续更新瞄准方向（手抖会影响）
            UpdateAimDirection();
            // 应用起飞仰角：水平瞄准方向抬升 launchPitch
            Vector3 flat = new Vector3(aimDirection.x, 0f, aimDirection.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                flat.Normalize();
                aimDirection = (flat + Vector3.up * Mathf.Tan(launchPitch * Mathf.Deg2Rad)).normalized;
            }
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
                // 从弹珠位置指向命中点（不是从手部位置），这样发射方向和辅助线一致
                Vector3 startPos = currentMarble != null ? currentMarble.transform.position : GetShootPosition();
                aimDirection = (hit.point - startPos).normalized;
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
            // 敌人回合/特写镜头期间不显示辅助线：发射器不属于当前回合玩家时直接隐藏
            if (!IsMyTurnToAim())
            {
                if (aimLine != null) aimLine.enabled = false;
                if (aimTarget != null) aimTarget.gameObject.SetActive(false);
                return;
            }

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
            // 检查玩家与弹珠的距离
            if (currentMarble == null)
            {
                ShowDistanceHint("没有选中弹珠！");
                return;
            }

            float distance = Vector3.Distance(transform.position, currentMarble.transform.position);
            if (distance > maxShootDistance)
            {
                ShowDistanceHint("距离过远，靠近弹珠！");
                return;
            }

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
                // isKinematic 赋值会重建 PhysX actor 并丢失 CCD 配对，必须重新指定连续碰撞检测
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.WakeUp();
                // VelocityChange：力度即出膛速度(m/s)，与手册"力度>5 打爆"判定一致，
                // 避免 Impulse 在轻质量弹珠上产生数百 m/s 的荒谬速度
                rb.AddForce(direction * force, ForceMode.VelocityChange);

                // 添加一点随机旋转
                rb.AddTorque(Random.insideUnitSphere * force * 0.05f, ForceMode.VelocityChange);
            }

            marble.state = MarbleState.Rolling;

            // 发射音效（玩家与 AI 共用此路径），力度归一化为 0~1
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayMarbleFlick(
                    Mathf.InverseLerp(minShootForce, maxShootForce, force));
            }
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

            // 清理高亮/辅助线，避免残留到对手回合的特写镜头里
            HideAimVisuals();

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

        /// <summary>
        /// 显示距离提示
        /// </summary>
        private void ShowDistanceHint(string message)
        {
            if (distanceHintText != null)
            {
                distanceHintText.text = message;
                distanceHintText.gameObject.SetActive(true);

                // 停止之前的协程，避免重复
                if (hideHintCoroutine != null)
                {
                    StopCoroutine(hideHintCoroutine);
                }
                // 启动新协程自动隐藏
                hideHintCoroutine = StartCoroutine(HideDistanceHintAfterDelay());
            }
            // 同一条提示只打一次日志（Update 每帧调用，避免刷屏）
            if (lastHintMessage != message)
            {
                lastHintMessage = message;
                Debug.Log($"[MarbleShooter] {message}");
            }
        }

        /// <summary>
        /// 立即隐藏距离提示
        /// </summary>
        private void HideDistanceHint()
        {
            if (hideHintCoroutine != null)
            {
                StopCoroutine(hideHintCoroutine);
                hideHintCoroutine = null;
            }
            if (distanceHintText != null)
            {
                distanceHintText.gameObject.SetActive(false);
            }
        }

        private System.Collections.IEnumerator HideDistanceHintAfterDelay()
        {
            yield return new WaitForSeconds(hintDisplayTime);
            if (distanceHintText != null)
            {
                distanceHintText.gameObject.SetActive(false);
            }
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
                // 三段变色：白（轻）→ 绿（中）→ 红（重）
                if (currentPower < 1f / 3f)
                    powerGaugeFill.color = Color.white;
                else if (currentPower < 2f / 3f)
                    powerGaugeFill.color = Color.green;
                else
                    powerGaugeFill.color = Color.red;
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
