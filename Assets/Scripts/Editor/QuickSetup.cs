#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Dapaolou.Game;
using Dapaolou.Player;
using Dapaolou.Marble;
using Dapaolou.Terrain;
using Dapaolou.UI;

namespace Dapaolou.Editor
{
    /// <summary>
    /// 快速设置工具 - 一键创建游戏场景
    /// </summary>
    public class QuickSetup : EditorWindow
    {
        [MenuItem("Dapaolou/Quick Setup")]
        public static void ShowWindow()
        {
            GetWindow<QuickSetup>("打炮楼快速设置");
        }
        
        private int playerCount = 2;
        private bool createTerrain = true;
        private bool createUI = true;
        
        void OnGUI()
        {
            GUILayout.Label("打炮楼游戏 - 快速设置", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            playerCount = EditorGUILayout.IntField("玩家数量", playerCount);
            createTerrain = EditorGUILayout.Toggle("创建地形", createTerrain);
            createUI = EditorGUILayout.Toggle("创建UI", createUI);
            
            GUILayout.Space(20);
            
            if (GUILayout.Button("一键创建场景", GUILayout.Height(40)))
            {
                CreateGameScene();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("创建弹珠预制体"))
            {
                CreateMarblePrefab();
            }
            
            if (GUILayout.Button("创建地形"))
            {
                CreateTerrain();
            }
            
            if (GUILayout.Button("创建玩家"))
            {
                CreatePlayers();
            }
            
            if (GUILayout.Button("创建UI"))
            {
                CreateGameUI();
            }
        }
        
        private void CreateGameScene()
        {
            // 创建游戏管理器
            CreateGameManager();
            
            // 创建地形
            if (createTerrain)
            {
                CreateTerrain();
            }
            
            // 创建玩家
            CreatePlayers();
            
            // 创建UI
            if (createUI)
            {
                CreateGameUI();
            }
            
            Debug.Log("游戏场景创建完成！");
        }
        
        private void CreateGameManager()
        {
            // GameManager
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
            
            // TerrainEffectSystem
            GameObject terrainSys = new GameObject("TerrainEffectSystem");
            terrainSys.AddComponent<TerrainEffectSystem>();
            
            // 生成点
            GameObject spawnPoints = new GameObject("SpawnPoints");
            
            // 玩家生成点
            for (int i = 0; i < playerCount; i++)
            {
                GameObject spawn = new GameObject($"Player{i + 1}Spawn");
                spawn.transform.SetParent(spawnPoints.transform);
                spawn.transform.position = new Vector3(i * 5f - 2.5f, 0, -3f);
            }
            
            // 炮楼生成点
            for (int i = 0; i < playerCount; i++)
            {
                GameObject spawn = new GameObject($"Tower{i + 1}Spawn");
                spawn.transform.SetParent(spawnPoints.transform);
                spawn.transform.position = new Vector3(i * 5f - 2.5f, 0, 0f);
            }
            
            // 小兵生成点
            for (int i = 0; i < playerCount; i++)
            {
                GameObject spawn = new GameObject($"Soldier{i + 1}Spawn");
                spawn.transform.SetParent(spawnPoints.transform);
                spawn.transform.position = new Vector3(i * 5f - 2.5f, 0, -1.5f);
            }
        }
        
        private void CreateTerrain()
        {
            // 地面
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2, 1, 2);
            ground.layer = LayerMask.NameToLayer("Default");
            
            // 添加地形标签
            TerrainTag tag = ground.AddComponent<TerrainTag>();
            tag.terrainType = TerrainType.Cement;
            
            // 创建不同地形区域
            CreateTerrainArea("CementArea", new Vector3(0, 0.01f, 0), new Vector3(10, 0.01f, 10), TerrainType.Cement, Color.gray);
            CreateTerrainArea("DirtArea", new Vector3(-5, 0.01f, 5), new Vector3(4, 0.01f, 4), TerrainType.Dirt, new Color(0.5f, 0.3f, 0.1f));
            CreateTerrainArea("GrassArea", new Vector3(5, 0.01f, 5), new Vector3(4, 0.01f, 4), TerrainType.Grass, Color.green);
            CreateTerrainArea("SandArea", new Vector3(0, 0.01f, 8), new Vector3(6, 0.01f, 3), TerrainType.LooseSand, new Color(0.9f, 0.8f, 0.5f));
        }
        
        private void CreateTerrainArea(string name, Vector3 position, Vector3 scale, TerrainType type, Color color)
        {
            GameObject area = GameObject.CreatePrimitive(PrimitiveType.Cube);
            area.name = name;
            area.transform.position = position;
            area.transform.localScale = scale;
            
            // 设置材质
            Renderer renderer = area.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = color;
            
            // 添加地形标签
            TerrainTag tag = area.AddComponent<TerrainTag>();
            tag.terrainType = type;
            tag.gizmoColor = color;
            
            // 设置Layer（需要先创建Terrain Layer）
            // area.layer = LayerMask.NameToLayer("Terrain");
        }
        
        private void CreatePlayers()
        {
            for (int i = 0; i < playerCount; i++)
            {
                CreatePlayer(i);
            }
        }
        
