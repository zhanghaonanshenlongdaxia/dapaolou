using UnityEngine;
using UnityEngine.UI;
using Dapaolou.Game;
using Dapaolou.Player;
using QFramework;

namespace Dapaolou.UI
{
    /// <summary>
    /// 布防轮盘面板（QFramework UIKit 版）：
    /// 方舟式圆环轮盘（4 扇区：炮楼/小兵/暗兵/完成布防），鼠标移入高亮、点击选择。
    /// Placement 阶段按 V 开关，选择后驱动 PlacementController 进入对应放置步骤。
    /// </summary>
    public class PlacementWheelPanelData : UIPanelData
    {
    }

    public partial class PlacementWheelPanel : UIPanel
    {
        private enum WheelItem { Tower, Soldier, Ambush, Finish, Count }

        [Header("轮盘参数")]
        [SerializeField] private float radius = 150f;
        [SerializeField] private Color normalColor = new Color(0.12f, 0.12f, 0.15f, 0.88f);
        [SerializeField] private Color hoverColor = new Color(0.3f, 0.6f, 1f, 0.95f);
        [SerializeField] private Color disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.45f);
        [SerializeField] private Color finishColor = new Color(0.2f, 0.7f, 0.35f, 0.95f);

        private bool wheelOpen = false;
        private WheelItem hoverItem = (WheelItem)(-1);
        private readonly System.Collections.Generic.List<(WheelItem item, Image img)> sectors
            = new System.Collections.Generic.List<(WheelItem, Image)>();

        private static readonly string[] ItemNames = { "炮楼", "小兵", "暗兵", "完成布防" };

        #region 生命周期

        protected override void OnInit(IUIData uiData = null)
        {
        }

        protected override void OnShow()
        {
            wheelOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            DisableDuplicateEventSystem();
            RefreshEnabledStates();
        }

