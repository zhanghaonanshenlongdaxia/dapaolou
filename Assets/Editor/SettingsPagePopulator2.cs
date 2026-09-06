using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SettingsPagePopulatorV3
{
    private static TMP_FontAsset _font;

    [MenuItem("Tools/Populate Settings Pages V3")]
    public static void Populate()
    {
        var canvas = GameObject.Find("GameCanvas").transform;
        var rootT = canvas.Find("SettingsRoot");
        if (rootT == null) { Debug.LogError("[PopV3] SettingsRoot missing"); return; }
        var panelT = rootT.Find("Panel");
        if (panelT == null) { Debug.LogError("[PopV3] Panel missing"); return; }
        var contentT = panelT.Find("Content");
        if (contentT == null) { Debug.LogError("[PopV3] Content missing"); return; }

        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset");

        // 补建缺失的页面容器
        EnsurePage(contentT, "Page_画质");
        EnsurePage(contentT, "Page_视频");
        EnsurePage(contentT, "Page_操控");
        EnsurePage(contentT, "Page_键盘");
        EnsurePage(contentT, "Page_音乐");
        EnsurePage(contentT, "Page_设定");

        // 键盘页文字
        var pk = contentT.Find("Page_键盘");
        if (pk.Find("Keys") == null) {
            MakeText(pk, "Keys", "W A S D —— 移动\n空格 —— 跳跃\n左 Shift —— 疾跑（瞄准时屏息）\n左 Ctrl / C —— 蹲下\n鼠标左键 —— 蓄力 / 发射\n滚轮 —— 调整仰角\nTab —— 切换视角\n按住左 Ctrl —— 阵营光柱\nE —— NPC 交互\n1 / 2 / 3 —— 猜拳\nY / N —— 任务对话", 17, TextAlignmentOptions.Left, Color.white, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
        }

        // 设定页按钮
        var ps = contentT.Find("Page_设定");
        if (ps.Find("Btn_Restart") == null) {
            MakeButton(ps, "Btn_Restart", "重新开始本局", new Vector2(0.25f, 0.62f), new Vector2(0.75f, 0.82f), 18);
            MakeButton(ps, "Btn_Quit", "退出游戏", new Vector2(0.25f, 0.32f), new Vector2(0.75f, 0.52f), 18);
        }

        // 音乐挂件（MusicController 组件 + 子控件）
        var pm = contentT.Find("Page_音乐");
        if (pm != null && pm.Find("MusicController") == null) {
            var wg = new GameObject("MusicController", typeof(RectTransform));
            wg.transform.SetParent(pm, false);
            var wgRt = wg.GetComponent<RectTransform>();
            wgRt.anchorMin = new Vector2(0, 0.15f); wgRt.anchorMax = new Vector2(1, 0.85f);
            wgRt.offsetMin = Vector2.zero; wgRt.offsetMax = Vector2.zero;

            var trackText = MakeText(wg.transform, "TrackText", "...", 15, TextAlignmentOptions.Left, Color.white, new Vector2(0, 0.55f), new Vector2(0.55f, 1f));

            var prevB = MakeButton(wg.transform, "PrevButton", "上一首", new Vector2(0.58f, 0.05f), new Vector2(0.72f, 0.55f), 15);
            var pauseB = MakeButton(wg.transform, "PauseButton", "暂停", new Vector2(0.74f, 0.05f), new Vector2(0.86f, 0.55f), 15);
            var nextB = MakeButton(wg.transform, "NextButton", "下一首", new Vector2(0.88f, 0.05f), new Vector2(1.0f, 0.55f), 15);

            var sgo = new GameObject("VolumeSlider", typeof(RectTransform));
            sgo.transform.SetParent(wg.transform, false);
            var srt = sgo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.58f, 0.65f); srt.anchorMax = new Vector2(1.0f, 0.95f); srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            var slider = sgo.AddComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 1; slider.direction = Slider.Direction.LeftToRight;
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sgo.transform, false);
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = Vector2.zero; bgrt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            var fa = new GameObject("Fill Area", typeof(RectTransform));
            fa.transform.SetParent(sgo.transform, false);
            var fart = fa.GetComponent<RectTransform>();
            fart.anchorMin = Vector2.zero; fart.anchorMax = Vector2.one; fart.offsetMin = new Vector2(4, 0); fart.offsetMax = new Vector2(-4, 0);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fa.transform, false);
            var frt = fill.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.4f, 0.75f, 0.45f, 1f);
            var hsa = new GameObject("Handle Slide Area", typeof(RectTransform));
            hsa.transform.SetParent(sgo.transform, false);
            var hsrt = hsa.GetComponent<RectTransform>();
            hsrt.anchorMin = Vector2.zero; hsrt.anchorMax = Vector2.one; hsrt.offsetMin = new Vector2(4, 0); hsrt.offsetMax = new Vector2(-4, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(hsa.transform, false);
            var hrt = handle.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(0.92f, 0.92f, 0.92f, 1f);
            slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = hrt; slider.targetGraphic = handle.GetComponent<Image>();

            var am = Object.FindObjectOfType<Dapaolou.Audio.AudioManager>();
            var mc = wg.AddComponent<Dapaolou.UI.MusicController>();
            var so = new SerializedObject(mc);
            so.FindProperty("prevButton").objectReferenceValue = prevB;
            so.FindProperty("pauseButton").objectReferenceValue = pauseB;
            so.FindProperty("nextButton").objectReferenceValue = nextB;
            so.FindProperty("volumeSlider").objectReferenceValue = slider;
            so.FindProperty("trackText").objectReferenceValue = trackText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[PopV3] done");
    }

    private static void EnsurePage(Transform content, string name)
    {
        if (content.Find(name) != null) return;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(content, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.SetActive(false);
    }

    private static TMP_Text MakeText(Transform parent, string nm, string label, int size, TextAlignmentOptions align, Color color, Vector2 aMin, Vector2 aMax)
    {
        var go = new GameObject(nm, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = label; tmp.fontSize = size; tmp.alignment = align; tmp.color = color; tmp.raycastTarget = false;
        return tmp;
    }

    private static Button MakeButton(Transform parent, string nm, string label, Vector2 aMin, Vector2 aMax, int fontSize)
    {
        var go = new GameObject(nm, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var im = go.GetComponent<Image>(); im.color = new Color(0.35f, 0.35f, 0.35f, 0.95f);
        var b = go.GetComponent<Button>(); b.targetGraphic = im;
        var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        t.transform.SetParent(rt, false);
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = label; tmp.fontSize = fontSize; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white; tmp.raycastTarget = false;
        return b;
    }
}
