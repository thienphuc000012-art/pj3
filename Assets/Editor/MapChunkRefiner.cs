using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit, transactional refinement of environment scenes. Original scenes are
// never edited; all hierarchy operations happen in independent working copies.
public static class MapChunkRefiner
{
    const string MapPath = "Assets/Scenes/mapgame.unity";
    const string OutputFolder = "Assets/Scenes/MapChunksRefined";
    const string WorkFolder = OutputFolder + "/_SourceWork";
    const string RequestPath = "Library/CombatValidation/MapChunkRefine.request";
    const string ResultPath = "Library/CombatValidation/MapChunkRefine.result.txt";
    const float CellSize = 40f;
    const float MatrixTolerance = .001f;

    sealed class Candidate
    {
        public GameObject root;
        public Bounds bounds;
        public Transform sourceParent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool activeSelf;
        public int parent;
    }
    sealed class ObjectState
    {
        public Matrix4x4 matrix;
        public bool active;
        public bool enabled;
        public int layer;
        public string definition;
    }
    sealed class Cell
    {
        public Vector2Int coordinate;
        public readonly List<Candidate> objects = new List<Candidate>();
        public Bounds bounds;
        public string path;
        public int renderers;
        public int colliders;
    }
    sealed class ReferenceState
    {
        public Component source;
        public string property;
        public UnityEngine.Object target;
    }

    [InitializeOnLoadMethod]
    static void Install() { EditorApplication.delayCall += CheckRequest; }

