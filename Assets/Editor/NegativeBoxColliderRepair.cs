using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Audits resolved collider instances, including scale inherited through prefabs.</summary>
public static class NegativeBoxColliderRepair
{
    const string Request = "Library/CombatValidation/NegativeBoxes.request";
    const string ReportPath = "Library/CombatValidation/NegativeBoxes.json";
    const string RepairRequest = "Library/CombatValidation/RepairNegativeBoxes.request";
    const string RepairResult = "Library/CombatValidation/RepairNegativeBoxes.json";
    [Serializable] public class Entry
    {
        public string scene, hierarchy, prefab, scripts;
        public long colliderId;
        public Vector3 scale, size;
        public bool animated, rigidbody, trigger;
    }
    [Serializable] public class Report
    {
        public int scenes, boxes, invalid;
        public bool positiveProxyFixture;
        public int fixtureRaycasts;
        public bool chunkConversionFixture;
        public List<Entry> entries = new List<Entry>();
        public string error;
    }
    [InitializeOnLoadMethod]
    static void Schedule() { if (File.Exists(Request) || File.Exists(RepairRequest)) EditorApplication.delayCall += RunRequested; }
    static void RunRequested()
    {
        if (!File.Exists(Request) && !File.Exists(RepairRequest)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        if (File.Exists(RepairRequest)) { File.Delete(RepairRequest); Repair(); }
        else { File.Delete(Request); Audit(); }
    }
    [MenuItem("Tools/Adventure/Repair/Audit Negative Box Colliders")]
    public static void Audit()
    {
        var report = new Report();
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
            Fixture(report);
            var paths = new HashSet<string>(Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')));
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) paths.Add(scene.path);
            foreach (string path in paths.OrderBy(p => p))
            {
                File.WriteAllText("Library/CombatValidation/NegativeBoxes.progress", path);
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    report.scenes++;
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
                    {
                        report.boxes++;
                        if (!Invalid(box)) continue;
                        report.invalid++;
                        report.entries.Add(new Entry { scene = path, hierarchy = Hierarchy(box.transform),
                            colliderId = (long)GlobalObjectId.GetGlobalObjectIdSlow(box).targetObjectId,
                            prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(box),
                            scripts = string.Join(",", box.GetComponents<MonoBehaviour>().Where(s => s != null).Select(s => s.GetType().Name)),
                            animated = box.GetComponentInParent<Animator>() != null, rigidbody = box.attachedRigidbody != null,
                            trigger = box.isTrigger, scale = box.transform.lossyScale, size = box.size });
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
        catch (Exception error) { report.error = error.ToString(); Debug.LogException(error); }
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log("Box collider audit: " + report.scenes + " scenes, " + report.boxes + " boxes, " + report.invalid + " invalid. " + ReportPath);
    }
    public static bool Invalid(BoxCollider box) => Negative(box.transform.lossyScale) || Negative(box.size);
    static bool Negative(Vector3 value) => value.x < 0f || value.y < 0f || value.z < 0f;
    static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    static Vector3 Signs(Vector3 value) => new Vector3(value.x < 0f ? -1f : 1f, value.y < 0f ? -1f : 1f, value.z < 0f ? -1f : 1f);
    static string Hierarchy(Transform item) => item.parent == null ? item.name : Hierarchy(item.parent) + "/" + item.name;

    static BoxCollider Proxy(BoxCollider source) => CreatePositiveBox(source, source.center, source.size);

    /// <summary>Creates a primitive directly on a valid transform; never activates a negative-scale box.</summary>
    public static BoxCollider CreatePositiveBox(Collider source, Vector3 center, Vector3 size)
    {
        GameObject owner = source.gameObject;
        if (Negative(source.transform.lossyScale))
        {
            owner = new GameObject("Positive Scale Collision") { layer = source.gameObject.layer, tag = source.gameObject.tag };
            GameObjectUtility.SetStaticEditorFlags(owner, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
            owner.transform.SetParent(source.transform, false);
            owner.transform.localPosition = center;
            owner.transform.localScale = Signs(source.transform.lossyScale);
            center = Vector3.zero;
        }
        var box = owner.AddComponent<BoxCollider>();
        box.center = center; box.size = Abs(size);
        box.enabled = source.enabled; box.isTrigger = source.isTrigger;
        box.sharedMaterial = source.sharedMaterial; box.contactOffset = source.contactOffset;
        box.includeLayers = source.includeLayers; box.excludeLayers = source.excludeLayers;
        box.layerOverridePriority = source.layerOverridePriority; box.providesContacts = source.providesContacts;
        return box;
    }
    /// <summary>Retains the visual transform and uses a symmetric primitive on a positive-scale child.</summary>
    public static BoxCollider MakePositive(BoxCollider source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (!Negative(source.transform.lossyScale)) { source.size = Abs(source.size); return source; }
        var before = Corners(source);
        var replacement = Proxy(source);
        if (Invalid(replacement) || CornerError(before, Corners(replacement)) > .001f)
        {
            UnityEngine.Object.DestroyImmediate(replacement.gameObject);
            throw new InvalidOperationException("Cannot retain box geometry at " + Hierarchy(source.transform));
        }
        return replacement;
    }
    static float CornerError(Vector3[] before, Vector3[] after) => before.Max(point => after.Min(p => Vector3.Distance(p, point)));

    [Serializable] class RepairReport
    {
        public int scenes, repaired, reloaded, remappedReferences, arenaBoxes;
        public float maximumCornerError;
        public bool fixturePassed, arenaTransferPassed;
        public string backup, error;
        public List<string> repairedScenes = new List<string>();
        public List<string> warningsAfterReload = new List<string>();
    }
    class SavedBox { public string id; public Vector3[] corners; }

    [MenuItem("Tools/Adventure/Repair/Fix Negative Box Colliders")]
    public static void Repair()
    {
        var result = new RepairReport();
        var active = SceneManager.GetActiveScene();
        var selection = Selection.objects;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
            var audit = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
            if (!string.IsNullOrEmpty(audit.error) || !audit.positiveProxyFixture)
                throw new InvalidOperationException("Complete the collider audit first.");
            var fixture = new Report(); Fixture(fixture); result.fixturePassed = fixture.positiveProxyFixture;
            result.backup = "Tools/Colliders/BeforePositiveBoxes-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(result.backup);
            File.Copy(ReportPath, result.backup + "/BeforeAudit.json");
            foreach (string path in audit.entries.Select(e => e.scene).Distinct())
            {
                File.WriteAllText("Library/CombatValidation/NegativeBoxes.progress", "Repair: " + path);
                string backupPath = result.backup + "/" + path;
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(path, backupPath);
                var scene = SceneManager.GetSceneByPath(path);
                bool open = scene.IsValid() && scene.isLoaded;
                if (open && scene.isDirty && !EditorSceneManager.SaveScene(scene, backupPath + ".Unsaved.unity", true))
                    throw new IOException("Could not back up current scene changes: " + path);
                if (!open) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var saved = new List<SavedBox>();
                try
                {
                    var boxes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider>(true)).Where(Invalid).ToArray();
                    var references = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                    foreach (var old in boxes)
                    {
                        // All audited failures are static environment. Do not silently relocate a gameplay hitbox.
                        if (old.attachedRigidbody != null || old.isTrigger || old.GetComponentInParent<Animator>() != null ||
                            old.GetComponents<MonoBehaviour>().Any(s => s != null))
                            throw new InvalidOperationException("Review gameplay collider separately: " + Hierarchy(old.transform));
                        var corners = Corners(old);
                        var box = MakePositive(old);
                        result.maximumCornerError = Mathf.Max(result.maximumCornerError, CornerError(corners, Corners(box)));
                        if (box != old) references.Add(old, box);
                        else EditorUtility.SetDirty(box);
                        saved.Add(new SavedBox { corners = corners, id = "" });
                    }
                    result.remappedReferences += RemapReferences(scene, references);
                    foreach (var pair in references) UnityEngine.Object.DestroyImmediate(pair.Key);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                    // Obtain IDs after Unity assigns persistent IDs to the new child components.
                    int index = 0;
                    foreach (var old in boxes)
                    {
                        // Destroyed Unity objects still retain their managed dictionary identity.
                        var box = references.TryGetValue(old, out var replacement) ? (BoxCollider)replacement : old;
                        saved[index++].id = GlobalObjectId.GetGlobalObjectIdSlow(box).ToString();
                    }
                    result.repaired += boxes.Length;
                    result.scenes++; result.repairedScenes.Add(path);
                }
                finally
                {
                    if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                    if (!open) EditorSceneManager.CloseScene(scene, true);
                }
                ValidateReload(path, saved, result);
            }
            ValidateArena(result);
            if (result.reloaded != result.repaired || result.warningsAfterReload.Count != 0)
                throw new InvalidOperationException("Collider reload validation failed.");
        }
        catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            Selection.objects = selection.Where(o => o != null).ToArray();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(RepairResult));
        File.WriteAllText(RepairResult, JsonUtility.ToJson(result, true));
        Debug.Log("Positive-scale BoxCollider repair: " + result.repaired + " fixed, " + result.reloaded + " reloaded; " +
            (string.IsNullOrEmpty(result.error) ? "PASS" : "FAIL") + ". " + RepairResult);
    }

    static int RemapReferences(Scene scene, Dictionary<UnityEngine.Object, UnityEngine.Object> replacements)
    {
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script == null) continue;
            using (var serialized = new SerializedObject(script))
            {
                var property = serialized.GetIterator(); bool changed = false;
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                        replacements.TryGetValue(property.objectReferenceValue, out var replacement))
                    { property.objectReferenceValue = replacement; changed = true; count++; }
                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.IsPartOfPrefabInstance(script)) PrefabUtility.RecordPrefabInstancePropertyModifications(script);
                }
            }
        }
        return count;
    }
    static void ValidateReload(string path, List<SavedBox> expected, RepairReport result)
    {
        Application.LogCallback listener = (message, stack, type) =>
        { if (message.Contains("BoxCollider does not support negative")) result.warningsAfterReload.Add(message); };
        Application.logMessageReceived += listener;
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene(path);
            var boxes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider>(true)).ToArray();
            if (boxes.Any(Invalid)) throw new InvalidOperationException("Negative collider survived reload: " + path);
            var byId = boxes.ToDictionary(b => GlobalObjectId.GetGlobalObjectIdSlow(b).ToString());
            foreach (var saved in expected)
            {
                if (!byId.TryGetValue(saved.id, out var box)) throw new InvalidOperationException("Repaired collider missing after reload: " + saved.id);
                float error = CornerError(saved.corners, Corners(box));
                result.maximumCornerError = Mathf.Max(result.maximumCornerError, error);
                if (error > .001f) throw new InvalidOperationException("Saved collider changed shape: " + Hierarchy(box.transform));
                result.reloaded++;
            }
            Physics.SyncTransforms();
        }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            Application.logMessageReceived -= listener;
        }
    }
    static void ValidateArena(RepairReport result)
    {
        var arena = EditorSceneManager.NewPreviewScene();
        try
        {
            var arenaRoot = new GameObject("CombatArenaRoot"); SceneManager.MoveGameObjectToScene(arenaRoot, arena);
            arenaRoot.transform.position = new Vector3(-18.6f, 280.56f, -65);
            foreach (string path in result.repairedScenes.Where(p => p.Contains("MapChunksRefined/")))
            {
                var chunk = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var root in chunk.GetRootGameObjects())
                    {
                        var boxes = root.GetComponentsInChildren<BoxCollider>(true);
                        var corners = boxes.Select(Corners).ToArray();
                        SceneManager.MoveGameObjectToScene(root, arena);
                        root.transform.SetParent(arenaRoot.transform, true);
                        for (int i = 0; i < boxes.Length; i++)
                        {
                            if (Invalid(boxes[i]) || CornerError(corners[i], Corners(boxes[i])) > .001f)
                                throw new InvalidOperationException("Combat transfer changed collider: " + Hierarchy(boxes[i].transform));
                            result.arenaBoxes++;
                        }
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(chunk); }
            }
            result.arenaTransferPassed = true;
        }
        finally { EditorSceneManager.ClosePreviewScene(arena); }
    }
    static Vector3[] Corners(BoxCollider box)
    {
        var result = new Vector3[8]; var extent = Abs(box.size) * .5f;
        for (int i = 0; i < 8; i++) result[i] = box.transform.TransformPoint(box.center + Vector3.Scale(extent,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
        return result;
    }
    static void Fixture(Report report)
    {
        var active = SceneManager.GetActiveScene();
        // Isolated geometry only: no campaign objects, Play Mode, or save-data writes.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            foreach (Vector3 scale in new[] { new Vector3(-1, 1, 1), new Vector3(1,-1,1), new Vector3(1,1,-1), new Vector3(-2,.5f,3), new Vector3(-1,-1,1) })
            {
                var root = new GameObject("Mirrored Parent"); SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);
                root.transform.position = new Vector3(10,30,10);
                root.transform.localScale = scale; root.transform.rotation = Quaternion.Euler(14,37,9);
                var child = new GameObject("Rotated Child"); child.transform.SetParent(root.transform, false);
                child.transform.localRotation = Quaternion.Euler(21,43,15);
                var old = child.AddComponent<BoxCollider>(); old.size = new Vector3(2,3,1); old.center = new Vector3(.3f,.7f,-.4f);
                var corners = Corners(old); var fixedBox = Proxy(old);
                if (Invalid(fixedBox)) throw new InvalidOperationException("Positive collider proxy still has negative world scale: " + fixedBox.transform.lossyScale);
                var fixedCorners = Corners(fixedBox);
                foreach (Vector3 point in corners)
                    if (fixedCorners.Min(p => Vector3.Distance(p, point)) > .001f) throw new InvalidOperationException("Proxy changed collider geometry.");
                UnityEngine.Object.DestroyImmediate(old);
                root.SetActive(true);
                Physics.SyncTransforms();
                if (fixedBox.bounds.size.sqrMagnitude < .01f) throw new InvalidOperationException("Proxy has no physics geometry.");
                foreach (Vector3 direction in new[] { Vector3.up, Vector3.right, Vector3.forward })
                {
                    Vector3 world = fixedBox.transform.rotation * direction;
                    float distance = fixedBox.bounds.size.magnitude + 2;
                    if (!fixedBox.Raycast(new Ray(fixedBox.bounds.center + world * distance, -world), out var hit, distance * 2))
                        throw new InvalidOperationException("Proxy raycast missed an axis.");
                    if (hit.distance <= 0 || float.IsNaN(hit.distance)) throw new InvalidOperationException("Invalid proxy raycast distance.");
                    report.fixtureRaycasts++;
                }
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(cube, scene);
            cube.SetActive(false);
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            cube.transform.localScale = new Vector3(-2, 1, 3);
            var mesh = cube.AddComponent<MeshCollider>(); mesh.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            var conversion = typeof(MapStreamingBuilder).GetMethod("TryBox", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (conversion == null || !(bool)conversion.Invoke(null, new object[] { mesh }) || cube.GetComponent<MeshCollider>() != null ||
                cube.GetComponentsInChildren<BoxCollider>(true).Length != 1 || cube.GetComponentsInChildren<BoxCollider>(true).Any(Invalid))
                throw new InvalidOperationException("Chunk converter recreated an invalid primitive collider.");
            report.chunkConversionFixture = true;
            report.positiveProxyFixture = true;
        }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
