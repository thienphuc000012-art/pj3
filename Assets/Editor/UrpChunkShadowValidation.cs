using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Resolve prefab inheritance in real chunk scenes; no campaign or player saves are run.</summary>
public static class UrpChunkShadowValidation
{
    const string Request = "Library/CombatValidation/UrpChunkShadows.request";
    const string Report = "Library/CombatValidation/UrpChunkShadows.json";
    [Serializable] class Entry
    {
        public string scene, hierarchy;
        public long componentId;
        public int nativeResolution, tier;
    }
    [Serializable] class Result
    {
        public int scenes, localLights, invalid;
        public List<Entry> lights = new List<Entry>();
        public int renderFrames, visibleShadowLights;
        public List<string> atlasMessages = new List<string>();
        public string error;
    }
    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Run; }
    [MenuItem("Tools/Adventure/Repair/Audit Resolved Chunk Shadows")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { if (File.Exists(Request)) EditorApplication.delayCall += Run; return; }
        if (File.Exists(Request)) File.Delete(Request);
        var result = new Result();
        try
        {
            foreach (string directory in new[] { "Assets/Scenes/MapChunks", "Assets/Scenes/MapChunksRefined" })
            foreach (string file in Directory.GetFiles(directory, "*.unity"))
            {
                // Every invalid Custom value must have an explicit Custom tier
                // here or in a prefab; inspect all light-containing chunk files.
                string source = File.ReadAllText(file);
                if (!source.Contains("m_AdditionalLightsShadowResolutionTier:") && !source.Contains("m_Shadows.")) continue;
                var scene = EditorSceneManager.OpenPreviewScene(file);
                try
                {
                    result.scenes++;
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                    {
                        if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                        result.localLights++;
                        var data = light.GetComponent<UniversalAdditionalLightData>();
                        if (data == null) continue;
                        int resolution = (int)light.shadowResolution;
                        int tier = data.additionalLightsShadowResolutionTier;
                        if (tier != -1 || resolution >= 128 && Mathf.IsPowerOfTwo(resolution)) continue;
                        var path = new StringBuilder(light.name);
                        for (var parent = light.transform.parent; parent != null; parent = parent.parent) path.Insert(0, parent.name + "/");
                        result.lights.Add(new Entry { scene = file.Replace('\\', '/'), hierarchy = path.ToString(),
                            componentId = (long)GlobalObjectId.GetGlobalObjectIdSlow(data).targetObjectId,
                            nativeResolution = resolution, tier = tier });
                        result.invalid++;
                    }
                    // Render the actual refined chunk assets, including their
                    // inherited prefab lights and real geometry, never a campaign.
                    if (file.Replace('\\', '/').Contains("/MapChunksRefined/")) RenderChunk(scene, result);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
        catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, JsonUtility.ToJson(result, true));
        Debug.Log("Resolved chunk shadow audit: " + result.scenes + " scenes, " + result.localLights +
            " local lights, " + result.invalid + " invalid Custom values; " + result.renderFrames +
            " real chunk renders, " + result.atlasMessages.Count + " atlas messages. " + Report);
    }
    static void RenderChunk(Scene scene, Result result)
    {
        var lights = new List<Light>();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var light in root.GetComponentsInChildren<Light>(true))
            if (light.isActiveAndEnabled && light.shadows != LightShadows.None &&
                (light.type == LightType.Point || light.type == LightType.Spot)) lights.Add(light);
        if (lights.Count == 0) return;
        var obj = new GameObject("Chunk Shadow Validation Camera") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(obj, scene);
        var camera = obj.AddComponent<Camera>();
        camera.enabled = false; camera.scene = scene;
        camera.fieldOfView = 80; camera.nearClipPlane = .1f; camera.farClipPlane = 80;
        camera.GetUniversalAdditionalCameraData().renderShadows = true;
        var texture = new RenderTexture(320, 180, 24);
        camera.targetTexture = texture;
        Action<ScriptableRenderContext, Camera> before = (context, rendering) =>
        {
            if (rendering != camera || !camera.TryGetCullingParameters(out var parameters)) return;
            var culled = context.Cull(ref parameters);
            for (int i = 0; i < culled.visibleLights.Length; i++)
            {
                var light = culled.visibleLights[i].light;
                if (light != null && light.shadows != LightShadows.None && culled.GetShadowCasterBounds(i, out _)) result.visibleShadowLights++;
            }
            result.renderFrames++;
        };
        Application.LogCallback logger = (message, stack, type) =>
        {
            if (message.Contains("Reduced additional punctual") || message.Contains("Too many additional punctual"))
                result.atlasMessages.Add(scene.path + ": " + message);
        };
        try
        {
            RenderPipelineManager.beginCameraRendering += before;
            Application.logMessageReceived += logger;
            Physics.SyncTransforms();
            var positions = new HashSet<Vector3>();
            foreach (int index in new[] { 0, lights.Count / 2, lights.Count - 1 })
            {
                Vector3 center = lights[index].transform.position;
                if (!positions.Add(center)) continue;
                camera.transform.SetPositionAndRotation(center + new Vector3(0, 2, -8), Quaternion.LookRotation(new Vector3(0, -2, 8)));
                camera.Render();
            }
        }
        finally
        {
            RenderPipelineManager.beginCameraRendering -= before;
            Application.logMessageReceived -= logger;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(obj);
            texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