    static void CheckRequest()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += CheckRequest; return; }
        File.Delete(RequestPath);
        try { Refine(); }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "FAILED; original chunk files retained.\n" + exception);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Adventure/Streaming/Refine oversized chunks")]
    public static void Refine()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before refining chunks.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save all open scenes first. Refinement will not overwrite unsaved edits.");
        if (Directory.Exists(OutputFolder) || File.Exists(OutputFolder + ".meta"))
            throw new InvalidOperationException("MapChunksRefined already exists. Review its previous result before refining again.");

        Scene originalActive = SceneManager.GetActiveScene();
        Scene map = SceneManager.GetSceneByPath(MapPath);
        bool openedMap = !map.IsValid() || !map.isLoaded;
        if (openedMap) map = EditorSceneManager.OpenScene(MapPath, OpenSceneMode.Additive);
        var setup = map.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MapGameSetup>(true)).Single();
        var streamer = setup.GetComponent<MapChunkStreamer>();
        if (streamer == null || streamer.chunks.Count == 0)
            throw new InvalidOperationException("Map has no existing chunk manifest.");
        var originalChunks = streamer.chunks.Select(chunk => new MapChunkStreamer.Chunk { scenePath = chunk.scenePath, bounds = chunk.bounds }).ToList();
        if (originalChunks.Any(chunk => !chunk.scenePath.StartsWith("Assets/Scenes/MapChunks/", StringComparison.Ordinal)))
            throw new InvalidOperationException("Only the original MapChunks manifest can be refined by this tool.");
        if (originalChunks.Select(chunk => chunk.scenePath).Distinct().Count() != originalChunks.Count ||
            originalChunks.Any(chunk => !File.Exists(chunk.scenePath)))
            throw new InvalidOperationException("The source chunk manifest has duplicate or missing scenes.");
        foreach (var chunk in originalChunks)
            if (SceneManager.GetSceneByPath(chunk.scenePath).isLoaded)
                throw new InvalidOperationException("Close the original chunk scenes before refinement (Tools > Adventure > Streaming > Close chunks before Play).");

        var player = setup.player != null ? setup.player : map.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerScript>(true)).Single();
        Vector3 spawn = player.transform.position;
        EditorBuildSettingsScene[] originalBuild = EditorBuildSettings.scenes;
        string backup = "Tools/MapStreaming/BeforeRefine-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(MapPath, backup + "/mapgame.unity");
        File.Copy("ProjectSettings/EditorBuildSettings.asset", backup + "/EditorBuildSettings.asset");
        Directory.CreateDirectory(backup + "/MapChunks");
        foreach (var chunk in originalChunks)
        {
            string name = Path.GetFileName(chunk.scenePath);
            File.Copy(chunk.scenePath, backup + "/MapChunks/" + name);
            if (File.Exists(chunk.scenePath + ".meta")) File.Copy(chunk.scenePath + ".meta", backup + "/MapChunks/" + name + ".meta");
        }

        var workingScenes = new List<Scene>();
        var outputScenes = new List<Scene>();
        var candidates = new List<Candidate>();
        var expectedByComponent = new Dictionary<Component, ObjectState>();
        var references = new List<ReferenceState>();
        int beforeInitialRenderers = 0, beforeInitialColliders = 0;
        bool committed = false;
        bool mapCommitStarted = false;
        try
        {
            Directory.CreateDirectory(WorkFolder);
            // Copy the whole YAML scene at once so same-scene object references
            // remain connected. Individual Instantiate calls would lose them.
            foreach (var chunk in originalChunks)
            {
                string copy = WorkFolder + "/" + Path.GetFileName(chunk.scenePath);
                File.Copy(chunk.scenePath, copy);
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
                var work = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
                workingScenes.Add(work);
                if (MapChunkStreamer.Distance(chunk, spawn) <= streamer.loadDistance)
                    foreach (var root in work.GetRootGameObjects())
                    {
                        beforeInitialRenderers += root.GetComponentsInChildren<Renderer>(true).Length;
                        beforeInitialColliders += root.GetComponentsInChildren<Collider>(true).Length;
                    }
            }
            var sourceHandles = new HashSet<int>(workingScenes.Select(scene => (int)scene.handle));
            var referencedGroups = FindReferencedObjects(workingScenes, sourceHandles, references);
            foreach (var scene in workingScenes)
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<PlayerScript>(true) != null || root.GetComponentInChildren<MapGameSetup>(true) != null ||
                        root.GetComponentInChildren<WorldInteraction>(true) != null)
                        throw new InvalidOperationException("Gameplay objects found in an environment chunk: " + root.name);
                    foreach (var component in Geometry(root)) expectedByComponent.Add(component, Capture(component));
                    Collect(root, referencedGroups, candidates);
                }
            if (candidates.Count == 0) throw new InvalidOperationException("No environment candidates found.");
            var owners = new Dictionary<GameObject, int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                candidates[i].parent = i;
                foreach (var transform in candidates[i].root.GetComponentsInChildren<Transform>(true)) owners.Add(transform.gameObject, i);
            }
            foreach (var reference in references)
            {
                GameObject targetObject = AsGameObject(reference.target);
                if (targetObject == null || !owners.TryGetValue(reference.source.gameObject, out int source) || !owners.TryGetValue(targetObject, out int target))
                    throw new InvalidOperationException("An environment reference would point to a discarded group: " + reference.source.name + "." + reference.property);
                Union(candidates, source, target);
            }

            var cells = MakeCells(candidates);
            if (cells.Count > 2048) throw new InvalidOperationException("Unexpectedly many cells; inspect source hierarchy before conversion.");
            foreach (var cell in cells)
            {
                cell.path = OutputFolder + "/Cell_" + cell.coordinate.x + "_" + cell.coordinate.y + ".unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                outputScenes.Add(scene);
                var parentCopies = new Dictionary<Transform, Transform>();
                foreach (var candidate in cell.objects)
                {
                    // Recreate the discarded Transform-only ancestor chain.
                    // A single world-TRS decomposition loses shear when a
                    // non-uniformly scaled ancestor has rotated descendants.
                    // Identical local chains preserve the full world matrix.
                    var parent = CopyParentChain(candidate.sourceParent, scene, parentCopies);
                    candidate.root.transform.SetParent(null, false);
                    SceneManager.MoveGameObjectToScene(candidate.root, scene);
                    candidate.root.transform.SetParent(parent, false);
                    candidate.root.transform.localPosition = candidate.localPosition;
                    candidate.root.transform.localRotation = candidate.localRotation;
                    candidate.root.transform.localScale = candidate.localScale;
                    candidate.root.SetActive(candidate.activeSelf);
                    cell.renderers += candidate.root.GetComponentsInChildren<Renderer>(true).Length;
                    cell.colliders += candidate.root.GetComponentsInChildren<Collider>(true).Length;
                }
                // Validate before writing anything that can become the manifest.
                ValidateStates(expectedByComponent.Where(pair => pair.Key.gameObject.scene == scene));
                ValidateReferences(references.Where(reference => reference.source.gameObject.scene == scene));
                if (!EditorSceneManager.SaveScene(scene, cell.path)) throw new IOException("Could not save " + cell.path);
            }

            // Save/reopen validation catches serialization problems as well as
            // transform errors. Keys are scene path + hierarchy/component index.
            var expectedSaved = CaptureSaved(outputScenes);
            var expectedReferences = CaptureSavedReferences(outputScenes);
            foreach (var scene in outputScenes) EditorSceneManager.CloseScene(scene, true);
            outputScenes.Clear();
            foreach (var cell in cells) outputScenes.Add(EditorSceneManager.OpenScene(cell.path, OpenSceneMode.Additive));
            var actualSaved = CaptureSaved(outputScenes);
            CompareSaved(expectedSaved, actualSaved);
            var actualReferences = CaptureSavedReferences(outputScenes);
            if (expectedReferences.Count != actualReferences.Count || expectedReferences.Any(pair =>
                !actualReferences.TryGetValue(pair.Key, out var target) || target != pair.Value))
                throw new InvalidOperationException("A serialized scene reference changed after save/reopen.");
            foreach (var scene in outputScenes) ValidateSavedReferences(scene);
            if (actualSaved.Count != expectedByComponent.Count) throw new InvalidOperationException("Renderer/collider/light count changed.");

            var nextChunks = cells.Select(cell => new MapChunkStreamer.Chunk { scenePath = cell.path, bounds = cell.bounds }).ToList();
            var sourcePaths = new HashSet<string>(originalChunks.Select(chunk => chunk.scenePath));
            var nextBuild = originalBuild.Where(entry => !sourcePaths.Contains(entry.path)).ToList();
            nextBuild.AddRange(cells.Select(cell => new EditorBuildSettingsScene(cell.path, true)));
            mapCommitStarted = true;
            streamer.chunks = nextChunks;
            EditorBuildSettings.scenes = nextBuild.ToArray();
            EditorSceneManager.MarkSceneDirty(map);
            if (!EditorSceneManager.SaveScene(map)) throw new IOException("Could not commit map manifest.");
            committed = true;

            int beforeInitial = originalChunks.Count(chunk => MapChunkStreamer.Distance(chunk, spawn) <= streamer.loadDistance);
            var initialCells = cells.Where(cell => MapChunkStreamer.Distance(new MapChunkStreamer.Chunk { bounds = cell.bounds }, spawn) <= streamer.loadDistance).ToList();
            string report = "SUCCESS\nBackup: " + backup + "\nCell size: " + CellSize + " m\nSpawn: " + spawn.ToString("F3") +
                "\nChunks before: " + originalChunks.Count + "; initial: " + beforeInitial +
                "\nChunks after: " + cells.Count + "; initial: " + initialCells.Count +
                "\nInitial renderers before: " + beforeInitialRenderers + "; colliders: " + beforeInitialColliders +
                "\nInitial renderers after: " + initialCells.Sum(cell => cell.renderers) + "; colliders: " + initialCells.Sum(cell => cell.colliders) +
                "\nAll renderers: " + cells.Sum(cell => cell.renderers) + "; colliders: " + cells.Sum(cell => cell.colliders) +
                "\nEnvironment leaf hierarchies: " + candidates.Count +
                "\nPASS: world transforms, effective active states, layers, renderer/collider/light definitions and scene references preserved after save/reopen." +
                "\nOriginal MapChunks files retained and removed from active Build Settings. This is structural validation, not an FPS benchmark.";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, report);
            File.WriteAllText(backup + "/Refinement.txt", report);
            Debug.Log(report);
        }
        catch
        {
            if (mapCommitStarted && !committed)
            {
                streamer.chunks = originalChunks;
                EditorBuildSettings.scenes = originalBuild;
                File.Copy(backup + "/mapgame.unity", MapPath, true);
                File.Copy(backup + "/EditorBuildSettings.asset", "ProjectSettings/EditorBuildSettings.asset", true);
                // Memory and disk now both contain the old manifest. Leave the
                // scene dirty if Unity could not commit, so the user can inspect
                // the recovered values; never discard unrelated open scene work.
                EditorSceneManager.MarkSceneDirty(map);
            }
            throw;
        }
        finally
        {
            foreach (var scene in outputScenes) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            foreach (var scene in workingScenes) if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            // Only disposable copies created by this run are removed.
            if (AssetDatabase.IsValidFolder(WorkFolder)) AssetDatabase.DeleteAsset(WorkFolder);
            if (openedMap && map.IsValid() && map.isLoaded) EditorSceneManager.CloseScene(map, true);
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
            AssetDatabase.Refresh();
        }
    }

    static HashSet<GameObject> FindReferencedObjects(List<Scene> scenes, HashSet<int> handles, List<ReferenceState> references)
    {
        var referenced = new HashSet<GameObject>();
        foreach (var scene in scenes)
        foreach (var root in scene.GetRootGameObjects())
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) throw new InvalidOperationException("Missing script found in " + root.name);
            if (component is Transform) continue;
            using (var serialized = new SerializedObject(component))
            {
                var iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = iterator.objectReferenceValue;
                    var target = AsGameObject(value);
                    if (target == null || EditorUtility.IsPersistent(target) || target == component.gameObject) continue;
                    if (!handles.Contains(target.scene.handle))
                        throw new InvalidOperationException("Environment component references a scene outside its source chunks: " + component.name + "." + iterator.propertyPath);
                    referenced.Add(target);
                    references.Add(new ReferenceState { source = component, property = iterator.propertyPath, target = value });
                }
            }
        }
        return referenced;
    }

    static GameObject AsGameObject(UnityEngine.Object value) => value as GameObject ?? (value as Component)?.gameObject;
    static void Collect(GameObject root, HashSet<GameObject> referenced, List<Candidate> candidates)
    {
        bool plainGroup = !PrefabUtility.IsPartOfPrefabInstance(root) && root.GetComponents<Component>().Length == 1 &&
            root.transform.childCount > 0 && !referenced.Contains(root) && root.CompareTag("Untagged");
        if (plainGroup)
        {
            foreach (Transform child in root.transform) Collect(child.gameObject, referenced, candidates);
            return;
        }
        candidates.Add(new Candidate {
            root = root, bounds = GetBounds(root), sourceParent = root.transform.parent,
            localPosition = root.transform.localPosition, localRotation = root.transform.localRotation,
            localScale = root.transform.localScale, activeSelf = root.activeSelf
        });
    }
    static Transform CopyParentChain(Transform source, Scene scene, Dictionary<Transform, Transform> copies)
    {
        if (source == null) return null;
        if (copies.TryGetValue(source, out var existing)) return existing;
        // Collect only descends through these pure grouping nodes. This check
        // prevents accidentally duplicating functional objects in the proxies.
        if (PrefabUtility.IsPartOfPrefabInstance(source) || source.GetComponents<Component>().Length != 1)
            throw new InvalidOperationException("Cannot duplicate a functional ancestor: " + source.name);
        var parent = CopyParentChain(source.parent, scene, copies);
        var clone = new GameObject(source.name);
        SceneManager.MoveGameObjectToScene(clone, scene);
        clone.layer = source.gameObject.layer;
        clone.tag = source.gameObject.tag;
        GameObjectUtility.SetStaticEditorFlags(clone, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
        clone.transform.SetParent(parent, false);
        clone.transform.localPosition = source.localPosition;
        clone.transform.localRotation = source.localRotation;
        clone.transform.localScale = source.localScale;
        clone.SetActive(source.gameObject.activeSelf);
        copies.Add(source, clone.transform);
        return clone.transform;
    }
    static int Find(List<Candidate> candidates, int index)
    {
        if (candidates[index].parent != index) candidates[index].parent = Find(candidates, candidates[index].parent);
        return candidates[index].parent;
    }
    static void Union(List<Candidate> candidates, int first, int second)
    {
        first = Find(candidates, first); second = Find(candidates, second);
        if (first != second) candidates[second].parent = first;
    }
    static List<Cell> MakeCells(List<Candidate> candidates)
    {
        var linked = new Dictionary<int, List<Candidate>>();
        for (int i = 0; i < candidates.Count; i++)
        {
            int owner = Find(candidates, i);
            if (!linked.TryGetValue(owner, out var group)) linked.Add(owner, group = new List<Candidate>());
            group.Add(candidates[i]);
        }
        var cells = new Dictionary<Vector2Int, Cell>();
        foreach (var group in linked.Values)
        {
            Bounds bounds = group[0].bounds;
            foreach (var candidate in group) bounds.Encapsulate(candidate.bounds);
            var coordinate = new Vector2Int(Mathf.FloorToInt(bounds.center.x / CellSize), Mathf.FloorToInt(bounds.center.z / CellSize));
            if (!cells.TryGetValue(coordinate, out var cell))
            { cell = new Cell { coordinate = coordinate, bounds = bounds }; cells.Add(coordinate, cell); }
            else cell.bounds.Encapsulate(bounds);
            cell.objects.AddRange(group);
        }
        return cells.Values.OrderBy(cell => cell.coordinate.x).ThenBy(cell => cell.coordinate.y).ToList();
    }
    static Bounds GetBounds(GameObject root)
    {
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        bool any = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        { if (!any) { bounds = renderer.bounds; any = true; } else bounds.Encapsulate(renderer.bounds); }
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            if (collider.enabled && collider.gameObject.activeInHierarchy)
            { if (!any) { bounds = collider.bounds; any = true; } else bounds.Encapsulate(collider.bounds); }
        foreach (var light in root.GetComponentsInChildren<Light>(true))
        {
            var lightBounds = new Bounds(light.transform.position, Vector3.one * light.range * 2);
            if (!any) { bounds = lightBounds; any = true; } else bounds.Encapsulate(lightBounds);
        }
        return bounds;
    }
    static IEnumerable<Component> Geometry(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
            if (component is Renderer || component is Collider || component is Light) yield return component;
    }
    static ObjectState Capture(Component component)
    {
        bool enabled = component is Renderer renderer ? renderer.enabled : component is Collider collider ? collider.enabled : ((Light)component).enabled;
        string definition = component.GetType().FullName;
        // Serialized identity changes when objects are saved into another scene.
        // Definitions are captured after moving for save/reopen validation; the
        // original state checks use type-specific shape/material data below.
        if (component is Renderer r)
        {
            var filter = r.GetComponent<MeshFilter>();
            var meshAsset = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : filter != null ? filter.sharedMesh : null;
            definition += ":" + AssetKey(meshAsset) + ":" + string.Join(",", r.sharedMaterials.Select(AssetKey)) + ":" + r.shadowCastingMode + ":" + r.receiveShadows;
        }
        else if (component is BoxCollider box) definition = "Box:" + box.center.ToString("R") + ":" + box.size.ToString("R") + ":" + box.isTrigger;
        else if (component is MeshCollider mesh) definition = "Mesh:" + AssetKey(mesh.sharedMesh) + ":" + mesh.convex + ":" + mesh.isTrigger + ":" + mesh.cookingOptions;
        else if (component is CapsuleCollider capsule) definition = "Capsule:" + capsule.center.ToString("R") + ":" + capsule.radius.ToString("R", CultureInfo.InvariantCulture) + ":" + capsule.height.ToString("R", CultureInfo.InvariantCulture) + ":" + capsule.direction + ":" + capsule.isTrigger;
        else if (component is SphereCollider sphere) definition = "Sphere:" + sphere.center.ToString("R") + ":" + sphere.radius.ToString("R", CultureInfo.InvariantCulture) + ":" + sphere.isTrigger;
        else if (component is TerrainCollider terrain) definition = "Terrain:" + AssetKey(terrain.terrainData) + ":" + terrain.isTrigger;
        else if (component is Light light) definition = "Light:" + light.type + ":" + light.range.ToString("R", CultureInfo.InvariantCulture) + ":" + light.color.ToString("R") + ":" + light.intensity.ToString("R", CultureInfo.InvariantCulture) + ":" + light.shadows;
        else throw new InvalidOperationException("Unsupported geometry type; leave its hierarchy unchanged: " + component.GetType().FullName);
        if (component is Collider c) definition += ":" + AssetKey(c.sharedMaterial) + ":" + c.includeLayers.value + ":" + c.excludeLayers.value + ":" + c.layerOverridePriority;
        return new ObjectState { matrix = component.transform.localToWorldMatrix, active = component.gameObject.activeInHierarchy, enabled = enabled, layer = component.gameObject.layer, definition = definition };
    }
    static string AssetKey(UnityEngine.Object asset)
    {
        if (asset == null) return "null";
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long local) ? guid + ":" + local : asset.name;
    }
    static void Compare(ObjectState expected, ObjectState actual, string name)
    {
        if (expected.active != actual.active || expected.enabled != actual.enabled || expected.layer != actual.layer || expected.definition != actual.definition)
            throw new InvalidOperationException("Environment state changed: " + name);
        for (int i = 0; i < 16; i++)
            if (Mathf.Abs(expected.matrix[i] - actual.matrix[i]) > MatrixTolerance)
                throw new InvalidOperationException("World transform changed: " + name + " (non-uniform/sheared parent transforms must stay grouped).");
    }
    static void ValidateStates(IEnumerable<KeyValuePair<Component, ObjectState>> states)
    { foreach (var pair in states) Compare(pair.Value, Capture(pair.Key), pair.Key.name); }
    static void ValidateReferences(IEnumerable<ReferenceState> references)
    {
        foreach (var reference in references)
        {
            using (var serialized = new SerializedObject(reference.source))
            {
                var property = serialized.FindProperty(reference.property);
                if (property == null || property.objectReferenceValue != reference.target || AsGameObject(reference.target).scene != reference.source.gameObject.scene)
                    throw new InvalidOperationException("Reference changed while splitting: " + reference.source.name + "." + reference.property);
            }
        }
    }
    static Dictionary<string, ObjectState> CaptureSaved(List<Scene> scenes)
    {
        var result = new Dictionary<string, ObjectState>();
        foreach (var scene in scenes)
            foreach (var root in scene.GetRootGameObjects()) CaptureSaved(root.transform, scene.path + "/" + root.transform.GetSiblingIndex(), result);
        return result;
    }
    static void CaptureSaved(Transform transform, string path, Dictionary<string, ObjectState> result)
    {
        var components = transform.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] is Renderer || components[i] is Collider || components[i] is Light) result.Add(path + ":" + i, Capture(components[i]));
        foreach (Transform child in transform) CaptureSaved(child, path + "/" + child.GetSiblingIndex(), result);
    }
    static void CompareSaved(Dictionary<string, ObjectState> expected, Dictionary<string, ObjectState> actual)
    {
        if (expected.Count != actual.Count) throw new InvalidOperationException("Saved environment count differs.");
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var value)) throw new InvalidOperationException("Saved component missing: " + pair.Key);
            Compare(pair.Value, value, pair.Key);
        }
    }
    static void ValidateSavedReferences(Scene scene)
    {
        var discarded = new List<ReferenceState>();
        FindReferencedObjects(new List<Scene> { scene }, new HashSet<int> { scene.handle }, discarded);
    }
    static Dictionary<string, string> CaptureSavedReferences(List<Scene> scenes)
    {
        var result = new Dictionary<string, string>();
        foreach (var scene in scenes)
        foreach (var root in scene.GetRootGameObjects())
        foreach (var source in root.GetComponentsInChildren<Component>(true))
        {
            if (source == null) throw new InvalidOperationException("Missing script in refined scene.");
            if (source is Transform) continue;
            using (var serialized = new SerializedObject(source))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = property.objectReferenceValue;
                    var targetObject = AsGameObject(target);
                    if (targetObject == null || EditorUtility.IsPersistent(targetObject)) continue;
                    if (targetObject.scene != scene) throw new InvalidOperationException("Cross-scene reference in refined output.");
                    result.Add(SceneObjectKey(source) + "." + property.propertyPath, SceneObjectKey(target));
                }
            }
        }
        return result;
    }
    static string SceneObjectKey(UnityEngine.Object value)
    {
        var gameObject = AsGameObject(value);
        string path = "";
        for (var transform = gameObject.transform; transform != null; transform = transform.parent)
            path = "/" + transform.GetSiblingIndex() + path;
        string suffix = "GameObject";
        if (value is Component component) suffix = "Component:" + Array.IndexOf(gameObject.GetComponents<Component>(), component);
        return gameObject.scene.path + path + ":" + suffix;
    }
}
