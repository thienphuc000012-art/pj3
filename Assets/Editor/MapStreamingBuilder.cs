using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapStreamingBuilder
{
    const string Map = "Assets/Scenes/mapgame.unity";
    const string Folder = "Assets/Scenes/MapChunks";
    const string Request = "Library/CombatValidation/MapStreaming.request";
    const string Result = "Library/CombatValidation/MapStreaming.result.txt";
    const float Cell = 40;
    [InitializeOnLoadMethod] static void Initialize() { EditorApplication.delayCall += Requested; EditorApplication.delayCall += ValidateRequested; }
    static void ValidateRequested()
    {
        const string request = "Library/CombatValidation/MapStreamingValidation.request";
        if (!File.Exists(request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += ValidateRequested; return; }
        File.Delete(request);
        try { Validate(); } catch (Exception ex) { File.WriteAllText("Library/CombatValidation/MapStreamingValidation.result.txt", ex.ToString()); Debug.LogException(ex); }
    }
    [MenuItem("Tools/Adventure/Streaming/Open all chunks for editing")]
    static void OpenChunks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var map = SceneManager.GetSceneByPath(Map);
        if (!map.IsValid()) map = EditorSceneManager.OpenScene(Map, OpenSceneMode.Additive);
        var streamer = map.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MapChunkStreamer>(true)).Single();
        foreach (var chunk in streamer.chunks)
            if (!SceneManager.GetSceneByPath(chunk.scenePath).isLoaded) EditorSceneManager.OpenScene(chunk.scenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(map);
    }
    [MenuItem("Tools/Adventure/Streaming/Close chunks before Play")]
    static void CloseChunks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(x => x.path.StartsWith(Folder + "/")).ToArray();
        if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(scenes)) return;
        foreach (var scene in scenes) EditorSceneManager.CloseScene(scene, true);
    }
    [MenuItem("Tools/Adventure/Streaming/Update chunk bounds after editing")]
    static void UpdateBounds()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var map = SceneManager.GetSceneByPath(Map);
        if (!map.IsValid()) map = EditorSceneManager.OpenScene(Map, OpenSceneMode.Additive);
        var streamer = map.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MapChunkStreamer>(true)).Single();
        Undo.RecordObject(streamer, "Update streaming bounds");
        foreach (var chunk in streamer.chunks)
        {
            var scene = SceneManager.GetSceneByPath(chunk.scenePath);
            bool preview = !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenPreviewScene(chunk.scenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Length == 0) continue;
                var bounds = GetBounds(roots[0]);
                foreach (var root in roots.Skip(1)) bounds.Encapsulate(GetBounds(root));
                chunk.bounds = bounds;
            }
            finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
        }
        EditorSceneManager.MarkSceneDirty(map);
    }
    [MenuItem("Tools/Adventure/Streaming/Validate chunks")]
    public static void Validate()
    {
        var report = new List<string>();
        var scene = EditorSceneManager.OpenPreviewScene(Map);
        try
        {
            var setup = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MapGameSetup>(true)).Single();
            var streamer = setup.GetComponent<MapChunkStreamer>();
            if (streamer == null || streamer.chunks.Count == 0) throw new Exception("Missing chunks.");
            var player = setup.player != null ? setup.player : scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerScript>(true)).Single();
            var spawn = player.transform.position;
            report.Add("Spawn: " + spawn);
            report.Add("Initial chunks: " + streamer.chunks.Count(x => MapChunkStreamer.Distance(x, spawn) <= streamer.loadDistance) + "/" + streamer.chunks.Count);
            if (streamer.safetyDistance >= streamer.loadDistance || streamer.unloadDistance <= streamer.loadDistance) throw new Exception("Invalid load/unload hysteresis.");
            var paths = new HashSet<string>();
            int roots = 0, mesh = 0, boxes = 0, triggers = 0, missing = 0, rendered = 0;
            foreach (var chunk in streamer.chunks)
            {
                if (!paths.Add(chunk.scenePath)) throw new Exception("Duplicate chunk.");
                if (!EditorBuildSettings.scenes.Any(x => x.enabled && x.path == chunk.scenePath)) throw new Exception("Chunk not in build: " + chunk.scenePath);
                if (MapChunkStreamer.Distance(chunk, chunk.bounds.center) != 0 || MapChunkStreamer.Distance(chunk, chunk.bounds.max + Vector3.right * 200) < 199) throw new Exception("Distance test failed.");
                var preview = EditorSceneManager.OpenPreviewScene(chunk.scenePath);
                try
                {
                    var objects = preview.GetRootGameObjects(); roots += objects.Length;
                    foreach (var root in objects)
                    {
                        missing += root.GetComponentsInChildren<Transform>(true).Sum(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject));
                        mesh += root.GetComponentsInChildren<MeshCollider>(true).Length;
                        boxes += root.GetComponentsInChildren<BoxCollider>(true).Length;
                        triggers += root.GetComponentsInChildren<Collider>(true).Count(x => x.isTrigger);
                        rendered += root.GetComponentsInChildren<Renderer>(true).Length;
                    }
                    if (objects.Any(x => x.GetComponentInChildren<PlayerScript>(true) != null || x.GetComponentInChildren<MapGameSetup>(true) != null)) throw new Exception("Gameplay root moved to chunk.");
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            if (missing != 0) throw new Exception("Missing scripts: " + missing);
            report.Add("PASS: enabled build entries, unique paths, distance/hysteresis, no missing scripts, no duplicated map/player systems.");
            report.Add("Roots: " + roots + "; renderers: " + rendered + "; mesh colliders: " + mesh + "; box colliders: " + boxes + "; triggers: " + triggers);
            File.WriteAllLines("Library/CombatValidation/MapStreamingValidation.result.txt", report);
            Debug.Log(string.Join("\n", report));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Requested; return; }
        File.Delete(Request);
        try { Build(); } catch (Exception ex) { File.WriteAllText(Result, ex.ToString()); Debug.LogException(ex); }
    }
    [MenuItem("Tools/Adventure/Streaming/Convert mapgame to chunks")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before converting.");
        var map = SceneManager.GetSceneByPath(Map);
        if (map.IsValid() && map.isDirty) throw new InvalidOperationException("Save mapgame first; conversion will not overwrite unsaved edits.");
        if (Directory.Exists(Folder)) throw new InvalidOperationException("MapChunks already exists. Use the existing chunks; conversion is one-time only.");
        if (!map.IsValid()) map = EditorSceneManager.OpenScene(Map, OpenSceneMode.Additive);
        var setup = map.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MapGameSetup>(true)).Single();
        if (setup.GetComponent<MapChunkStreamer>() != null) throw new InvalidOperationException("Map already has streaming.");
        var backup = "Tools/MapStreaming/Backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(Map, backup + "/mapgame.unity");
        File.Copy("ProjectSettings/EditorBuildSettings.asset", backup + "/EditorBuildSettings.asset");
        // Freeze the old full ID before any hierarchy changes, retaining existing save files.
        foreach (var point in map.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<WorldInteraction>(true)))
            point.savedWorldId = point.Id;
        var groupNames = new HashSet<string> { "Meshes", "PointLights", "SpotLights", "FogSheets", "FloorDirt" };
        var candidates = map.GetRootGameObjects().Where(x => groupNames.Contains(x.name))
            .SelectMany(x => x.transform.Cast<Transform>()).Select(x => x.gameObject).ToList();
        // Never split a prefab instance or a child hierarchy containing gameplay references.
        var owners = new Dictionary<GameObject, GameObject>();
        foreach (var obj in candidates)
            foreach (var t in obj.GetComponentsInChildren<Transform>(true)) owners[t.gameObject] = obj;
        var pinned = new HashSet<GameObject>();
        foreach (var root in map.GetRootGameObjects())
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform) continue;
            owners.TryGetValue(component.gameObject, out var source);
            var iterator = new SerializedObject(component).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = iterator.objectReferenceValue;
                var go = value as GameObject ?? (value as Component)?.gameObject;
                if (go == null || go.scene != map) continue;
                owners.TryGetValue(go, out var dest);
                if (source == dest) continue;
                // Keep cross-object references in the persistent scene instead of losing them.
                if (source != null) pinned.Add(source);
                if (dest != null) pinned.Add(dest);
            }
        }
        candidates.RemoveAll(x => pinned.Contains(x));
        if (candidates.Count == 0) throw new InvalidOperationException("No independent environment objects to stream.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var streamer = setup.gameObject.AddComponent<MapChunkStreamer>();
        streamer.loadDistance = 45; streamer.unloadDistance = 75; streamer.safetyDistance = 15;
        var groups = candidates.GroupBy(x => { var b = GetBounds(x); return new Vector2Int(Mathf.FloorToInt(b.center.x / Cell), Mathf.FloorToInt(b.center.z / Cell)); }).ToArray();
        var build = EditorBuildSettings.scenes.ToList();
        int objects = 0, colliders = 0, lights = 0;
        foreach (var group in groups)
        {
            string path = Folder + "/Map_" + group.Key.x + "_" + group.Key.y + ".unity";
            var chunk = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var bounds = GetBounds(group.First());
            foreach (var obj in group)
            {
                bounds.Encapsulate(GetBounds(obj));
                // Root objects must preserve inherited inactive state when detached.
                bool active = obj.activeInHierarchy;
                obj.transform.SetParent(null, true);
                obj.SetActive(active);
                SceneManager.MoveGameObjectToScene(obj, chunk);
                foreach (var mc in obj.GetComponentsInChildren<MeshCollider>(true))
                    if (TryBox(mc)) colliders++;
                foreach (var light in obj.GetComponentsInChildren<Light>(true))
                    if (light.type != LightType.Directional)
                    {
                        light.shadowCustomResolution = 256;
                        light.shadowResolution = (UnityEngine.Rendering.LightShadowResolution)256;
                        var data = light.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                        if (data == null) data = light.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                        var serialized = new SerializedObject(data);
                        serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = -1;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        lights++;
                    }
                objects++;
            }
            if (!EditorSceneManager.SaveScene(chunk, path)) throw new IOException("Failed to save " + path);
            streamer.chunks.Add(new MapChunkStreamer.Chunk { scenePath = path, bounds = bounds });
            build.Add(new EditorBuildSettingsScene(path, true));
            EditorSceneManager.CloseScene(chunk, true);
        }
        EditorBuildSettings.scenes = build.ToArray();
        EditorSceneManager.MarkSceneDirty(map);
        if (!EditorSceneManager.SaveScene(map)) throw new IOException("Failed to save mapgame.");
        AssetDatabase.SaveAssets();
        string report = "SUCCESS\nChunks: " + groups.Length + "\nMoved roots: " + objects + "\nPinned roots (cross references): " + pinned.Count +
            "\nExact box colliders simplified: " + colliders + "\nLocal light resolutions capped: " + lights + "\nBackup: " + backup;
        File.WriteAllText(Result, report); Debug.Log(report);
    }
    static Bounds GetBounds(GameObject obj)
    {
        var bounds = new Bounds(obj.transform.position, Vector3.zero);
        bool any = false;
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
        { if (!any) { bounds = renderer.bounds; any = true; } else bounds.Encapsulate(renderer.bounds); }
        foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) if (collider.enabled && collider.gameObject.activeInHierarchy) bounds.Encapsulate(collider.bounds);
        foreach (var light in obj.GetComponentsInChildren<Light>(true)) bounds.Encapsulate(new Bounds(light.transform.position, Vector3.one * light.range * 2));
        return bounds;
    }
    static bool TryBox(MeshCollider collider)
    {
        var mesh = collider.sharedMesh;
        if (mesh == null || !mesh.isReadable || collider.convex || collider.isTrigger || mesh.triangles.Length != 36) return false;
        var b = mesh.bounds;
        // Only exact closed cube geometry; never approximate stairs/terrain with a box.
        var vertices = mesh.vertices.Distinct().ToArray();
        if (vertices.Length != 8 || b.size.x <= 0 || b.size.y <= 0 || b.size.z <= 0) return false;
        if (vertices.Any(v => Mathf.Abs(Mathf.Abs(v.x-b.center.x)-b.extents.x) > .0001f || Mathf.Abs(Mathf.Abs(v.y-b.center.y)-b.extents.y) > .0001f || Mathf.Abs(Mathf.Abs(v.z-b.center.z)-b.extents.z) > .0001f)) return false;
        var box = collider.gameObject.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size; box.sharedMaterial = collider.sharedMaterial; box.enabled = collider.enabled;
        box.includeLayers = collider.includeLayers; box.excludeLayers = collider.excludeLayers; box.layerOverridePriority = collider.layerOverridePriority;
        UnityEngine.Object.DestroyImmediate(collider);
        return true;
    }
}
