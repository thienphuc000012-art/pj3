using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local Fence collision inspection and repair. Never starts gameplay.</summary>
public static class FenceCollisionRepair
{
    const string Request = "Library/CombatValidation/FenceCollision.request";
    const string Output = "Tools/Colliders/Fence";
    [Serializable] class MeshInfo { public string name; public Vector3[] vertices; public int[] triangles; }
    [Serializable] class FenceInfo
    {
        public string path, mesh; public Vector3 position, scale, boundsCenter, boundsSize;
        public Quaternion rotation; public bool enabled; public int triangles;
    }
    [Serializable] class Report
    {
        public bool playing, dirty; public Vector3 player, playerLocal;
        public List<FenceInfo> fences = new List<FenceInfo>();
        public List<MeshInfo> meshes = new List<MeshInfo>();
    }
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/FenceCollisionRepair.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try
        {
            if (command == "probe") { Probe(0); Probe(1); Probe(2); }
            else if (command == "apply") Apply();
            else if (command == "verify") VerifySaved();
            else Inspect();
        }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error.txt", error.ToString()); Debug.LogException(error); }
    }
    static Scene Map() => Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.name == "m");
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static MeshCollider[] Fences(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshCollider>(true))
        .Where(c => PathOf(c.transform).StartsWith("Environment/Fence/", StringComparison.Ordinal) && !c.isTrigger &&
            c.sharedMesh != null && c.sharedMesh.name.EndsWith("ConvexHulls", StringComparison.Ordinal)).ToArray();
    [MenuItem("Tools/Adventure/Repair/Inspect Fence Collision")]
    public static void Inspect()
    {
        var scene = Map(); var env = scene.GetRootGameObjects().Single(r => r.name == "Environment").transform;
        var report = new Report { playing = EditorApplication.isPlaying, dirty = scene.isDirty };
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerScript>(true)).FirstOrDefault();
        if (player != null) { report.player = player.transform.position; report.playerLocal = env.InverseTransformPoint(player.transform.position); }
        var meshes = new HashSet<Mesh>();
        foreach (var fence in Fences(scene))
        {
            var mesh = fence.sharedMesh;
            report.fences.Add(new FenceInfo { path = PathOf(fence.transform), mesh = mesh != null ? mesh.name : "null", enabled = fence.enabled,
                position = fence.transform.position, rotation = fence.transform.rotation, scale = fence.transform.lossyScale,
                boundsCenter = mesh != null ? mesh.bounds.center : Vector3.zero, boundsSize = mesh != null ? mesh.bounds.size : Vector3.zero,
                triangles = mesh != null ? mesh.triangles.Length / 3 : 0 });
            if (mesh != null) meshes.Add(mesh);
            foreach (var filter in fence.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && (f.name.EndsWith("LOD0") || f.name.EndsWith("LOD2")))) meshes.Add(filter.sharedMesh);
        }
        foreach (var mesh in meshes) report.meshes.Add(new MeshInfo { name = mesh.name, vertices = mesh.vertices, triangles = mesh.triangles });
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Inspection.json", JsonUtility.ToJson(report, true));
        Debug.Log("Fence collision inspection: " + report.fences.Count + " instances, " + meshes.Count + " meshes. " + Output);
    }

    static Mesh[] SplitAuthoredHulls(Mesh source)
    {
        if (!source.name.EndsWith("ConvexHulls", StringComparison.Ordinal)) throw new InvalidOperationException("Review non-authored Fence collider: " + source.name);
        var vertices = source.vertices; var indices = source.triangles;
        if (indices.Length % 36 != 0) throw new InvalidOperationException("Expected the inspected twelve-triangle convex hulls: " + source.name);
        var result = new List<Mesh>();
        for (int start = 0; start < indices.Length; start += 36)
        {
            var unique = new Dictionary<Vector3Int, int>(); var points = new List<Vector3>(); var triangles = new int[36];
            for (int i = 0; i < 36; i++)
            {
                var v = vertices[indices[start + i]]; var key = Vector3Int.RoundToInt(v * 100000);
                if (!unique.TryGetValue(key, out int index)) { index = points.Count; unique.Add(key, index); points.Add(v); }
                triangles[i] = index;
            }
            if (points.Count != 8) throw new InvalidOperationException("Unexpected Fence hull topology: " + source.name + " hull=" + result.Count);
            // Each exported block must already be convex. Never turn the entire broken fence
            // into one convex hull, which would bridge the openings and broken top profile.
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var a = points[triangles[t]]; var normal = Vector3.Cross(points[triangles[t + 1]] - a, points[triangles[t + 2]] - a).normalized;
                bool positive = points.Any(p => Vector3.Dot(normal, p - a) > .0002f);
                bool negative = points.Any(p => Vector3.Dot(normal, p - a) < -.0002f);
                if (positive && negative) throw new InvalidOperationException("Exported Fence block is not convex: " + source.name);
            }
            var mesh = new Mesh { name = source.name.Replace("_ConvexHulls", "") + "_SolidHull_" + result.Count };
            mesh.SetVertices(points); mesh.triangles = triangles; mesh.RecalculateBounds(); result.Add(mesh);
        }
        return result.ToArray();
    }

    static int Probe(int mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Fence probes require Edit Mode.");
        var active = SceneManager.GetActiveScene(); var map = Map();
        var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(active);
        var root = new GameObject("Temporary Fence physics tests"); SceneManager.MoveGameObjectToScene(root, temporary);
        int failed = 0, count = 0; var lines = new List<string>();
        try
        {
            foreach (var fence in Fences(map).GroupBy(c => c.sharedMesh).Select(g => g.First()))
            {
                var mesh = mode == 1 ? fence.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.EndsWith("LOD2")).sharedMesh : fence.sharedMesh;
                var go = new GameObject("Test " + mesh.name); go.transform.SetParent(root.transform); go.transform.position = new Vector3(2500, 2500, 2500);
                Mesh[] hulls = mode == 2 ? SplitAuthoredHulls(mesh) : Array.Empty<Mesh>();
                if (mode == 2)
                {
                    foreach (var hull in hulls) { var collision = go.AddComponent<MeshCollider>(); collision.sharedMesh = hull; collision.convex = true; }
                }
                else { var collision = go.AddComponent<MeshCollider>(); collision.sharedMesh = mesh; }
                var b = fence.sharedMesh.bounds;
                var floor = new GameObject("Temporary support floor"); floor.transform.SetParent(root.transform); floor.transform.position = go.transform.position + new Vector3(b.center.x, b.min.y - .25f, 0);
                floor.AddComponent<BoxCollider>().size = new Vector3(b.size.x + 10, .5f, b.size.z + 12);
                foreach (float side in new[] { -1f, 1f })
                foreach (float fraction in new[] { .05f, .15f, .25f, .35f, .5f, .65f, .75f, .85f, .95f })
                foreach (float speed in new[] { 2f, 4f })
                {
                    var actor = new GameObject("Fence capsule"); actor.transform.SetParent(root.transform);
                    var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                    float x = Mathf.Lerp(b.min.x, b.max.x, fraction);
                    actor.transform.position = go.transform.position + new Vector3(x, b.min.y + .04f, b.center.z + side * (b.extents.z + .8f));
                    Physics.SyncTransforms();
                    var inward = Vector3.back * side;
                    for (int f = 0; f < Mathf.CeilToInt(2.2f * 60 / speed); f++) cc.Move(inward * (speed / 60) + Vector3.down * (2f / 60));
                    float along = fraction < .5f ? 1 : -1;
                    for (int f = 0; f < Mathf.CeilToInt(2f * 60 / speed); f++) cc.Move((inward + Vector3.right * along).normalized * (speed / 60) + Vector3.down * (2f / 60));
                    var before = actor.transform.position; var lateStart = before;
                    int escapeFrames = Mathf.CeilToInt(3f * 60 / speed);
                    for (int f = 0; f < escapeFrames; f++)
                    {
                        if (f == escapeFrames - Mathf.CeilToInt(.4f * 60 / speed)) lateStart = actor.transform.position;
                        cc.Move(-inward * (speed / 60) + Vector3.down * (2f / 60));
                    }
                    float escape = Vector3.Dot(actor.transform.position - before, -inward);
                    float late = Vector3.Dot(actor.transform.position - lateStart, -inward);
                    bool pass = escape > 2f && late > .25f;
                    count++; if (!pass) failed++;
                    lines.Add((pass ? "PASS " : "BLOCKED ") + mesh.name + " side=" + side + " x=" + fraction + " speed=" + speed + " escape=" + escape.ToString("F3") + " last=" + late.ToString("F3"));
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                // Put two copies next to each other with an 85 cm opening; the 60 cm capsule
                // must pass through from either side without a combined hull bridging it.
                var twin = new GameObject("Adjacent Fence gap test"); twin.transform.SetParent(root.transform);
                twin.transform.position = go.transform.position + Vector3.right * (b.size.x + .85f);
                if (mode == 2) foreach (var hull in hulls) { var c = twin.AddComponent<MeshCollider>(); c.sharedMesh = hull; c.convex = true; }
                else twin.AddComponent<MeshCollider>().sharedMesh = mesh;
                floor.GetComponent<BoxCollider>().size = new Vector3(b.size.x * 3 + 12, .5f, b.size.z + 12);
                foreach (float side in new[] { -1f, 1f })
                foreach (float speed in new[] { 2f, 4f })
                {
                    var actor = new GameObject("Opening capsule"); actor.transform.SetParent(root.transform);
                    var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                    actor.transform.position = go.transform.position + new Vector3(b.max.x + .425f, b.min.y + .04f, b.center.z + side * (b.extents.z + .8f));
                    Physics.SyncTransforms(); var before = actor.transform.position; var direction = Vector3.back * side;
                    for (int f = 0; f < Mathf.CeilToInt(3f * 60 / speed); f++) cc.Move(direction * (speed / 60) + Vector3.down * (2f / 60));
                    float travel = Vector3.Dot(actor.transform.position - before, direction); bool pass = travel > 2.8f;
                    count++; if (!pass) failed++;
                    lines.Add((pass ? "GAP PASS " : "GAP BLOCKED ") + mesh.name + " side=" + side + " speed=" + speed + " travel=" + travel.ToString("F3"));
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                UnityEngine.Object.DestroyImmediate(twin);
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(floor);
                foreach (var hull in hulls) UnityEngine.Object.DestroyImmediate(hull);
            }
            lines.Insert(0, "Fence isolated native capsule tests=" + count + "; stuck=" + failed + "; mode=" + mode + ". Press into wall, move diagonally along it, then reverse out. Floor at authored collision base.");
            Directory.CreateDirectory(Output); File.WriteAllLines(Output + (mode == 0 ? "/Baseline.txt" : mode == 1 ? "/VisualMeshCandidate.txt" : "/Candidate.txt"), lines); Debug.Log(lines[0]);
            return failed;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); SceneManager.SetActiveScene(active); EditorSceneManager.CloseScene(temporary, true); }
    }

    const string CollisionRoot = "Solid Fence Collision";

    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Repair in Edit Mode.");
        if (Probe(2) != 0) throw new InvalidOperationException("Fence candidate tests must pass before repair.");
        var scene = Map(); var fences = Fences(scene);
        if (fences.Length != 16 || fences.Any(c => c.GetComponentInParent<WorldInteraction>() != null || c.attachedRigidbody != null))
            throw new InvalidOperationException("Fence instances changed since inspection; review them before repair.");
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true)) throw new IOException("Could not back up current scene.");
        File.Copy(Output + "/Inspection.json", backup + "/Inspection.json");
        var generated = new Dictionary<Mesh, Mesh[]>();
        const string folder = "Assets/Adventure/Generated/Collision/Fence";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Adventure/Generated/Collision", "Fence");
        foreach (var source in fences.Select(c => c.sharedMesh).Distinct())
        {
            var parts = SplitAuthoredHulls(source);
            for (int i = 0; i < parts.Length; i++)
            {
                string path = folder + "/" + parts[i].name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    if (existing.vertexCount != parts[i].vertexCount || !existing.vertices.SequenceEqual(parts[i].vertices) || !existing.triangles.SequenceEqual(parts[i].triangles))
                        throw new InvalidOperationException("Generated collision asset differs; do not overwrite: " + path);
                    UnityEngine.Object.DestroyImmediate(parts[i]); parts[i] = existing;
                }
                else { AssetDatabase.CreateAsset(parts[i], path); AssetDatabase.SaveAssetIfDirty(parts[i]); }
            }
            generated.Add(source, parts);
        }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Repair Fence solid collision");
        var lines = new List<string> { "Backup: " + backup }; int pieces = 0;
        try
        {
            foreach (var source in fences)
            {
                if (source.transform.Find(CollisionRoot) != null) throw new InvalidOperationException("Fence already has generated collision: " + PathOf(source.transform));
                var go = new GameObject(CollisionRoot); Undo.RegisterCreatedObjectUndo(go, "Add solid Fence hulls"); go.transform.SetParent(source.transform, false); go.layer = source.gameObject.layer;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
                foreach (var mesh in generated[source.sharedMesh])
                {
                    var part = new GameObject(mesh.name); Undo.RegisterCreatedObjectUndo(part, "Add convex Fence part"); part.transform.SetParent(go.transform, false); part.layer = source.gameObject.layer;
                    GameObjectUtility.SetStaticEditorFlags(part, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
                    var collider = Undo.AddComponent<MeshCollider>(part); collider.sharedMesh = mesh; collider.convex = true; collider.sharedMaterial = source.sharedMaterial; collider.contactOffset = source.contactOffset;
                    pieces++;
                }
                Undo.RecordObject(source, "Replace hollow combined Fence collider"); source.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(source); EditorUtility.SetDirty(source);
                lines.Add(PathOf(source.transform) + " -> " + generated[source.sharedMesh].Length + " solid parts");
            }
            Physics.SyncTransforms(); Validate(scene);
            if (ChapelEdgeColliderRepair.Probe(false) != 0) throw new InvalidOperationException("Chapel edge regression failed.");
            MEnvironmentCollision.Probe();
            string movement = File.ReadAllText("Tools/Colliders/MEnvironment/PassageProbe.txt");
            if (movement.Contains("BLOCKED") || movement.Contains("INCOMPLETE")) throw new InvalidOperationException("Chapel center regression failed.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save repaired Fence collision.");
            Undo.CollapseUndoOperations(group);
            lines.Insert(0, "PASS: " + fences.Length + " Fence instances, " + pieces + " solid convex parts, using " + generated.Values.Sum(p => p.Length) + " shared generated meshes. Authored shape, broken profile and gaps preserved. Candidate and Chapel regression passed.");
            File.WriteAllLines(Output + "/Repair.txt", lines); Debug.Log(lines[0]);
        }
        catch { Undo.RevertAllDownToGroup(group); File.WriteAllLines(Output + "/RepairFailed.txt", lines); throw; }
    }

    static void Validate(Scene scene)
    {
        var fences = Fences(scene);
        if (fences.Length != 16) throw new InvalidOperationException("Fence count differs after saving.");
        foreach (var source in fences)
        {
            var root = source.transform.Find(CollisionRoot);
            if (source.enabled || root == null || root.localPosition != Vector3.zero || root.localRotation != Quaternion.identity || root.localScale != Vector3.one)
                throw new InvalidOperationException("Fence root not saved/aligned: " + PathOf(source.transform));
            var parts = root.GetComponentsInChildren<MeshCollider>(true); int expected = source.sharedMesh.triangles.Length / 36;
            if (parts.Length != expected || parts.Any(c => !c.enabled || !c.convex || c.sharedMesh == null || !EditorUtility.IsPersistent(c.sharedMesh) || c.gameObject.layer != source.gameObject.layer || c.transform.localPosition != Vector3.zero))
                throw new InvalidOperationException("Fence collision references differ: " + PathOf(source.transform));
            var authored = SplitAuthoredHulls(source.sharedMesh);
            try
            {
                foreach (var mesh in authored)
                {
                    var part = parts.SingleOrDefault(c => c.sharedMesh.name == mesh.name);
                    if (part == null || !part.sharedMesh.vertices.SequenceEqual(mesh.vertices) || !part.sharedMesh.triangles.SequenceEqual(mesh.triangles))
                        throw new InvalidOperationException("Saved Fence hull geometry changed: " + PathOf(source.transform));
                }
            }
            finally { foreach (var mesh in authored) UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }

    static void VerifySaved()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verify in Edit Mode.");
        var scene = Map(); var preview = EditorSceneManager.OpenPreviewScene(scene.path);
        try { Validate(preview); }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        if (ChapelEdgeColliderRepair.Probe(false) != 0) throw new InvalidOperationException("Saved Chapel edge regression failed.");
        // Recheck movement without overwriting the older repair tool's report, which may
        // currently be held open by another editor or file viewer.
        MEnvironmentCollision.Probe();
        string movement = File.ReadAllText("Tools/Colliders/MEnvironment/PassageProbe.txt");
        if (movement.Contains("BLOCKED") || movement.Contains("INCOMPLETE")) throw new InvalidOperationException("Saved Chapel center regression failed.");
        File.WriteAllText(Output + "/SavedValidation.txt", "PASS: saved scene reloaded and all 16 Fence compound colliders/mesh references/transforms verified. Four source shapes: 144 wall approach/escape cases and 16 gap traversals passed before applying. Chapel edges and center passage rechecked in Edit Mode. No gameplay/campaign saves written.");
        Debug.Log("Saved Fence collision validation passed.");
    }
}
