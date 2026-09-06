using UnityEngine;
using System.Collections;
using Dapaolou.Marble;
using Dapaolou.Game;

namespace Dapaolou.Player
{
    /// <summary>
    /// 玩家管理器 - 整合第一人称、第三人称、射击系统
    /// </summary>
    public class PlayerManager : MonoBehaviour
    {
        [Header("玩家配置")]
        [SerializeField] private int playerId = 0;
        [SerializeField] private Color playerColor = Color.blue;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private bool isLocalHuman = false;    // 是否人类玩家（AI 玩家为 false）
        
        [Header("组件引用")]
        [SerializeField] private FirstPersonController fpsController;
        [SerializeField] private ThirdPersonModel thirdPersonModel;
        [SerializeField] private MarbleShooter marbleShooter;
        [SerializeField] private HandTremorSystem tremorSystem;
        [SerializeField] private Camera playerCamera;
        
        [Header("摄像机设置")]
        [SerializeField] private bool isFirstPerson = true;
        [SerializeField] private Transform firstPersonCameraPos;
        [SerializeField] private Transform thirdPersonCameraPos;
        
        [Header("手部模型")]
        [SerializeField] private Transform handModel;              // 第一人称手部模型
        [SerializeField] private GameObject marblePrefab;          // 弹珠预制体
        
        // 内部状态
        private bool isMyTurn = false;
        private bool isAiming = false;
        private bool isSpectating = false;
        
        void Awake()
        {
            // 自动获取组件
            if (fpsController == null) fpsController = GetComponent<FirstPersonController>();
            if (thirdPersonModel == null) thirdPersonModel = GetComponentInChildren<ThirdPersonModel>();
            if (marbleShooter == null) marbleShooter = GetComponent<MarbleShooter>();
            if (tremorSystem == null) tremorSystem = GetComponent<HandTremorSystem>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        }
        
        void Start()
        {
            InitializePlayer();
        }
        
        void Update()
        {
            if (!isMyTurn) return;
            
            HandleInput();
            UpdateTremorFactors();
        }
        
        #region 初始化
        
        private void InitializePlayer()
        {
            // 设置玩家颜色
            if (thirdPersonModel != null)
            {
                thirdPersonModel.SetPlayerColor(playerColor);
            }
            
            // 订阅游戏事件
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerTurnStart += OnTurnStart;
                GameManager.Instance.OnPlayerTurnEnd += OnTurnEnd;
                GameManager.Instance.OnMarbleDestroyed += OnMarbleDestroyedReaction;
                GameManager.Instance.OnGameOver += OnGameOverReaction;
            }
            
            // 订阅射击事件
            if (marbleShooter != null)
            {
                marbleShooter.OnMarbleShot += OnMarbleShot;
                marbleShooter.OnStateChanged += OnShootStateChanged;
            }
            
            // 人类玩家默认第一人称视角（身体模型不遮挡视野），Tab 可切换；AI 玩家不使用相机
            if (isLocalHuman)
            {
                SetCameraView(true);
            }
        }
        
        #endregion
        
        #region 输入处理
        
        private void HandleInput()
        {
            // 右键瞄准
            if (Input.GetMouseButtonDown(1))
            {
                StartAiming();
            }
            else if (Input.GetMouseButtonUp(1))
            {
                StopAiming();
            }
            
            // 左键发射（由MarbleShooter处理）
            
            // Tab切换视角
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleCameraView();
            }
            
