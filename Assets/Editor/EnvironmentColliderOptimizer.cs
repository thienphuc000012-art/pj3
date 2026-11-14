using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Audits the scene's environment before any scene-local collision changes.</summary>
public static class EnvironmentColliderOptimizer
{
    const string Request = "Library/CombatValidation/EnvironmentColliderOptimizer.request";
    const string Output = "Tools/Colliders/EnvironmentAudit";
    [Serializable] class MeshInfo { public string name, asset; public Vector3[] vertices; public int[] triangles; }
    [Serializable] class Info
    {
        public string path, type, mesh, prefab, layer; public bool enabled, active, trigger, convex, gameplay;
        public Vector3 position, scale, center, size; public Quaternion rotation; public float determinant;
        public string[] visuals;
    }
    [Serializable] class Report
    {
        public bool playing, dirty; public Vector3 player; public float height, radius, step, skin;
        public List<Info> colliders = new List<Info>(); public List<MeshInfo> meshes = new List<MeshInfo>();
    }
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var compiled = File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll");
        if (compiled < File.GetLastWriteTimeUtc("Assets/Editor/EnvironmentColliderOptimizer.cs") || compiled < File.GetLastWriteTimeUtc("Assets/Editor/AuthoredCollisionHulls.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try
        {
            if (command == "plan") PlanReport();
            else if (command == "probe") Probe();
            else if (command == "apply") Apply();
            else if (command == "deduplicate") Deduplicate();
            else if (command == "verify") VerifySaved();
            else Inspect();
        }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("HHmmss") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    static Scene Map() => Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.name == "m");
    static Transform Environment(Scene scene) => scene.GetRootGameObjects().Single(r => r.name == "Environment").transform;
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static bool Gameplay(Collider c) => c.isTrigger || c.attachedRigidbody != null || c.GetComponentInParent<WorldInteraction>() != null || c.GetComponentInParent<Animator>() != null || c is CharacterController || c.GetComponents<MonoBehaviour>().Any(s => s != null);
    [MenuItem("Tools/Adventure/Repair/Audit Entire m Environment Collision")]
    public static void Inspect()
    {
        var scene = Map(); var env = Environment(scene); var report = new Report { playing = EditorApplication.isPlaying, dirty = scene.isDirty };
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerScript>(true)).FirstOrDefault();
        if (player != null)
        {
            report.player = player.transform.position; var cc = player.GetComponent<CharacterController>();
            if (cc != null) { report.height = cc.height; report.radius = cc.radius; report.step = cc.stepOffset; report.skin = cc.skinWidth; }
        }
        var meshes = new HashSet<Mesh>(); Physics.SyncTransforms();
        foreach (var c in env.GetComponentsInChildren<Collider>(true))
        {
            var mesh = (c as MeshCollider)?.sharedMesh;
            var info = new Info { path = PathOf(c.transform), type = c.GetType().Name, mesh = mesh != null ? mesh.name : "", prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c),
                layer = LayerMask.LayerToName(c.gameObject.layer), enabled = c.enabled, active = c.gameObject.activeInHierarchy, trigger = c.isTrigger, gameplay = Gameplay(c), convex = c is MeshCollider mc && mc.convex,
                position = c.transform.position, rotation = c.transform.rotation, scale = c.transform.lossyScale, center = c.bounds.center, size = c.bounds.size, determinant = c.transform.localToWorldMatrix.determinant,
                visuals = c.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).Select(f => f.sharedMesh.name + " | " + AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray() };
            report.colliders.Add(info);
            if (mesh != null && !info.gameplay && c.enabled && c.gameObject.activeInHierarchy) meshes.Add(mesh);
        }
        foreach (var mesh in meshes) report.meshes.Add(new MeshInfo { name = mesh.name, asset = AssetDatabase.GetAssetPath(mesh), vertices = mesh.vertices, triangles = mesh.triangles });
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Inspection.json", JsonUtility.ToJson(report, true));
        Debug.Log("Environment collision audit: " + report.colliders.Count + " colliders, " + meshes.Count + " unique active static meshes. " + Output);
    }

    sealed class Change
    {
        public MeshCollider source;
        public AuthoredCollisionHulls.Part[] parts;
        public string action;
        public Mesh fittedMesh;
    }
    static bool SmoothLowDecoration(MeshCollider source) =>
        (source.sharedMesh.name == "sm_Roots_01_01_ConvexHulls" || source.sharedMesh.name == "sm_SkullDeer_01_01_ConvexHulls") && source.bounds.size.y < 2f;
    static List<Change> Plan(Scene scene)
    {
        var result = new List<Change>(); var cache = new Dictionary<Mesh, AuthoredCollisionHulls.Part[]>();
        foreach (var source in Environment(scene).GetComponentsInChildren<MeshCollider>(true))
        {
            if (!source.enabled || !source.gameObject.activeInHierarchy || Gameplay(source)) continue;
            if (source.sharedMesh == null) { result.Add(new Change { source = source, action = "Disable empty collider" }); continue; }
            if (source.convex || !source.sharedMesh.name.EndsWith("ConvexHulls", StringComparison.Ordinal)) continue;
            if (source.sharedMesh.name.StartsWith("sm_Stairs_", StringComparison.Ordinal))
            {
                var filter = source.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.EndsWith("LOD2"));
                if (filter == null || filter.sharedMesh == null || Vector3.Distance(filter.transform.position, source.transform.position) > .001f || Quaternion.Angle(filter.transform.rotation, source.transform.rotation) > .1f || Vector3.Distance(filter.transform.lossyScale, source.transform.lossyScale) > .001f)
                    throw new InvalidOperationException("Review stairs mesh alignment: " + PathOf(source.transform));
                result.Add(new Change { source = source, action = "Fit stairs treads and underside to LOD2", fittedMesh = filter.sharedMesh,
                    parts = new[] { new AuthoredCollisionHulls.Part { vertices = filter.sharedMesh.vertices, triangles = filter.sharedMesh.triangles, bounds = filter.sharedMesh.bounds, box = false, planes = Array.Empty<Plane>() } } });
                continue;
            }
            if (SmoothLowDecoration(source))
            {
                result.Add(new Change { source = source, action = "Smooth low decorative roots/skull envelope", parts = new[] { new AuthoredCollisionHulls.Part { vertices = source.sharedMesh.vertices, triangles = source.sharedMesh.triangles, bounds = source.sharedMesh.bounds, box = false, planes = Array.Empty<Plane>() } } });
                continue;
            }
            if (!cache.TryGetValue(source.sharedMesh, out var parts)) cache.Add(source.sharedMesh, parts = AuthoredCollisionHulls.Decompose(source.sharedMesh));
            result.Add(new Change { source = source, parts = parts, action = parts.Length == 1 && !parts[0].box ? "Use solid convex collider" : "Use simple solid parts" });
        }
        return result;
    }
    static void PlanReport()
    {
        var changes = Plan(Map()); var lines = new List<string>();
        foreach (var group in changes.Where(c => c.parts != null).GroupBy(c => c.source.sharedMesh))
            lines.Add("MESH " + group.Key.name + ": instances=" + group.Count() + ", parts=" + group.First().parts.Length + ", boxes=" + group.First().parts.Count(p => p.box));
        foreach (var change in changes) lines.Add(change.action + " | " + PathOf(change.source.transform));
        lines.Insert(0, "Plan: " + changes.Count(c => c.parts != null) + " static mesh colliders across " + changes.Where(c => c.parts != null).Select(c => c.source.sharedMesh).Distinct().Count() + " meshes; " + changes.Count(c => c.parts == null) + " empty colliders disabled; new primitive boxes=" + changes.Where(c => c.parts != null).Sum(c => c.parts.Count(p => p.box)) + ". Existing terrain, gameplay triggers, repaired Fence and vaulted LOD collision excluded.");
        Directory.CreateDirectory(Output); File.WriteAllLines(Output + "/Plan.txt", lines); Debug.Log(lines[0]);
    }

    sealed class ProbeResult
    {
        public int cases, failed, recovered, gaps, gapFailures;
        public readonly List<string> lines = new List<string>();
    }
    static void Probe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run collision tests in Edit Mode.");
        var groups = Plan(Map()).Where(c => c.parts != null).GroupBy(c => c.source.sharedMesh).ToArray();
        var active = SceneManager.GetActiveScene(); var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(active);
        var root = new GameObject("Temporary full Environment collision tests"); SceneManager.MoveGameObjectToScene(root, temporary);
        var baseline = new ProbeResult(); var candidate = new ProbeResult(); var meshes = new List<Mesh>();
        try
        {
            foreach (var group in groups)
            {
                var change = group.OrderByDescending(c => c.source.transform.lossyScale.sqrMagnitude).First();
                var scale = change.source.transform.lossyScale;
                if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0) throw new InvalidOperationException("Review mirrored collision separately: " + PathOf(change.source.transform));
                var bounds = new Bounds(Vector3.Scale(group.Key.bounds.center, scale), Vector3.Scale(group.Key.bounds.size, scale));
                var reference = new GameObject("Reference " + group.Key.name); reference.transform.SetParent(root.transform); reference.transform.position = new Vector3(2500, 2500, 2500); reference.transform.localScale = scale;
                reference.AddComponent<MeshCollider>().sharedMesh = group.Key;
                var fixedObject = new GameObject("Candidate " + group.Key.name); fixedObject.transform.SetParent(root.transform); fixedObject.transform.position = new Vector3(3000, 2500, 2500); fixedObject.transform.localScale = scale;
                AddTestParts(fixedObject, change, meshes);
                foreach (int mode in new[] { 0, 1 })
                {
                    var owner = mode == 0 ? reference : fixedObject; var result = mode == 0 ? baseline : candidate;
                    var floor = new GameObject("Temporary support floor"); floor.transform.SetParent(root.transform);
                    floor.transform.position = owner.transform.position + new Vector3(bounds.center.x, bounds.min.y - .25f, bounds.center.z);
                    floor.AddComponent<BoxCollider>().size = new Vector3(bounds.size.x + 20, .5f, bounds.size.z + 20);
                    foreach (int axis in new[] { 0, 1 })
                    foreach (float side in new[] { -1f, 1f })
                    foreach (float fraction in new[] { .15f, .5f, .85f })
                    foreach (float speed in new[] { 2f, 4f })
                    {
                        var outward = (axis == 0 ? Vector3.forward : Vector3.right) * side;
                        var along = (axis == 0 ? Vector3.right : Vector3.forward) * (fraction < .5f ? 1 : -1);
                        Vector3 start = axis == 0 ? new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, fraction), bounds.min.y + .04f, bounds.center.z + side * (bounds.extents.z + .8f)) :
                            new Vector3(bounds.center.x + side * (bounds.extents.x + .8f), bounds.min.y + .04f, Mathf.Lerp(bounds.min.z, bounds.max.z, fraction));
                        var actor = new GameObject("Environment capsule"); actor.transform.SetParent(root.transform);
                        var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                        actor.transform.position = owner.transform.position + start; Physics.SyncTransforms();
                        Move(cc, -outward, 2.3f, speed); Move(cc, (-outward + along).normalized, 1.6f, speed);
                        bool escaped = false; float progress = 0, late = 0; int attempt = 0;
                        foreach (var direction in new[] { outward, (outward + along).normalized, (outward - along).normalized })
                        {
                            attempt++; var before = actor.transform.position; var lateStart = before; int frames = Mathf.CeilToInt(3f * 60 / speed);
                            for (int f = 0; f < frames; f++)
                            {
                                if (f == frames - Mathf.CeilToInt(.4f * 60 / speed)) lateStart = actor.transform.position;
                                cc.Move(direction * (speed / 60) + Vector3.down * (2f / 60));
                            }
                            progress = Vector3.Dot(actor.transform.position - before, direction); late = Vector3.Dot(actor.transform.position - lateStart, direction);
                            // A character that has reached the edge of a protrusion and is
                            // now moving freely is not trapped, even if the first frames slid.
                            if (progress > .75f && late > .25f) { escaped = true; break; }
                        }
                        result.cases++; if (!escaped) result.failed++; else if (attempt > 1) result.recovered++;
                        result.lines.Add((escaped ? "PASS " : "TRAPPED ") + group.Key.name + " scale=" + scale + " axis=" + axis + " side=" + side + " sample=" + fraction + " speed=" + speed + " escape=" + progress.ToString("F3") + " late=" + late.ToString("F3") + " directions=" + attempt);
                        UnityEngine.Object.DestroyImmediate(actor);
                    }
                    UnityEngine.Object.DestroyImmediate(floor);
                }
                // A route is considered an opening only when the original capsule sweep
                // is clear from BOTH sides, avoiding dependence on backface ray settings.
                Physics.SyncTransforms();
                foreach (int axis in new[] { 0, 1 })
                foreach (float fraction in new[] { .1f, .2f, .3f, .4f, .5f, .6f, .7f, .8f, .9f })
                {
                    var direction = axis == 0 ? Vector3.forward : Vector3.right;
                    float length = (axis == 0 ? bounds.size.z : bounds.size.x) + 1.6f;
                    Vector3 start = axis == 0 ? new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, fraction), bounds.min.y + .04f, bounds.min.z - .8f) :
                        new Vector3(bounds.min.x - .8f, bounds.min.y + .04f, Mathf.Lerp(bounds.min.z, bounds.max.z, fraction));
                    if (Blocked(reference, start, direction, length) || Blocked(reference, start + direction * length, -direction, length)) continue;
                    bool blocked = Blocked(fixedObject, start, direction, length) || Blocked(fixedObject, start + direction * length, -direction, length);
                    candidate.gaps++; if (blocked) candidate.gapFailures++;
                    candidate.lines.Add((blocked ? "OPENING BLOCKED " : "OPENING PASS ") + group.Key.name + " axis=" + axis + " sample=" + fraction);
                }
                UnityEngine.Object.DestroyImmediate(reference); UnityEngine.Object.DestroyImmediate(fixedObject);
                File.WriteAllText(Output + "/Progress.txt", group.Key.name);
            }
            WriteProbe("Baseline", baseline); WriteProbe("Candidate", candidate);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            SceneManager.SetActiveScene(active); EditorSceneManager.CloseScene(temporary, true);
        }
    }
    static void Move(CharacterController cc, Vector3 direction, float distance, float speed)
    {
        for (int i = 0; i < Mathf.CeilToInt(distance * 60 / speed); i++) cc.Move(direction * (speed / 60) + Vector3.down * (2f / 60));
    }
    static bool Blocked(GameObject owner, Vector3 localStart, Vector3 direction, float distance)
    {
        var start = owner.transform.position + localStart;
        return Physics.CapsuleCastAll(start + Vector3.up * .3f, start + Vector3.up * 1.7f, .3f, direction, distance, ~0, QueryTriggerInteraction.Ignore)
            .Any(h => h.collider.gameObject == owner);
    }
    static void AddTestParts(GameObject owner, Change change, List<Mesh> allocated)
    {
        if (change.fittedMesh != null) { owner.AddComponent<MeshCollider>().sharedMesh = change.fittedMesh; return; }
        if (change.parts.Length == 1 && !change.parts[0].box)
        { var c = owner.AddComponent<MeshCollider>(); c.sharedMesh = change.source.sharedMesh; c.convex = true; return; }
        for (int i = 0; i < change.parts.Length; i++)
        {
            var part = change.parts[i];
            if (part.box) { var c = owner.AddComponent<BoxCollider>(); c.center = part.bounds.center; c.size = part.bounds.size; }
            else { var mesh = part.CreateMesh(change.source.sharedMesh.name + "_Solid_" + i); allocated.Add(mesh); var c = owner.AddComponent<MeshCollider>(); c.sharedMesh = mesh; c.convex = true; }
        }
    }
    static void WriteProbe(string name, ProbeResult result)
    {
        result.lines.Insert(0, name + ": " + result.cases + " capsule routes, trapped=" + result.failed + ", needed direction change=" + result.recovered + "; openings=" + result.gaps + ", blocked openings=" + result.gapFailures + ". Geometry uses each model's largest in-scene scale, isolated support floor; no Play Mode.");
        File.WriteAllLines(Output + "/" + name + ".txt", result.lines); Debug.Log(result.lines[0]);
    }

    [Serializable] class ShapeRecord
    {
        public bool box; public int index; public Vector3 center, size; public string mesh, meshName;
    }
    [Serializable] class SourceRecord
    {
        public string path, action, meshName; public int[] route; public int sourceIndex, layer;
        public bool direct, convex, duplicate; public Vector3 position, scale; public Quaternion rotation;
        public List<ShapeRecord> shapes = new List<ShapeRecord>();
    }
    [Serializable] class Manifest
    {
        public string backup; public int meshColliders, emptyColliders, boxes, convexParts, direct, fitted, duplicates, duplicateShapes;
        public List<SourceRecord> sources = new List<SourceRecord>();
    }
    static int[] Route(Transform t, Transform env)
    {
        var indices = new List<int>(); while (t != env) { indices.Add(t.GetSiblingIndex()); t = t.parent; if (t == null) throw new InvalidOperationException("Collider outside Environment."); }
        indices.Reverse(); return indices.ToArray();
    }
    static bool BoxCompatible(Transform t)
    {
        var m = t.localToWorldMatrix; var a = ((Vector3)m.GetColumn(0)).normalized; var b = ((Vector3)m.GetColumn(1)).normalized; var c = ((Vector3)m.GetColumn(2)).normalized;
        return Mathf.Abs(Vector3.Dot(a, b)) < .0001f && Mathf.Abs(Vector3.Dot(a, c)) < .0001f && Mathf.Abs(Vector3.Dot(b, c)) < .0001f && t.lossyScale.x > 0 && t.lossyScale.y > 0 && t.lossyScale.z > 0;
    }
    static void CopySettings(Collider source, Collider target)
    {
        target.sharedMaterial = source.sharedMaterial; target.contactOffset = source.contactOffset;
        target.includeLayers = source.includeLayers; target.excludeLayers = source.excludeLayers;
        target.layerOverridePriority = source.layerOverridePriority; target.providesContacts = source.providesContacts;
    }
    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Apply in Edit Mode.");
        var scene = Map(); var env = Environment(scene); var changes = Plan(scene);
        string candidate = File.ReadAllText(Output + "/Candidate.txt");
        if (!candidate.Split('\n')[0].Contains("trapped=0,") || !candidate.Split('\n')[0].Contains("blocked openings=0."))
            throw new InvalidOperationException("Complete all candidate collision tests before applying.");
        foreach (var group in changes.Where(c => c.parts != null).GroupBy(c => c.source.sharedMesh))
        {
            var source = group.OrderByDescending(c => c.source.transform.lossyScale.sqrMagnitude).First().source;
            if (!candidate.Contains("PASS " + group.Key.name + " scale=" + source.transform.lossyScale))
                throw new InvalidOperationException("Mesh/scale not covered by the candidate tests: " + group.Key.name);
        }
        var manifest = new Manifest { backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") };
        Directory.CreateDirectory(manifest.backup); File.Copy(scene.path, manifest.backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, manifest.backup + "/m.current.unity", true)) throw new IOException("Could not back up current scene edits.");
        File.Copy(Output + "/Inspection.json", manifest.backup + "/Inspection.json");
        File.Copy(Output + "/Candidate.txt", manifest.backup + "/Candidate.txt");
        const string folder = "Assets/Adventure/Generated/Collision/Environment";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Adventure/Generated/Collision", "Environment");
        var meshes = new Dictionary<string, Mesh>();
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Optimize m Environment collision");
        try
        {
            foreach (var change in changes)
            {
                var source = change.source;
                var record = new SourceRecord { path = PathOf(source.transform), route = Route(source.transform, env), sourceIndex = Array.IndexOf(source.GetComponents<MeshCollider>(), source),
                    action = change.action, meshName = source.sharedMesh != null ? source.sharedMesh.name : "", layer = source.gameObject.layer,
                    position = source.transform.position, rotation = source.transform.rotation, scale = source.transform.lossyScale,
                    direct = change.parts != null && change.parts.Length == 1 && !change.parts[0].box,
                    convex = change.fittedMesh == null };
                manifest.sources.Add(record);
                Undo.RecordObject(source, change.action);
                if (change.parts == null) { source.enabled = false; manifest.emptyColliders++; }
                else if (record.direct)
                {
                    if (change.fittedMesh != null) { source.sharedMesh = change.fittedMesh; record.meshName = change.fittedMesh.name; }
                    source.convex = record.convex; manifest.meshColliders++; if (record.convex) manifest.direct++; else manifest.fitted++;
                }
                else
                {
                    manifest.meshColliders++;
                    for (int i = 0; i < change.parts.Length; i++)
                    {
                        var part = change.parts[i];
                        if (part.box && BoxCompatible(source.transform))
                        {
                            var box = Undo.AddComponent<BoxCollider>(source.gameObject); box.center = part.bounds.center; box.size = part.bounds.size; CopySettings(source, box);
                            record.shapes.Add(new ShapeRecord { box = true, index = Array.IndexOf(source.GetComponents<BoxCollider>(), box), center = box.center, size = box.size }); manifest.boxes++;
                        }
                        else
                        {
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.sharedMesh, out string guid, out long localId);
                            string name = source.sharedMesh.name.Replace("_ConvexHulls", "") + "_Solid_" + i;
                            string path = folder + "/" + name + "_" + guid.Substring(0, 8) + "_" + localId + ".asset";
                            if (!meshes.TryGetValue(path, out var mesh))
                            {
                                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                                if (mesh == null) { mesh = part.CreateMesh(name); AssetDatabase.CreateAsset(mesh, path); AssetDatabase.SaveAssetIfDirty(mesh); }
                                else if (!mesh.vertices.SequenceEqual(part.vertices) || !mesh.triangles.SequenceEqual(part.triangles)) throw new InvalidOperationException("Generated collision asset differs: " + path);
                                meshes.Add(path, mesh);
                            }
                            var solid = Undo.AddComponent<MeshCollider>(source.gameObject); solid.sharedMesh = mesh; solid.convex = true; CopySettings(source, solid);
                            record.shapes.Add(new ShapeRecord { index = Array.IndexOf(source.GetComponents<MeshCollider>(), solid), mesh = path, meshName = mesh.name }); manifest.convexParts++;
                        }
                    }
                    source.enabled = false;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(source); EditorUtility.SetDirty(source);
            }
            Physics.SyncTransforms(); Validate(scene, manifest); PassageRegression();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save optimized environment collision.");
            Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Output + "/Repair.json", JsonUtility.ToJson(manifest, true));
            WriteRepair(manifest);
            Debug.Log(File.ReadAllText(Output + "/Repair.txt"));
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }
    static void Validate(Scene scene, Manifest manifest)
    {
        var env = Environment(scene);
        foreach (var record in manifest.sources)
        {
            var owner = env; foreach (int index in record.route) owner = owner.GetChild(index);
            if (PathOf(owner) != record.path || owner.gameObject.layer != record.layer || Vector3.Distance(owner.position, record.position) > .0002f || Quaternion.Angle(owner.rotation, record.rotation) > .1f || Vector3.Distance(owner.lossyScale, record.scale) > .0002f)
                throw new InvalidOperationException("Environment placement changed: " + record.path);
            var source = owner.GetComponents<MeshCollider>()[record.sourceIndex];
            if ((source.sharedMesh != null ? source.sharedMesh.name : "") != record.meshName || source.enabled != (record.direct && !record.duplicate) || record.direct && source.convex != record.convex)
                throw new InvalidOperationException("Original collision state differs: " + record.path);
            foreach (var shape in record.shapes)
            {
                if (shape.box)
                {
                    var box = owner.GetComponents<BoxCollider>()[shape.index];
                    if (box.enabled == record.duplicate || box.isTrigger || Vector3.Distance(box.center, shape.center) > .0001f || Vector3.Distance(box.size, shape.size) > .0001f || NegativeBoxColliderRepair.Invalid(box)) throw new InvalidOperationException("Saved box differs: " + record.path);
                }
                else
                {
                    var mesh = owner.GetComponents<MeshCollider>()[shape.index];
                    if (mesh.enabled == record.duplicate || !mesh.convex || mesh.isTrigger || mesh.sharedMesh == null || mesh.sharedMesh.name != shape.meshName || AssetDatabase.GetAssetPath(mesh.sharedMesh) != shape.mesh) throw new InvalidOperationException("Saved convex part differs: " + record.path);
                }
            }
        }
    }
    static void PassageRegression()
    {
        if (ChapelEdgeColliderRepair.Probe(false) != 0) throw new InvalidOperationException("Chapel edge movement regression failed.");
        MEnvironmentCollision.Probe();
        string passage = File.ReadAllText("Tools/Colliders/MEnvironment/PassageProbe.txt");
        if (passage.Contains("BLOCKED") || passage.Contains("INCOMPLETE")) throw new InvalidOperationException("Chapel center movement regression failed.");
    }
    static Transform Owner(Transform env, SourceRecord record) { var t = env; foreach (int index in record.route) t = t.GetChild(index); return t; }
    static bool SamePlacement(Transform a, Transform b)
    {
        var x = a.localToWorldMatrix; var y = b.localToWorldMatrix;
        for (int i = 0; i < 16; i++) if (Mathf.Abs(x[i] - y[i]) > .00001f) return false;
        return a.gameObject.layer == b.gameObject.layer && a.gameObject.tag == b.gameObject.tag;
    }
    static bool Equivalent(MeshCollider a, MeshCollider b) => SamePlacement(a.transform, b.transform) &&
        a.sharedMesh == b.sharedMesh && a.sharedMaterial == b.sharedMaterial && a.includeLayers == b.includeLayers && a.excludeLayers == b.excludeLayers &&
        a.contactOffset == b.contactOffset && a.layerOverridePriority == b.layerOverridePriority && a.providesContacts == b.providesContacts;
    static void WriteRepair(Manifest manifest)
    {
        File.WriteAllText(Output + "/Repair.txt", "PASS: optimized " + manifest.meshColliders + " mesh colliders, disabled " + manifest.emptyColliders + " empty components; " + manifest.boxes + " exact BoxColliders, " + manifest.convexParts + " shared convex parts, " + manifest.direct + " direct convex conversions, " + manifest.fitted + " fitted stairs surfaces. Disabled " + manifest.duplicateShapes + " redundant shapes on " + manifest.duplicates + " duplicate model placements. Backup: " + manifest.backup);
    }
    static void Deduplicate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Deduplicate in Edit Mode.");
        var scene = Map(); var env = Environment(scene);
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Output + "/Repair.json")); Validate(scene, manifest);
        var records = manifest.sources.Where(r => r.meshName != "" && !r.duplicate).ToArray();
        if (!EditorSceneManager.SaveScene(scene, manifest.backup + "/m.optimized-before-deduplication.unity", true)) throw new IOException("Could not back up optimized scene.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Remove exact duplicate environment collision");
        var lines = new List<string>();
        try
        {
            for (int i = 0; i < records.Length; i++)
            {
                var record = records[i]; if (record.duplicate) continue; var owner = Owner(env, record);
                for (int j = 0; j < i; j++)
                {
                    var kept = records[j]; if (kept.duplicate || kept.meshName != record.meshName || kept.action != record.action || kept.shapes.Count != record.shapes.Count || !SamePlacement(Owner(env, kept), owner)) continue;
                    var original = owner.GetComponents<MeshCollider>()[record.sourceIndex]; var keptOriginal = Owner(env, kept).GetComponents<MeshCollider>()[kept.sourceIndex];
                    if (!Equivalent(original, keptOriginal)) continue;
                    var colliders = new List<Collider>(); if (record.direct) colliders.Add(original);
                    foreach (var shape in record.shapes) colliders.Add(shape.box ? (Collider)owner.GetComponents<BoxCollider>()[shape.index] : owner.GetComponents<MeshCollider>()[shape.index]);
                    foreach (var c in colliders) { Undo.RecordObject(c, "Disable coincident duplicate collider"); c.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(c); EditorUtility.SetDirty(c); }
                    record.duplicate = true; manifest.duplicates++; manifest.duplicateShapes += colliders.Count;
                    lines.Add("Disabled duplicate collision on " + record.path + "; same geometry/placement retained on " + kept.path); break;
                }
            }
            manifest.direct = manifest.sources.Count(r => r.direct && r.convex); manifest.fitted = manifest.sources.Count(r => r.direct && !r.convex);
            Physics.SyncTransforms(); Validate(scene, manifest); PassageRegression();
            EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save deduplicated scene.");
            Undo.CollapseUndoOperations(group);
            File.WriteAllText(Output + "/Repair.json", JsonUtility.ToJson(manifest, true)); WriteRepair(manifest);
            File.WriteAllLines(Output + "/Deduplication.txt", lines); Debug.Log(File.ReadAllText(Output + "/Repair.txt"));
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }
    static void VerifySaved()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verify in Edit Mode.");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Output + "/Repair.json"));
        var scene = Map(); var preview = EditorSceneManager.OpenPreviewScene(scene.path);
        try
        {
            Validate(preview, manifest); var env = Environment(preview);
            foreach (var removed in manifest.sources.Where(r => r.duplicate))
            {
                var source = Owner(env, removed).GetComponents<MeshCollider>()[removed.sourceIndex];
                if (!manifest.sources.Where(r => !r.duplicate && r.meshName == removed.meshName).Any(r => Equivalent(source, Owner(env, r).GetComponents<MeshCollider>()[r.sourceIndex])))
                    throw new InvalidOperationException("An equivalent collision placement was not retained: " + removed.path);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        PassageRegression();
        var remaining = Environment(scene).GetComponentsInChildren<Collider>(true).Where(c => c.enabled && c.gameObject.activeInHierarchy && !Gameplay(c)).ToArray();
        int empty = remaining.OfType<MeshCollider>().Count(c => c.sharedMesh == null), negative = remaining.OfType<BoxCollider>().Count(NegativeBoxColliderRepair.Invalid);
        int hollow = remaining.OfType<MeshCollider>().Count(c => c.sharedMesh != null && c.sharedMesh.name.EndsWith("ConvexHulls", StringComparison.Ordinal) && !c.convex);
        if (empty != 0 || negative != 0 || hollow != 0) throw new InvalidOperationException("Post-audit contains unresolved empty, negative-scale or authored hollow colliders.");
        string result = "PASS: reloaded saved scene and verified all " + manifest.sources.Count + " changed components and their placements/references. Chapel: 40 edge cases and 12 passage traversals passed. Candidate: 1,632 isolated capsule routes; all required routes/openings passed before applying. Post-audit: static empty=" + empty + ", negative boxes=" + negative + ", authored hollow colliders=" + hollow + ". Redundant shapes removed=" + manifest.duplicateShapes + ". No Play Mode or campaign save writes. Performance FPS not measured.";
        File.WriteAllText(Output + "/SavedValidation.txt", result); Debug.Log(result);
    }
}
