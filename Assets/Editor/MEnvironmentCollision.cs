using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-local inspection and repair of exploration collision; never enters Play Mode.</summary>
public static class MEnvironmentCollision
{
    const string Request = "Library/CombatValidation/MEnvironmentCollision.request";
    const string Output = "Tools/Colliders/MEnvironment";
    [InitializeOnLoadMethod] static void Initialize() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MEnvironmentCollision.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); }
        catch (IOException) { return; }
        try { if (command == "inspect") Inspect(); else if (command == "probe") Probe(); else if (command == "candidate") Probe(true); else if (command == "apply") Apply(); else if (command == "verify") VerifySaved(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error.txt", error.ToString()); Debug.LogException(error); }
    }
    [Serializable] class ColliderInfo
    {
        public string id, path, type, mesh, prefab, layer;
        public bool enabled, active, trigger, convex;
        public int vertices, triangles;
        public Vector3 localCenter, worldCenter, size, localMin, localMax, scale;
    }
    [Serializable] class Inspection
    {
        public bool playing, dirty;
        public Vector3 player, playerLocal, camera, cameraLocal, cameraForward;
        public float height, radius, stepOffset, skinWidth;
        public int terrainHoleCells;
        public List<string> viewHits = new List<string>();
        public List<ColliderInfo> colliders = new List<ColliderInfo>();
    }
    static Scene Map() => Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.name == "m");
    static Transform Environment(Scene scene) => scene.GetRootGameObjects().Single(r => r.name == "Environment").transform;
    static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
    [MenuItem("Tools/Adventure/Repair/Inspect m Environment Collision")]
    public static void Inspect()
    {
        var scene = Map(); var env = Environment(scene);
        var report = new Inspection { playing = EditorApplication.isPlaying, dirty = scene.isDirty };
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerScript>(true)).FirstOrDefault();
        if (player != null)
        {
            report.player = player.transform.position; report.playerLocal = env.InverseTransformPoint(report.player);
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) { report.height = cc.height; report.radius = cc.radius; report.stepOffset = cc.stepOffset; report.skinWidth = cc.skinWidth; }
        }
        Physics.SyncTransforms();
        foreach (var terrain in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerrainCollider>(true)))
        {
            var data = terrain.terrainData; if (data == null) continue;
            var holes = data.GetHoles(0, 0, data.holesResolution, data.holesResolution);
            foreach (bool surface in holes) if (!surface) report.terrainHoleCells++;
        }
        var camera = SceneView.lastActiveSceneView?.camera;
        if (camera != null)
        {
            report.camera = camera.transform.position; report.cameraLocal = env.InverseTransformPoint(report.camera); report.cameraForward = env.InverseTransformDirection(camera.transform.forward);
            foreach (var hit in Physics.RaycastAll(camera.ViewportPointToRay(new Vector3(.5f, .5f)), 100, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Take(15))
                report.viewHits.Add(Hierarchy(hit.transform) + " local=" + env.InverseTransformPoint(hit.point).ToString("F3") + " normal=" + env.InverseTransformDirection(hit.normal));
        }
        foreach (var collider in env.GetComponentsInChildren<Collider>(true))
        {
            var info = new ColliderInfo { id = GlobalObjectId.GetGlobalObjectIdSlow(collider).ToString(), path = Hierarchy(collider.transform),
                type = collider.GetType().Name, enabled = collider.enabled, active = collider.gameObject.activeInHierarchy, trigger = collider.isTrigger,
                prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(collider), layer = LayerMask.LayerToName(collider.gameObject.layer),
                worldCenter = collider.bounds.center, localCenter = env.InverseTransformPoint(collider.bounds.center), size = collider.bounds.size,
                scale = collider.transform.lossyScale };
            var b = collider.bounds; var localBounds = new Bounds(env.InverseTransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) localBounds.Encapsulate(env.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            info.localMin = localBounds.min; info.localMax = localBounds.max;
            if (collider is MeshCollider mesh && mesh.sharedMesh != null)
            { info.convex = mesh.convex; info.mesh = AssetDatabase.GetAssetPath(mesh.sharedMesh); info.vertices = mesh.sharedMesh.vertexCount;
                for (int i = 0; i < mesh.sharedMesh.subMeshCount; i++) info.triangles += (int)mesh.sharedMesh.GetIndexCount(i) / 3; }
            report.colliders.Add(info);
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Inspection.json", JsonUtility.ToJson(report, true));
        Debug.Log("m environment collision inspection: " + report.colliders.Count + " colliders. " + Output);
    }

    [MenuItem("Tools/Adventure/Repair/Probe Chapel Passage")]
    public static void ProbeChapel() => Probe();
    public static void Probe(bool candidate = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Probe requires Edit Mode.");
        var scene = Map(); var env = Environment(scene); var active = SceneManager.GetActiveScene();
        var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(active);
        var root = new GameObject("Temporary Chapel collision probe"); SceneManager.MoveGameObjectToScene(root, temporary);
        Vector3 offset = new Vector3(2000, 2000, 2000);
        var report = new List<string>();
        var temporaryMeshes = new List<Mesh>();
        try
        {
            var region = new Bounds(env.TransformPoint(new Vector3(6.3f, -1, -5)), new Vector3(40, 16, 40));
            var sources = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.bounds.Intersects(region) && !(c is CharacterController) && c.GetComponentInParent<PlayerScript>() == null).ToArray();
            foreach (var terrain in sources.OfType<TerrainCollider>()) report.Add("TERRAIN " + Hierarchy(terrain.transform) + " data=" + AssetDatabase.GetAssetPath(terrain.terrainData) + " size=" + terrain.terrainData.size + " holes=" + terrain.terrainData.holesResolution);
            foreach (var c in sources.Where(c => c.name == "Cube38" || c.name == "sm_Crypt_04_5" || c.name == "sm_Debris_04_01_FromFoliage_1274"))
                report.Add("IMPORTANT " + Hierarchy(c.transform) + " center=" + env.InverseTransformPoint(c.bounds.center) + " size=" + c.bounds.size);
            foreach (var source in sources)
            {
                if (candidate && IsLooseDecoration(source, env, sources)) { report.Add("DISABLE " + Hierarchy(source.transform)); continue; }
                var copy = new GameObject(Hierarchy(source.transform)); copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation); copy.transform.localScale = source.transform.lossyScale;
                if (source is MeshCollider mesh)
                {
                    var c = copy.AddComponent<MeshCollider>(); c.sharedMesh = mesh.sharedMesh; c.convex = mesh.convex;
                    if (candidate && ShouldUseVisualCollision(mesh))
                    {
                        var filter = mesh.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.EndsWith("LOD2"));
                        if (filter != null) { c.sharedMesh = filter.sharedMesh; report.Add("CEILING " + Hierarchy(source.transform) + " -> " + filter.sharedMesh.name + " vertices=" + filter.sharedMesh.vertexCount); }
                    }
                }
                else if (source is BoxCollider box) { var c = copy.AddComponent<BoxCollider>(); c.center = box.center; c.size = box.size; }
                else if (source is TerrainCollider terrain)
                {
                    var c = copy.AddComponent<TerrainCollider>(); c.terrainData = terrain.terrainData;
                }
                else { UnityEngine.Object.DestroyImmediate(copy); }
            }
            if (candidate)
            {
                var ramp = new GameObject("Chapel entrance smooth collision ramp"); ramp.transform.SetParent(root.transform, false);
                ramp.transform.SetPositionAndRotation(env.position + offset, env.rotation);
                var mesh = EntranceRamp(); temporaryMeshes.Add(mesh); ramp.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            Physics.SyncTransforms();
            var sampleText = new List<string> { "x,z,y,normalY,collider" };
            for (float z = 2; z >= -12; z -= .25f)
            for (float x = 5.5f; x <= 7.1f; x += .2f)
            {
                Vector3 origin = env.TransformPoint(new Vector3(x, .25f, z)) + offset;
                foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore).Where(h => h.normal.y > .5f).OrderBy(h => h.distance).Take(4))
                    sampleText.Add(x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," +
                        env.InverseTransformPoint(hit.point - offset).y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + hit.normal.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + hit.collider.name);
            }
            Directory.CreateDirectory(Output); File.WriteAllLines(Output + (candidate ? "/CandidateGround.csv" : "/GroundSamples.csv"), sampleText);
            foreach (float speed in new[] { 2f, 4f })
            foreach (float lane in new[] { 5.95f, 6.3f, 6.65f })
            {
                var actor = new GameObject("Temporary player capsule"); actor.transform.SetParent(root.transform, false);
                var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                actor.transform.position = env.TransformPoint(new Vector3(lane, -.8f, 1.5f)) + offset;
                Physics.SyncTransforms(); for (int i = 0; i < 80; i++) cc.Move(Vector3.down * .025f);
                report.Add("Lane " + lane + " speed=" + speed + " start=" + env.InverseTransformPoint(actor.transform.position - offset).ToString("F3"));
                foreach (int direction in new[] { -1, 1 })
                {
                    Vector3 horizontal = env.TransformDirection(Vector3.forward * direction); int stalled = 0;
                    for (int frame = 0; frame < 420; frame++)
                    {
                        Vector3 before = actor.transform.position;
                        var flags = cc.Move(horizontal * (speed / 60) + Vector3.down * (2f / 60));
                        Vector3 local = env.InverseTransformPoint(actor.transform.position - offset);
                        if (frame % 30 == 0) report.Add("  dir=" + direction + " frame=" + frame + " at=" + local.ToString("F3") + " flags=" + flags);
                        if (Vector3.Dot(actor.transform.position - before, horizontal) < .003f) stalled++; else stalled = 0;
                        if (stalled > 12)
                        {
                            report.Add("  BLOCKED at " + local.ToString("F3"));
                            Vector3 bottom = actor.transform.position + Vector3.up * .32f, top = actor.transform.position + Vector3.up * 1.68f;
                            foreach (var c in Physics.OverlapCapsule(bottom + horizontal * .08f, top + horizontal * .08f, .32f).Where(c => c != cc).Take(15))
                                report.Add("    " + c.name);
                            break;
                        }
                        if (direction < 0 && local.z < -9.8f || direction > 0 && local.z > 1.5f) break;
                    }
                    float endpoint = env.InverseTransformPoint(actor.transform.position - offset).z;
                    report.Add("  END dir=" + direction + " z=" + endpoint.ToString("F3"));
                    if (direction < 0 && endpoint > -9.3f || direction > 0 && endpoint < 1.4f) report.Add("  INCOMPLETE traversal");
                }
                UnityEngine.Object.DestroyImmediate(actor);
            }
            File.WriteAllLines(Output + (candidate ? "/CandidateProbe.txt" : "/PassageProbe.txt"), report); Debug.Log("Chapel passage probe complete: " + Output);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); SceneManager.SetActiveScene(active); EditorSceneManager.CloseScene(temporary, true);
            foreach (var mesh in temporaryMeshes) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    static bool IsLooseDecoration(Collider collider, Transform env, Collider[] all)
    {
        string path = Hierarchy(collider.transform);
        if (collider.GetComponentInParent<WorldInteraction>() != null || collider.GetComponentInParent<Animator>() != null) return false;
        if ((path.Contains("/HumanRemains/") || path.Contains("/OldCup/")) && collider.attachedRigidbody == null && !collider.isTrigger &&
            collider.bounds.size.y < .85f && Mathf.Max(collider.bounds.size.x, collider.bounds.size.z) < 1.6f) return true;
        if (!path.Contains("/Debris/") && !path.Contains("/Brick/")) return false;
        if (collider.attachedRigidbody != null || collider.isTrigger || collider.bounds.size.y > .24f || Mathf.Max(collider.bounds.size.x, collider.bounds.size.z) > 1.35f) return false;
        if (path.Contains("/Debris/")) return true;
        Vector3 origin = collider.bounds.center; origin.y = collider.bounds.max.y + .05f;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, .7f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (hit.collider == collider || !all.Contains(hit.collider) || hit.normal.y < .75f) continue;
            string support = Hierarchy(hit.transform);
            if (support.Contains("/Debris/") || support.Contains("/Brick/")) continue;
            return collider.bounds.max.y - hit.point.y <= .24f;
        }
        return false;
    }
    static bool ShouldUseVisualCollision(MeshCollider collider) => collider.sharedMesh != null &&
        (collider.sharedMesh.name.IndexOf("CryptCeiling", StringComparison.OrdinalIgnoreCase) >= 0 ||
        collider.sharedMesh.name.IndexOf("CryptEntrance", StringComparison.OrdinalIgnoreCase) >= 0);
    static Mesh EntranceRamp()
    {
        var mesh = new Mesh { name = "Chapel_Entrance_SmoothCollision" };
        float left = 5.65f, right = 6.95f, front = -2.45f, back = -4.2f, high = -1.68f, low = -2.35f;
        var vertices = new[] { new Vector3(left, high, front), new Vector3(right, high, front), new Vector3(left, low, back), new Vector3(right, low, back),
            new Vector3(left, high - .3f, front), new Vector3(right, high - .3f, front), new Vector3(left, low - .3f, back), new Vector3(right, low - .3f, back) };
        mesh.vertices = vertices; mesh.triangles = new[] { 0,1,2, 1,3,2, 4,6,5, 5,6,7, 0,4,1, 1,4,5, 2,3,6, 3,7,6, 0,2,4, 2,6,4, 1,5,3, 3,5,7 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    [MenuItem("Tools/Adventure/Repair/Apply m Environment Collision Repair")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Repair requires Edit Mode.");
        var scene = Map(); var env = Environment(scene);
        string candidate = Output + "/CandidateProbe.txt";
        if (!File.Exists(candidate) || File.ReadAllText(candidate).Contains("BLOCKED") || File.ReadAllText(candidate).Contains("INCOMPLETE")) throw new InvalidOperationException("Complete the candidate passage test first.");
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true)) throw new IOException("Could not back up the open scene.");
        File.Copy(Output + "/PassageProbe.txt", backup + "/BeforeProbe.txt");
        File.Copy(Output + "/Inspection.json", backup + "/BeforeInspection.json");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger).ToArray();
        var disable = env.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && IsLooseDecoration(c, env, all)).ToArray();
        var replacements = env.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled && ShouldUseVisualCollision(c))
            .Select(c => new { collider = c, filter = c.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.EndsWith("LOD2")) }).Where(p => p.filter != null && p.collider.sharedMesh != p.filter.sharedMesh).ToArray();
        foreach (var p in replacements)
            if (Vector3.Distance(p.collider.transform.position, p.filter.transform.position) > .001f ||
                Quaternion.Angle(p.collider.transform.rotation, p.filter.transform.rotation) > .01f || Vector3.Distance(p.collider.transform.lossyScale, p.filter.transform.lossyScale) > .001f)
                throw new InvalidOperationException("Review transformed LOD collision separately: " + Hierarchy(p.collider.transform));
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Repair m exploration collision");
        var lines = new List<string> { "Backup: " + backup };
        try
        {
            foreach (var c in disable)
            {
                Undo.RecordObject(c, "Disable small decorative collision"); c.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(c); EditorUtility.SetDirty(c); lines.Add("Disabled " + Hierarchy(c.transform));
            }
            foreach (var p in replacements)
            {
                Undo.RecordObject(p.collider, "Fit vaulted collision to model"); p.collider.sharedMesh = p.filter.sharedMesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(p.collider); EditorUtility.SetDirty(p.collider);
                lines.Add("Fitted " + Hierarchy(p.collider.transform) + " -> " + p.filter.sharedMesh.name);
            }
            const string meshPath = "Assets/Adventure/Generated/Collision/Chapel_Entrance_SmoothCollision.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Adventure/Generated/Collision")) AssetDatabase.CreateFolder("Assets/Adventure/Generated", "Collision");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = EntranceRamp(); AssetDatabase.CreateAsset(mesh, meshPath); AssetDatabase.SaveAssetIfDirty(mesh); }
            var geometry = env.Find("Collision Geometry");
            if (geometry == null)
            {
                var obj = new GameObject("Collision Geometry"); Undo.RegisterCreatedObjectUndo(obj, "Add collision geometry"); obj.transform.SetParent(env, false); geometry = obj.transform;
            }
            if (geometry.Find("Chapel Entrance Ramp") == null)
            {
                var ramp = new GameObject("Chapel Entrance Ramp"); Undo.RegisterCreatedObjectUndo(ramp, "Smooth Chapel entry steps");
                ramp.transform.SetParent(geometry, false); ramp.layer = LayerMask.NameToLayer("Surface");
                var collider = Undo.AddComponent<MeshCollider>(ramp); collider.sharedMesh = mesh;
            }
            Physics.SyncTransforms();
            Probe();
            string traversal = File.ReadAllText(Output + "/PassageProbe.txt");
            if (traversal.Contains("BLOCKED") || traversal.Contains("INCOMPLETE")) throw new InvalidOperationException("Live-scene collision repair did not pass the passage test.");
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save repaired scene.");
            File.Copy(Output + "/PassageProbe.txt", Output + "/ValidationAfter.txt", true);
            lines.Insert(0, "PASS: " + disable.Length + " small scenery colliders disabled; " + replacements.Length + " vaulted colliders fitted; entrance ramp added. Three lanes entered and returned. No Play Mode/campaign/save.");
            Undo.CollapseUndoOperations(group);
            Inspect();
            Debug.Log(lines[0]);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group); throw;
        }
        finally { File.WriteAllLines(Output + "/Repair.txt", lines); }
    }

    [MenuItem("Tools/Adventure/Repair/Verify Saved m Collision")]
    public static void VerifySaved()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Validation requires Edit Mode.");
        var scene = Map(); var saved = EditorSceneManager.OpenPreviewScene(scene.path); int disabled = 0, fitted = 0;
        try
        {
            var env = Environment(saved);
            foreach (string line in File.ReadAllLines(Output + "/Repair.txt"))
            {
                if (line.StartsWith("Disabled "))
                {
                    var path = line.Substring("Disabled Environment/".Length); var obj = env.Find(path);
                    if (obj == null || obj.GetComponents<Collider>().Any(c => c.enabled)) throw new InvalidOperationException("Disabled collider was not saved: " + path);
                    disabled++;
                }
                if (line.StartsWith("Fitted "))
                {
                    var path = line.Substring("Fitted Environment/".Length).Split(new[] { " -> " }, StringSplitOptions.None)[0];
                    var obj = env.Find(path); var c = obj?.GetComponent<MeshCollider>();
                    if (c == null || c.sharedMesh == null || !c.sharedMesh.name.EndsWith("LOD2")) throw new InvalidOperationException("Fitted collider mesh was not saved: " + path);
                    fitted++;
                }
            }
            var ramp = env.Find("Collision Geometry/Chapel Entrance Ramp")?.GetComponent<MeshCollider>();
            if (ramp == null || ramp.sharedMesh == null || ramp.sharedMesh.vertexCount != 8) throw new InvalidOperationException("Ramp asset/reference missing after scene reload.");
        }
        finally { EditorSceneManager.ClosePreviewScene(saved); }
        Probe();
        string movement = File.ReadAllText(Output + "/PassageProbe.txt");
        if (movement.Contains("BLOCKED") || movement.Contains("INCOMPLETE")) throw new InvalidOperationException("Walk/run passage validation failed.");
        File.Copy(Output + "/PassageProbe.txt", Output + "/ValidationAfter.txt", true);
        string summary = "PASS: saved scene reloaded with " + disabled + " decorative colliders disabled, " + fitted + " fitted vaulted colliders and persistent ramp mesh. " +
            "Walk 2 m/s and run 4 m/s: three lanes entered and returned without trapping. No Play Mode/campaign/save.";
        File.WriteAllText(Output + "/SavedValidation.txt", summary); Debug.Log(summary);
        Inspect();
    }
}
