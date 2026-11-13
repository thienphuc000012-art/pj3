using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class CombatShadowValidation
{
    const string Request = "Library/CombatValidation/CombatShadows.request";
    [Serializable] class LightInfo { public string scene, hierarchy, prefab; public int type, shadows, tier, nativeResolution; }
    [Serializable] class Report { public List<LightInfo> lights = new List<LightInfo>(); public int chunks, renders, visibleFaces, budgetFaces; public bool transferred, vfxLightPreserved, originalShadowsRestored; public List<string> atlasMessages = new List<string>(); public string error; }
    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += Audit; }
    [MenuItem("Tools/Adventure/Repair/Audit Combat Shadows")]
    public static void Audit()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { if (File.Exists(Request)) EditorApplication.delayCall += Audit; return; }
        if (File.Exists(Request)) File.Delete(Request);
        var report = new Report();
        Scene preview = default;
        try
        {
            preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/combattest.unity");
            // Reproduce the last reported boss encounter without starting play,
            // running CampaignSession, or writing player save data.
            Vector3 center = new Vector3(-18.60f, 280.56f, -65f);
            MapChunkStreamer streamer = null;
            foreach (var item in UnityEngine.Object.FindObjectsByType<MapChunkStreamer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.gameObject.scene.name == "mapgame") { streamer = item; break; }
            if (streamer == null) throw new InvalidOperationException("Open mapgame in Edit Mode to read its actual chunk catalogue.");
            var paths = new List<string>(); streamer.CollectChunkPaths(center, 39f, paths);
            foreach (string path in paths)
            {
                var chunk = EditorSceneManager.OpenPreviewScene(path);
                try { foreach (var root in chunk.GetRootGameObjects()) SceneManager.MoveGameObjectToScene(root, preview); }
                finally { EditorSceneManager.ClosePreviewScene(chunk); }
                report.chunks++;
            }
            foreach (var root in preview.GetRootGameObjects())
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Point && light.type != LightType.Spot) continue;
                var data = light.GetComponent<UniversalAdditionalLightData>();
                string hierarchy = light.name;
                for (var parent = light.transform.parent; parent != null; parent = parent.parent) hierarchy = parent.name + "/" + hierarchy;
                report.lights.Add(new LightInfo { scene = preview.path, hierarchy = hierarchy,
                    prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(light), type = (int)light.type,
                    shadows = (int)light.shadows, tier = data != null ? data.additionalLightsShadowResolutionTier : 2,
                    nativeResolution = (int)light.shadowResolution });
            }
            var originals = new Dictionary<Light, LightShadows>();
            foreach (var root in preview.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>(true)) originals[light] = light.shadows;
            var loading = EditorSceneManager.NewPreviewScene();
            CombatShadowBudget budget = null;
            try
            {
                budget = CombatShadowBudget.Ensure(loading);
                budget.SetCenter(center); budget.RegisterScene(preview); budget.ApplyBudget();
                if (CombatShadowBudget.Ensure(preview) != budget || budget.gameObject.scene != preview)
                    throw new InvalidOperationException("Loading/combat shadow budget was duplicated or lost during transfer.");
                report.transferred = true;
                var effect = new GameObject("Late Spawned VFX Light"); SceneManager.MoveGameObjectToScene(effect, preview);
                var flash = effect.AddComponent<Light>(); flash.type = LightType.Point; flash.shadows = LightShadows.Soft;
                flash.intensity = 3; flash.range = 12; flash.transform.position = center + Vector3.up;
                budget.RegisterHierarchy(effect, true); budget.ApplyBudget();
                if (flash.shadows != LightShadows.None || !flash.enabled || flash.intensity != 3 || flash.range != 12)
                    throw new InvalidOperationException("VFX shadow suppression changed light illumination.");
                budget.allowVfxShadows = true; budget.ApplyBudget();
                if (flash.shadows != LightShadows.None)
                    throw new InvalidOperationException("A High-tier Point Light exceeded the atlas area budget.");
                budget.allowVfxShadows = false; budget.ApplyBudget();
                report.vfxLightPreserved = true;
                report.budgetFaces = budget.SelectedShadowFaces;
                if (report.budgetFaces > budget.maximumShadowFaces || report.budgetFaces == 0)
                    throw new InvalidOperationException("No useful bounded environment shadows remained.");
                RenderArena(preview, center, report);
                if (report.renders != 4 || report.visibleFaces > budget.maximumShadowFaces || report.atlasMessages.Count != 0)
                    throw new InvalidOperationException("Combined arena render still exceeded the shadow budget.");
            }
            finally
            {
                if (budget != null)
                {
                    // Non-ExecuteAlways runtime behaviours do not receive the
                    // Play Mode lifecycle in these isolated Editor fixtures.
                    budget.Release(); UnityEngine.Object.DestroyImmediate(budget.gameObject);
                }
                EditorSceneManager.ClosePreviewScene(loading);
            }
            foreach (var pair in originals)
                if (pair.Key != null && pair.Key.shadows != pair.Value) throw new InvalidOperationException("Original light shadows were not restored.");
            report.originalShadowsRestored = true;
        }
        catch (Exception error) { report.error = error.ToString(); Debug.LogException(error); }
        finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
        Directory.CreateDirectory("Library/CombatValidation");
        File.WriteAllText("Library/CombatValidation/CombatShadows.json", JsonUtility.ToJson(report, true));
        Debug.Log("Combat shadow audit: " + report.lights.Count + " local lights, " + report.budgetFaces + " budgeted faces, " +
            report.atlasMessages.Count + " atlas warnings; " + (string.IsNullOrEmpty(report.error) ? "PASS" : "FAIL"));
    }
    static void RenderArena(Scene scene, Vector3 center, Report report)
    {
        var obj = new GameObject("Combat Shadow Test Camera") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(obj, scene);
        var camera = obj.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
        camera.cameraType = CameraType.Game; camera.fieldOfView = 65; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.GetUniversalAdditionalCameraData().renderShadows = true;
        var texture = new RenderTexture(320, 180, 24); camera.targetTexture = texture;
        Application.LogCallback listener = (message, stack, type) => { if (message.Contains("Reduced additional punctual") || message.Contains("Too many additional punctual")) report.atlasMessages.Add(message); };
        Action<ScriptableRenderContext, Camera> before = (context, rendering) =>
        {
            if (rendering != camera || !camera.TryGetCullingParameters(out var parameters)) return;
            var culled = context.Cull(ref parameters); int faces = 0;
            foreach (var visible in culled.visibleLights)
                if (visible.light != null && visible.light.shadows != LightShadows.None)
                    faces += visible.lightType == LightType.Point ? 6 : visible.lightType == LightType.Spot ? 1 : 0;
            report.visibleFaces = Mathf.Max(report.visibleFaces, faces); report.renders++;
        };
        try
        {
            Application.logMessageReceived += listener; RenderPipelineManager.beginCameraRendering += before;
            Physics.SyncTransforms();
            foreach (Vector3 offset in new[] { new Vector3(0,2,-6), new Vector3(6,2,0), new Vector3(0,3,8), new Vector3(-8,4,0) })
            { camera.transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(center + Vector3.up - (center + offset))); camera.Render(); }
        }
        finally
        {
            Application.logMessageReceived -= listener; RenderPipelineManager.beginCameraRendering -= before;
            camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(obj); texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