            // 鼠标滚轮调整力度（备用）
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // 可以用来微调瞄准
            }

            // 屏息：瞄准时按住左 Shift 减少手抖（辅助线更稳）
            if (tremorSystem != null)
            {
                tremorSystem.OnBreathHold(Input.GetKey(KeyCode.LeftShift) && isAiming);
            }
        }
        
        #endregion
        
        #region 瞄准系统
        
        private void StartAiming()
        {
            isAiming = true;
            
            // 切换到第一人称视角
            SetCameraView(true);
            
            // 降低移动速度
            if (fpsController != null)
            {
                // FPS控制器会自动处理
            }
            
            // 通知手抖系统
            if (tremorSystem != null)
            {
                tremorSystem.SetNervousness(0.3f); // 瞄准时略微紧张
            }
        }
        
        private void StopAiming()
        {
            isAiming = false;
            
            // 可以选择保持第一人称或切回第三人称
            // SetCameraView(false);
            
            // 重置手抖
            if (tremorSystem != null)
            {
                tremorSystem.SetNervousness(0f);
            }
        }
        
        #endregion
        
        #region 视角切换
        
        private void SetCameraView(bool firstPerson)
        {
            // AI 玩家不操作相机；其第三人称模型保持可见，供人类玩家观看
            if (!isLocalHuman) return;

            isFirstPerson = firstPerson;
            
            if (playerCamera == null) return;
            
            if (firstPerson)
            {
                // 第一人称视角
                if (firstPersonCameraPos != null)
                {
                    playerCamera.transform.SetParent(firstPersonCameraPos);
                    playerCamera.transform.localPosition = Vector3.zero;
                    playerCamera.transform.localRotation = Quaternion.identity;
                }
                
                // 隐藏第三人称模型
                if (thirdPersonModel != null)
                {
                    SetModelVisible(thirdPersonModel.transform, false);
                }
                
                // 显示手部模型
                if (handModel != null)
                {
                    handModel.gameObject.SetActive(true);
                }
            }
            else
            {
                // 第三人称视角
                if (thirdPersonCameraPos != null)
                {
                    playerCamera.transform.SetParent(thirdPersonCameraPos);
                    playerCamera.transform.localPosition = Vector3.zero;
                    playerCamera.transform.localRotation = Quaternion.identity;
                }
                
                // 显示第三人称模型
                if (thirdPersonModel != null)
                {
                    SetModelVisible(thirdPersonModel.transform, true);
                }
                
                // 隐藏手部模型
                if (handModel != null)
                {
                    handModel.gameObject.SetActive(false);
                }
            }
        }
        
        private void ToggleCameraView()
        {
            SetCameraView(!isFirstPerson);
        }
        
        private void SetModelVisible(Transform model, bool visible)
        {
            if (model == null) return;
            
            // 只对自己隐藏，对其他玩家可见
            // 这里需要网络同步，暂时简单处理
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }

        /// <summary>
        /// 弹珠特写：镜头短暂跟随刚射出的弹珠，随后恢复第一人称
        /// </summary>
        public void PlayShotCloseup(MarbleData marble, float duration = 1.4f)
        {
            if (!isLocalHuman || playerCamera == null || marble == null) return;
            StartCoroutine(ShotCloseupRoutine(marble, duration));
        }

        private IEnumerator ShotCloseupRoutine(MarbleData marble, float duration)
        {
            if (fpsController != null) fpsController.enabled = false;

            var rb = marble.GetComponent<Rigidbody>();
            Vector3 vdir = (rb != null && rb.velocity.sqrMagnitude > 0.01f)
                ? rb.velocity.normalized
                : transform.forward;

            playerCamera.transform.SetParent(null);
            float t = 0f;
            while (t < duration && marble != null)
            {
                Vector3 mp = marble.transform.position;
                playerCamera.transform.position = mp - vdir * 0.9f + Vector3.up * 0.45f;
                playerCamera.transform.LookAt(mp + vdir * 0.6f);
                t += Time.deltaTime;
                yield return null;
            }

            RestoreFirstPersonCamera();
            if (fpsController != null) fpsController.enabled = true;
        }

        /// <summary>
        /// 恢复第一人称机位
        /// </summary>
        private void RestoreFirstPersonCamera()
        {
            if (playerCamera == null) return;
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
            isSpectating = false;
        }
        
        #endregion
        
        #region 手抖系统
        
        private void UpdateTremorFactors()
        {
            if (tremorSystem == null) return;
            
            // 根据游戏状态更新手抖因素
            GamePhase phase = GameManager.Instance?.GetCurrentPhase() ?? GamePhase.Setup;
            
            if (phase == GamePhase.Playing)
            {
                // 游戏进行中，根据情况调整
                if (isAiming)
                {
                    // 瞄准时紧张度增加
                    float timePressure = 1f - (GameManager.Instance?.GetCurrentPlayer()?.shotsThisTurn ?? 0) * 0.1f;
                    tremorSystem.SetNervousness(Mathf.Clamp01(0.3f + (1f - timePressure) * 0.3f));
                }
                
                // 根据地形调整
                Terrain.TerrainType terrainType = GetCurrentTerrain();
                float terrainEffect = GetTerrainTremorEffect(terrainType);
                tremorSystem.SetTerrainEffect(terrainEffect);
            }
        }
        
        private Terrain.TerrainType GetCurrentTerrain()
        {
            // 获取脚下地形类型
            if (Terrain.TerrainEffectSystem.Instance != null)
            {
                return Terrain.TerrainEffectSystem.Instance.GetTerrainType(transform.position);
            }
            return Terrain.TerrainType.Cement;
        }
        
        private float GetTerrainTremorEffect(Terrain.TerrainType terrainType)
        {
            switch (terrainType)
            {
                case Terrain.TerrainType.Cement:
                    return 0f;      // 水泥地最稳定
                case Terrain.TerrainType.Dirt:
                    return 0.2f;    // 泥土地轻微影响
                case Terrain.TerrainType.Grass:
                    return 0.1f;    // 草地影响较小
                case Terrain.TerrainType.LooseSand:
                    return 0.4f;    // 松散土地影响最大
                default:
                    return 0f;
            }
        }
        
        #endregion
        
        #region 回调
        
        /// <summary>
        /// 弹珠被摧毁时的情绪反应：己方炮楼/小兵被打掉 → 沮丧；打掉敌方 → 欢呼
        /// </summary>
        private void OnMarbleDestroyedReaction(MarbleData attacker, MarbleData victim)
        {
            if (attacker == null || victim == null || thirdPersonModel == null) return;
            int attackerId = attacker.GetEffectiveAttackerId();

            if (victim.ownerPlayerId == playerId && attackerId != playerId)
            {
                thirdPersonModel.PlaySad();
            }
            else if (victim.ownerPlayerId != playerId && attackerId == playerId)
            {
                thirdPersonModel.PlayCheer();
            }
        }

        /// <summary>
        /// 游戏结束：赢家欢呼、输家沮丧（人物模型加戏）
        /// </summary>
        private void OnGameOverReaction(int winnerId)
        {
            if (thirdPersonModel == null) return;
            if (winnerId == playerId) thirdPersonModel.PlayCheer(5f);
            else thirdPersonModel.PlaySad(6f);
        }
        
        private void OnTurnStart(int playerIndex)
        {
            if (playerIndex == playerId)
            {
                isMyTurn = true;
                Debug.Log($"Player {playerId}: My turn started!");
                
                // 启用输入
                if (fpsController != null)
                {
                    fpsController.enabled = true;
                }

                // 轮到我：回到第一人称
                if (isLocalHuman)
                {
                    RestoreFirstPersonCamera();
                }
            }
            else
            {
                isMyTurn = false;

                // 人类玩家保持第一人称且可移动走位，自由观看对手发射；AI 玩家无输入
                if (!isLocalHuman && fpsController != null)
                {
                    fpsController.enabled = false;
                }
            }
        }
        
        private void OnTurnEnd(int playerIndex)
        {
            if (playerIndex == playerId)
            {
                isMyTurn = false;
                isAiming = false;
                Debug.Log($"Player {playerId}: My turn ended!");
            }
        }
        
        private void OnMarbleShot(MarbleData marble, Vector3 direction, float force)
        {
            // 播放弹弹珠动画
            if (thirdPersonModel != null)
            {
                thirdPersonModel.StartFlickAnimation();
            }
            
            // 第一人称手部动画
            if (fpsController != null)
            {
                fpsController.StartFlickAnimation();
            }
        }
        
        private void OnShootStateChanged(ShootState newState)
        {
            // 更新第三人称模型状态
            if (thirdPersonModel != null)
            {
                switch (newState)
                {
                    case ShootState.Aiming:
                    case ShootState.Charging:
                        thirdPersonModel.SetState(ThirdPersonModel.AnimatorState.Flicking);
                        break;
                    default:
                        thirdPersonModel.SetState(ThirdPersonModel.AnimatorState.Idle);
                        break;
                }
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取玩家ID
        /// </summary>
        public int GetPlayerId()
        {
            return playerId;
        }
        
        /// <summary>
        /// 设置玩家ID
        /// </summary>
        public void SetPlayerId(int id)
        {
            playerId = id;
        }
        
        /// <summary>
        /// 设置玩家颜色
        /// </summary>
        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            if (thirdPersonModel != null)
            {
                thirdPersonModel.SetPlayerColor(color);
            }
        }

        /// <summary>
        /// 标记是否为人类玩家（AI 玩家为 false）
        /// </summary>
        public void SetLocalHuman(bool human)
        {
            isLocalHuman = human;
        }

        /// <summary>
        /// 是否人类玩家
        /// </summary>
        public bool IsLocalHuman => isLocalHuman;
        
        /// <summary>
        /// 是否是当前回合
        /// </summary>
        public bool IsMyTurn()
        {
            return isMyTurn;
        }
        
        /// <summary>
        /// 是否正在瞄准
        /// </summary>
        public bool IsAiming()
        {
            return isAiming;
        }
        
        #endregion
        
        void OnDestroy()
        {
            // 取消订阅事件
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerTurnStart -= OnTurnStart;
                GameManager.Instance.OnPlayerTurnEnd -= OnTurnEnd;
                GameManager.Instance.OnMarbleDestroyed -= OnMarbleDestroyedReaction;
                GameManager.Instance.OnGameOver -= OnGameOverReaction;
            }
            
            if (marbleShooter != null)
            {
                marbleShooter.OnMarbleShot -= OnMarbleShot;
                marbleShooter.OnStateChanged -= OnShootStateChanged;
            }
        }
    }
}
