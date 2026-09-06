using UnityEngine;

namespace Dapaolou.Player
{
    /// <summary>
    /// 第三人称模型 - 其他玩家可见的角色模型
    /// 支持两种模式：骨骼模型（riggedModelRoot，Animator 驱动 Idle/Walk）与简单几何体（程序化动画）
    /// </summary>
    public class ThirdPersonModel : MonoBehaviour
    {
        [Header("身体部件")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        
        [Header("手部")]
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform marbleInHand;      // 手中的弹珠
        
        [Header("动画参数")]
        [SerializeField] private float walkAnimationSpeed = 5f;
        [SerializeField] private float armSwingAngle = 30f;
        [SerializeField] private float legSwingAngle = 25f;
        [SerializeField] private float headBobAmount = 0.05f;
        
        [Header("蹲下动画")]
        [SerializeField] private float crouchHeight = 0.7f;
        [SerializeField] private float crouchTransitionSpeed = 8f;
        
        [Header("弹弹珠动画")]
        [SerializeField] private Transform flickReadyPose;     // 准备弹的姿势
        [SerializeField] private Transform flickReleasePose;   // 弹出的姿势
        [SerializeField] private float flickAnimDuration = 0.4f;
        
        [Header("材质")]
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material headMaterial;
        [SerializeField] private Color playerColor = Color.blue;
        
        [Header("骨骼模型模式")]
        [Tooltip("导入的骨骼动画模型（如 Peasant Nolant）。设置后走 Animator 驱动，不再使用占位几何体")]
        [SerializeField] private GameObject riggedModelRoot;
        [SerializeField] private Animator modelAnimator;
        [Tooltip("根对象移速超过该值（米/秒）视为移动中，自动切换 Idle/Walk")]
        [SerializeField] private float moveSpeedThreshold = 0.25f;
        
        [Header("阵营标识")]
        [Tooltip("头顶常驻上下弹跳的小球，颜色区分阵营（替代身体染色）")]
        [SerializeField] private float markerHeight = 2.05f;
        [SerializeField] private float markerSize = 0.12f;
        [SerializeField] private float markerBounceAmplitude = 0.12f;
        [SerializeField] private float markerBounceSpeed = 3f;
        
        // 内部状态
        private AnimatorState currentState = AnimatorState.Idle;
        private AnimatorState appliedClipState = (AnimatorState)(-1);
        private Vector3 lastRootPosition;
        private float walkCycle = 0f;
        private float currentCrouchAmount = 0f;
        private bool isFlicking = false;
        private float flickTimer = 0f;
        
        // 部件引用
        private Transform[] bodyParts;
        
        // 阵营标识
        private Transform factionMarker;
        private Material markerMaterial;

        // 脚步贴地校准
        private bool footCalibrated = false;

        // 反应姿态（程序化骨骼叠加，LateUpdate 在 Animator 之后可安全覆盖）
        public enum ReactionType { None, Sad, Cheer }
        private ReactionType reactionType = ReactionType.None;
        private float reactionTimer = 0f;
        private float reactionDuration = 0f;
        private float reactionBlend = 0f;
        private bool crouching = false;
        private float crouchBlend = 0f;

        // 骨骼缓存
        private Transform hipsBone, spineBone, headBone;
        private Transform upperArmL, upperArmR, forearmL, forearmR;
        private Transform thighL, thighR, shinL, shinR;
        
        public enum AnimatorState
        {
            Idle,
            Walking,
            Crouching,
            Flicking
        }
        
        /// <summary>是否使用导入的骨骼模型（Animator 驱动）</summary>
        public bool IsRigged => riggedModelRoot != null;
        
        void Awake()
        {
            // 骨骼模型模式：驱动 Animator，不创建占位几何体
            if (IsRigged)
            {
                if (modelAnimator == null)
                {
                    modelAnimator = riggedModelRoot.GetComponentInChildren<Animator>();
                }
                lastRootPosition = GetRootPosition();
            }
            else if (bodyRoot == null)
            {
                // 如果没有手动设置部件，自动创建
                CreateDefaultModel();
            }
            
            // 缓存部件
            bodyParts = new Transform[] { bodyRoot, head, leftArm, rightArm, leftLeg, rightLeg };

            CreateFactionMarker();
        }

        void Update()
        {
            // 阵营标识持续上下弹跳
            if (factionMarker != null)
            {
                float bob = Mathf.Sin(Time.time * markerBounceSpeed) * markerBounceAmplitude;
                factionMarker.localPosition = new Vector3(0f, markerHeight + bob, 0f);
            }

            if (IsRigged)
            {
                if (!footCalibrated && Time.timeSinceLevelLoad > 0.05f) CalibrateFeetToGround();
                UpdateRiggedAnimation();
                return;
            }

            switch (currentState)
            {
                case AnimatorState.Idle:
                    UpdateIdleAnimation();
                    break;
                case AnimatorState.Walking:
                    UpdateWalkAnimation();
                    break;
                case AnimatorState.Crouching:
                    UpdateCrouchAnimation();
                    break;
                case AnimatorState.Flicking:
                    UpdateFlickAnimation();
                    break;
            }
        }
        
        #region 骨骼模型动画
        
        private Vector3 GetRootPosition()
        {
            Transform root = transform.root != null ? transform.root : transform.parent;
            return root != null ? root.position : transform.position;
        }
        
        private void UpdateRiggedAnimation()
        {
            if (currentState == AnimatorState.Flicking)
            {
                // 包内无弹珠动作剪辑：计时结束自动回 Idle（部件引用为空，过程动画自动跳过）
                UpdateFlickAnimation();
                return;
            }
            
            // 根据根对象速度自动切换 Idle/Walk（速度阈值不受帧率影响）
            Vector3 rootPos = GetRootPosition();
            float dt = Time.deltaTime;
            float speed = dt > 0f ? (rootPos - lastRootPosition).magnitude / dt : 0f;
            lastRootPosition = rootPos;
            bool moved = speed > moveSpeedThreshold;
            
            if (moved && currentState != AnimatorState.Walking)
            {
                SetState(AnimatorState.Walking);
            }
            else if (!moved && currentState == AnimatorState.Walking)
            {
                SetState(AnimatorState.Idle);
            }
            
            ApplyRiggedClip();
        }
        
        private void ApplyRiggedClip()
        {
            if (modelAnimator == null || currentState == appliedClipState) return;
            
            string clipState;
            switch (currentState)
            {
                case AnimatorState.Walking:
                    clipState = "metarig|Walk";
                    break;
                default:
                    // Idle/Crouching/Flicking 包内无对应剪辑，统一用 Idle
                    clipState = "metarig|Idle";
                    break;
            }
            
            // 必须用 FixedTime：CrossFade 的时长参数是目标剪辑长度的比例（Idle 3s→0.45s 过渡！），
            // 会让站立时腿部残留走路、起步时先滑步再迈腿
            modelAnimator.CrossFadeInFixedTime(clipState, 0.12f, 0, 0f);
            appliedClipState = currentState;
        }

        /// <summary>
        /// 脚步贴地校准：CharacterController 皮肤宽度会让根对象悬在地面之上，
        /// 用射线取脚下真实地面点，把模型下移让脚骨贴地（仅执行一次）
        /// </summary>
        private void CalibrateFeetToGround()
        {
            footCalibrated = true;
            if (modelAnimator == null) return;

            var footL = modelAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var footR = modelAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (footL == null || footR == null || riggedModelRoot == null) return;

            float footY = Mathf.Min(footL.position.y, footR.position.y);
            Vector3 origin = new Vector3(transform.position.x, footY + 0.5f, transform.position.z);
            float groundY = float.NaN;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 2.5f))
            {
                // 忽略自身胶囊体，取最高的命中点作为脚下地面
                if (hit.collider.transform.root == transform.root) continue;
                if (float.IsNaN(groundY) || hit.point.y > groundY) groundY = hit.point.y;
            }
            if (float.IsNaN(groundY)) return;

