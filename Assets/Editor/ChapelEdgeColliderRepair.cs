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
    [Serializable] class Snapshot { public Vector3 playerLocal; }
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/ChapelEdgeColliderRepair.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try { Probe(command == "candidate"); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error.txt", error.ToString()); Debug.LogException(error); }
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static bool IsArch(MeshCollider c) => c.sharedMesh != null && c.sharedMesh.name.IndexOf("CryptArch", StringComparison.OrdinalIgnoreCase) >= 0;

    [MenuItem("Tools/Adventure/Repair/Probe Chapel Edges")]
    public static void ProbeCurrent() => Probe(false);
    public static void Probe(bool candidate)
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
                foreach (float x in new[] { 2.95f, 9.65f })
                {
                    var guide = new GameObject("Chapel smooth side wall " + x); guide.transform.SetParent(root.transform, false);
                    guide.transform.SetPositionAndRotation(env.TransformPoint(new Vector3(x, -1.25f, -7.75f)) + offset, env.rotation);
                    guide.AddComponent<BoxCollider>().size = new Vector3(1.5f, 3.5f, 4.5f);
                }
            }
            Physics.SyncTransforms();
            var captured = JsonUtility.FromJson<Snapshot>(File.ReadAllText(Output + "/LiveBefore.json")).playerLocal;
            var starts = new List<Vector3> { captured, new Vector3(12.6f - captured.x, captured.y, captured.z) };
            foreach (float x in new[] { 3.5f, 3.8f, 8.8f, 9.15f }) foreach (float z in new[] { -5f, -6.6f, -8.05f, -9f }) starts.Add(new Vector3(x, -2.32f, z));
            int total = 0, failed = 0;
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
                    total++; if (!success) failed++;
                    lines.Add((success ? "PASS " : "BLOCKED ") + "start=" + start.ToString("F3") + " direction=" + localDirection + " speed=" + speed + " end=" + end.ToString("F3") + " travel=" + progress.ToString("F3"));
                    if (!success)
                    {
                        Vector3 lower = actor.transform.position + Vector3.up * .3f, upper = actor.transform.position + Vector3.up * 1.7f;
                        foreach (var c in Physics.OverlapCapsule(lower, upper, .33f).Where(c => c != cc)) lines.Add("  CONTACT " + c.name);
                    }
                    UnityEngine.Object.DestroyImmediate(actor);
                }
            }
            lines.Insert(0, "Edge escape tests: " + total + "; blocked=" + failed + "; candidate=" + candidate + ". Actual captured point plus both wall strips; inward and diagonal escape, walk/run.");
            int wallTests = 0, wallFailed = 0;
            foreach (float side in new[] { -1f, 1f })
            foreach (float z in new[] { -5.8f, -6.6f, -7.3f, -8.05f, -8.8f })
            foreach (float speed in new[] { 2f, 4f })
            {
                var actor = new GameObject("Wall approach capsule"); actor.transform.SetParent(root.transform, false);
                actor.transform.position = env.TransformPoint(new Vector3(6.3f, -2.32f, z)) + offset;
                var cc = actor.AddComponent<CharacterController>(); cc.height = 2; cc.radius = .3f; cc.center = Vector3.up; cc.skinWidth = .03f; cc.stepOffset = .25f; cc.slopeLimit = 50;
                Physics.SyncTransforms();
                var outward = env.TransformDirection(Vector3.right * side);
                for (int frame = 0; frame < Mathf.CeilToInt(3.6f * 60 / speed); frame++) cc.Move(outward * (speed / 60) + Vector3.down * (2f / 60));
                Vector3 contact = env.InverseTransformPoint(actor.transform.position - offset);
                var before = actor.transform.position;
                for (int frame = 0; frame < Mathf.CeilToInt(1.2f * 60 / speed); frame++) cc.Move(-outward * (speed / 60) + Vector3.down * (2f / 60));
                Vector3 end = env.InverseTransformPoint(actor.transform.position - offset);
                float progress = Vector3.Dot(actor.transform.position - before, -outward);
                bool success = progress > .8f && end.y > -2.9f;
                wallTests++; if (!success) wallFailed++;
                lines.Add((success ? "WALL PASS " : "WALL BLOCKED ") + "side=" + side + " z=" + z + " speed=" + speed + " contact=" + contact.ToString("F3") + " end=" + end.ToString("F3") + " escaped=" + progress.ToString("F3"));
                if (!success)
                {
                    foreach (var c in Physics.OverlapCapsule(actor.transform.position + Vector3.up * .3f, actor.transform.position + Vector3.up * 1.7f, .33f).Where(c => c != cc)) lines.Add("  WALL CONTACT " + c.name);
                }
                UnityEngine.Object.DestroyImmediate(actor);
            }
            lines.Insert(1, "Wall approach and escape: " + wallTests + "; blocked=" + wallFailed + ". Started in the room, pressed against each wall, then reversed inward.");
            Directory.CreateDirectory(Output); File.WriteAllLines(Output + (candidate ? "/Candidate.txt" : "/Baseline.txt"), lines); Debug.Log(lines[0]);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true); }
    }
}
