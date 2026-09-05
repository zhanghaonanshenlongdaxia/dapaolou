using UnityEngine;
using UnityEditor;

public class VillageTerrainTool : EditorWindow
{
    // ===== 房屋放置 =====
    GameObject housePrefab;
    float houseYaw = 0f;
    bool placeMode = false;

    // ===== 游戏区保护 =====
    float gameAreaHalf = 12f;

    [MenuItem("Tools/Village Terrain Editor")]
    static void ShowWindow()
    {
        GetWindow<VillageTerrainTool>("Village Terrain Editor");
    }

    void OnGUI()
    {
        GUILayout.Label("=== 房屋放置 ===", EditorStyles.boldLabel);
        housePrefab = (GameObject)EditorGUILayout.ObjectField("房屋模型", housePrefab, typeof(GameObject), false);
        houseYaw = EditorGUILayout.Slider("朝向(度)", houseYaw, 0f, 360f);
        placeMode = EditorGUILayout.Toggle("点击地形放置", placeMode);

        if (placeMode && housePrefab == null)
            EditorGUILayout.HelpBox("请先选择房屋模型", MessageType.Warning);
        else if (placeMode)
            EditorGUILayout.HelpBox("在 Scene 视图中点击地形即可放置\n（自动落地对齐）", MessageType.Info);

        GUILayout.Space(10);
        GUILayout.Label("=== 地形工具 ===", EditorStyles.boldLabel);

        if (GUILayout.Button("平整游戏区 (±12m)"))
        {
            FlattenGameArea();
            ShowNotification(new GUIContent("游戏区已平整 ✓"));
        }

        if (GUILayout.Button("重置所有弹珠到出生点"))
        {
            foreach (var m in Object.FindObjectsOfType<Dapaolou.Marble.MarbleData>())
                m.ResetToInitial();
            ShowNotification(new GUIContent("弹珠已归位 ✓"));
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "地形雕刻请使用 Unity 自带工具：\n" +
            "选中 VillageTerrain → Inspector → 刷子图标\n" +
            "• Raise/Lower Terrain：升高/降低地形\n" +
            "• Paint Texture：绘制材质\n" +
            "• Smooth Height：平滑\n" +
            "• Set Height：设为固定高度\n\n" +
            "提示：游戏区(±12m)请保持平坦，弹珠游戏在水泥板上进行",
            MessageType.Info);
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!placeMode || housePrefab == null) return;

        // 在鼠标位置预览房屋
        Event e = Event.current;
        if (e.type == EventType.MouseMove)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain != null)
            {
                // 沿射线找地形交点
                if (Physics.Raycast(ray, out var hit, 500f))
                {
                    Handles.Label(hit.point, "放置预览");
                }
            }
        }

        // 点击放置
        if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 1)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var terrain = Object.FindObjectOfType<Terrain>();

            Vector3 placePos;
            if (terrain != null)
            {
                // 求射线与地形水平面的交点（从上方往下）
                float rayDirY = ray.direction.y;
                if (Mathf.Abs(rayDirY) > 0.001f)
                {
                    float t = (ray.origin.y - terrain.transform.position.y) / -rayDirY;
                    if (t > 0)
                    {
                        placePos = ray.origin + ray.direction * t;
                        PlaceHouse(placePos);
                        e.Use();
                        return;
                    }
                }
                // fallback：直接用射线方向推算
                placePos = ray.origin + ray.direction * 30f;
                placePos.y = terrain.SampleHeight(placePos) + terrain.transform.position.y;
                PlaceHouse(placePos);
                e.Use();
            }
        }
    }

    void PlaceHouse(Vector3 position)
    {
        var house = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab);
        if (house == null)
        {
            house = Object.Instantiate(housePrefab);
        }
        house.name = housePrefab.name;
        house.transform.position = position;
        house.transform.rotation = Quaternion.Euler(0, houseYaw, 0);

        // 落地对齐：底部贴地形
        var rend = house.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            float bottom = rend.bounds.min.y;
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain != null)
            {
                float groundY = terrain.SampleHeight(new Vector3(position.x, 0, position.z)) + terrain.transform.position.y;
                house.transform.position = new Vector3(position.x, groundY - (bottom - position.y), position.z);
            }
        }

        Undo.RegisterCreatedObjectUndo(house, "放置房屋");
        Debug.Log("房屋已放置: " + house.name + " @ " + house.transform.position);
    }

    void FlattenGameArea()
    {
        var terrain = Object.FindObjectOfType<Terrain>();
        if (terrain == null) { Debug.LogWarning("没有 Terrain"); return; }

        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);
        int center = res / 2;
        // 游戏区半径（占地形比例）：12m / 200m
        float gameRatio = gameAreaHalf / data.size.x;
        int gamePixels = Mathf.RoundToInt(gameRatio * res);

        for (int z = center - gamePixels; z <= center + gamePixels; z++)
        {
            for (int x = center - gamePixels; x <= center + gamePixels; x++)
            {
                if (x >= 0 && x < res && z >= 0 && z < res)
                    heights[z, x] = 0f;
            }
        }
        data.SetHeights(0, 0, heights);
        Debug.Log("游戏区已平整 (" + gamePixels * 2 + "x" + gamePixels * 2 + " 像素区域)");
    }
}