            float delta = footY - groundY;
            if (Mathf.Abs(delta) > 0.005f)
            {
                var lp = riggedModelRoot.transform.localPosition;
                riggedModelRoot.transform.localPosition = new Vector3(lp.x, lp.y - delta, lp.z);
            }
        }

        #endregion

        #region 反应姿态叠加（沮丧/欢呼/半蹲）

        /// <summary>沮丧：己方炮楼/小兵被打掉时低头垂肩</summary>
        public void PlaySad(float duration = 2.5f)
        {
            reactionType = ReactionType.Sad;
            reactionTimer = 0f;
            reactionDuration = duration;
        }

        /// <summary>欢呼：打掉敌方时举臂蹦跳</summary>
        public void PlayCheer(float duration = 2f)
        {
            reactionType = ReactionType.Cheer;
            reactionTimer = 0f;
            reactionDuration = duration;
        }

        /// <summary>半蹲蓄力（弹弹珠前）</summary>
        public void SetCrouching(bool on)
        {
            crouching = on;
        }

        private void CacheBones()
        {
            if (hipsBone != null || modelAnimator == null) return;
            hipsBone = modelAnimator.GetBoneTransform(HumanBodyBones.Hips);
            spineBone = modelAnimator.GetBoneTransform(HumanBodyBones.Spine);
            headBone = modelAnimator.GetBoneTransform(HumanBodyBones.Head);
            upperArmL = modelAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            upperArmR = modelAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            forearmL = modelAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            forearmR = modelAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            thighL = modelAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            thighR = modelAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            shinL = modelAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            shinR = modelAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }

