using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Repairs stale loaded light data, then renders an isolated atlas regression fixture.</summary>
public static class UrpShadowResolutionValidation
{
    const string Request = "Library/CombatValidation/UrpShadowResolution.request";
    const string Result = "Library/CombatValidation/UrpShadowResolution.result.txt";

    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        File.Delete(Request); Validate();
    }

    [MenuItem("Tools/Adventure/Repair/Validate URP Local Light Shadows")]
    public static void Validate()
    {
        Scene preview = default;
        Material material = null;
        RenderTexture render = null;
        var atlasMessages = new List<string>();
        int renderedCameras = 0;
        int visibleShadowLights = 0;
        Camera fixtureCamera = null;
        Action<ScriptableRenderContext, Camera> beforeRender = (context, camera) =>
        {
            if (camera != fixtureCamera || !camera.TryGetCullingParameters(out var parameters)) return;
            var culled = context.Cull(ref parameters);
            visibleShadowLights = 0;
            for (int i = 0; i < culled.visibleLights.Length; i++)
                if (culled.visibleLights[i].light != null && culled.visibleLights[i].light.shadows != LightShadows.None &&
                    culled.GetShadowCasterBounds(i, out _)) visibleShadowLights++;
            renderedCameras++;
        };
        Application.LogCallback listener = (message, stack, type) =>
        {
            if (message.Contains("Reduced additional punctual") || message.Contains("Too many additional punctual")) atlasMessages.Add(message);
        };
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run in Edit Mode.");
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Check(pipeline != null, "URP must be active.");
            Check(pipeline.additionalLightsShadowmapResolution == 2048 &&
                pipeline.additionalLightsShadowResolutionTierLow == 256,
                "Expected project settings: 2048 atlas and Low = 256.");
            int liveRepairs = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                        if (RepairInvalidCustom(light)) liveRepairs++;
            }
            // Loaded scenes with user edits stay open/dirty. Canonical chunk files
            // were patched separately; do not save unrelated scene edits here.
            preview = EditorSceneManager.NewPreviewScene();
            var cameraRoot = InScene(new GameObject("Shadow Atlas Test Camera"), preview);
            var camera = cameraRoot.AddComponent<Camera>();
            fixtureCamera = camera;
            camera.scene = preview;
            camera.transform.SetPositionAndRotation(new Vector3(0, 3, -9), Quaternion.LookRotation(new Vector3(0, -1, 12)));
            camera.fieldOfView = 65; camera.nearClipPlane = .1f; camera.farClipPlane = 40;
            camera.cameraType = CameraType.Game;
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderShadows = true; additional.renderPostProcessing = false;
            render = new RenderTexture(320, 180, 24); camera.targetTexture = render;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var ground = InScene(GameObject.CreatePrimitive(PrimitiveType.Cube), preview);
            ground.transform.position = new Vector3(0, -.25f, 3); ground.transform.localScale = new Vector3(16, .5f, 16);
            ground.GetComponent<Renderer>().sharedMaterial = material;
            var blocker = InScene(GameObject.CreatePrimitive(PrimitiveType.Cube), preview);
            blocker.transform.position = new Vector3(0, 1, 3); blocker.transform.localScale = new Vector3(1, 2, 1);
            blocker.GetComponent<Renderer>().sharedMaterial = material;
            var lamps = new List<Light>();
            for (int i = 0; i < 29; i++)
            {
                var light = InScene(new GameObject("Atlas Fixture Light " + i), preview).AddComponent<Light>();
                light.type = LightType.Spot; light.range = 18; light.spotAngle = 100; light.intensity = 1;
                light.transform.SetPositionAndRotation(new Vector3((i % 5 - 2) * .6f, 4, 1 + i / 5f * .1f), Quaternion.Euler(90, 0, 0));
                light.shadows = LightShadows.Soft;
                Tier(light, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow);
                lamps.Add(light);
            }
            var first = lamps[0];
            Tier(first, -1); NativeResolution(first, -1);
            Check(RepairInvalidCustom(first) && first.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier == 0,
                "Legacy shadowCustomResolution must not be mistaken for URP Custom native resolution.");
            Tier(first, -1);
            // The URP Light Inspector writes the serialized integer directly;
            // the public enum setter rejects pixel values such as 512.
            NativeResolution(first, 512);
            Check(!RepairInvalidCustom(first) && first.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier == -1,
                "Valid artist-authored Custom settings were modified.");
            Tier(first, 0);
            Physics.SyncTransforms();
            Application.logMessageReceived += listener;
            RenderPipelineManager.beginCameraRendering += beforeRender;
            foreach (int count in new[] { 2, 3, 6, 13, 16, 29 })
            {
                for (int i = 0; i < lamps.Count; i++) { lamps[i].type = LightType.Spot; lamps[i].enabled = i < count; }
                camera.Render();
                Check(visibleShadowLights >= count, "Fixture did not render the expected shadow casters: " + visibleShadowLights + "/" + count);
                Check(atlasMessages.Count == 0, "Atlas reduction remains with " + count + " spots: " + string.Join("\n", atlasMessages));
            }
            // Four Point lights use 24 faces; five Spot lights add another five.
            for (int i = 0; i < lamps.Count; i++) { lamps[i].type = i < 4 ? LightType.Point : LightType.Spot; lamps[i].enabled = i < 9; }
            camera.Render();
            Check(visibleShadowLights >= 9 && renderedCameras >= 7, "Mixed shadow fixture was not rendered by URP.");
            Check(atlasMessages.Count == 0, "Mixed 29-face Point/Spot fixture still overflowed the atlas.");
            var directional = InScene(new GameObject("Main Shadow Preservation Check"), preview).AddComponent<Light>();
            directional.type = LightType.Directional; directional.shadows = LightShadows.Soft;
            Tier(directional, -1); NativeResolution(directional, -1);
            Check(!RepairInvalidCustom(directional) && directional.shadows == LightShadows.Soft, "Main directional shadow settings changed.");
            Check(pipeline.additionalLightsShadowmapResolution == 2048, "Atlas size was changed.");
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "PASS: loaded scene invalid Custom lights repaired: " + liveRepairs +
                "; URP rendered 2/3/6/13/16/29 Spot shadows and mixed 4 Point + 5 Spot (29 faces), with no atlas downscale messages. " +
                "Legacy native -1 fixed; valid Custom 512 and Directional shadows preserved; atlas remains 2048. No gameplay, player saves or unrelated scene edits saved.");
            Debug.Log(File.ReadAllText(Result));
            SceneView.RepaintAll();
        }
        catch (Exception error)
        { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL: " + error); Debug.LogException(error); }
        finally
        {
            Application.logMessageReceived -= listener;
            RenderPipelineManager.beginCameraRendering -= beforeRender;
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (material != null) Object.DestroyImmediate(material);
            if (render != null) { render.Release(); Object.DestroyImmediate(render); }
        }
    }
    static bool RepairInvalidCustom(Light light)
    {
        if (light.type != LightType.Point && light.type != LightType.Spot) return false;
        var data = light.GetComponent<UniversalAdditionalLightData>();
        if (data == null || data.additionalLightsShadowResolutionTier != -1) return false;
        int resolution = (int)light.shadowResolution;
        if (resolution >= 128 && Mathf.IsPowerOfTwo(resolution)) return false;
        Tier(light, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow);
        return true;
    }
    static void Tier(Light light, int tier)
    {
        var data = light.GetUniversalAdditionalLightData(); var serialized = new SerializedObject(data);
        serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = tier;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        if (PrefabUtility.IsPartOfPrefabInstance(data)) PrefabUtility.RecordPrefabInstancePropertyModifications(data);
    }
    static void NativeResolution(Light light, int pixels)
    {
        var serialized = new SerializedObject(light);
        serialized.FindProperty("m_Shadows.m_Resolution").intValue = pixels;
        serialized.FindProperty("m_Shadows.m_CustomResolution").intValue = 256;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static GameObject InScene(GameObject obj, Scene scene) { SceneManager.MoveGameObjectToScene(obj, scene); return obj; }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
