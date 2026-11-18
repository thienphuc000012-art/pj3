using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class RestPointCatalogBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => WorldSystemsValidation.RebuildCatalog();
}

[InitializeOnLoad]
public static class WorldSystemsValidation
{
    const string Request = "Library/CombatValidation/WorldSystems.request";
    const string Output = "Tools/WorldSystems";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static WorldSystemsValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/WorldSystemsValidation.cs") ||
            File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/Adventure/AdventureCheatPanel.cs")) return;
        try { File.ReadAllText(Request); File.Delete(Request); } catch (IOException) { return; }
        try { Run(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    [MenuItem("Tools/Adventure/Cheats/Rebuild Save Point Catalog")]
    public static void RebuildCatalog()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Build the catalog in Edit Mode.");
        var config = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        var points = new List<UnlockedRestPoint>();
        var maps = new[] { config.mapScene }.Concat(config.additionalMapScenes).Distinct().ToArray();
        foreach (string map in maps)
        {
            string mapPath = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == map);
            if (mapPath == null) continue;
            Inspect(mapPath, map, true);
        }
        void Inspect(string path, string map, bool inspectChunks)
        {
            var scene = SceneManager.GetSceneByPath(path); bool preview = !scene.IsValid() || !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenPreviewScene(path);
            var nearbyPreviews = new List<Scene>();
            try
            {
                var roots = scene.GetRootGameObjects();
                var restPoints = roots.SelectMany(root => root.GetComponentsInChildren<WorldInteraction>(true)).Where(point => point.kind == WorldInteractionKind.RestPoint).ToArray();
                var geometryRoots = new List<GameObject>(roots);
                if (inspectChunks && restPoints.Length > 0)
                    foreach (var streamer in roots.SelectMany(root => root.GetComponentsInChildren<MapChunkStreamer>(true)))
                        foreach (var chunk in streamer.chunks)
                        {
                            if (chunk == null || string.IsNullOrEmpty(chunk.scenePath) || !restPoints.Any(point => chunk.bounds.SqrDistance(point.transform.position) < 100)) continue;
                            var geometry = SceneManager.GetSceneByPath(chunk.scenePath);
                            if (!geometry.IsValid() || !geometry.isLoaded) { geometry = EditorSceneManager.OpenPreviewScene(chunk.scenePath); nearbyPreviews.Add(geometry); }
                            geometryRoots.AddRange(geometry.GetRootGameObjects());
                        }
                var colliders = geometryRoots.SelectMany(root => root.GetComponentsInChildren<Collider>(true)).Where(collider => collider.enabled && !collider.isTrigger).ToArray();
                foreach (var point in roots.SelectMany(root => root.GetComponentsInChildren<WorldInteraction>(true)))
                {
                    if (point.kind != WorldInteractionKind.RestPoint || points.Any(value => value.id == point.Id)) continue;
                    var entry = AdventureRestPointCatalog.FromPoint(point, map);
                    bool grounded = false;
                    // Prefer a standing position beside the campfire, never its collider center.
                    for (int angle = 0; angle < 8 && !grounded; angle++)
                    {
                        Vector3 candidate = point.transform.position + Quaternion.Euler(0, angle * 45, 0) * point.transform.forward * Mathf.Max(1.5f, point.range * .7f);
                        var ray = new Ray(candidate + Vector3.up * 3, Vector3.down); float nearest = float.PositiveInfinity; Vector3 floor = default;
                        foreach (var collider in colliders)
                        {
                            if (collider.GetComponentInParent<WorldInteraction>() != null || collider.GetComponentInParent<PlayerScript>() != null) continue;
                            if (collider.Raycast(ray, out var hit, 7) && hit.normal.y >= .65f && hit.distance < nearest) { nearest = hit.distance; floor = hit.point; }
                        }
                        if (float.IsPositiveInfinity(nearest)) continue;
                        var capsule = new Bounds(floor + Vector3.up, new Vector3(.7f, 1.8f, .7f));
                        bool blocked = colliders.Any(collider => collider.GetComponentInParent<WorldInteraction>() != null && collider.bounds.Intersects(capsule));
                        if (!blocked) { entry.position = floor + Vector3.up * .06f; grounded = true; }
                    }
                    if (!grounded) Debug.LogWarning("Save-point cheat: verify standing position near " + map + "/" + point.name);
                    points.Add(entry);
                }
                if (inspectChunks)
                    foreach (var streamer in roots.SelectMany(root => root.GetComponentsInChildren<MapChunkStreamer>(true)))
                        foreach (var chunk in streamer.chunks)
                            if (chunk != null && !string.IsNullOrEmpty(chunk.scenePath)) Inspect(chunk.scenePath, map, false);
            }
            finally { foreach (var geometry in nearbyPreviews) EditorSceneManager.ClosePreviewScene(geometry); if (preview) EditorSceneManager.ClosePreviewScene(scene); }
        }
        const string assetPath = "Assets/Resources/Adventure/RestPointCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<AdventureRestPointCatalog>(assetPath);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<AdventureRestPointCatalog>(); AssetDatabase.CreateAsset(catalog, assetPath); }
        catalog.points = points; EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
    }

