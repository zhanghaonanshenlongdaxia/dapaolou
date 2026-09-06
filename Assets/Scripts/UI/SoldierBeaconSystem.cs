using System.Collections.Generic;
using UnityEngine;
using Dapaolou.Game;
using Dapaolou.Marble;

namespace Dapaolou.UI
{
    /// <summary>
    /// 小兵光柱定位系统：按住 Ctrl 时，所有存活小兵头顶出现一根射向天空的半透明光柱，
    /// 颜色按阵营区分（蓝=玩家1，红=玩家2），弹珠移动时光柱实时跟随，松开 Ctrl 消失。
    /// 光柱无碰撞体，不干扰弹珠物理与瞄准射线。
    /// </summary>
    public class SoldierBeaconSystem : MonoBehaviour
    {
        [Header("光柱参数")]
        [SerializeField] private float beamHeight = 40f;      // 光柱高度（米）
        [SerializeField] private float beamDiameter = 0.22f;  // 光柱直径（米），太粗会遮挡视线
        [SerializeField] private float beamAlpha = 0.45f;     // 透明度

        private Transform beamRoot;                            // 所有光柱的父节点
        private readonly Dictionary<MarbleData, GameObject> beams = new Dictionary<MarbleData, GameObject>();
        private bool visible = false;

        /// <summary>测试/脚本用：强制显示光柱（绕过 Ctrl 按键）</summary>
        public bool forceVisible { get; set; } = false;

        void Update()
        {
            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            SetBeaconsVisible(ctrlHeld || forceVisible);
        }

        /// <summary>
        /// 显示/隐藏光柱并逐帧跟随小兵。visible=true 时每帧刷新，false 时清空。
        /// </summary>
        public void SetBeaconsVisible(bool show)
        {
            if (show)
            {
                if (GameManager.Instance == null) { HideBeams(); return; }
                EnsureRoot();

                // 当前所有存活小兵
                var seen = new HashSet<MarbleData>();
                foreach (var soldier in Object.FindObjectsOfType<MarbleData>())
                {
                    if (soldier.marbleType != MarbleType.Soldier) continue;
                    if (soldier.state == MarbleState.Destroyed || !soldier.gameObject.activeInHierarchy) continue;
                    seen.Add(soldier);

                    if (!beams.TryGetValue(soldier, out GameObject beam) || beam == null)
                        beam = CreateBeam(soldier);

                    // 跟随：光柱底端贴住弹珠
                    beam.transform.position = soldier.transform.position + Vector3.up * (beamHeight / 2f);
                }

                // 清理已消失小兵的光柱
                var stale = new List<MarbleData>();
                foreach (var kv in beams)
                    if (kv.Key == null || !seen.Contains(kv.Key)) stale.Add(kv.Key);
                foreach (var key in stale)
                {
                    if (beams[key] != null) Destroy(beams[key]);
                    beams.Remove(key);
                }
                visible = true;
            }
            else if (visible)
            {
                HideBeams();
            }
        }

        private void HideBeams()
        {
            foreach (var kv in beams)
                if (kv.Value != null) Destroy(kv.Value);
            beams.Clear();
            if (beamRoot != null) Destroy(beamRoot.gameObject);   // 销毁整个对象而非 Transform
            beamRoot = null;
            visible = false;
        }

        private void EnsureRoot()
        {
            if (beamRoot != null) return;
            beamRoot = new GameObject("SoldierBeacons").transform;
        }

        private GameObject CreateBeam(MarbleData soldier)
        {
            if (beamRoot == null) EnsureRoot();

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var capCol = beam.GetComponent<Collider>();
            if (capCol != null) DestroyImmediate(capCol);    // 光柱不参与物理（立即销毁，避免弹珠撞到自己的光柱）
            beam.name = "Beam_" + soldier.name;
            beam.transform.SetParent(beamRoot);
            beam.transform.localScale = new Vector3(beamDiameter, beamHeight / 2f, beamDiameter);
            beam.layer = soldier.gameObject.layer;

            // 阵营色半透明材质（复用玻璃材质的 URP 透明管线配置）
            var owner = GameManager.Instance != null ? GameManager.Instance.GetPlayer(soldier.ownerPlayerId) : null;
            Color tint = owner != null ? owner.playerColor : Color.white;
            var renderer = beam.GetComponent<Renderer>();
            renderer.material = MarbleData.CreateGlassMaterial(tint);
            var c = renderer.material.GetColor("_BaseColor");
            c.a = beamAlpha;
            renderer.material.SetColor("_BaseColor", c);

            beams[soldier] = beam;
            return beam;
        }
    }
}
