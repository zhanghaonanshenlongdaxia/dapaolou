using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ============ 1. 反射探针 ============
var probeGo = new GameObject("ReflectionProbe_Central");
probeGo.transform.position = new Vector3(0f, 1.2f, -0.5f);
var probe = probeGo.AddComponent<ReflectionProbe>();
probe.size = new Vector3(22f, 8f, 16f);
probe.intensity = 0.9f;
probe.boxProjection = true;
probe.mode = ReflectionProbeMode.Realtime;
probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;

// ============ 2. SSAO ============
int ssaoAdded = 0;
string[] rendererGuids = AssetDatabase.FindAssets("t:UniversalRendererData");
foreach (var guid in rendererGuids)
{
    string path = AssetDatabase.GUIDToAssetPath(guid);
    var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
    if (rendererData == null) continue;

    bool has = false;
    foreach (var f in rendererData.rendererFeatures)
        if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") { has = true; break; }
    if (has) continue;

    var ssaoType = System.Type.GetType("Unity.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
    if (ssaoType == null) { Debug.Log("SSAO 类型未找到: " + path); continue; }
    var ssao = ScriptableObject.CreateInstance(ssaoType);
    ssao.name = "SSAO";
    AssetDatabase.AddObjectToAsset(ssao, rendererData);

    var so = new SerializedObject(rendererData);
    var features = so.FindProperty("m_RendererFeatures");
    features.arraySize++;
    features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
    var map = so.FindProperty("m_RendererFeaturesMap");
    if (map != null)
    {
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).objectReferenceValue = ssao;
    }
    so.ApplyModifiedProperties();

    var ssaoSo = new SerializedObject(ssao);
    var inten = ssaoSo.FindProperty("m_Intensity");
    if (inten != null) inten.floatValue = 0.6f;
    var radius = ssaoSo.FindProperty("m_Radius");
    if (radius != null) radius.floatValue = 0.5f;
    ssaoSo.ApplyModifiedProperties();
    ssaoAdded++;
}
Debug.Log("SSAO 注入: " + ssaoAdded + " 个 RendererData");

// ============ 3. 阴影 ============
var urp = UniversalRenderPipeline.asset;
urp.shadowDistance = 25f;
var urpSo = new SerializedObject(urp);
var cascade = urpSo.FindProperty("m_CascadeCount");
if (cascade != null) cascade.intValue = 4;
var soft = urpSo.FindProperty("m_SoftShadowsSupported");
if (soft != null) soft.boolValue = true;
urpSo.ApplyModifiedProperties();

foreach (var light in Object.FindObjectsOfType<Light>())
{
    if (light.type == LightType.Directional)
    {
        light.shadowStrength = 0.9f;
        Debug.Log("主光阴影: " + light.name + " strength=0.9");
    }
}

// ============ 保存 ============
var sc = EditorSceneManager.GetActiveScene();
EditorSceneManager.MarkSceneDirty(sc);
EditorSceneManager.SaveOpenScenes();
AssetDatabase.SaveAssets();
Debug.Log("第一阶段全部完成");