        /// <summary>把骨骼当前的肢体朝向往目标方向掰 blend 比例（世界空间，不依赖绑定轴）</summary>
        private void BendLimb(Transform from, Transform to, Vector3 targetDir, float blend)
        {
            if (from == null || to == null || blend <= 0f) return;
            Vector3 cur = to.position - from.position;
            if (cur.sqrMagnitude < 0.000001f) return;
            Vector3 want = Vector3.Slerp(cur.normalized, targetDir.normalized, blend);
            Quaternion delta = Quaternion.FromToRotation(cur.normalized, want);
            from.rotation = delta * from.rotation;
        }

        /// <summary>以角色朝向为基准轴的骨骼旋转（pitch=绕角色右轴点头，yaw=绕上轴，roll=绕前轴），世界轴会随人物朝向歪掉</summary>
        private void RotateBone(Transform bone, float pitchDeg, float yawDeg, float rollDeg, float blend)
        {
            if (bone == null || blend <= 0f) return;
            Transform rootT = transform.root != null ? transform.root : transform;
            Quaternion delta = Quaternion.AngleAxis(pitchDeg * blend, rootT.right)
                             * Quaternion.AngleAxis(yawDeg * blend, rootT.up)
                             * Quaternion.AngleAxis(rollDeg * blend, rootT.forward);
            bone.rotation = delta * bone.rotation;
        }

        void LateUpdate()
        {
            if (!IsRigged || modelAnimator == null) return;
            CacheBones();

            // 半蹲混合
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, Time.deltaTime * 5f);

            // 反应计时与淡入淡出
            if (reactionType != ReactionType.None)
            {
                reactionTimer += Time.deltaTime;
                float fadeIn = Mathf.Clamp01(reactionTimer / 0.2f);
                float fadeOut = Mathf.Clamp01((reactionDuration - reactionTimer) / 0.35f);
                reactionBlend = Mathf.Min(fadeIn, fadeOut);
                if (reactionTimer >= reactionDuration)
                {
                    reactionType = ReactionType.None;
                    reactionBlend = 0f;
                }
            }
            else reactionBlend = Mathf.MoveTowards(reactionBlend, 0f, Time.deltaTime * 4f);

            if (crouchBlend <= 0f && reactionBlend <= 0f) return;

