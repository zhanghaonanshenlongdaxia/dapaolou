using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using Dapaolou.Game;

namespace Dapaolou.EditorTools
{
    /// <summary>
    /// 编辑器工具：创建 PlacementWheelPanel 预制体（QFramework UIKit 格式）
    /// 菜单：Dapaolou/创建布防轮盘预制体
    /// </summary>
    public static class PlacementWheelPanelBuilder
    {
        [MenuItem("Dapaolou/创建布防轮盘预制体")]
        public static void Build()
        {
            // 1) 构建面板层级
            var root = new GameObject("PlacementWheelPanel", typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 全屏遮罩（挡住点击穿透）
            var maskGo = new GameObject("Blocker", typeof(Image), typeof(RectTransform));
            maskGo.transform.SetParent(root.transform, false);
            var mrt = maskGo.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            var mImg = maskGo.GetComponent<Image>();
            mImg.color = new Color(0f, 0f, 0f, 0.3f);
            mImg.raycastTarget = true;

            // 轮盘根节点
            var wheelGo = new GameObject("WheelRoot", typeof(RectTransform));
            wheelGo.transform.SetParent(root.transform, false);
            var wrt = wheelGo.GetComponent<RectTransform>();
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = Vector2.zero;

            // 挂 UIPanel 子类脚本
            root.AddComponent<Dapaolou.UI.PlacementWheelPanel>();

            // 2) 保存预制体
            const string dir = "Assets/Prefabs/UIPanel";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Prefabs", "UIPanel");
            const string prefabPath = dir + "/PlacementWheelPanel.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            // 3) 场景接线：挂到 Player1 + 移除旧手写轮盘
            var p1 = GameObject.Find("Player1");
            if (p1 == null) { EditorUtility.DisplayDialog("error", "Player1 not found", "OK"); return; }

            var panelInScene = p1.GetComponent<Dapaolou.UI.PlacementWheelPanel>();
            if (panelInScene == null) panelInScene = p1.AddComponent<Dapaolou.UI.PlacementWheelPanel>();
            EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<Dapaolou.UI.PlacementWheelPanel>(), panelInScene);

            var gc = GameObject.Find("GameCanvas");
            if (gc != null)
            {
                var oldWheel = gc.GetComponentInChildren<Dapaolou.UI.PlacementWheelUI>(true);
                if (oldWheel != null) Object.DestroyImmediate(oldWheel);
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            EditorUtility.DisplayDialog("done", "PlacementWheelPanel prefab + scene wiring OK\n预制体: " + prefabPath, "OK");
        }
    }
}
