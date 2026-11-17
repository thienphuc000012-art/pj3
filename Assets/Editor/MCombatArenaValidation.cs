using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates real m scenery transfer and combat placement in Edit Mode, without campaign or scene saves.</summary>
[InitializeOnLoad]
public static class MCombatArenaValidation
{
    const string Request = "Library/CombatValidation/MCombatArena.request";
    const string Output = "Tools/MCombatArena";
    static MCombatArenaValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MCombatArenaValidation.cs")) return;
        File.Delete(Request); try { Validate(); } catch (Exception error) { Debug.LogException(error); }
    }
    public static void RunBatch()
    {
        try { Validate(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static bool Same(Matrix4x4 a, Matrix4x4 b) { for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > .002f) return false; return true; }
    [MenuItem("Tools/Adventure/Combat/Validate m Environment Transfer")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run outside Play Mode.");
        Directory.CreateDirectory(Output); var report = new StringBuilder(); var active = SceneManager.GetActiveScene();
        try
        {
            CombatArenaStageValidation.ValidateSpawnAndScan();
            report.AppendLine("PASS: existing streamed-map origin and chunk selection regression.");
            CheckTransferRollback(report);
            CheckM(report);
            report.AppendLine("PASS: Edit Mode only. No campaign, prefab, source scene or Build Settings writes.");
            File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[m Combat Arena] PASS: " + Output + "/Validation.txt");
        }
        catch (Exception error) { File.WriteAllText(Output + "/Validation.txt", report + "\nFAIL: " + error); throw; }
        finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
    }
    static void CheckTransferRollback(StringBuilder report)
    {
        var source = EditorSceneManager.NewPreviewScene();
        var staging = EditorSceneManager.NewPreviewScene(); CombatSceneEnvironment transfer = null;
        try
        {
            var parent = new GameObject("Rotated mirrored parent"); SceneManager.MoveGameObjectToScene(parent, source);
            parent.transform.SetPositionAndRotation(new Vector3(4, 0, -2), Quaternion.Euler(0, 38, 0)); parent.transform.localScale = new Vector3(-1.2f, 1, 1.7f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Nearby floor"; floor.transform.SetParent(parent.transform, false);
            floor.transform.localPosition = Vector3.down * .25f; floor.transform.localRotation = Quaternion.Euler(0, 17, 0); floor.transform.localScale = new Vector3(20, .5f, 20);
            var enemy = new GameObject("Gameplay must stay behind"); SceneManager.MoveGameObjectToScene(enemy, source); enemy.transform.position = new Vector3(4, 0, -2);
            var point = enemy.AddComponent<WorldInteraction>(); point.kind = WorldInteractionKind.Encounter;
            var remote = GameObject.CreatePrimitive(PrimitiveType.Sphere); remote.name = "Remote scenery"; SceneManager.MoveGameObjectToScene(remote, source); remote.transform.position = Vector3.one * 500;
            Physics.SyncTransforms();
            var matrix = floor.transform.localToWorldMatrix; var originalParent = floor.transform.parent; int sibling = floor.transform.GetSiblingIndex();
            Check(CombatAreaScanner.TryScanAuthored(point, null, out var scan, out var error), error);
            Check(scan.EnvironmentObjects.Contains(floor) && !scan.EnvironmentObjects.Contains(enemy) && !scan.EnvironmentObjects.Contains(remote), "Spatial/gameplay filtering.");
            Check(enemy.GetComponent<EnemyCombatOrigin>() == null, "Edit Mode validation must not add or capture runtime origins.");
            Check(CombatSceneEnvironment.TryPrepare(scan, out transfer, out error, staging), error);
            Check(floor.scene == staging && Same(matrix, floor.transform.localToWorldMatrix), "Transfer must preserve reflected/nonuniform world matrix exactly.");
            transfer.Dispose(); transfer = null;
            Check(floor.scene == source && floor.transform.parent == originalParent && floor.transform.GetSiblingIndex() == sibling && Same(matrix, floor.transform.localToWorldMatrix), "Aborted loading must restore exact hierarchy and pose.");
            report.AppendLine("PASS: unchunked read-only scan, nearby static floor, remote/gameplay exclusion, negative/nonuniform parent matrices, and aborted-load rollback.");
        }
        finally { transfer?.Dispose(); EditorSceneManager.ClosePreviewScene(source); EditorSceneManager.ClosePreviewScene(staging); }
    }
    static void CheckM(StringBuilder report)
    {
        // An independent preview protects all objects and unsaved edits in a live m scene.
        var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/m.unity");
        var staging = EditorSceneManager.NewPreviewScene();
        CombatSceneEnvironment transfer = null; Scene battle = default, template = default;
        try
        {
            // Scene instances in this disposable copy can be unpacked; the source prefabs and scene are never saved.
            foreach (var root in source.GetRootGameObjects())
                if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var root in source.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject)) PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Physics.SyncTransforms();
            var points = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldInteraction>(true)).Where(p => p.kind == WorldInteractionKind.Encounter && p.gameObject.activeInHierarchy).ToArray();
            Check(points.Length > 0, "m must contain enemy encounters.");
            var scans = new Dictionary<WorldInteraction, CombatAreaScan>();
            foreach (var point in points)
            {
                Check(CombatAreaScanner.TryScanAuthored(point, null, out var scan, out var error), point.name + ": " + error);
                Check(scan.UsesSceneEnvironment && scan.ChunkPaths.Count == 0 && scan.EnvironmentObjects.Any(o => o.GetComponent<Terrain>() != null || o.GetComponentInChildren<Collider>() != null), "m scan must contain physical terrain/scenery.");
                scans.Add(point, scan);
                report.AppendLine("m scan: " + point.name + ", setup=" + point.combatSetupId + ", spawn=" + scan.SpawnPose.position.ToString("F3") + ", radius=" + scan.Radius + ", objects=" + scan.EnvironmentObjects.Count + ", terrain=" + scan.EnvironmentObjects.Sum(o => o.GetComponentsInChildren<Terrain>(true).Length));
            }
            // Prove that a real m environment survives unloading its map, then passes the same arena builder used by Loading.
            var selected = points.OrderBy(p => Vector3.Distance(p.transform.position, new Vector3(-36.84558f, -85.8805f, -29.93403f))).First();
            template = EditorSceneManager.OpenPreviewScene("Assets/Scenes/combattest.unity");
            var manager = template.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CombatManager>(true)).Single();
            battle = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            foreach (var other in points.Where(p => p != selected))
            {
                var otherScan = scans[other];
                Check(CombatSceneEnvironment.TryPrepare(otherScan, out transfer, out var otherError, staging), otherError);
                Check(transfer.Stage(staging, out otherError), otherError);
                Build(battle, staging, template, manager, otherScan, other.enemyPrefabs.Where(e => e != null).ToList(), other.combatSetupId);
                transfer.Dispose(); transfer = null;
                var oldArena = battle.GetRootGameObjects().Single(o => o.name == "CombatArenaRoot"); UnityEngine.Object.DestroyImmediate(oldArena);
                report.AppendLine("PASS: safe two-player arena layout for m encounter at " + otherScan.SpawnPose.position.ToString("F3"));
            }
            var chosen = scans[selected]; var enemies = selected.enemyPrefabs.Where(e => e != null).ToList(); string setupId = selected.combatSetupId;
            Check(enemies.Count > 0, "Selected encounter has no combat prefabs.");
            var matrices = chosen.EnvironmentObjects.ToDictionary(o => o, o => o.transform.localToWorldMatrix);
            Check(CombatSceneEnvironment.TryPrepare(chosen, out transfer, out var prepareError, staging), prepareError);
            foreach (var pair in matrices) Check(Same(pair.Value, pair.Key.transform.localToWorldMatrix), "m geometry moved during capture: " + pair.Key.name);
            EditorSceneManager.ClosePreviewScene(source); source = default;
            Check(transfer.Stage(staging, out var stageError), stageError);
            Check(transfer.Root.GetComponentsInChildren<Renderer>(true).Length > 0 && transfer.Root.GetComponentsInChildren<Collider>(true).Length > 0, "Transferred map must survive original scene unload.");
            Build(battle, staging, template, manager, chosen, enemies, setupId);
            var arena = battle.GetRootGameObjects().Single(o => o.name == "CombatArenaRoot");
            Check(arena.GetComponentInChildren<Terrain>() != null || arena.GetComponentsInChildren<Renderer>().Length > 0, "Combat scene has no visible environment.");
            Check(arena.GetComponentInChildren<PlayerScript>(true) == null && arena.GetComponentInChildren<WorldInteraction>(true) == null, "No map gameplay migrated into combat.");
            foreach (var pair in matrices) Check(Same(pair.Value, pair.Key.transform.localToWorldMatrix), "m geometry moved during arena parenting: " + pair.Key.name);
            transfer.Release(); transfer = null;
            report.AppendLine("PASS: actual m tutorial encounter -> captured local scenery -> map scene unloaded -> combattest arena builder; safe player/enemy slots and visible colliders/terrain at unchanged world poses.");
        }
        finally
        {
            transfer?.Dispose();
            if (battle.IsValid() && battle.isLoaded) EditorSceneManager.CloseScene(battle, true);
            if (template.IsValid() && template.isLoaded) EditorSceneManager.ClosePreviewScene(template);
            if (source.IsValid() && source.isLoaded) EditorSceneManager.ClosePreviewScene(source);
            if (staging.IsValid() && staging.isLoaded) EditorSceneManager.ClosePreviewScene(staging);
        }
    }
    static void Build(Scene battle, Scene staging, Scene template, CombatManager manager, CombatAreaScan scan, List<BattleUnit> enemies, string setupId)
    {
        var setup = template.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleEncounterSetup>(true)).FirstOrDefault(s => s.setupId == setupId);
        var settings = setup != null ? setup.arenaSpawn : new CombatArenaSpawnSettings();
        var builder = typeof(CampaignSession).Assembly.GetType("CombatArenaBuilder").GetMethod("TryBuild", BindingFlags.Public | BindingFlags.Static);
        object[] args = { battle, new List<Scene> { staging }, scan, settings, 2, enemies, manager.playerSlots.Length, manager.enemySlots.Length, manager.playersWalkIn ? manager.playerEntryDistance : 0f, null, null };
        Check((bool)builder.Invoke(null, args), "Real m arena placement at " + scan.SpawnPose.position + ": " + args[10]);
    }
}
