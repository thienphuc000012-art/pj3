using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks actual melee destinations on unequal ground heights without entering Play Mode.</summary>
[InitializeOnLoad]
public static class MeleeGroundingValidation
{
    const string Request = "Library/CombatValidation/MeleeGrounding.request", Output = "Tools/MeleeGrounding";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static MeleeGroundingValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MeleeGroundingValidation.cs") ||
            File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/combat/CombatManager.cs")) return;
        try { File.ReadAllText(Request); File.Delete(Request); } catch (IOException) { return; }
        try { Run(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [MenuItem("Tools/Combat/Validate Melee Landing Ground")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode."); Directory.CreateDirectory(Output);
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty; var report = new StringBuilder();
        var map = SceneManager.GetSceneByPath("Assets/Scenes/m.unity"); bool opened = !map.IsValid() || !map.isLoaded;
        if (opened) map = EditorSceneManager.OpenPreviewScene("Assets/Scenes/m.unity");
        var test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(active);
        var offset = new Vector3(14000, 15000, 16000); Mesh mesh = null;
        try
        {
            var root = new GameObject("Melee grounding fixture"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, test);
            GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
            var manager = Child("Combat").AddComponent<CombatManager>(); manager.meleeApproachDistance = 1.6f;
            var player = Child("Player").AddComponent<BattleUnit>(); player.isPlayer = true;
            var enemy = Child("OakTreeEnt stand-in").AddComponent<BattleUnit>(); manager.enemyParty.Add(enemy);
            var floor = Child("Floor").AddComponent<BoxCollider>(); floor.size = new Vector3(80, 1, 80); floor.transform.position = offset + Vector3.down * .5f;
            var body = enemy.gameObject.AddComponent<CapsuleCollider>(); body.radius = 3; body.height = 9; body.center = Vector3.up * 4.5f;
            var trigger = Child("Effect trigger").AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = Vector3.one * 30; trigger.transform.position = offset + Vector3.up;
            root.SetActive(true);
            void Place(Vector3 a, Vector3 b) { player.transform.position = offset + a; enemy.transform.position = offset + b; Physics.SyncTransforms(); }
            Vector3 LegacyXZ(BattleUnit attacker, BattleUnit target, float distance, Vector3 configured)
            { var direction = Vector3.ProjectOnPlane(target.transform.position - attacker.transform.position, Vector3.up).normalized; var p = target.transform.position - direction * distance + Quaternion.LookRotation(direction) * configured; return p; }
            void Expect(BattleUnit attacker, BattleUnit target, float height, float distance, Vector3 configured)
            {
                var expected = LegacyXZ(attacker, target, distance, configured); var actual = manager.GetMeleeAttackPosition(attacker, target);
                Check(Mathf.Abs(actual.y - (offset.y + height)) < .008f && Vector2.Distance(new Vector2(actual.x, actual.z), new Vector2(expected.x, expected.z)) < .008f,
                    "Melee landing mismatch: expected Y=" + (offset.y + height) + ", actual=" + actual);
            }
            Place(new Vector3(0, .02f, 5), new Vector3(0, .02f, 0)); Expect(player, enemy, .02f, 1.6f, Vector3.zero);
            report.AppendLine("PASS: flat destination, authored approach distance, large enemy body collider and VFX triggers ignored.");
            floor.enabled = false;
            var ramp = Child("Slope").AddComponent<MeshCollider>(); ramp.transform.position = offset;
            foreach (float angle in new[] { 20f, -20f, 35f, -35f })
            {
                float slope = Mathf.Tan(angle * Mathf.Deg2Rad); if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                mesh = new Mesh { vertices = new[] { new Vector3(-15, -15 * slope, -15), new Vector3(15, -15 * slope, -15), new Vector3(-15, 15 * slope, 15), new Vector3(15, 15 * slope, 15) }, triangles = new[] { 0, 2, 1, 1, 2, 3 } }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); ramp.sharedMesh = mesh;
                Place(new Vector3(0, 5 * slope + .02f, 5), new Vector3(0, .02f, 0)); Expect(player, enemy, 1.6f * slope + .02f, 1.6f, Vector3.zero);
                report.AppendLine("PASS: " + angle + " degree slope, old height error=" + ((5 - 1.6f) * slope).ToString("F3") + "m; grounded destination keeps horizontal approach.");
            }
            ramp.enabled = false; floor.enabled = true;
            Place(new Vector3(0, .8f, 5), new Vector3(0, .02f, 0)); Expect(player, enemy, .8f, 1.6f, Vector3.zero);
            manager.playerMeleePositionOffset = new Vector3(.4f, .25f, .2f); Expect(player, enemy, 1.05f, 1.6f, manager.playerMeleePositionOffset); manager.playerMeleePositionOffset = Vector3.zero;
            var setup = Child("Encounter setup").AddComponent<BattleEncounterSetup>(); setup.enemies.Add(new EnemyCombatSetup { meleeApproachDistance = 2.6f, meleePositionOffset = new Vector3(.3f, .15f, .2f) }); manager.activeEncounterSetup = setup;
            Place(new Vector3(0, .02f, 5), new Vector3(0, .02f, 0)); Expect(enemy, player, .17f, 2.6f, setup.enemies[0].meleePositionOffset); manager.activeEncounterSetup = null;
            report.AppendLine("PASS: elevated caster pivot, player XYZ offset and per-enemy approach distance/offset preserved.");
            floor.size = new Vector3(2, 1, 2); floor.transform.position = offset + new Vector3(0, -.5f, 5); Physics.SyncTransforms();
            Check(manager.GetMeleeAttackPosition(player, enemy) == player.transform.position, "A missing destination floor must retain the original safe standing position."); floor.enabled = false;
            report.AppendLine("PASS: unsupported destination does not leave the attacker standing over a hole.");

            var terrain = map.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerrainCollider>(true)).First(t => t.name == "Terrain Main");
            var terrainCopy = Child("Actual m Terrain").AddComponent<TerrainCollider>(); terrainCopy.terrainData = terrain.terrainData; terrainCopy.transform.position = terrain.transform.position + offset;
            var data = terrain.terrainData;
            Vector3 OnTerrain(Vector3 p) { var local = p - terrain.transform.position; p.y = terrain.transform.position.y + data.GetInterpolatedHeight(Mathf.Clamp01(local.x / data.size.x), Mathf.Clamp01(local.z / data.size.z)); return p; }
            var points = map.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldInteraction>(true)).Where(p => p.kind == WorldInteractionKind.Encounter).ToArray();
            var oaks = points.Where(p => p.enemyPrefabs.Any(e => e != null && e.name.IndexOf("OakTreeEnt", StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            foreach (var point in points) report.AppendLine("Encounter: " + point.transform.position.ToString("F3") + " | " + string.Join(", ", point.enemyPrefabs.Where(e => e != null).Select(e => AssetDatabase.GetAssetPath(e))));
            Check(oaks.Length > 0, "Find the user's OakTreeEnt encounter in live/unsaved m.");
            int grounded = 0, safe = 0;
            foreach (var oak in oaks) for (int heading = 0; heading < 8; heading++)
            {
                Vector3 target = OnTerrain(oak.transform.position) + Vector3.up * .02f;
                Vector3 start = OnTerrain(target + Quaternion.Euler(0, heading * 45, 0) * Vector3.forward * 4.5f) + Vector3.up * .02f;
                Place(start, target); var original = player.transform.position; var originalRotation = player.transform.rotation;
                var destination = manager.GetMeleeAttackPosition(player, enemy);
                if (destination == original) { safe++; continue; }
                Vector3 expected = OnTerrain(destination - offset) + offset + Vector3.up * .02f;
                Check(Mathf.Abs(destination.y - expected.y) < .03f, "Actual m melee destination must use local terrain height, not original player Y.");
                var move = typeof(CombatManager).GetMethod("MoveToPosition", Private);
                var landing = (IEnumerator)move.Invoke(manager, new object[] { player.transform, destination, 0f }); Check(!landing.MoveNext() && player.transform.position == destination, "Jump landing must finish at grounded destination.");
                var back = (IEnumerator)move.Invoke(manager, new object[] { player.transform, original, 0f }); Check(!back.MoveNext() && player.transform.position == original && player.transform.rotation == originalRotation, "Jump-back must restore original slot without drift.");
                grounded++; report.AppendLine("PASS: actual OakTreeEnt terrain heading " + heading * 45 + ", encounter=" + oak.transform.position.ToString("F3") + ", old hover/bury error=" + (original.y - destination.y).ToString("F3") + "m; landed=" + (destination - offset).ToString("F3") + "; exact return slot.");
            }
            Check(grounded >= 4, "Need several real OakTreeEnt terrain approaches, including height differences.");
            var action = AssetDatabase.LoadAssetAtPath<ActionData>("Assets/Adventure/Prefabs/player/Mele/ActionData/Sword Attack 3.asset");
            Check(action != null && action.isMelee && !action.isHeal && !action.isFriendlyAction && !action.isSummonSkill, "Sword Attack 3 uses the corrected common melee branch.");
            report.AppendLine("PASS: Sword Attack 3 melee branch; grounded real terrain paths=" + grounded + ", unsafe points held at original slot=" + safe + ". Action asset and animation trigger unchanged.");
        }
        finally { EditorSceneManager.CloseScene(test, true); if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh); if (opened) EditorSceneManager.ClosePreviewScene(map); }
        Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty, "Live scene and unsaved edits must be preserved.");
        report.AppendLine("PASS: isolated Edit Mode queries only; no Play Mode, campaign writes, live player movement or scene/prefab saves.");
        File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Melee Grounding] PASS: " + Output + "/Validation.txt");
    }
}
