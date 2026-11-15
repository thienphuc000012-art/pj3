using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Conservative vegetation settings and isolated visual-pool checks. Never enters gameplay.</summary>
public static class CombatVegetationPerformanceSetup
{
    const string Map = "Assets/Scenes/m.unity";
    const string Output = "Tools/Performance";
    const string Request = "Library/CombatValidation/CombatVegetationPerformance.request";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/CombatVegetationPerformanceSetup.cs")) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if (command == "setup") Setup(); else Validate(); }
        catch (Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", ex.ToString()); Debug.LogException(ex); }
    }

    [MenuItem("Tools/Adventure/Performance/Optimize m Vegetation and Validate VFX")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before optimizing the saved terrain.");
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(Map, backup + "/m.disk.unity");
        Scene original = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(Map); bool opened = false;
        var report = new StringBuilder();
        try
        {
            if (scene.IsValid() && scene.isLoaded)
                Check(EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true), "Could not back up the in-memory scene.");
            else { scene = EditorSceneManager.OpenScene(Map, OpenSceneMode.Additive); opened = true; }
            var terrain = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Terrain>(true)).Single(t => t.name == "Terrain Main");
            Check(terrain.terrainData != null, "Terrain Main has no TerrainData.");
            var data = terrain.terrainData; string dataPath = AssetDatabase.GetAssetPath(data), dataHash = Hash(dataPath);
            report.AppendLine("TerrainData: " + dataPath + "; painted instances=" + data.treeInstanceCount + "; palette=" + data.treePrototypes.Length);
            report.AppendLine("Before: Tree Distance=" + terrain.treeDistance + ", Billboard Start=" + terrain.treeBillboardDistance + ", Detail Distance=" + terrain.detailObjectDistance);
            var trees = data.treeInstances;
            var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayerScript>(true)).FirstOrDefault();
            Vector3 sample = player != null ? player.transform.position : terrain.transform.position + data.size * .5f;
            int before = CountNear(trees, terrain, sample, terrain.treeDistance);
            Undo.RecordObject(terrain, "Optimize m vegetation distances");
            terrain.treeDistance = 200f; terrain.treeBillboardDistance = 80f; terrain.detailObjectDistance = 80f;
            // Keep density, LOD meshes, tree collision, shadows and painted instances intact.
            EditorUtility.SetDirty(terrain); EditorSceneManager.MarkSceneDirty(scene);
            Check(EditorSceneManager.SaveScene(scene), "Could not save terrain display settings.");
            Check(Hash(dataPath) == dataHash && data.treeInstanceCount == trees.Length, "TerrainData/paint must not change.");
            report.AppendLine("After: Tree Distance=200, Billboard Start=80, Detail Distance=80. Paint, density, colliders, materials and height data unchanged.");
            report.AppendLine("Distance candidates from the authored player position: " + before + " -> " + CountNear(trees, terrain, sample, terrain.treeDistance) +
                " (radius counts, not measured visible trees/FPS).");
            report.AppendLine("TerrainData SHA256 unchanged: " + dataHash);
            report.AppendLine("Terrain size: " + data.size + "; player sample=" + sample);
            foreach (float radius in new[] { 500f, 250f, 200f, 150f, 100f, 80f })
                report.AppendLine("Radius " + radius + "m: " + CountNear(trees, terrain, sample, radius) + " instance candidates");
            report.AppendLine("Backup: " + backup);
            var prototypes = data.treePrototypes;
            for (int i = 0; i < prototypes.Length; i++)
            {
                var prefab = prototypes[i].prefab;
                int painted = trees.Count(tree => tree.prototypeIndex == i);
                report.AppendLine("Tree " + i + ": " + (prefab != null ? prefab.name : "missing") + "; painted=" + painted + "; LODGroups=" +
                    (prefab != null ? prefab.GetComponentsInChildren<LODGroup>(true).Length : 0));
            }
            File.WriteAllText(Output + "/Vegetation.txt", report.ToString());
        }
        finally
        {
            if (opened && scene.IsValid() && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
        }
        Validate();
    }
    static int CountNear(TreeInstance[] trees, Terrain terrain, Vector3 point, float distance)
    {
        int count = 0;
        foreach (var tree in trees)
            if ((terrain.transform.position + Vector3.Scale(tree.position, terrain.terrainData.size) - point).sqrMagnitude <= distance * distance) count++;
        return count;
    }

    [MenuItem("Tools/Adventure/Performance/Validate Visual VFX Reuse")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Validate in Edit Mode.");
        Directory.CreateDirectory(Output); var report = new StringBuilder();
        var preview = EditorSceneManager.NewPreviewScene(); CombatVisualVfxPool pool = null;
        var oldCombat = CombatManager.Instance;
        try
        {
            var owner = new GameObject("Visual pool preview"); SceneManager.MoveGameObjectToScene(owner, preview);
            var pure = new GameObject("Pure particle fixture", typeof(ParticleSystem)); SceneManager.MoveGameObjectToScene(pure, preview); pure.SetActive(false);
            var main = pure.GetComponent<ParticleSystem>().main; main.simulationSpeed = 1.75f; main.stopAction = ParticleSystemStopAction.None;
            var child = new GameObject("Child particles", typeof(ParticleSystem)); child.transform.SetParent(pure.transform, false);
            child.transform.localPosition = new Vector3(1, 2, 3);
            pool = new CombatVisualVfxPool(owner.transform, 2, 3);
            Check(CombatVisualVfxPool.CanPool(pure), "Pure particles must be eligible.");
            var instance = pool.Create(pure, Vector3.one, Quaternion.identity); int first = instance.GetInstanceID();
            instance.GetComponent<ParticleSystem>().Simulate(.2f, true, true, false);
            instance.transform.GetChild(0).localPosition = Vector3.zero;
            var modified = instance.GetComponent<ParticleSystem>().main; modified.simulationSpeed = .1f;
            pool.Schedule(instance, 2f, 0); pool.Tick(1); Check(pool.ActiveCount == 1, "Do not return an effect before its lifetime.");
            pool.Tick(2); Check(pool.ActiveCount == 0 && pool.CachedCount == 1 && !instance.activeSelf, "Expired visuals must stop and return inactive.");
            instance = pool.Create(pure, Vector3.up * 3, Quaternion.Euler(0, 45, 0));
            Check(instance.GetInstanceID() == first && instance.GetComponent<ParticleSystem>().particleCount == 0,
                "Reuse the original object with no stale particles.");
            Check(instance.GetComponent<ParticleSystem>().main.simulationSpeed == 1.75f && instance.transform.GetChild(0).localPosition == new Vector3(1, 2, 3),
                "Restore prefab speed and child transforms on each lease.");
            pool.Schedule(instance, 8f, 2); pool.Tick(2.1); Check(pool.ActiveCount == 1, "An old expiry must not end a new lease.");
            pool.Release(instance);
            for (int i = 0; i < 256; i++) { instance = pool.Create(pure, Vector3.zero, Quaternion.identity); pool.Release(instance); }
            Check(pool.CreatedCount == 1 && pool.ReusedCount >= 257 && pool.CachedCount == 1, "Repeated effects must not instantiate after warm-up.");
            var burst = Enumerable.Range(0, 10).Select(i => pool.Create(pure, Vector3.zero, Quaternion.identity)).ToArray();
            foreach (var effect in burst) pool.Release(effect);
            Check(pool.CachedCount <= 2 && pool.ActiveCount == 0, "Per-prefab cache must remain bounded.");
            var second = UnityEngine.Object.Instantiate(pure); second.name = "Second particle fixture";
            SceneManager.MoveGameObjectToScene(second, preview);
            var secondBurst = Enumerable.Range(0, 4).Select(i => pool.Create(second, Vector3.zero, Quaternion.identity)).ToArray();
            foreach (var effect in secondBurst) pool.Release(effect);
            Check(pool.CachedCount == 3 && pool.ActiveCount == 0, "Total cache must be bounded across different prefabs.");
            var externallyDestroyed = pool.Create(pure, Vector3.zero, Quaternion.identity);
            UnityEngine.Object.DestroyImmediate(externallyDestroyed); pool.Tick(20);
            Check(pool.ActiveCount == 0, "External scene/parent cleanup must not leave active leases.");
            instance = pool.Create(pure, Vector3.zero, Quaternion.identity); pool.Release(instance);
            UnityEngine.Object.DestroyImmediate(instance);
            instance = pool.Create(pure, Vector3.zero, Quaternion.identity);
            Check(instance != null, "An externally destroyed cached instance must be replaced safely."); pool.Release(instance);
            var unsafeObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/asset/vfx/Realistic Effects Pack v4/Effects/Prefabs/Effects/Effect20.prefab");
            Check(unsafeObject != null && !CombatVisualVfxPool.CanPool(unsafeObject), "Scripted/RFX4 effects must retain their original lifecycle.");
            var destroyAction = pure.GetComponent<ParticleSystem>().main; destroyAction.stopAction = ParticleSystemStopAction.Destroy;
            Check(!CombatVisualVfxPool.CanPool(pure), "Self-destroying effects must not be pooled implicitly."); destroyAction.stopAction = ParticleSystemStopAction.None;
            report.AppendLine("PASS: 256 repeated leases reused one native particle instance; particle/speed/child-transform reset; exact lifetime, early cleanup and stale-expiry isolation; per-prefab and cross-prefab cache bounds; external object cleanup.");
            report.AppendLine("PASS: scripted/RFX4 and self-destroying effects excluded; gameplay/projectile prefab lifecycle untouched.");
            pool.Dispose(); pool = null;
            ValidateManager(preview, pure, report);
            AuditPrefabs(report);
            report.AppendLine("Edit Mode only; no campaign saves or combat scenes written. These are reuse/correctness checks, not an FPS benchmark.");
            File.WriteAllText(Output + "/VFX-Validation.txt", report.ToString()); Debug.Log("[Performance] " + report);
        }
        finally { pool?.Dispose(); CombatManager.Instance = oldCombat; EditorSceneManager.ClosePreviewScene(preview); }
    }
    static void ValidateManager(Scene scene, GameObject prefab, StringBuilder report)
    {
        var go = new GameObject("Combat visual integration preview"); SceneManager.MoveGameObjectToScene(go, scene);
        var manager = go.AddComponent<CombatManager>();
        var spawn = typeof(CombatManager).GetMethod("SpawnHitVFX", Private);
        Check(spawn != null, "Missing hit VFX factory.");
        var clear = typeof(CombatManager).GetMethod("ClearCombatVFX", Private);
        for (int i = 0; i < 20; i++)
        {
            spawn.Invoke(manager, new object[] { prefab, Vector3.zero, 1f }); clear.Invoke(manager, null);
        }
        var pool = (CombatVisualVfxPool)typeof(CombatManager).GetField("visualVfxPool", Private).GetValue(manager);
        Check(pool.CreatedCount == 1 && pool.ReusedCount == 19 && pool.ActiveCount == 0, "CombatManager hit/turn cleanup must use bounded reuse.");
        pool.Dispose(); typeof(CombatManager).GetField("visualVfxPool", Private).SetValue(manager, null);
        report.AppendLine("PASS: actual CombatManager hit VFX creation and turn cleanup reused one instance for 20 effects.");
    }
    static void AuditPrefabs(StringBuilder report)
    {
        var prefabs = new HashSet<GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:ActionData", new[] { "Assets/Adventure", "Assets/Scripts/combat/ActionData" }))
        {
            var action = AssetDatabase.LoadAssetAtPath<ActionData>(AssetDatabase.GUIDToAssetPath(guid));
            if (action == null) continue;
            if (action.vfxType == ActionData.VfxType.SpawnAtTarget && action.vfxPrefab != null) prefabs.Add(action.vfxPrefab);
            if (action.vfxType != ActionData.VfxType.Beam && action.castVfxPrefab != null) prefabs.Add(action.castVfxPrefab);
            if (action.hitVfxPrefab != null) prefabs.Add(action.hitVfxPrefab);
            if (action.summonSpawnVfxPrefab != null) prefabs.Add(action.summonSpawnVfxPrefab);
        }
        int eligible = 0;
        foreach (var prefab in prefabs.OrderBy(p => p.name))
        {
            bool safe = CombatVisualVfxPool.CanPool(prefab); if (safe) eligible++;
            report.AppendLine((safe ? "POOLED " : "ORIGINAL LIFECYCLE ") + AssetDatabase.GetAssetPath(prefab));
        }
        report.AppendLine("Configured visual prefab candidates eligible: " + eligible + "/" + prefabs.Count);
    }
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
