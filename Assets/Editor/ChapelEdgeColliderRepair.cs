using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChapelEdgeColliderRepair
{
    const string Request = "Library/CombatValidation/ChapelEdges.request";
    const string Output = "Tools/Colliders/ChapelEdges";
    static readonly string[] GuideNames = { "Chapel Left Smooth Wall", "Chapel Right Smooth Wall" };
    static readonly float[] GuideX = { 2.95f, 9.65f };
    static readonly Vector3 GuideSize = new Vector3(1f, 3.5f, 4.5f);
    [Serializable] class Snapshot { public Vector3 playerLocal; }
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/ChapelEdgeColliderRepair.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try
        {
            if (command == "apply") Apply();
            else if (command == "verify") VerifySaved();
            else Probe(command == "candidate");
        }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error.txt", error.ToString()); Debug.LogException(error); }
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static bool IsArch(MeshCollider c) => c.sharedMesh != null && c.sharedMesh.name.IndexOf("CryptArch", StringComparison.OrdinalIgnoreCase) >= 0;

    [MenuItem("Tools/Adventure/Repair/Probe Chapel Edges")]
    public static void ProbeCurrent() => Probe(false);
    public static int Probe(bool candidate)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run edge tests in Edit Mode.");
        var original = SceneManager.GetActiveScene();
        var map = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.name == "m");
        var env = map.GetRootGameObjects().Single(r => r.name == "Environment").transform;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(original);
        var root = new GameObject("Temporary Chapel edge tests"); SceneManager.MoveGameObjectToScene(root, scene);
        var offset = new Vector3(2000, 2000, 2000); var lines = new List<string>();
        try
        {
            var region = new Bounds(env.TransformPoint(new Vector3(6.3f, -1, -7)), new Vector3(35, 14, 35));
            foreach (var source in map.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c.bounds.Intersects(region) && !(c is CharacterController) && c.GetComponentInParent<PlayerScript>() == null))
            {
                if (candidate && PathOf(source.transform) == "Environment/sm_Rubble_02_02") { lines.Add("Disabled decorative rubble " + PathOf(source.transform)); continue; }
                var go = new GameObject(PathOf(source.transform)); go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation); go.transform.localScale = source.transform.lossyScale;
                if (source is MeshCollider mesh)
                {
                    var c = go.AddComponent<MeshCollider>(); c.sharedMesh = mesh.sharedMesh; c.convex = mesh.convex;
                    if (candidate && IsArch(mesh))
                    {
                        var filter = mesh.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.EndsWith("LOD2"));
                        if (filter != null) { c.sharedMesh = filter.sharedMesh; lines.Add("Fitted " + PathOf(mesh.transform) + " -> " + filter.sharedMesh.name); }
                    }
                }
                else if (source is BoxCollider box) { var c = go.AddComponent<BoxCollider>(); c.center = box.center; c.size = box.size; }
                else if (source is TerrainCollider terrain) { var c = go.AddComponent<TerrainCollider>(); c.terrainData = terrain.terrainData; }
                else UnityEngine.Object.DestroyImmediate(go);
            }
            if (candidate)
            {
                for (int i = 0; i < GuideNames.Length; i++)
                {
                    if (env.Find("Collision Geometry/" + GuideNames[i]) != null) continue;
                    var guide = new GameObject(GuideNames[i]); guide.transform.SetParent(root.transform, false);
                    guide.transform.SetPositionAndRotation(env.TransformPoint(new Vector3(GuideX[i], -1.25f, -7.75f)) + offset, env.rotation);
                    guide.AddComponent<BoxCollider>().size = GuideSize;
                }
            }
            Physics.SyncTransforms();
            var captured = JsonUtility.FromJson<Snapshot>(File.ReadAllText(Output + "/LiveBefore.json")).playerLocal;
            var starts = new List<Vector3> { captured, new Vector3(12.6f - captured.x, captured.y, captured.z) };
            foreach (float x in new[] { 3.5f, 3.8f, 8.8f, 9.15f }) foreach (float z in new[] { -5f, -6.6f, -8.05f, -9f }) starts.Add(new Vector3(x, -2.32f, z));
            int total = 0, failed = 0, criticalFailures = 0, startIndex = 0;
            foreach (var start in starts)
            {
                float inward = start.x > 6.3f ? -1 : 1;
                foreach (float speed in new[] { 2f, 4f })
                foreach (var localDirection in new[] { new Vector3(inward, 0, 0), new Vector3(inward, 0, 1).normalized, new Vector3(inward, 0, -1).normalized })
                {
                    var actor = new GameObject("Edge capsule"); actor.transform.SetParent(root.transform, false);
                    actor.transform.position = env.TransformPoint(start) + offset;
                    var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                    Physics.SyncTransforms(); Vector3 before = actor.transform.position;
                    Vector3 direction = env.TransformDirection(localDirection);
                    for (int frame = 0; frame < Mathf.CeilToInt(1.2f * 60 / speed); frame++) cc.Move(direction * (speed / 60) + Vector3.down * (2f / 60));
                    Vector3 end = env.InverseTransformPoint(actor.transform.position - offset);
                    float progress = Vector3.Dot(actor.transform.position - before, direction);
                    bool success = progress > .6f && end.y > -2.9f;
                    total++; if (!success) { failed++; if (startIndex < 2) criticalFailures++; }
                    lines.Add((success ? "PASS " : "BLOCKED ") + "start=" + start.ToString("F3") + " direction=" + localDirection + " speed=" + speed + " end=" + end.ToString("F3") + " travel=" + progress.ToString("F3"));
                    if (!success)
                    {
                        Vector3 lower = actor.transform.position + Vector3.up * .3f, upper = actor.transform.position + Vector3.up * 1.7f;
                        foreach (var c in Physics.OverlapCapsule(lower, upper, .33f).Where(c => c != cc)) lines.Add("  CONTACT " + c.name);
                    }
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                startIndex++;
            }
            lines.Insert(0, "Edge escape tests: " + total + "; blocked=" + failed + "; candidate=" + candidate + ". Actual captured point plus both wall strips; inward and diagonal escape, walk/run.");
            int wallTests = 0, wallFailed = 0;
            foreach (float side in new[] { -1f, 1f })
            foreach (float z in new[] { -5.8f, -6.6f, -7.3f, -8.05f, -8.8f })
            foreach (float speed in new[] { 2f, 4f })
            {
                var actor = new GameObject("Wall approach capsule"); actor.transform.SetParent(root.transform, false);
                var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                EnterRoom(actor, cc, env, offset, z, speed);
                var outward = env.TransformDirection(Vector3.right * side);
                for (int frame = 0; frame < Mathf.CeilToInt(3.6f * 60 / speed); frame++) cc.Move(outward * (speed / 60) + Vector3.down * (2f / 60));
                Vector3 contact = env.InverseTransformPoint(actor.transform.position - offset);
                var before = actor.transform.position;
                for (int frame = 0; frame < Mathf.CeilToInt(1.2f * 60 / speed); frame++) cc.Move(-outward * (speed / 60) + Vector3.down * (2f / 60));
                Vector3 end = env.InverseTransformPoint(actor.transform.position - offset);
                float progress = Vector3.Dot(actor.transform.position - before, -outward);
                bool success = progress > .8f && end.y > -2.9f;
                wallTests++; if (!success) { wallFailed++; criticalFailures++; }
                lines.Add((success ? "WALL PASS " : "WALL BLOCKED ") + "side=" + side + " z=" + z + " speed=" + speed + " contact=" + contact.ToString("F3") + " end=" + end.ToString("F3") + " escaped=" + progress.ToString("F3"));
                if (!success)
                {
                    foreach (var c in Physics.OverlapCapsule(actor.transform.position + Vector3.up * .3f, actor.transform.position + Vector3.up * 1.7f, .33f).Where(c => c != cc)) lines.Add("  WALL CONTACT " + c.name);
                }
                UnityEngine.Object.DestroyImmediate(actor);
            }
            lines.Insert(1, "Wall approach and escape: " + wallTests + "; blocked=" + wallFailed + ". Started in the room, pressed against each wall, then reversed inward.");
            int slideFailures = 0;
            foreach (float side in new[] { -1f, 1f })
            foreach (float along in new[] { -1f, 1f })
            foreach (float speed in new[] { 2f, 4f })
            {
                var actor = new GameObject("Wall slide capsule"); actor.transform.SetParent(root.transform, false);
                var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                EnterRoom(actor, cc, env, offset, along < 0 ? -6.3f : -8.4f, speed);
                var outward = env.TransformDirection(Vector3.right * side);
                for (int frame = 0; frame < Mathf.CeilToInt(3.6f * 60 / speed); frame++) cc.Move(outward * (speed / 60) + Vector3.down * (2f / 60));
                Vector3 before = actor.transform.position;
                var alongWall = env.TransformDirection(Vector3.forward * along);
                var diagonal = (outward + alongWall).normalized;
                for (int frame = 0; frame < Mathf.CeilToInt(2.6f * 60 / speed); frame++) cc.Move(diagonal * (speed / 60) + Vector3.down * (2f / 60));
                float travel = Vector3.Dot(actor.transform.position - before, alongWall);
                before = actor.transform.position;
                for (int frame = 0; frame < Mathf.CeilToInt(1.2f * 60 / speed); frame++) cc.Move(-outward * (speed / 60) + Vector3.down * (2f / 60));
                float escape = Vector3.Dot(actor.transform.position - before, -outward);
                bool success = travel > 1.4f && escape > .8f;
                if (!success) { slideFailures++; criticalFailures++; }
                lines.Add((success ? "SLIDE PASS " : "SLIDE BLOCKED ") + "side=" + side + " along=" + along + " speed=" + speed + " travel=" + travel.ToString("F3") + " escape=" + escape.ToString("F3"));
                UnityEngine.Object.DestroyImmediate(actor);
            }
            lines.Insert(0, "Required tests: captured position + mirrored position (12), approach both walls from entry (20), slide along both walls and escape (8). Failures=" + criticalFailures);
            lines.Insert(3, "Wall sliding: 8; blocked=" + slideFailures + ". Broad grid survey may include points inside solid front walls or furniture; it is reported separately.");
            Directory.CreateDirectory(Output); File.WriteAllLines(Output + (candidate ? "/Candidate.txt" : "/Baseline.txt"), lines); Debug.Log(lines[0]);
            return criticalFailures;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true); }
    }

    static void EnterRoom(GameObject actor, CharacterController cc, Transform env, Vector3 offset, float z, float speed)
    {
        // Approach from the existing passage instead of spawning inside uneven floor stones.
        actor.transform.position = env.TransformPoint(new Vector3(6.3f, -.8f, 1.5f)) + offset;
        Physics.SyncTransforms();
        for (int frame = 0; frame < 80; frame++) cc.Move(Vector3.down * .025f);
        for (int frame = 0; frame < 600; frame++)
        {
            if (env.InverseTransformPoint(actor.transform.position - offset).z <= z) break;
            cc.Move(env.TransformDirection(Vector3.back) * (speed / 60) + Vector3.down * (2f / 60));
        }
    }

    static void Apply()
    {
        if (Probe(true) != 0) throw new InvalidOperationException("Candidate edge tests must pass before applying.");
        var scene = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.name == "m");
        var env = scene.GetRootGameObjects().Single(r => r.name == "Environment").transform;
        var arches = env.GetComponentsInChildren<MeshCollider>(true).Where(c => c.enabled && IsArch(c)).ToArray();
        if (arches.Length != 3) throw new InvalidOperationException("Expected the three inspected Chapel arches; inspect changes before repairing.");
        foreach (var arch in arches)
        {
            var filter = arch.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.EndsWith("LOD2"));
            if (filter == null || filter.sharedMesh == null || Vector3.Distance(filter.transform.position, arch.transform.position) > .001f ||
                Quaternion.Angle(filter.transform.rotation, arch.transform.rotation) > .01f || Vector3.Distance(filter.transform.lossyScale, arch.transform.lossyScale) > .001f)
                throw new InvalidOperationException("Arch LOD transform mismatch: " + PathOf(arch.transform));
        }
        var rubble = env.Find("sm_Rubble_02_02")?.GetComponent<MeshCollider>();
        var floor = env.Find("Cube/Cube38")?.GetComponent<BoxCollider>();
        if (rubble == null || floor == null || !floor.enabled) throw new InvalidOperationException("Inspected rubble or basement support floor is missing.");
        var supportRay = new Ray(env.TransformPoint(new Vector3(9.16f, -1.6f, -8.05f)), Vector3.down);
        if (!floor.Raycast(supportRay, out var support, 2f) || env.InverseTransformPoint(support.point).y < -2.4f)
            throw new InvalidOperationException("Rubble must have the existing solid floor underneath it.");

        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        if (!EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true)) throw new IOException("Could not back up current in-memory scene.");
        File.Copy(Output + "/Candidate.txt", backup + "/Candidate.txt");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Repair Chapel side collision");
        var lines = new List<string> { "Backup: " + backup };
        try
        {
            foreach (var arch in arches)
            {
                var filter = arch.GetComponentsInChildren<MeshFilter>(true).First(f => f.name.EndsWith("LOD2"));
                Undo.RecordObject(arch, "Fit arch collision to vaulted opening"); arch.sharedMesh = filter.sharedMesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(arch); EditorUtility.SetDirty(arch);
                lines.Add("Fitted " + PathOf(arch.transform) + " -> " + filter.sharedMesh.name);
            }
            Undo.RecordObject(rubble, "Remove coarse decorative rubble collision"); rubble.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rubble); EditorUtility.SetDirty(rubble);
            lines.Add("Disabled " + PathOf(rubble.transform));
            var geometry = env.Find("Collision Geometry");
            if (geometry == null)
            {
                var go = new GameObject("Collision Geometry"); Undo.RegisterCreatedObjectUndo(go, "Add collision geometry");
                go.transform.SetParent(env, false); geometry = go.transform;
            }
            for (int i = 0; i < GuideNames.Length; i++)
            {
                var guide = geometry.Find(GuideNames[i]);
                if (guide == null)
                {
                    var go = new GameObject(GuideNames[i]); Undo.RegisterCreatedObjectUndo(go, "Smooth low-vault wall clearance");
                    go.transform.SetParent(geometry, false); guide = go.transform;
                }
                Undo.RecordObject(guide, "Fit side clearance");
                guide.localPosition = new Vector3(GuideX[i], -1.25f, -7.75f); guide.localRotation = Quaternion.identity; guide.localScale = Vector3.one;
                guide.gameObject.layer = LayerMask.NameToLayer("Surface");
                var box = guide.GetComponent<BoxCollider>(); if (box == null) box = Undo.AddComponent<BoxCollider>(guide.gameObject);
                Undo.RecordObject(box, "Smooth side wall"); box.center = Vector3.zero; box.size = GuideSize;
                lines.Add("Guide " + PathOf(guide));
            }
            Physics.SyncTransforms();
            if (Probe(false) != 0) throw new InvalidOperationException("Required edge tests failed on repaired scene.");
            MEnvironmentCollision.Probe();
            string passage = File.ReadAllText("Tools/Colliders/MEnvironment/PassageProbe.txt");
            if (passage.Contains("BLOCKED") || passage.Contains("INCOMPLETE")) throw new InvalidOperationException("Center passage regression test failed.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save repaired Chapel scene.");
            Undo.CollapseUndoOperations(group);
            File.Copy(Output + "/Baseline.txt", Output + "/ValidationAfter.txt", true);
            lines.Insert(0, "PASS: three arch colliders fitted, one coarse rubble collider disabled over existing floor, two smooth side-wall guides. Required edge tests and center passage passed. No Play Mode or campaign save writes.");
            File.WriteAllLines(Output + "/Repair.txt", lines); Debug.Log(lines[0]);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group); File.WriteAllLines(Output + "/RepairFailed.txt", lines); throw;
        }
    }

    static void VerifySaved()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Verify in Edit Mode.");
        var scene = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Single(s => s.name == "m");
        var preview = EditorSceneManager.OpenPreviewScene(scene.path);
        try
        {
            var env = preview.GetRootGameObjects().Single(r => r.name == "Environment").transform;
            var arches = env.GetComponentsInChildren<MeshCollider>(true).Where(c => c.enabled && IsArch(c)).ToArray();
            if (arches.Length != 3 || arches.Any(c => !c.sharedMesh.name.EndsWith("LOD2"))) throw new InvalidOperationException("Arch changes were not saved.");
            if (env.Find("sm_Rubble_02_02").GetComponent<MeshCollider>().enabled) throw new InvalidOperationException("Rubble collider change was not saved.");
            for (int i = 0; i < GuideNames.Length; i++)
            {
                var t = env.Find("Collision Geometry/" + GuideNames[i]); var box = t?.GetComponent<BoxCollider>();
                if (box == null || !box.enabled || box.size != GuideSize || Vector3.Distance(t.localPosition, new Vector3(GuideX[i], -1.25f, -7.75f)) > .001f)
                    throw new InvalidOperationException("Side-wall guide was not saved: " + GuideNames[i]);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        if (Probe(false) != 0) throw new InvalidOperationException("Saved Chapel edge regression tests failed.");
        MEnvironmentCollision.VerifySaved();
        File.Copy(Output + "/Baseline.txt", Output + "/ValidationAfter.txt", true);
        File.WriteAllText(Output + "/SavedValidation.txt", "PASS: saved scene references verified. Captured point/mirror: 12; wall approach/escape: 20; wall sliding/escape: 8; middle walk/run passage: 12 traversals. All required tests passed. No Play Mode, no gameplay save writes.");
        Debug.Log("Saved Chapel edge collision validation passed.");
    }
}
