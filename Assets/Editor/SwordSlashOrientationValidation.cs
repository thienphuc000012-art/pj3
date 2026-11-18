using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;

public static class SwordSlashOrientationValidation
{
    const string Request = "Library/CombatValidation/SwordSlashOrientation.request";
    const string Output = "Tools/SlashOrientation";
    const string Free = "Assets/asset/Free Slash VFX/Prefabs";
    const string Stylized = "Assets/Vefects/Stylized VFX/Stylized VFX Shuriken/Skills/Slashes Piercing";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/SwordSlashOrientationValidation.cs")) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if (command == "verify") Validate(); else Inspect(); }
        catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error.txt", e.ToString()); Debug.LogException(e); }
    }
    static string[] Paths() => AssetDatabase.FindAssets("t:Prefab", new[] { Free, Stylized }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
    [MenuItem("Tools/Adventure/Combat/Validate Sword Slash Orientation")]
    public static void Validate()
    {
        var report = new StringBuilder(); var details = new StringBuilder();
        Scene preview = EditorSceneManager.NewPreviewScene();
        int cases = 0, prefabs = 0;
        var oldManager = CombatManager.Instance;
        try
        {
            var cameraGo = new GameObject("Slash orientation camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraGo, preview);
            var camera = cameraGo.GetComponent<Camera>(); camera.enabled = false;
            camera.scene = preview; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .05f);
            camera.transform.position = new Vector3(0, 0, -8); camera.transform.rotation = Quaternion.identity;
            foreach (string path in Paths().Where(p => (p.Contains("/Slash ") && !p.Contains("/Projectiles/")) || p.Contains("/VFX_Slash_")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); string before = Hash(path);
                Check(CombatSlashOrientation.TryGetAuthoredFrame(prefab, out var frame), "Missing primary slash mesh: " + path);
                Check(!frame.meshTransform.name.ToLowerInvariant().Contains("disto"), "Distortion was selected over the visible slash: " + path);
                prefabs++;
                foreach (var pose in new[] { Quaternion.identity, Quaternion.Euler(0, 0, 90), Quaternion.Euler(25, -40, 37), Quaternion.Euler(-28, 127, -54) })
                {
                    Vector3 sweep = pose * Vector3.right, blade = pose * Vector3.up, normal = Vector3.Cross(sweep, blade);
                    Quaternion rotation = CombatSlashOrientation.Rotation(prefab, sweep, blade, normal);
                    var clone = Clone(prefab, preview); var localPath = Relative(frame.meshTransform, prefab.transform);
                    var renderer = (localPath.Length == 0 ? clone.transform : clone.transform.Find(localPath)).GetComponent<ParticleSystemRenderer>();
                    Mesh baked = new Mesh();
                    try
                    {
                        clone.transform.rotation = rotation;
                        var system = renderer.GetComponent<ParticleSystem>(); system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        system.useAutoRandomSeed = false; system.randomSeed = 422;
                        foreach (float time in new[] { .08f, .18f, .28f, .4f, .6f })
                        {
                            system.Play(false); system.Simulate(time, false, true, false);
                            renderer.BakeMesh(baked, camera, ParticleSystemBakeMeshOptions.BakeRotationAndScale);
                            if (baked.vertexCount > 0 && system.particleCount > 0) break;
                        }
                        Check(baked.vertexCount > 0, "Slash emitted no bakeable mesh: " + path);
                        float deviation = Deviation(baked.vertices, normal);
                        details.AppendLine(path + " | " + pose.eulerAngles + " primary=" + renderer.name + " deviation=" + deviation + " birth=" + system.main.startRotationX.constantMax * Mathf.Rad2Deg);
                        Check(deviation < .12f, "Actual particle geometry is not in the sword plane: " + path + " deviation=" + deviation);
                        cases++;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(clone); }
                }
                Check(before == Hash(path), "Validation modified a package prefab: " + path);
            }
            report.AppendLine("PASS: " + cases + " native particle mesh bakes across " + prefabs + " slash prefabs in both requested packages, with horizontal/vertical/diagonal/yawed sword frames; visible slash chosen over distortion, authored 90/45-degree particle rotations compensated.");
            ValidateCurrentSwordSample(preview);
            report.AppendLine("PASS: current animation-event blade pose and latest distinct motion sample; translated/rotated blades; inactive/stowed trail rejected.");
            ValidateManagerSpawn(preview, camera);
            report.AppendLine("PASS: actual CombatManager spawns and local Euler corrections; in-flight non-projectile slash keeps the release sword plane rather than world-up.");
            report.AppendLine("Edit Mode only. No live scene, package prefab, ActionData or player save writes.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString()); File.WriteAllText(Output + "/Cases.txt", details.ToString());
            Debug.Log("[Sword Slash Orientation] " + report);
        }
        catch { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Cases.txt", details.ToString()); throw; }
        finally { CombatManager.Instance = oldManager; EditorSceneManager.ClosePreviewScene(preview); }
    }
    static void ValidateCurrentSwordSample(Scene preview)
    {
        var go = new GameObject("Sword pose sampler"); SceneManager.MoveGameObjectToScene(go, preview);
        var trail = go.AddComponent<SwordBladeTrail>();
        trail.pointBase = new GameObject("Base").transform; trail.pointBase.SetParent(go.transform);
        trail.pointTip = new GameObject("Tip").transform; trail.pointTip.SetParent(go.transform);
        var record = typeof(SwordBladeTrail).GetMethod("RecordMotion", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var orientation in new[] { Quaternion.identity, Quaternion.Euler(0, 0, 75), Quaternion.Euler(25, 90, -30) })
        {
            go.SetActive(false); go.SetActive(true);
            typeof(SwordBladeTrail).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trail, null);
            Vector3 blade = orientation * Vector3.up, sweep = orientation * Vector3.right;
            trail.pointBase.position = Vector3.zero; trail.pointTip.position = blade;
            record.Invoke(trail, new object[] { Time.time - .025f });
            // Mimic the Animator updating the bone after the previous LateUpdate, before its hit event.
            trail.pointBase.position = sweep * .2f; trail.pointTip.position = blade + sweep * .2f;
            Check(trail.TryGetSlashPose(out var center, out var actualBlade, out var actualSweep), "Current active blade points were rejected");
            Check(Vector3.Distance(center, blade * .5f + sweep * .2f) < .0001f && Vector3.Dot(actualBlade, blade) > .999f && Vector3.Dot(actualSweep, sweep) > .999f, "Hit-event pose used stale trail points");
        }
        go.SetActive(false); Check(!trail.TryGetSlashPose(out _, out _, out _), "A stowed sword supplied a slash pose");
        UnityEngine.Object.DestroyImmediate(go);
    }
    static void ValidateManagerSpawn(Scene preview, Camera camera)
    {
        var go = new GameObject("Slash manager"); SceneManager.MoveGameObjectToScene(go, preview);
        var manager = go.AddComponent<CombatManager>(); manager.enabled = false;
        var actor = new GameObject("Slash actor"); SceneManager.MoveGameObjectToScene(actor, preview); var unit = actor.AddComponent<BattleUnit>(); unit.SetPersistentHP(100);
        var trailGo = new GameObject("Sword trail"); trailGo.transform.SetParent(actor.transform); var trail = trailGo.AddComponent<SwordBladeTrail>();
        trail.pointBase = new GameObject("Base").transform; trail.pointBase.SetParent(trailGo.transform);
        trail.pointTip = new GameObject("Tip").transform; trail.pointTip.SetParent(trailGo.transform);
        var pose = Quaternion.Euler(14, 25, 58); Vector3 blade = pose * Vector3.up, sweep = pose * Vector3.right;
        trail.pointBase.position = -sweep * .1f; trail.pointTip.position = blade - sweep * .1f;
        typeof(SwordBladeTrail).GetMethod("RecordMotion", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trail, new object[] { Time.time - .02f });
        trail.pointBase.position = Vector3.zero; trail.pointTip.position = blade;
        var action = ScriptableObject.CreateInstance<ActionData>(); action.swordSlashVfxLocalEuler = new Vector3(0, 0, 13);
        var tracked = (HashSet<GameObject>)typeof(CombatManager).GetField("combatVfxInstances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        try
        {
            foreach (var path in new[] { Free + "/Slash VFX.prefab", Stylized + "/Fire/VFX_Slash_Fire.prefab" })
            {
                action.swordSlashVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                typeof(CombatManager).GetMethod("SpawnSwordSlashVFX", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { unit, null, action });
                var clone = tracked.Single();
                var normal = Vector3.Cross(sweep, blade).normalized;
                if (Vector3.Dot(normal, unit.transform.forward) < 0) normal = -normal;
                var expected = CombatSlashOrientation.Rotation(action.swordSlashVfxPrefab, sweep, blade, normal) * Quaternion.Euler(action.swordSlashVfxLocalEuler);
                Check(Quaternion.Angle(expected, clone.transform.rotation) < .05f, "Combat spawn lost the sword plane or Euler correction");
                Quaternion release = clone.transform.rotation;
                var routine = (System.Collections.IEnumerator)typeof(CombatManager).GetMethod("MoveSwordSlashVFXRoutine", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { clone, null, clone.transform.position + Vector3.forward * 100, 1f, Vector3.zero, 2f });
                Check(routine.MoveNext() && Quaternion.Angle(release, clone.transform.rotation) < .01f, "Flight overwrote sword roll");
                UnityEngine.Object.DestroyImmediate(clone); tracked.Clear();
            }
        }
        finally { foreach (var item in tracked.ToArray()) if (item != null) UnityEngine.Object.DestroyImmediate(item); UnityEngine.Object.DestroyImmediate(action); UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(go); }
    }
    static GameObject Clone(GameObject prefab, Scene scene)
    {
        var staging = new GameObject("Inactive slash setup"); SceneManager.MoveGameObjectToScene(staging, scene); staging.SetActive(false);
        var clone = UnityEngine.Object.Instantiate(prefab, staging.transform);
        foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        clone.SetActive(false); clone.transform.SetParent(null, false); UnityEngine.Object.DestroyImmediate(staging); clone.transform.position = Vector3.zero; clone.transform.rotation = Quaternion.identity; clone.SetActive(true);
        return clone;
    }
    static float Deviation(Vector3[] points, Vector3 normal)
    {
        Vector3 center = Vector3.zero; foreach (var p in points) center += p; center /= points.Length;
        float height = 0, planar = 0;
        foreach (var point in points) { var delta = point - center; float h = Vector3.Dot(delta, normal); height += h * h; planar += delta.sqrMagnitude - h * h; }
        return Mathf.Sqrt(height / Mathf.Max(.000001f, planar));
    }
    static string Relative(Transform item, Transform root) => item == root ? "" : item.parent == root ? item.name : Relative(item.parent, root) + "/" + item.name;
    static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Inspect()
    {
        var text = new StringBuilder();
        foreach (string path in Paths())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); text.AppendLine("PREFAB " + path);
            foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (renderer.renderMode != ParticleSystemRenderMode.Mesh || renderer.mesh == null) continue;
                var main = renderer.GetComponent<ParticleSystem>().main;
                text.AppendLine("MESH " + renderer.name + " active=" + renderer.gameObject.activeSelf + " mesh=" + renderer.mesh.name + " bounds=" + renderer.mesh.bounds + " alignment=" + renderer.alignment + " simulation=" + main.simulationSpace + " relative=" + (Quaternion.Inverse(prefab.transform.rotation) * renderer.transform.rotation).eulerAngles + " start3D=" + main.startRotation3D + " XYZ=" + new Vector3(main.startRotationX.constantMax, main.startRotationY.constantMax, main.startRotationZ.constantMax) * Mathf.Rad2Deg + " modes=" + main.startRotationX.mode + "," + main.startRotationY.mode + "," + main.startRotationZ.mode);
            }
        }
        text.AppendLine(string.Join("\n", typeof(ParticleSystemRenderer).GetMethods().Where(m => m.Name == "BakeMesh").Select(m => m.ToString())));
        foreach (var unit in UnityEngine.Object.FindObjectsByType<BattleUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var trail in unit.GetComponentsInChildren<SwordBladeTrail>(true)) text.AppendLine("UNIT TRAIL " + unit.name + " " + trail.name + " active=" + trail.isActiveAndEnabled + " base=" + trail.pointBase + " tip=" + trail.pointTip);
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Inspection.txt", text.ToString()); Debug.Log("[Sword Slash] Inspected package orientation: " + Output);
    }
}
