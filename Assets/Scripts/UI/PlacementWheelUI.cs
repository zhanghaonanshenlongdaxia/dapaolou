using UnityEngine;
using UnityEngine.UI;
using Dapaolou.Game;
using Dapaolou.Player;

namespace Dapaolou.UI
{
    /// <summary>
    /// 方舟式轮盘物品栏（布防阶段）：按 V 开关的圆环扇形 UI（屏幕中心），
    /// 鼠标移入扇区高亮、点击选择。扇区：炮楼 / 小兵 / 暗兵（灰显） / 完成布防。
    /// 选择后驱动 PlacementController 进入对应放置步骤。
    /// </summary>
    public class PlacementWheelUI : MonoBehaviour
    {
        private enum WheelItem { Tower, Soldier, Ambush, Finish, Count }

        [Header("引用")]
        [SerializeField] private Canvas parentCanvas;          // GameCanvas（Screen Space Overlay）
        [SerializeField] private PlacementController placement;

        [Header("轮盘参数")]
        [SerializeField] private float radius = 150f;          // 扇区中心半径（像素，1920 基准）
        [SerializeField] private Color normalColor = new Color(0.12f, 0.12f, 0.15f, 0.88f);
        [SerializeField] private Color hoverColor = new Color(0.3f, 0.6f, 1f, 0.95f);
        [SerializeField] private Color disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.45f);
        [SerializeField] private Color finishColor = new Color(0.2f, 0.7f, 0.35f, 0.95f);

        private bool wheelOpen = false;
        private WheelItem hoverItem = (WheelItem)(-1);
        private GameObject wheelRoot;
        private readonly System.Collections.Generic.List<(WheelItem item, Image img)> sectors
            = new System.Collections.Generic.List<(WheelItem, Image)>();
        private readonly System.Collections.Generic.List<(WheelItem item, Image icon)> icons
            = new System.Collections.Generic.List<(WheelItem, Image)>();

        private int myPlayerId = 0;
        private static readonly string[] ItemNames = { "炮楼", "小兵", "暗兵", "完成布防" };

        void Start()
        {
            if (GameManager.Instance == null) return;
            myPlayerId = 0;   // 轮盘只服务人类玩家
            BuildWheel();
            SetOpen(false);
        }

        void Update()
        {
            if (GameManager.Instance == null) return;

            // V 开关（仅布防阶段）
            if (Input.GetKeyDown(KeyCode.V)
                && GameManager.Instance.GetCurrentPhase() == GamePhase.Placement)
            {
                SetOpen(!wheelOpen);
            }
            // ESC 关闭
            if (wheelOpen && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);

            if (!wheelOpen) return;
            UpdateHover();
            HandleClick();
        }

        // ===== 构建 =====

        private void BuildWheel()
        {
            if (parentCanvas == null) return;
            wheelRoot = new GameObject("PlacementWheel", typeof(RectTransform));
            wheelRoot.transform.SetParent(parentCanvas.transform, false);
            var rt = wheelRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // 屏幕中心
            rt.sizeDelta = Vector2.zero;

            // 每个扇区：一张预渲染的 120° 扇环 Sprite（外扩版），旋转到对应角度
            Sprite sectorSprite = CreateSectorSprite(160);
            for (int i = 0; i < (int)WheelItem.Count; i++)
            {
                var item = (WheelItem)i;
                var go = new GameObject($"Wheel_{ItemNames[i]}", typeof(Image), typeof(RectTransform));
                go.transform.SetParent(wheelRoot.transform, false);
                var img = go.GetComponent<Image>();
                img.sprite = sectorSprite;
                img.type = Image.Type.Simple;
                img.raycastTarget = true;   // 自行做鼠标命中，不走 GraphicRaycaster

                // 扇形 Sprite 的几何中心在圆心 → 旋转使每个扇区指向对应角度
                var irt = go.GetComponent<RectTransform>();
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.localPosition = Vector3.zero;
                irt.localRotation = Quaternion.Euler(0f, 0f, -i * (360f / (float)WheelItem.Count));
                // Sprite 直径按 1080p 基准换算：外径顶到 radius+90
                irt.sizeDelta = Vector2.one * (radius + 90f) * 2f * (Screen.height / 1080f);

                sectors.Add((item, img));

                // 物品图标：叠在扇区中心方向、离圆心 radius 处
                var iconGo = new GameObject($"WheelIcon_{ItemNames[i]}", typeof(Image), typeof(RectTransform));
                iconGo.transform.SetParent(wheelRoot.transform, false);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = CreateItemIconSprite(item);
                iconImg.raycastTarget = false;
                float iconAng = (90f - i * (360f / (float)WheelItem.Count)) * Mathf.Deg2Rad;
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.localPosition = new Vector3(Mathf.Cos(iconAng) * radius, Mathf.Sin(iconAng) * radius, 0f);
                iconRt.sizeDelta = Vector2.one * 52f * (Screen.height / 1080f);
                icons.Add((item, iconImg));
            }

            // 中心标签
            var centerGo = new GameObject("WheelCenter", typeof(Text), typeof(RectTransform));
            centerGo.transform.SetParent(wheelRoot.transform, false);
            var ct = centerGo.GetComponent<Text>();
            ct.text = "";
            ct.alignment = TextAnchor.MiddleCenter;
            ct.fontSize = 22;
            ct.color = Color.white;
            var crt = centerGo.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(120, 40);
            crt.localPosition = Vector3.zero;

            wheelRoot.SetActive(false);
        }

