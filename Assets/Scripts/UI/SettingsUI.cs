using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dapaolou.UI
{
    /// <summary>
    /// 设置界面：画质 / 视频 / 操控 / 键盘 / 音乐 / 设定 六个标签页。
    /// 页面为场景中的 Page_* 容器（可直接在 Hierarchy 手动调整布局），
    /// 本脚本负责：标签切换 → 各页行控件接线 → PlayerPrefs 持久化。
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> pages = new Dictionary<string, GameObject>();
        private readonly List<Button> tabButtons = new List<Button>();

        private int qualityIdx;
        private int resIdx;
        private List<(int w, int h)> resDims;

        private Dapaolou.Audio.AudioManager AM => Dapaolou.Audio.AudioManager.Instance;

        void Start()
        {
            ApplySavedGlobal();
            WireTabBar();
            FindPages();
            WirePages();
            ShowTab("画质");
        }

        private void ApplySavedGlobal()
        {
            QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("set_quality", QualitySettings.GetQualityLevel()), true);
            QualitySettings.vSyncCount = PlayerPrefs.GetInt("set_vsync", QualitySettings.vSyncCount);
            var fps = Object.FindObjectOfType<Dapaolou.Player.FirstPersonController>();
            if (fps != null) fps.MouseSensitivity = PlayerPrefs.GetFloat("set_sens", fps.MouseSensitivity);
        }

        private void WireTabBar()
        {
            var bar = transform.Find("Panel/TabBar");
            if (bar == null) return;
            foreach (Transform c in bar) {
                var b = c.GetComponent<Button>();
                if (b == null) continue;
                var tab = c.name.StartsWith("Tab_") ? c.name.Substring(4) : c.name;
                tabButtons.Add(b);
                b.onClick.AddListener(() => ShowTab(tab));
            }
        }

        private void FindPages()
        {
            var content = transform.Find("Panel/Content");
            if (content == null) { Debug.LogError("[SettingsUI] Panel/Content 不存在"); return; }
            foreach (Transform c in content)
                if (c.name.StartsWith("Page_"))
                    pages[c.name.Substring(5)] = c.gameObject;
        }

        private void WirePages()
        {
            // 画质：步进切换质量等级
            if (pages.TryGetValue("画质", out var pq)) {
                var names = QualitySettings.names;
                var val = pq.transform.Find("Value")?.GetComponent<TMP_Text>();
                var prev = pq.transform.Find("Prev")?.GetComponent<Button>();
                var next = pq.transform.Find("Next")?.GetComponent<Button>();
                qualityIdx = PlayerPrefs.GetInt("set_quality", QualitySettings.GetQualityLevel());
                QualitySettings.SetQualityLevel(qualityIdx, true);
                if (val != null) val.text = names[qualityIdx];
                System.Action<int> apply = i => {
                    qualityIdx = i;
                    QualitySettings.SetQualityLevel(i, true);
                    PlayerPrefs.SetInt("set_quality", i);
                    if (val != null) val.text = names[i];
                };
                if (prev != null) prev.onClick.AddListener(() => apply((qualityIdx - 1 + names.Length) % names.Length));
                if (next != null) next.onClick.AddListener(() => apply((qualityIdx + 1) % names.Length));
            }

            // 视频：分辨率步进 + 全屏 + 垂直同步
            if (pages.TryGetValue("视频", out var pv)) {
                resDims = new List<(int w, int h)>();
                var seen = new HashSet<string>();
                var opts = new List<string>();
                foreach (var r in Screen.resolutions) {
                    var key = r.width + "x" + r.height;
                    if (seen.Add(key)) { resDims.Add((r.width, r.height)); opts.Add(key); }
                }
                resIdx = 0;
                for (int i = 0; i < resDims.Count; i++)
                    if (resDims[i].w == Screen.currentResolution.width && resDims[i].h == Screen.currentResolution.height) { resIdx = i; break; }
                var resVal = pv.transform.Find("Res_Value")?.GetComponent<TMP_Text>();
                var resPrev = pv.transform.Find("Res_Prev")?.GetComponent<Button>();
                var resNext = pv.transform.Find("Res_Next")?.GetComponent<Button>();
                if (resVal != null) resVal.text = opts[resIdx];
                System.Action<int> applyRes = i => {
                    resIdx = i;
                    Screen.SetResolution(resDims[i].w, resDims[i].h, Screen.fullScreenMode);
                    if (resVal != null) resVal.text = opts[i];
                };
                if (resPrev != null) resPrev.onClick.AddListener(() => applyRes((resIdx - 1 + resDims.Count) % resDims.Count));
                if (resNext != null) resNext.onClick.AddListener(() => applyRes((resIdx + 1) % resDims.Count));

                var fsTgl = pv.transform.Find("Toggle_全屏")?.GetComponent<Toggle>();
                if (fsTgl != null) {
                    fsTgl.isOn = Screen.fullScreen;
                    fsTgl.onValueChanged.AddListener(on => {
                        var d = resDims[Mathf.Clamp(resIdx, 0, resDims.Count - 1)];
                        Screen.SetResolution(d.w, d.h, on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                    });
                }
                var vsTgl = pv.transform.Find("Toggle_垂直同步")?.GetComponent<Toggle>();
                if (vsTgl != null) {
                    vsTgl.isOn = QualitySettings.vSyncCount > 0;
                    vsTgl.onValueChanged.AddListener(on => QualitySettings.vSyncCount = on ? 1 : 0);
                }
            }

            // 操控：鼠标灵敏度
            if (pages.TryGetValue("操控", out var pc)) {
                var slider = pc.transform.Find("Slider")?.GetComponent<Slider>();
                var fps = Object.FindObjectOfType<Dapaolou.Player.FirstPersonController>();
                if (slider != null && fps != null) {
                    slider.minValue = 0.2f; slider.maxValue = 6f;
                    slider.SetValueWithoutNotify(fps.MouseSensitivity);
                    slider.onValueChanged.AddListener(v => fps.MouseSensitivity = v);
                }
            }
        }

        public void ShowTab(string tabName)
        {
            foreach (var kv in pages) kv.Value.SetActive(kv.Key == tabName);
            foreach (var b in tabButtons) {
                var isOn = b.name == "Tab_" + tabName;
                if (b.image != null) b.image.color = isOn ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.55f, 0.55f, 0.55f);
                var lbl = b.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.color = isOn ? Color.black : Color.white;
            }
        }
    }
}