        /// <summary>UIRoot 自带 EventSystem 会和场景的冲突，禁用 UIRoot 下的那个</summary>
        private void DisableDuplicateEventSystem()
        {
            var uiRoot = Object.FindObjectOfType<QFramework.UIRoot>();
            if (uiRoot == null) return;
            var es = uiRoot.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true);
            if (es != null && es.gameObject != GameObject.Find("EventSystem"))
                es.gameObject.SetActive(false);
        }

        protected override void OnHide()
        {
            wheelOpen = false;
            hoverItem = (WheelItem)(-1);
            // 恢复 FPS 鼠标锁定（布防仍在进行，走位继续）
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        protected override void OnClose()
        {
            sectors.Clear();
        }

        #endregion

        void Update()
        {
            if (!wheelOpen) return;

            // ESC 关闭
            if (Input.GetKeyDown(KeyCode.Escape)) UIKit.HidePanel<PlacementWheelPanel>();

            UpdateHover();
            HandleClick();
        }

        void LateUpdate()
        {
            // V 键开关（仅布防阶段），由面板自己监听
            if (Input.GetKeyDown(KeyCode.V)
                && GameManager.Instance != null
                && GameManager.Instance.GetCurrentPhase() == GamePhase.Placement)
            {
                Toggle();
            }
        }

        // ===== 面板开关入口（V 键 / 布防控制器调用）=====

        public void Toggle()
        {
            if (State == PanelState.Opening || gameObject.activeSelf)
                UIKit.HidePanel<PlacementWheelPanel>();
            else
                UIKit.ShowPanel<PlacementWheelPanel>();
        }

        // ===== 构建（首次 Show 时由 Awake 已有子物体驱动，此处仅构建一次）=====

        private bool mBuilt = false;

        public void BuildIfNotBuilt(PlacementController controller)
        {
            if (mBuilt) return;
            mBuilt = true;
            BuildSectors(controller);
        }

        private void BuildSectors(PlacementController controller)
        {
            var root = transform.Find("WheelRoot");
            if (root == null) return;

            // 清空旧的动态扇区
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
            sectors.Clear();

            // 扇形背景 + 图标
            Sprite sectorSprite = CreateSectorSprite(160);
            for (int i = 0; i < (int)WheelItem.Count; i++)
            {
                var item = (WheelItem)i;
                float iconAng = (90f - i * (360f / (float)WheelItem.Count)) * Mathf.Deg2Rad;

                // 扇区背景
                var go = new GameObject($"Sector_{ItemNames[i]}", typeof(Image), typeof(RectTransform));
                go.transform.SetParent(root, false);
                var img = go.GetComponent<Image>();
                img.sprite = sectorSprite;
                img.type = Image.Type.Simple;
                img.raycastTarget = false;
                var irt = go.GetComponent<RectTransform>();
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.localPosition = Vector3.zero;
                irt.localRotation = Quaternion.Euler(0f, 0f, -i * (360f / (float)WheelItem.Count));
                irt.sizeDelta = Vector2.one * (radius + 90f) * 2f * (Screen.height / 1080f);

                // 物品图标
                var iconGo = new GameObject($"Icon_{ItemNames[i]}", typeof(Image), typeof(RectTransform));
                iconGo.transform.SetParent(root, false);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = CreateItemIconSprite(item);
                iconImg.raycastTarget = false;
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.localPosition = new Vector3(Mathf.Cos(iconAng) * radius, Mathf.Sin(iconAng) * radius, 0f);
                iconRt.sizeDelta = Vector2.one * 52f * (Screen.height / 1080f);

                sectors.Add((item, img));
            }
        }

        // ===== 鼠标交互 =====

        private void UpdateHover()
        {
            hoverItem = (WheelItem)(-1);
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 offset = (Vector2)Input.mousePosition - center;
            float dist = offset.magnitude;

            float scale = Screen.height / 1080f;
            float r0 = radius * scale - 40f * scale;
            float r1 = (radius + 55f) * scale;
            if (dist < r0 || dist > r1) { SetHoverVisual(); return; }

            float ang = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            float fromTop = (90f - ang + 360f) % 360f;
            float sweep = 360f / (float)WheelItem.Count;
            int idx = Mathf.RoundToInt(fromTop / sweep) % (int)WheelItem.Count;
            hoverItem = (WheelItem)idx;
            SetHoverVisual();
        }

        private void SetHoverVisual()
        {
            foreach (var (item, img) in sectors)
            {
                int remaining = placement != null ? placement.GetRemaining(ToStep(item)) : 0;
                bool disabled = item switch
                {
                    WheelItem.Tower => remaining <= 0,
                    WheelItem.Soldier => remaining <= 0,
                    WheelItem.Ambush => !(placement != null && placement.IsAmbushModeEnabled && remaining > 0),
                    WheelItem.Finish => false,
                    _ => true,
                };
                Color c = disabled ? disabledColor : normalColor;
                if (item == WheelItem.Finish) c = finishColor;
                if (item == hoverItem && !disabled) c = hoverColor;
                img.color = c;
            }
        }

        private void RefreshEnabledStates()
        {
            SetHoverVisual();
        }

        private void HandleClick()
        {
            if (Input.GetMouseButtonDown(0) && hoverItem >= 0 && placement != null)
            {
                switch (hoverItem)
                {
                    case WheelItem.Tower:
                        if (placement.StartStep(ToStep(WheelItem.Tower))) UIKit.HidePanel<PlacementWheelPanel>();
                        break;
                    case WheelItem.Soldier:
                        if (placement.StartStep(ToStep(WheelItem.Soldier))) UIKit.HidePanel<PlacementWheelPanel>();
                        break;
                    case WheelItem.Ambush:
                        if (placement.StartStep(ToStep(WheelItem.Ambush))) UIKit.HidePanel<PlacementWheelPanel>();
                        break;
                    case WheelItem.Finish:
                        if (placement.TryFinish()) UIKit.HidePanel<PlacementWheelPanel>();
                        break;
                }
            }
        }

        private static PlacementController.Step ToStep(WheelItem item)
        {
            return item switch
            {
                WheelItem.Tower => PlacementController.Step.Tower,
                WheelItem.Soldier => PlacementController.Step.Soldier,
                WheelItem.Ambush => PlacementController.Step.Ambush,
                _ => PlacementController.Step.None,
            };
        }

        #region 引用注入（由 PlacementController 或 Bootstrap 设置）

        private PlacementController placement;

        public void SetPlacementController(PlacementController controller)
        {
            placement = controller;
            BuildIfNotBuilt(controller);
        }

        #endregion

        /// <summary>CPU 渲染 120° 圆环扇形贴图（外扩版）</summary>
        private Sprite CreateSectorSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float half = size / 2f;
            float r0 = half * 0.30f, r1 = half * 0.99f;
            float halfSweep = 60f - 3f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half + 0.5f, dy = y - half + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    bool inSweep = Mathf.Abs(Mathf.DeltaAngle(ang, 90f)) <= halfSweep;
                    bool inRing = dist >= r0 && dist <= r1;
                    Color c = Color.clear;
                    if (inRing)
                    {
                        if (inSweep)
                        {
                            c = Color.white;
                            float edge = Mathf.Min((dist - r0), (r1 - dist));
                            if (edge < 1.5f) c.a = edge / 1.5f;
                        }
                        else if (Mathf.Abs(Mathf.DeltaAngle(ang, 90f)) <= halfSweep + 1.5f)
                            c = new Color(1f, 1f, 1f, 0.5f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>CPU 渲染物品图标贴图</summary>
        private Sprite CreateItemIconSprite(WheelItem item)
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float half = size / 2f;

            bool InCircle(float px, float py, float cx, float cy, float r)
            {
                float dx = px - cx, dy = py - cy;
                return dx * dx + dy * dy <= r * r;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    Color c = Color.clear;

                    switch (item)
                    {
                        case WheelItem.Tower:
                        {
                            float r = 11f;
                            if (InCircle(px, py, half, half - 22f, r)) c = Color.white;
                            else if (InCircle(px, py, half - 12f, half - 2f, r)) c = Color.white;
                            else if (InCircle(px, py, half + 12f, half - 2f, r)) c = Color.white;
                            else if (InCircle(px, py, half, half + 16f, r)) c = Color.white;
                            break;
                        }
                        case WheelItem.Soldier:
                        {
                            if (InCircle(px, py, half, half, 16f))
                            {
                                c = Color.white;
                                if (InCircle(px, py, half - 5f, half + 5f, 5f)) c = new Color(1f, 1f, 1f, 0.55f);
                            }
                            break;
                        }
                        case WheelItem.Ambush:
                        {
                            float dx = px - half, dy = py - (half - 6f);
                            if (dx * dx + dy * dy <= 18f * 18f && py >= half - 6f) c = Color.white;
                            break;
                        }
                        case WheelItem.Finish:
                        {
                            if ((Mathf.Abs(px - py - 6f) < 4f && px > half - 16f && px < half + 6f && py > half - 10f)
                                || (Mathf.Abs(px + py - (size + 26f)) < 4f && px > half + 2f && px < half + 26f && py > half - 22f && py < half + 2f))
                                c = Color.white;
                            break;
                        }
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