    [MenuItem("Tools/Adventure/Cheats/Validate World Systems")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode; never write a real campaign save in validation.");
        Directory.CreateDirectory(Output); var report = new StringBuilder();
        RebuildCatalog();
        var catalog = Resources.Load<AdventureRestPointCatalog>("Adventure/RestPointCatalog");
        Check(catalog != null && catalog.points.Count > 0 && catalog.points.All(point => !string.IsNullOrEmpty(point.id) && !string.IsNullOrEmpty(point.sceneName)), "Save point catalog is incomplete.");
        foreach (var point in catalog.points) report.AppendLine("Save point: " + point.sceneName + " | " + point.id + " | " + point.displayName + " | " + point.position);
        var realConfig = Resources.Load<CampaignConfig>("Adventure/CampaignConfig");
        var liveMap = SceneManager.GetSceneByName("m");
        if (liveMap.IsValid() && liveMap.isLoaded)
            foreach (var point in liveMap.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorldInteraction>(true)).Where(point => point.kind == WorldInteractionKind.Encounter))
                report.AppendLine("Enemy m: " + point.displayName + " | " + string.Join(",", point.enemyPrefabs.Where(enemy => enemy != null).Select(enemy => enemy.name)) + " | " + (EnemySpawnRules.IsPermanent(point, realConfig) ? "Permanent/boss/tutorial" : "Regular, " + (point.enemySpawn?.respawnSeconds ?? 300) + "s, " + (point.enemySpawn?.timeOfDay ?? EnemyTimeOfDay.Always)));
        var data = new CampaignSave { playTimeSeconds = 100, hasWorldTime = true, worldTimeHours = 8 };
        data.collected.Add("normal"); EnemySpawnRules.Schedule(data, "normal", 300);
        data.playTimeSeconds = 399.99; Check(!EnemySpawnRules.IsReady(data, "normal"), "No early respawn.");
        var resumed = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data)); resumed.playTimeSeconds = 400;
        Check(EnemySpawnRules.Revive(resumed, "normal") && !resumed.collected.Contains("normal") && resumed.enemyRespawns.Count == 0, "Respawn restores eligibility and rewards after save roundtrip.");
        Check(!EnemySpawnRules.Revive(resumed, "normal"), "Cannot revive twice.");
        foreach (double hour in new[] { 0, 5.99, 6, 12, 17.99, 18, 23.99, 30, 42 })
        {
            bool day = hour % 24 >= 6 && hour % 24 < 18;
            Check(EnemySpawnRules.IsTimeAllowed(EnemyTimeOfDay.Always, hour) && EnemySpawnRules.IsTimeAllowed(EnemyTimeOfDay.DayOnly, hour) == day && EnemySpawnRules.IsTimeAllowed(EnemyTimeOfDay.NightOnly, hour) != day, "Day/night boundary " + hour);
        }
        report.AppendLine("PASS: five-minute timer, no early/double respawn, save/load continuation, fresh loot eligibility, and day/night boundaries (06:00, 18:00).");
        var old = JsonUtility.FromJson<CampaignSave>("{\"version\":5,\"playTimeSeconds\":12,\"collected\":[\"old-normal\"]}");
        EnemySpawnRules.Schedule(old, "old-normal", 300); Check(!EnemySpawnRules.IsReady(old, "old-normal"), "Old collected enemies must wait, not instantly respawn.");
        report.AppendLine("PASS: old saves without respawn/time override fields remain compatible.");
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var fixture = new GameObject("World Systems Isolated Fixture"); SceneManager.MoveGameObjectToScene(fixture, scene);
        var config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = scene.name;
        var template = new GameObject("Test unit").AddComponent<BattleUnit>(); template.transform.SetParent(fixture.transform);
        template.maxHP = 100; template.baseAtk = 10; template.baseDef = 5; template.speed = 15; template.baseCrit = 10; template.baseCritDamage = 150;
        var skill = ScriptableObject.CreateInstance<ActionData>(); skill.name = "World Systems Skill";
        template.characterSkills = new List<ActionData> { skill }; config.startingParty.Add(template);
        var beforeInstance = CampaignSession.Instance;
        try
        {
            var normal = new GameObject("Normal").AddComponent<WorldInteraction>(); normal.transform.SetParent(fixture.transform);
            normal.kind = WorldInteractionKind.Encounter; normal.persistentId = "world-systems-normal"; normal.enemyPrefabs.Add(template);
            Check(!EnemySpawnRules.IsPermanent(normal, config), "Normal enemies can respawn."); template.isBoss = true;
            Check(EnemySpawnRules.IsPermanent(normal, config), "Boss ignores respawn/day/night."); template.isBoss = false;
            config.SetGuidedTutorialEncounter(normal); Check(EnemySpawnRules.IsPermanent(normal, config), "Tutorial must not respawn."); config.SetGuidedTutorialEncounter(null);
            normal.savedWorldId = QuestPortalDefinition.Current.enemyWorldIds.First(); Check(EnemySpawnRules.IsPermanent(normal, config), "Main Quest presentation targets are permanent."); normal.savedWorldId = null;
            report.AppendLine("PASS: normal enemies are managed; bosses, tutorial and Main Quest targets remain permanent.");
            data.party.Add(new PartyMemberProgress { prefabIndex = 0, hp = 42 });
            AdventureCheats.ApplyStats(config, data.party[0], new[] { 275, 300, 77, 44, 31, 120, 220, 15, 20 });
            Check(AdventureCheats.ReadStats(config, data.party[0]).SequenceEqual(new[] { 275, 300, 77, 44, 31, 100, 220, 15, 20 }), "Absolute stats and clamp.");
            Check(template.maxHP == 100 && template.baseAtk == 10, "Cheats must not mutate source prefabs.");
            AdventureCheats.LevelUp(data.party[0], 3); Check(data.party[0].level == 4, "Level-up cheat.");
            AdventureCheats.UnlockAllSkills(config, data); AdventureCheats.UnlockAllSkills(config, data);
            Check(data.party[0].Rank(skill) == 1 && data.party[0].skills.Count == 1 && data.party[0].equippedSkills.Contains(skill.name), "All-skills cheat is repeatable without duplicates.");
            int unlocked = AdventureCheats.UnlockRestPoints(data, catalog.points); Check(unlocked == catalog.points.Count && AdventureCheats.UnlockRestPoints(data, catalog.points) == 0, "Unlock points without duplicates.");
            var originalDayLength = AdventureWorldTime.Settings.dayLengthMinutes;
            data.dayLengthMinutesOverride = 2; double beforeHours = data.worldTimeHours; AdventureWorldTime.Advance(data, 10);
            Check(Math.Abs(data.worldTimeHours - beforeHours - 2) < .0001 && AdventureWorldTime.Settings.dayLengthMinutes == originalDayLength, "Per-save time speed.");
            var roundtrip = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data));
            Check(roundtrip.party[0].atkBonus == 67 && roundtrip.dayLengthMinutesOverride == 2 && roundtrip.unlockedRestPoints.Count == catalog.points.Count, "Cheats serialize.");
            report.AppendLine("PASS: stat edit/clamping, source assets untouched, levels, all skills, skill loadout, save points, deduplication and per-save day speed survive JSON save/load.");
            var session = fixture.AddComponent<CampaignSession>();
            typeof(CampaignSession).GetProperty("Config").SetValue(session, config);
            typeof(CampaignSession).GetProperty("Data").SetValue(session, data);
            var cheat = fixture.AddComponent<AdventureCheatPanel>(); typeof(AdventureCheatPanel).GetField("session", Private).SetValue(cheat, session);
            var oldTimeScale = Time.timeScale; var oldCursor = Cursor.lockState; bool oldVisible = Cursor.visible;
            Check(cheat.Open(), "Cheat panel opens on map.");
            Check(AdventureCheatPanel.IsOpen && Time.timeScale == 0 && CampaignSession.InputBlocked, "Panel owns input and pause.");
            var canvas = fixture.GetComponentInChildren<Canvas>(true);
            Check(canvas != null && canvas.GetComponentsInChildren<TMP_InputField>().Length == 12 && canvas.GetComponentsInChildren<Button>().Length == 8, "All cheat controls exist.");
            Capture(canvas, scene, Output + "/CheatPanel.png");
            cheat.Close(); Check(!AdventureCheatPanel.IsOpen && Time.timeScale == oldTimeScale && Cursor.lockState == oldCursor && Cursor.visible == oldVisible, "Close restores input, cursor and time.");
            Check(cheat.Open(), "Panel can reopen."); cheat.Close();
            report.AppendLine("PASS: native panel construction, pause/input ownership, close/reopen and cursor/time restore; preview rendered without Play Mode.");
            // Verify teleport snap resets both controller and Cinemachine history without moving the player.
            var player = new GameObject("Camera player"); player.transform.SetParent(fixture.transform); player.transform.position = new Vector3(20, 7, -3);
            var target = new GameObject("Camera target"); target.transform.SetParent(fixture.transform);
            var controller = new GameObject("Camera controller").AddComponent<MainCameraController>(); controller.transform.SetParent(fixture.transform);
            typeof(MainCameraController).GetField("playerTarget", Private).SetValue(controller, player.transform);
            typeof(MainCameraController).GetField("cameraTarget", Private).SetValue(controller, target.transform);
            controller.SetCameraRotation(124, 12); controller.SnapCameraTarget();
            Check(Vector3.Distance(target.transform.position, player.transform.position + Vector3.up * 1.4f) < .0001f && Mathf.Abs(Mathf.DeltaAngle(target.transform.eulerAngles.x, 12)) < .001f, "Camera snap position/pitch.");
            report.AppendLine("PASS: camera target snaps directly to the player and retains requested pitch/yaw.");
            controller.SuppressMouseLook();
            Check(!(bool)typeof(MainCameraController).GetField("mouseLookWasReady", Private).GetValue(controller) &&
                (int)typeof(MainCameraController).GetField("ignoreMouseUntilFrame", Private).GetValue(controller) > Time.frameCount, "Cursor/focus handoff discards initial mouse deltas.");
            report.AppendLine("PASS: cursor/focus/cutscene handoff suppresses initial mouse-look frames.");

            // Exercise actual registration/hide/restore while keeping Update and Save out of the fixture.
            typeof(CampaignSession).GetProperty("Instance").SetValue(null, session);
            try
            {
                var spawns = fixture.AddComponent<WorldEnemySpawns>();
                normal.enemySpawn = new EnemySpawnSettings { respawnSeconds = 300, timeOfDay = EnemyTimeOfDay.NightOnly };
                data.worldTimeHours = 12; spawns.Register(normal); Check(!normal.gameObject.activeSelf, "Daytime hides a night-only normal enemy.");
                var entries = (System.Collections.IDictionary)typeof(WorldEnemySpawns).GetField("entries", Private).GetValue(spawns);
                object entry = entries[normal];
                var apply = typeof(WorldEnemySpawns).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
                data.worldTimeHours = 20; apply.Invoke(null, new[] { entry, session }); Check(normal.gameObject.activeSelf, "Night re-enables an enemy hidden by the spawn manager.");
                data.collected.Add(normal.Id); EnemySpawnRules.Schedule(data, normal.Id, 300); apply.Invoke(null, new[] { entry, session });
                Check(!normal.gameObject.activeSelf, "Consumed normal enemy hides until its timer.");
                data.playTimeSeconds += 300; Check(EnemySpawnRules.Revive(data, normal.Id), "Timer expires.");
                apply.Invoke(null, new[] { entry, session }); Check(normal.gameObject.activeSelf, "Respawn restores the same interaction root.");
                report.AppendLine("PASS: native encounter registration, day/night hiding, reactivation, defeat hiding and timer-driven return.");
            }
            finally { typeof(CampaignSession).GetProperty("Instance").SetValue(null, beforeInstance); }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Transition scenery fixture";
            floor.transform.position = new Vector3(20000, 0, 20000);
            var scan = (CombatAreaScan)Activator.CreateInstance(typeof(CombatAreaScan), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { new Pose(floor.transform.position, Quaternion.identity), 12f, new List<string>(), new List<GameObject> { floor } }, null);
            SceneTransitionCover.Show();
            try
            {
                Check(SceneTransitionCover.IsVisible, "Cover precedes scene staging.");
                Check(CombatSceneEnvironment.TryPrepare(scan, out var transfer, out string error, scene), "Native environment staging: " + error);
                using (transfer)
                {
                    Check(!floor.activeInHierarchy && SceneTransitionCover.IsVisible, "Scenery is hidden only behind the opaque cover.");
                    Check(transfer.Stage(scene, out error) && floor.activeInHierarchy, "Staged scenery reappears at destination.");
                }
            }
            finally { SceneTransitionCover.Hide(); UnityEngine.Object.DestroyImmediate(floor); }
            Check(!SceneTransitionCover.IsVisible, "Loading/failure can release the cover.");
            report.AppendLine("PASS: opaque transition cover stays active while native map scenery is staged, then releases cleanly.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture); UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(skill);
            EditorSceneManager.CloseScene(scene, true); if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            Check(CampaignSession.Instance == beforeInstance, "Validation must leave the real session unchanged.");
            Check(active.isDirty == dirty, "Validation must preserve the user's unsaved scene state.");
        }
        report.AppendLine("PASS: no Play Mode entered, no real campaign save written, live scene edits preserved.");
        File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("World systems PASS: " + Output + "/Validation.txt");
    }
    static void Capture(Canvas canvas, Scene scene, string path)
    {
        var cameraObject = new GameObject("Cheat preview camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << 31;
        var rt = new RenderTexture(1600, 900, 24); var previous = RenderTexture.active; Texture2D texture = null;
        try
        {
            foreach (var node in canvas.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            camera.targetTexture = rt; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
            texture = new Texture2D(1600, 900, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); if (texture != null) UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(cameraObject); }
    }
}
