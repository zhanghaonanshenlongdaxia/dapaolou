using UnityEngine;

namespace Dapaolou.Player
{
    /// <summary>
    /// 玩家姿态
    /// </summary>
    public enum PlayerStance
    {
        Standing,       // 站立
        Crouching,      // 蹲下
        Prone           // 趴下（可选）
    }

    /// <summary>
    /// 第一人称控制器 - 类CS手感，支持蹲下和弹弹珠动作
    /// </summary>
    public class FirstPersonController : MonoBehaviour
    {
        [Header("移动参数")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float sprintSpeed = 5f;
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float gravity = -15f;
        
        [Header("视角参数")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxLookAngle = 80f;
        [SerializeField] private float minLookAngle = -80f;
        
        [Header("蹲下参数")]
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchingHeight = 1.2f;
        [SerializeField] private float crouchTransitionSpeed = 8f;
        [SerializeField] private LayerMask ceilingCheckLayer;
        
        [Header("手部动画")]
        [SerializeField] private Transform handTransform;          // 手部模型
        [SerializeField] private Transform aimPosition;            // 瞄准位置
        [SerializeField] private Transform restPosition;           // 休息位置
        [SerializeField] private float handMoveSpeed = 5f;
        
        [Header("弹弹珠动画")]
        [SerializeField] private Transform flickStartPos;          // 弹指开始位置
        [SerializeField] private Transform flickEndPos;            // 弹指结束位置
        [SerializeField] private float flickDuration = 0.3f;
        [SerializeField] private AnimationCurve flickCurve;        // 弹指动画曲线
        
        [Header("摄像机")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float cameraBobFrequency = 1.5f;
        [SerializeField] private float cameraBobAmplitude = 0.05f;
        
        // 内部状态
        private CharacterController characterController;
        private Vector3 velocity;
        private float xRotation = 0f;
        private PlayerStance currentStance = PlayerStance.Standing;
        private float currentHeight;
        private bool isFlicking = false;
        private float flickTimer = 0f;
        
        // 摄像机晃动
        private float cameraBobTimer = 0f;
        private Vector3 cameraOriginalPosition;
        
        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
            }
            
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
            
            // 初始化
            currentHeight = standingHeight;
            if (playerCamera != null)
            {
                cameraOriginalPosition = playerCamera.transform.localPosition;
            }
            
            // 锁定鼠标
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // 初始化动画曲线
            if (flickCurve == null || flickCurve.keys.Length == 0)
            {
                flickCurve = new AnimationCurve(
                    new Keyframe(0, 0, 0, 2),
                    new Keyframe(0.5f, 1, 0, 0),
                    new Keyframe(1, 0, -2, 0)
                );
            }
        }
        
        void Update()
        {
            // 设置面板打开时：屏蔽视角/移动，鼠标让给 UI
            if (Dapaolou.UI.SettingsPanel.IsOpen) return;
            HandleMouseLook();
            HandleMovement();
            HandleCrouch();
            HandleHandAnimation();
            HandleCameraBob();
        }
        
        #region 鼠标视角
        
        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            // 垂直旋转（上下看）
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, minLookAngle, maxLookAngle);
            
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }
            
