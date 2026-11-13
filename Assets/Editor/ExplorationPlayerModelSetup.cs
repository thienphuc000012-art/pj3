using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Use mapgame's exploration prefab in the other campaign maps, preserving scene camera wiring.</summary>
public static class ExplorationPlayerModelSetup
{
    const string Request = "Library/CombatValidation/ExplorationPlayerModel.request";
    const string Result = "Library/CombatValidation/ExplorationPlayerModel.result.txt";
    const string PlayerPrefab = "Assets/Adventure/Prefabs/Exploration/ExplorationPlayer.prefab";
    static string report;

    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request)) EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        File.Delete(Request); Setup();
    }

    [MenuItem("Tools/Adventure/Repair/Use mapgame Player Model in Exploration Maps")]
    public static void Setup()
    {
        report = "";
        Scene source = default;
        bool closeSource = false;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before repairing scene references.");
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            var config = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
            Check(config != null, "Campaign config missing.");
            string sourcePath = ScenePath(config.mapScene);
            source = SceneManager.GetSceneByPath(sourcePath);
            closeSource = !source.IsValid() || !source.isLoaded;
            if (closeSource) source = EditorSceneManager.OpenPreviewScene(sourcePath);
            var reference = Players(source).Single();
            string backup = "Tools/ExplorationPlayer/BeforeRepair-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            if (File.Exists(PlayerPrefab)) File.Copy(PlayerPrefab, backup + "/ExplorationPlayer.prefab");
            var prefab = CreateSharedPlayer(reference);
            Check(Appearance(reference) == Appearance(prefab.GetComponent<PlayerScript>()), "Exported prefab does not match mapgame.");
            Check(reference.animator.avatar == prefab.GetComponent<PlayerScript>().animator.avatar, "Avatar does not match mapgame.");
            report += "Reference: " + sourcePath + " -> " + PlayerPrefab + " (includes scene appearance overrides)\n";
            foreach (var map in config.additionalMapScenes.Distinct().Where(name => name != config.mapScene))
            {
                string path = ScenePath(map);
                Scene scene = SceneManager.GetSceneByPath(path);
                bool open = scene.IsValid() && scene.isLoaded;
                var active = SceneManager.GetActiveScene();
                if (open && scene.isDirty) EditorSceneManager.SaveScene(scene, backup + "/" + map + ".unity", true);
                else File.Copy(path, backup + "/" + map + ".unity");
                if (!open) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var old = Players(scene).Single();
                    Vector3 position = old.transform.position;
                    Quaternion rotation = old.transform.rotation;
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject) != PlayerPrefab || Appearance(old) != Appearance(reference))
                        ReplacePlayer(scene, old, prefab);
                    var player = Players(scene).Single();
                    Check(Vector3.Distance(player.transform.position, position) < .0001f && Quaternion.Angle(player.transform.rotation, rotation) < .01f,
                        "Replacing the model changed the map spawn.");
                    ValidateWiring(scene, player, reference);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Check(EditorSceneManager.SaveScene(scene), "Could not save " + path);
                    report += "PASS " + map + ": shared prefab, same meshes/materials/avatar/controller, camera targets and player references; spawn kept at " + position + ".\n";
                }
                finally
                {
                    if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                    if (!open) EditorSceneManager.CloseScene(scene, true);
                }
                // Read back the serialized result in isolation, just as a later map load will.
                var checkScene = EditorSceneManager.OpenPreviewScene(path);
                try { ValidateWiring(checkScene, Players(checkScene).Single(), reference); }
                finally { EditorSceneManager.ClosePreviewScene(checkScene); }
            }
            report += "PASS: saved scene reopened successfully. No campaign or player saves changed. Backup: " + backup;
            File.WriteAllText(Result, "PASS\n" + report); Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception error)
        { Directory.CreateDirectory(Path.GetDirectoryName(Result)); File.WriteAllText(Result, "FAIL\n" + report + error); Debug.LogException(error); }
        finally { if (closeSource && source.IsValid()) EditorSceneManager.ClosePreviewScene(source); }
    }

    static GameObject CreateSharedPlayer(PlayerScript reference)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefab));
        AssetDatabase.Refresh();
        var staging = new GameObject("Exploration Player Export"); staging.SetActive(false);
        var clone = Object.Instantiate(reference.gameObject, staging.transform, false);
        try
        {
            clone.GetComponent<PlayerScript>().MCC = null; // Each map owns its camera.
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (PrefabUtility.IsPartOfPrefabInstance(clone))
                PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Check(PrefabUtility.SaveAsPrefabAsset(clone, PlayerPrefab) != null, "Could not export exploration prefab.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        }
        finally { Object.DestroyImmediate(staging); }
    }

    static void ReplacePlayer(Scene scene, PlayerScript old, GameObject prefab)
    {
        var created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        try
        {
            var player = created.GetComponent<PlayerScript>();
            created.name = old.name;
            created.SetActive(old.gameObject.activeSelf);
            created.transform.SetParent(old.transform.parent, false);
            created.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            created.transform.localScale = old.transform.localScale;
            created.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            player.MCC = old.MCC;
            Check(player.MCC != null, "Original player has no map camera controller.");
            var references = new Dictionary<Object, Object>();
            var oldObjects = new HashSet<Object>();
            foreach (var child in old.GetComponentsInChildren<Transform>(true))
            {
                oldObjects.Add(child); oldObjects.Add(child.gameObject);
                string relative = AnimationUtility.CalculateTransformPath(child, old.transform);
                var replacement = string.IsNullOrEmpty(relative) ? player.transform : player.transform.Find(relative);
                if (replacement != null) { references[child] = replacement; references[child.gameObject] = replacement.gameObject; }
                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    oldObjects.Add(component);
                    if (replacement == null) continue;
                    var candidates = replacement.GetComponents(component.GetType());
                    int index = Array.IndexOf(child.GetComponents(component.GetType()), component);
                    if (index < candidates.Length) references[component] = candidates[index];
                }
            }
            var changes = new List<(Component component, string path, Object replacement)>();
            foreach (var component in Components(scene))
            {
                if (component == null || component.transform.IsChildOf(old.transform) || component.transform.IsChildOf(created.transform)) continue;
                var serialized = new SerializedObject(component);
                var iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object previous = iterator.objectReferenceValue;
                    if (previous == null || !oldObjects.Contains(previous)) continue;
                    Check(references.ContainsKey(previous), "Cannot rebind " + component.name + "." + iterator.propertyPath + " -> " + previous.name);
                    changes.Add((component, iterator.propertyPath, references[previous]));
                }
            }
            // Resolve every reference before making changes, so missing paths cannot leave a partial rebind.
            foreach (var change in changes)
            {
                var serialized = new SerializedObject(change.component);
                serialized.FindProperty(change.path).objectReferenceValue = change.replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(change.component);
                if (PrefabUtility.IsPartOfPrefabInstance(change.component)) PrefabUtility.RecordPrefabInstancePropertyModifications(change.component);
            }
            foreach (var setup in Components(scene).OfType<MapGameSetup>())
            { setup.player = player; EditorUtility.SetDirty(setup); }
            EditorUtility.SetDirty(player); PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            report += "Replaced " + old.name + "; remapped " + changes.Count + " external scene references.\n";
            Object.DestroyImmediate(old.gameObject);
        }
        catch { Object.DestroyImmediate(created); throw; }
    }

    static void ValidateWiring(Scene scene, PlayerScript player, PlayerScript reference)
    {
        Check(Appearance(player) == Appearance(reference), "Appearance differs from the requested mapgame model.");
        Check(player.animator != null && player.animator.avatar == reference.animator.avatar &&
            player.animator.runtimeAnimatorController == reference.animator.runtimeAnimatorController, "Animator/avatar changed.");
        Check(player.CC != null && player.CC.transform == player.transform && player.enviromentChecker != null, "Movement/collider references missing.");
        Check(player.MCC != null && player.MCC.gameObject.scene == scene, "Camera controller is not in the destination map.");
        var cameraState = new SerializedObject(player.MCC);
        Check(cameraState.FindProperty("playerTarget").objectReferenceValue == player.transform, "Camera still targets the old player.");
        var target = cameraState.FindProperty("cameraTarget").objectReferenceValue as Transform;
        Check(target != null && target.IsChildOf(player.transform), "Camera target was lost during player replacement.");
        foreach (var camera in Components(scene).OfType<CinemachineCamera>())
            if (camera.Follow != null) Check(camera.Follow == target || !camera.Follow.IsChildOf(player.transform), "Cinemachine follows a different player child.");
        foreach (var setup in Components(scene).OfType<MapGameSetup>()) Check(setup.player == player, "MapGameSetup did not bind the shared player.");
        foreach (var script in player.GetComponents<MonoBehaviour>())
        {
            if (script == null) throw new InvalidOperationException("Missing script on the player.");
            var serialized = new SerializedObject(script); var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is Component component && !EditorUtility.IsPersistent(component))
                    Check(component.gameObject.scene == scene, "Player has a cross-scene reference: " + property.propertyPath);
        }
    }

    static string Appearance(PlayerScript player) => string.Join("\n", player.GetComponentsInChildren<Renderer>(true)
        .Where(renderer => renderer.enabled && (renderer is SkinnedMeshRenderer || renderer is MeshRenderer) && ActiveWithin(renderer.transform, player.transform))
        .Select(renderer =>
        {
            Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            return AssetDatabase.GetAssetPath(mesh) + ":" + (mesh != null ? mesh.name : "") + "|" +
                string.Join(",", renderer.sharedMaterials.Select(material => AssetDatabase.GetAssetPath(material)));
        }).OrderBy(value => value));
    static bool ActiveWithin(Transform child, Transform root)
    { for (var node = child; node != null; node = node.parent) { if (!node.gameObject.activeSelf) return false; if (node == root) return true; } return false; }
    static IEnumerable<Component> Components(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true));
    static IEnumerable<PlayerScript> Players(Scene scene) => Components(scene).OfType<PlayerScript>();
    static string ScenePath(string name) => EditorBuildSettings.scenes.Select(scene => scene.path).Single(path => Path.GetFileNameWithoutExtension(path) == name);
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