        /// <summary>
        /// CPU 渲染一张 120° 圆环扇形贴图（透明背景），pivot 设在圆心。
        /// 外扩：内径降低、外径顶满，扇区间只留 3° 缝隙
        /// </summary>
        private Sprite CreateSectorSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float half = size / 2f;
            float r0 = half * 0.30f, r1 = half * 0.99f;
            float halfSweep = 60f - 3f;   // 留 3° 缝隙（外扩减少重叠感）

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half + 0.5f, dy = y - half + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;   // -180..180，0=右
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
                        {
                            c = new Color(1f, 1f, 1f, 0.5f);
                        }
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);   // pivot=圆心
            sp.name = "WheelSector";
            return sp;
        }

        /// <summary>
        /// CPU 渲染物品图标贴图（96px 透明底）：
        /// tower=三叠圆塔 / soldier=单弹珠+高光 / ambush=土包 / finish=对勾
        /// </summary>
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

            Sprite sp = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            sp.name = $"WheelIcon_{item}";
            return sp;
        }

        // ===== 开关与交互 =====

        private void SetOpen(bool open)
        {
            wheelOpen = open;
            if (wheelRoot != null) wheelRoot.SetActive(open);
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshEnabledStates();
            }
            else
            {
                // 关轮盘恢复 FPS 锁定（GameOver 的解锁在 GameUIManager，不冲突）
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
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

        private void RefreshEnabledStates()
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
                img.color = disabled ? disabledColor : normalColor;
            }
        }

        private void UpdateHover()
        {
            hoverItem = (WheelItem)(-1);
            // 屏幕中心为轮盘圆心（GameCanvas 锚定屏幕中心）
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 offset = (Vector2)Input.mousePosition - center;
            float dist = offset.magnitude;

            // 半径换算：Screen 尺寸与 1920 基准的比例
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
            // 图标联动灰显/高亮
            foreach (var (item, icon) in icons)
            {
                bool disabled = item switch
                {
                    WheelItem.Tower => placement.GetRemaining(PlacementController.Step.Tower) <= 0,
                    WheelItem.Soldier => placement.GetRemaining(PlacementController.Step.Soldier) <= 0,
                    WheelItem.Ambush => !(placement.IsAmbushModeEnabled && placement.GetRemaining(PlacementController.Step.Ambush) > 0),
                    _ => false,
                };
                if (icon != null) icon.color = (item == hoverItem && !disabled) ? Color.white
                    : (disabled ? new Color(1f, 1f, 1f, 0.35f) : Color.white);
            }
        }

        private void HandleClick()
        {
            if (Input.GetMouseButtonDown(0) && hoverItem >= 0 && placement != null)
            {
                switch (hoverItem)
                {
                    case WheelItem.Tower:
                        if (placement.StartStep(ToStep(WheelItem.Tower))) SetOpen(false);
                        break;
                    case WheelItem.Soldier:
                        if (placement.StartStep(ToStep(WheelItem.Soldier))) SetOpen(false);
                        break;
                    case WheelItem.Ambush:
                        if (placement.StartStep(ToStep(WheelItem.Ambush))) SetOpen(false);
                        break;
                    case WheelItem.Finish:
                        if (placement.TryFinish()) SetOpen(false);
                        break;
                }
            }
        }
    }
}