            // 水平旋转（左右转）
            transform.Rotate(Vector3.up * mouseX);
        }
        
        #endregion
        
        #region 移动系统
        
        private void HandleMovement()
        {
            // 获取输入
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            
            // 计算移动方向
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
            
            // 根据姿态调整速度
            float currentSpeed = GetCurrentSpeed();
            
            // 应用移动
            characterController.Move(moveDirection * currentSpeed * Time.deltaTime);
            
            // 重力
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            
            // 跳跃
            if (Input.GetButtonDown("Jump") && characterController.isGrounded)
            {
                velocity.y = jumpForce;
            }
            
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }
        
        private float GetCurrentSpeed()
        {
            switch (currentStance)
            {
                case PlayerStance.Standing:
                    return Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
                case PlayerStance.Crouching:
                    return crouchSpeed;
                default:
                    return walkSpeed;
            }
        }
        
        #endregion
        
        #region 蹲下系统
        
        private void HandleCrouch()
        {
            // 按Ctrl切换蹲下
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
            {
                ToggleCrouch();
            }
        }
        
        private void ToggleCrouch()
        {
            if (currentStance == PlayerStance.Standing)
            {
                // 尝试蹲下
                if (CanCrouch())
                {
                    currentStance = PlayerStance.Crouching;
                }
            }
            else if (currentStance == PlayerStance.Crouching)
            {
                // 尝试站起
                if (CanStand())
                {
                    currentStance = PlayerStance.Standing;
                }
            }
            
            // 通知手抖系统
            NotifyTremorSystem();
        }
        
        private bool CanCrouch()
        {
            // 检查头顶是否有空间
            Vector3 checkPosition = transform.position + Vector3.up * standingHeight;
            float checkRadius = characterController.radius;
            
            return !Physics.CheckSphere(checkPosition, checkRadius, ceilingCheckLayer);
        }
        
        private bool CanStand()
        {
            // 检查站立时头顶是否有空间
            Vector3 checkPosition = transform.position + Vector3.up * crouchingHeight;
            float checkRadius = characterController.radius;
            
            return !Physics.CheckSphere(checkPosition, checkRadius, ceilingCheckLayer);
        }
        
        private void UpdateCrouch()
        {
            float targetHeight = currentStance == PlayerStance.Crouching ? crouchingHeight : standingHeight;
            
            // 平滑过渡高度
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            
            // 更新CharacterController高度
            characterController.height = currentHeight;
            
            // 调整中心点
            Vector3 center = characterController.center;
            center.y = currentHeight / 2f;
            characterController.center = center;
            
            // 调整摄像机位置
            if (playerCamera != null)
            {
                Vector3 cameraPos = playerCamera.transform.localPosition;
                cameraPos.y = currentHeight - 0.2f; // 摄像机略低于头顶
                playerCamera.transform.localPosition = Vector3.Lerp(
                    playerCamera.transform.localPosition,
                    cameraPos,
                    crouchTransitionSpeed * Time.deltaTime
                );
            }
        }
        
        private void NotifyTremorSystem()
        {
            // 通知手抖系统姿态变化
            Dapaolou.Marble.HandTremorSystem tremorSystem = GetComponent<Dapaolou.Marble.HandTremorSystem>();
            if (tremorSystem != null)
            {
                tremorSystem.OnStanceChanged(currentStance == PlayerStance.Crouching);
            }
        }
        
        #endregion
        
        #region 手部动画
        
        private void HandleHandAnimation()
        {
            if (handTransform == null) return;
            
            // 弹弹珠动画
            if (isFlicking)
            {
                UpdateFlickAnimation();
            }
            else
            {
                // 正常手部位置
                UpdateHandPosition();
            }
        }
        
        private void UpdateHandPosition()
        {
            // 根据是否瞄准决定手部位置
            bool isAiming = Input.GetMouseButton(1); // 右键瞄准
            
            Transform targetPosition = isAiming ? aimPosition : restPosition;
            
            if (targetPosition != null)
            {
                handTransform.localPosition = Vector3.Lerp(
                    handTransform.localPosition,
                    targetPosition.localPosition,
                    handMoveSpeed * Time.deltaTime
                );
                
                handTransform.localRotation = Quaternion.Lerp(
                    handTransform.localRotation,
                    targetPosition.localRotation,
                    handMoveSpeed * Time.deltaTime
                );
            }
        }
        
        /// <summary>
        /// 开始弹弹珠动画
        /// </summary>
        public void StartFlickAnimation()
        {
            isFlicking = true;
            flickTimer = 0f;
        }
        
        private void UpdateFlickAnimation()
        {
            flickTimer += Time.deltaTime / flickDuration;
            
            if (flickTimer >= 1f)
            {
                isFlicking = false;
                flickTimer = 0f;
                return;
            }
            
            // 使用动画曲线计算手部位置
            float curveValue = flickCurve.Evaluate(flickTimer);
            
            // 在开始和结束位置之间插值
            Vector3 startPos = flickStartPos != null ? flickStartPos.localPosition : Vector3.zero;
            Vector3 endPos = flickEndPos != null ? flickEndPos.localPosition : Vector3.forward * 0.3f;
            
            handTransform.localPosition = Vector3.Lerp(startPos, endPos, curveValue);
            
            // 添加手腕旋转
            float rotationAngle = curveValue * 45f; // 最大旋转45度
            handTransform.localRotation = Quaternion.Euler(-rotationAngle, 0, 0);
        }
        
        #endregion
        
        #region 摄像机晃动
        
        private void HandleCameraBob()
        {
            if (playerCamera == null) return;
            
            // 移动时摄像机轻微晃动
            bool isMoving = characterController.velocity.magnitude > 0.1f && characterController.isGrounded;
            
            if (isMoving)
            {
                cameraBobTimer += Time.deltaTime * cameraBobFrequency;
                float bobOffset = Mathf.Sin(cameraBobTimer) * cameraBobAmplitude;
                
                Vector3 cameraPos = cameraOriginalPosition;
                cameraPos.y += bobOffset;
                playerCamera.transform.localPosition = cameraPos;
            }
            else
            {
                // 回归原位
                cameraBobTimer = 0f;
                playerCamera.transform.localPosition = Vector3.Lerp(
                    playerCamera.transform.localPosition,
                    cameraOriginalPosition,
                    5f * Time.deltaTime
                );
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 获取当前姿态
        /// </summary>
        public PlayerStance GetCurrentStance()
        {
            return currentStance;
        }
        
        /// <summary>
        /// 是否正在蹲下
        /// </summary>
        public bool IsCrouching()
        {
            return currentStance == PlayerStance.Crouching;
        }
        
        /// <summary>
        /// 是否正在弹弹珠
        /// </summary>
        public bool IsFlicking()
        {
            return isFlicking;
        }
        
        /// <summary>
        /// 设置鼠标灵敏度
        /// </summary>
        public void SetMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = sensitivity;
        }
        
        #endregion
        
        void OnDrawGizmosSelected()
        {
            // 绘制蹲下检测范围
            Gizmos.color = Color.green;
            Vector3 checkPos = transform.position + Vector3.up * standingHeight;
            Gizmos.DrawWireSphere(checkPos, 0.3f);
            
            Gizmos.color = Color.yellow;
            checkPos = transform.position + Vector3.up * crouchingHeight;
            Gizmos.DrawWireSphere(checkPos, 0.3f);
        }
    }
}