        private void CreatePlayer(int playerIndex)
        {
            // 玩家根对象
            GameObject playerObj = new GameObject($"Player{playerIndex + 1}");
            playerObj.transform.position = new Vector3(playerIndex * 5f - 2.5f, 1f, -3f);
            
            // 添加CharacterController
            CharacterController cc = playerObj.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 1f, 0);
            
            // 添加第一人称控制器
            FirstPersonController fps = playerObj.AddComponent<FirstPersonController>();
            
            // 添加弹珠发射器
            MarbleShooter shooter = playerObj.AddComponent<MarbleShooter>();
            
            // 添加手抖系统
            HandTremorSystem tremor = playerObj.AddComponent<HandTremorSystem>();
            
            // 添加玩家管理器
            PlayerManager playerManager = playerObj.AddComponent<PlayerManager>();
            
            // 创建摄像机
            GameObject cameraObj = new GameObject("Camera");
            cameraObj.transform.SetParent(playerObj.transform);
            cameraObj.transform.localPosition = new Vector3(0, 1.6f, 0);
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();
            
            // 创建第三人称模型
            GameObject thirdPersonObj = new GameObject("ThirdPersonModel");
            thirdPersonObj.transform.SetParent(playerObj.transform);
            thirdPersonObj.AddComponent<ThirdPersonModel>();
            
            // 设置颜色
            Color[] colors = { Color.blue, Color.red, Color.green, Color.yellow };
            playerManager.SetPlayerColor(colors[playerIndex % colors.Length]);
        }
        
        private void CreateMarblePrefab()
        {
            // 创建弹珠预制体
            GameObject marble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marble.name = "MarblePrefab";
            marble.transform.localScale = Vector3.one * 0.05f;
            
            // 添加物理组件
            Rigidbody rb = marble.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            
            // 物理材质由 MarbleData.Awake 统一设置为玻璃材质

            // 添加游戏组件
            marble.AddComponent<MarbleData>();
            marble.AddComponent<MarbleCollisionHandler>();
            
            // 设置材质
            Renderer renderer = marble.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.white;
            
            // 保存为预制体
            string prefabPath = "Assets/Prefabs/MarblePrefab.prefab";
            System.IO.Directory.CreateDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(marble, prefabPath);
            
            // 删除场景中的临时对象
            DestroyImmediate(marble);
            
            Debug.Log($"弹珠预制体已创建: {prefabPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        
        private void CreateGameUI()
        {
            // 创建Canvas
            GameObject canvasObj = new GameObject("GameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // 创建UI管理器
            canvasObj.AddComponent<GameUIManager>();
            
            // 创建玩家信息面板
            CreatePlayerInfoPanel(canvasObj.transform);
            
            // 创建力度条
            CreatePowerBar(canvasObj.transform);
            
            // 创建提示文本
            CreateHintText(canvasObj.transform);
            
            // 创建游戏结束面板
            CreateGameOverPanel(canvasObj.transform);
        }
        
        private void CreatePlayerInfoPanel(Transform parent)
        {
            GameObject panel = new GameObject("PlayerInfoPanel");
            panel.transform.SetParent(parent);
            
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(200, 150);
            
            // 添加背景
            UnityEngine.UI.Image bg = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.5f);
            
            // 添加文本
            GameObject textObj = new GameObject("InfoText");
            textObj.transform.SetParent(panel.transform);
            
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10, 10);
            textRt.offsetMax = new Vector2(-10, -10);
            
            TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "玩家信息";
            tmp.fontSize = 14;
        }
        
        private void CreatePowerBar(Transform parent)
        {
            GameObject sliderObj = new GameObject("PowerBar");
            sliderObj.transform.SetParent(parent);
            
            RectTransform rt = sliderObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 50);
            rt.sizeDelta = new Vector2(300, 30);
            
            UnityEngine.UI.Slider slider = sliderObj.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
        }
        
        private void CreateHintText(Transform parent)
        {
            GameObject textObj = new GameObject("HintText");
            textObj.transform.SetParent(parent);
            
            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 50);
            
            TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "";
            tmp.fontSize = 24;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
        }
        
        private void CreateGameOverPanel(Transform parent)
        {
            GameObject panel = new GameObject("GameOverPanel");
            panel.transform.SetParent(parent);
            panel.SetActive(false);
            
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            UnityEngine.UI.Image bg = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            
            // 获胜文本
            GameObject textObj = new GameObject("WinnerText");
            textObj.transform.SetParent(panel.transform);
            
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.6f);
            textRt.anchorMax = new Vector2(0.5f, 0.6f);
            textRt.sizeDelta = new Vector2(300, 100);
            
            TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = "游戏结束";
            tmp.fontSize = 36;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            
            // 重新开始按钮
            GameObject buttonObj = new GameObject("RestartButton");
            buttonObj.transform.SetParent(panel.transform);
            
            RectTransform buttonRt = buttonObj.AddComponent<RectTransform>();
            buttonRt.anchorMin = new Vector2(0.5f, 0.4f);
            buttonRt.anchorMax = new Vector2(0.5f, 0.4f);
            buttonRt.sizeDelta = new Vector2(200, 50);
            
            UnityEngine.UI.Image buttonBg = buttonObj.AddComponent<UnityEngine.UI.Image>();
            buttonBg.color = Color.white;
            
            UnityEngine.UI.Button button = buttonObj.AddComponent<UnityEngine.UI.Button>();
        }
    }
}
#endif