            // ==== 半蹲蓄力 ====
            if (crouchBlend > 0f)
            {
                float b = crouchBlend;
                Vector3 fwd = transform.root != null ? transform.root.forward : Vector3.forward;
                if (hipsBone != null) hipsBone.position += Vector3.down * (0.13f * b);
                // 大腿前抬+小腿后收，保持脚掌大致贴地
                BendLimb(thighL, shinL, fwd * 0.7f + Vector3.down * 0.7f, b);
                BendLimb(thighR, shinR, fwd * 0.7f + Vector3.down * 0.7f, b);
                BendLimb(shinL, modelAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), -fwd * 0.5f + Vector3.down * 0.85f, b);
                BendLimb(shinR, modelAnimator.GetBoneTransform(HumanBodyBones.RightFoot), -fwd * 0.5f + Vector3.down * 0.85f, b);
                RotateBone(spineBone, 14f, 0f, 0f, b);
                RotateBone(headBone, -8f, 0f, 0f, b);
            }

            // ==== 沮丧 ====
            if (reactionType == ReactionType.Sad && reactionBlend > 0f)
            {
                float b = reactionBlend;
                float sway = Mathf.Sin(reactionTimer * 1.5f) * 6f;
                RotateBone(headBone, 30f, sway, 0f, b);
                RotateBone(spineBone, 12f, 0f, 0f, b);
                // 手臂无力前垂
                BendLimb(upperArmL, forearmL, Vector3.down + transform.root.forward * 0.35f, b * 0.5f);
                BendLimb(upperArmR, forearmR, Vector3.down + transform.root.forward * 0.35f, b * 0.5f);
            }

            // ==== 欢呼 ====
            if (reactionType == ReactionType.Cheer && reactionBlend > 0f)
            {
                float b = reactionBlend;
                float hop = Mathf.Abs(Mathf.Sin(reactionTimer * 8f)) * 0.12f;
                if (hipsBone != null) hipsBone.position += Vector3.up * (hop * b);
                Transform rootT = transform.root != null ? transform.root : transform;
                // 举臂过头（沿角色左右略外张）
                BendLimb(upperArmL, forearmL, Vector3.up - rootT.right * 0.25f, b);
                BendLimb(upperArmR, forearmR, Vector3.up + rootT.right * 0.25f, b);
                RotateBone(headBone, -18f, 0f, 0f, b);
                RotateBone(spineBone, -6f, 0f, 0f, b);
            }
        }

        #endregion
        
        #region 阵营标识
        
        /// <summary>
        /// 创建头顶阵营标识锥：尖朝下悬在头顶，无碰撞、无阴影，Unlit 材质保证远处也醒目
        /// </summary>
        private void CreateFactionMarker()
        {
            GameObject marker = new GameObject("FactionMarker");
            marker.AddComponent<MeshFilter>().sharedMesh = CreateConeMesh(0.26f, 0.085f);
            Renderer r = marker.AddComponent<MeshRenderer>();
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, markerHeight, 0f);
            marker.transform.localScale = Vector3.one;
            
            markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            markerMaterial.SetColor("_BaseColor", playerColor);
            // 双面渲染：手搓锥体网格不纠结三角形绕序，正反都可见
            if (markerMaterial.HasProperty("_CullMode")) markerMaterial.SetFloat("_CullMode", 0f);
            r.sharedMaterial = markerMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            
            factionMarker = marker.transform;
        }

        /// <summary>
        /// 程序化锥体网格：尖端在原点(y=0) 朝下，底面朝上
        /// </summary>
        private Mesh CreateConeMesh(float height, float radius)
        {
            const int segments = 14;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            verts.Add(Vector3.zero);                        // 0 = 尖端
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius));
            }
            verts.Add(new Vector3(0f, height, 0f));          // 底面圆心
            int cap = verts.Count - 1;

            for (int i = 1; i <= segments; i++)
            {
                tris.Add(0); tris.Add(i + 1); tris.Add(i);   // 侧面
                tris.Add(cap); tris.Add(i); tris.Add(i + 1); // 底面
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        
        #endregion
        
        #region 模型创建
        
        /// <summary>
        /// 创建默认模型（使用基本几何体）
        /// </summary>
        private void CreateDefaultModel()
        {
            // 身体根节点
            bodyRoot = CreateBodyPart("Body", new Vector3(0, 0.9f, 0), new Vector3(0.4f, 0.6f, 0.25f));
            
            // 头部
            head = CreateBodyPart("Head", new Vector3(0, 1.45f, 0), new Vector3(0.3f, 0.3f, 0.3f), bodyRoot);
            
            // 手臂
            leftArm = CreateBodyPart("LeftArm", new Vector3(-0.35f, 1.1f, 0), new Vector3(0.15f, 0.5f, 0.15f), bodyRoot);
            rightArm = CreateBodyPart("RightArm", new Vector3(0.35f, 1.1f, 0), new Vector3(0.15f, 0.5f, 0.15f), bodyRoot);
            
            // 手部
            rightHand = CreateBodyPart("RightHand", new Vector3(0, -0.3f, 0.1f), new Vector3(0.12f, 0.12f, 0.12f), rightArm);
            
            // 腿部
            leftLeg = CreateBodyPart("LeftLeg", new Vector3(-0.15f, 0.3f, 0), new Vector3(0.18f, 0.6f, 0.18f), bodyRoot);
            rightLeg = CreateBodyPart("RightLeg", new Vector3(0.15f, 0.3f, 0), new Vector3(0.18f, 0.6f, 0.18f), bodyRoot);
            
            // 设置材质
            ApplyMaterial(bodyRoot, bodyMaterial, playerColor);
            ApplyMaterial(head, headMaterial, playerColor * 1.1f); // 头部稍微亮一点
        }
        
        private Transform CreateBodyPart(string name, Vector3 localPosition, Vector3 localScale, Transform parent = null)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            
            if (parent != null)
            {
                part.transform.SetParent(parent, false);
            }
            else
            {
                part.transform.SetParent(transform, false);
            }
            
            // 移除碰撞器（不需要物理碰撞）
            Collider col = part.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
            
            return part.transform;
        }
        
        private void ApplyMaterial(Transform part, Material material, Color color)
        {
            if (part == null) return;
            
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (material != null)
                {
                    renderer.material = material;
                }
                renderer.material.color = color;
            }
            
            // 递归应用到子对象
            foreach (Transform child in part)
            {
                ApplyMaterial(child, material, color);
            }
        }
        
        #endregion
        
        #region 动画更新
        
        private void UpdateIdleAnimation()
        {
            // 轻微呼吸动画
            float breathe = Mathf.Sin(Time.time * 1.5f) * 0.01f;
            if (bodyRoot != null)
            {
                bodyRoot.localScale = new Vector3(0.4f, 0.6f + breathe, 0.25f);
            }
        }
        
        private void UpdateWalkAnimation()
        {
            walkCycle += Time.deltaTime * walkAnimationSpeed;
            
            // 手臂摆动
            if (leftArm != null && rightArm != null)
            {
                float armSwing = Mathf.Sin(walkCycle) * armSwingAngle;
                leftArm.localRotation = Quaternion.Euler(armSwing, 0, 0);
                rightArm.localRotation = Quaternion.Euler(-armSwing, 0, 0);
            }
            
            // 腿部摆动
            if (leftLeg != null && rightLeg != null)
            {
                float legSwing = Mathf.Sin(walkCycle) * legSwingAngle;
                leftLeg.localRotation = Quaternion.Euler(-legSwing, 0, 0);
                rightLeg.localRotation = Quaternion.Euler(legSwing, 0, 0);
            }
            
            // 头部轻微晃动
            if (head != null)
            {
                float headBob = Mathf.Sin(walkCycle * 2) * headBobAmount;
                head.localPosition = new Vector3(0, 1.45f + headBob, 0);
            }
        }
        
        private void UpdateCrouchAnimation()
        {
            // 平滑过渡到蹲下姿势
            currentCrouchAmount = Mathf.Lerp(currentCrouchAmount, 1f, crouchTransitionSpeed * Time.deltaTime);
            
            if (bodyRoot != null)
            {
                // 降低身体高度
                Vector3 bodyPos = bodyRoot.localPosition;
                bodyPos.y = Mathf.Lerp(0.9f, 0.5f, currentCrouchAmount);
                bodyRoot.localPosition = bodyPos;
                
                // 调整身体比例（更扁）
                bodyRoot.localScale = Vector3.Lerp(
                    new Vector3(0.4f, 0.6f, 0.25f),
                    new Vector3(0.45f, 0.4f, 0.3f),
                    currentCrouchAmount
                );
            }
            
            // 腿部弯曲
            if (leftLeg != null && rightLeg != null)
            {
                float bendAngle = Mathf.Lerp(0f, 45f, currentCrouchAmount);
                leftLeg.localRotation = Quaternion.Euler(bendAngle, 0, 0);
                rightLeg.localRotation = Quaternion.Euler(bendAngle, 0, 0);
            }
        }
        
        private void UpdateFlickAnimation()
        {
            flickTimer += Time.deltaTime / flickAnimDuration;
            
            if (flickTimer >= 1f)
            {
                isFlicking = false;
                flickTimer = 0f;
                currentState = AnimatorState.Idle;
                
                // 隐藏手中的弹珠
                if (marbleInHand != null)
                {
                    marbleInHand.gameObject.SetActive(false);
                }
                return;
            }
            
            // 使用动画曲线
            float t = flickTimer;
            
            // 蓄力阶段（0-0.6）
            if (t < 0.6f)
            {
                float prepareT = t / 0.6f;
                UpdateFlickPrepare(prepareT);
            }
            // 释放阶段（0.6-1.0）
            else
            {
                float releaseT = (t - 0.6f) / 0.4f;
                UpdateFlickRelease(releaseT);
            }
        }
        
        private void UpdateFlickPrepare(float t)
        {
            // 向后拉手
            if (rightArm != null)
            {
                float pullBack = Mathf.Lerp(0f, -45f, t);
                rightArm.localRotation = Quaternion.Euler(pullBack, 0, -30f);
            }
            
            // 身体略微后仰
            if (bodyRoot != null)
            {
                bodyRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0f, -5f, t));
            }
        }
        
        private void UpdateFlickRelease(float t)
        {
            // 快速弹出
            if (rightArm != null)
            {
                float flickForward = Mathf.Lerp(-45f, 30f, t * t); // 二次缓动
                rightArm.localRotation = Quaternion.Euler(flickForward, 0, 0);
            }
            
            // 身体回正
            if (bodyRoot != null)
            {
                bodyRoot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-5f, 0f, t));
            }
            
            // 在t=0.5时隐藏弹珠（表示弹出）
            if (t >= 0.5f && marbleInHand != null)
            {
                marbleInHand.gameObject.SetActive(false);
            }
        }
        
        #endregion
        
        #region 公共接口
        
        /// <summary>
        /// 设置动画状态
        /// </summary>
        public void SetState(AnimatorState newState)
        {
            if (currentState == newState) return;
            
            // 骨骼模型模式：状态切换交给 ApplyRiggedClip，无需重置过程动画
            if (IsRigged)
            {
                currentState = newState;
                return;
            }
            
            // 重置上一个状态
            switch (currentState)
            {
                case AnimatorState.Walking:
                    ResetWalkAnimation();
                    break;
                case AnimatorState.Crouching:
                    ResetCrouchAnimation();
                    break;
            }
            
            currentState = newState;
        }
        
        /// <summary>
        /// 开始弹弹珠动画
        /// </summary>
        public void StartFlickAnimation()
        {
            currentState = AnimatorState.Flicking;
            isFlicking = true;
            flickTimer = 0f;
            
            if (IsRigged)
            {
                // 无弹珠剪辑：立即切 Idle 播放，计时结束后由 UpdateFlickAnimation 回 Idle 状态
                appliedClipState = currentState;
                if (modelAnimator != null)
                {
                    modelAnimator.CrossFadeInFixedTime("metarig|Idle", 0.12f, 0, 0f);
                }
                return;
            }
            
            // 显示手中的弹珠
            if (marbleInHand != null)
            {
                marbleInHand.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// 设置玩家颜色
        /// </summary>
        public void SetPlayerColor(Color color)
        {
            playerColor = color;

            // 阵营色渲染到头顶标识上（身体不再按阵营染色）
            if (markerMaterial != null)
            {
                markerMaterial.SetColor("_BaseColor", color);
            }

            // 骨骼模型使用预制体自带配色（Blue/Brown/Green/Yellow），不做运行时重染色
            if (IsRigged) return;

            ApplyMaterial(bodyRoot, bodyMaterial, playerColor);
            ApplyMaterial(head, headMaterial, playerColor * 1.1f);
        }
        
        /// <summary>
        /// 设置手中的弹珠
        /// </summary>
        public void SetMarbleInHand(GameObject marblePrefab)
        {
            if (marbleInHand != null)
            {
                Destroy(marbleInHand.gameObject);
            }
            
            if (marblePrefab != null && rightHand != null)
            {
                GameObject marble = Instantiate(marblePrefab, rightHand);
                marble.transform.localPosition = Vector3.zero;
                marble.transform.localScale = Vector3.one * 0.05f;
                marbleInHand = marble.transform;
                marble.SetActive(false);
            }
        }
        
        #endregion
        
        #region 重置动画
        
        private void ResetWalkAnimation()
        {
            if (leftArm != null) leftArm.localRotation = Quaternion.identity;
            if (rightArm != null) rightArm.localRotation = Quaternion.identity;
            if (leftLeg != null) leftLeg.localRotation = Quaternion.identity;
            if (rightLeg != null) rightLeg.localRotation = Quaternion.identity;
            if (head != null) head.localPosition = new Vector3(0, 1.45f, 0);
        }
        
        private void ResetCrouchAnimation()
        {
            currentCrouchAmount = 0f;
            if (bodyRoot != null)
            {
                bodyRoot.localPosition = new Vector3(0, 0.9f, 0);
                bodyRoot.localScale = new Vector3(0.4f, 0.6f, 0.25f);
            }
        }
        
        #endregion
        
        void OnDrawGizmosSelected()
        {
            // 绘制角色轮廓
            Gizmos.color = playerColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.9f, new Vector3(0.4f, 1.8f, 0.25f));
            
            // 绘制头部
            Gizmos.color = playerColor * 1.1f;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.6f, 0.15f);
        }
    }
}
