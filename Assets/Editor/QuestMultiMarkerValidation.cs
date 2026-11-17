using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Isolated compass and quest-data checks. Never enters Play or invokes campaign Save.</summary>
[InitializeOnLoad]
public static class QuestMultiMarkerValidation
{
    const string Request = "Library/CombatValidation/QuestMultiMarker.request";
    const string Output = "Tools/QuestMultiMarker";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static QuestMultiMarkerValidation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/QuestMultiMarkerValidation.cs")) return;
        if (new[] { "CampaignSession", "CampaignConfig", "AdventureCompassHUD" }.Any(n => File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/Adventure/" + n + ".cs"))) return;
        File.Delete(Request); try { Validate(); } catch (Exception error) { Debug.LogException(error); }
    }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    [MenuItem("Tools/Adventure/Quests/Validate Multiple Compass Markers")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Use Edit Mode."); Directory.CreateDirectory(Output);
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var previous = CampaignSession.Instance; var instance = typeof(CampaignSession).GetField("<Instance>k__BackingField", Static);
        var languageField = typeof(GameLocalization).GetField("language", Static); var language = GameLocalization.CurrentLanguage;
        var scene = EditorSceneManager.NewPreviewScene(); CampaignConfig config = null; AdventureQuest quest = null;
        var report = new StringBuilder();
        try
        {
            var host = new GameObject("Isolated quest marker fixture"); SceneManager.MoveGameObjectToScene(host, scene);
            var inactive = new GameObject("Inactive campaign"); inactive.SetActive(false); inactive.transform.SetParent(host.transform);
            var session = inactive.AddComponent<CampaignSession>(); config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = active.name;
            quest = ScriptableObject.CreateInstance<AdventureQuest>(); quest.questId = "two-enemies-test"; quest.requiredCount = 2; quest.objectiveType = AdventureQuestObjectiveType.DefeatEnemy;
            config.quests.Add(quest); var data = new CampaignSave { trackedQuestId = quest.Id };
            var progress = new AdventureQuestProgress { questId = quest.Id }; data.quests.Add(progress);
            Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", data); instance.SetValue(null, session);
            var playerGo = new GameObject("Player"); playerGo.transform.SetParent(host.transform); var player = playerGo.AddComponent<PlayerScript>(); player.enabled = false;
            Set(session, "<MapPlayer>k__BackingField", player);
            WorldInteraction Point(string id, Vector3 position, WorldInteractionKind kind)
            {
                var go = new GameObject(id); go.transform.SetParent(host.transform); go.transform.position = position;
                var point = go.AddComponent<WorldInteraction>(); point.kind = kind; point.savedWorldId = id; return point;
            }
            var a = Point("enemy-a", new Vector3(-40, 0, 90), WorldInteractionKind.Encounter);
            var b = Point("enemy-b", new Vector3(45, 0, 140), WorldInteractionKind.Encounter);
            var npc = Point("quest-giver", new Vector3(0, 0, -120), WorldInteractionKind.DialogueNPC);
            var targets = new List<QuestObjectivePosition>(); Set(session, "worldPoints", new[] { a, b, npc });
            session.CollectTrackedQuestTargets(targets); Check(targets.Count == 2 && targets.Select(t => t.worldPointId).Distinct().Count() == 2, "Both available encounters need distinct destinations.");
            Check(session.TryGetTrackedQuestPosition(out var nearest) && nearest == a.transform.position, "The old single-position API must still return the nearest target.");
            var roundTrip = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data));
            Check(roundTrip.quests[0].objectivePositions.Count == 2, "Both cached positions must survive save serialization.");
            Set(session, "worldPoints", Array.Empty<WorldInteraction>()); session.CollectTrackedQuestTargets(targets); Check(targets.Count == 2, "Unloading a chunk must keep both known positions.");
            Set(session, "worldPoints", new[] { a, b, npc });
            a.questMarkerId = b.questMarkerId = "quest-pack"; quest.objectiveWorldPointId = "quest-pack";
            var unrelated = Point("unrelated-enemy", Vector3.right * 20, WorldInteractionKind.Encounter);
            Set(session, "worldPoints", new[] { a, b, unrelated, npc }); session.CollectTrackedQuestTargets(targets);
            Check(targets.Count == 2, "A shared quest marker group must include both enemies and exclude unrelated points.");
            b.enabled = false; session.CollectTrackedQuestTargets(targets); Check(targets.Count == 1 && targets[0].worldPointId == a.Id, "Unavailable enemies must not remain as cached ghosts."); b.enabled = true;
            report.AppendLine("PASS: two distinct enemy destinations, nearest legacy API, grouped marker IDs, unrelated/disabled enemy exclusion, multi-position JSON round trip and unloaded-chunk cache.");

            var canvasHost = new GameObject("Quest compass canvas", typeof(RectTransform), typeof(Canvas), typeof(AdventureCanvasRoot)); canvasHost.transform.SetParent(host.transform);
            var root = canvasHost.GetComponent<AdventureCanvasRoot>(); root.defaultFont = Resources.Load<GameObject>("Adventure/UI/AdventureCanvas").GetComponent<AdventureCanvasRoot>().defaultFont;
            var hud = new GameObject("HUD", typeof(RectTransform)); hud.transform.SetParent(canvasHost.transform, false); var hudRect = hud.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero; hudRect.anchorMax = Vector2.one; hudRect.offsetMin = hudRect.offsetMax = Vector2.zero;
            var compass = canvasHost.AddComponent<AdventureCompassHUD>(); Call(compass, "Awake");
            var strip = canvasHost.transform.Find("HUD/Horizontal Compass HUD");
            Transform[] Icons() => strip.Cast<Transform>().Where(t => t.name.StartsWith("Tracked Quest Marker", StringComparison.Ordinal) && t.gameObject.activeSelf).ToArray();
            Transform[] Distances() => strip.Cast<Transform>().Where(t => t.name.StartsWith("Tracked Quest Distance", StringComparison.Ordinal) && t.gameObject.activeSelf).ToArray();
            foreach (var lang in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            {
                languageField.SetValue(null, lang); Call(compass, "UpdateTrackedObjective", session, 0f);
                Check(Icons().Length == 2 && Distances().Length == 2, "Each target needs its own icon and distance in " + lang);
                Check(Distances().All(t => t.GetComponent<TMP_Text>().text.EndsWith(" m")), "Localized distance labels.");
            }
            Capture(canvasHost.GetComponent<Canvas>(), compass, scene, "Separate-bearings", report);
            a.transform.position = Vector3.forward * 100; b.transform.position = Vector3.forward * 170;
            Call(compass, "UpdateTrackedObjective", session, 0f);
            var iconRects = Icons().Select(t => t.GetComponent<RectTransform>()).ToArray();
            Check(Mathf.Approximately(iconRects[0].anchoredPosition.x, iconRects[1].anchoredPosition.x) && Mathf.Abs(iconRects[0].anchoredPosition.y - iconRects[1].anchoredPosition.y) >= 60, "Same-bearing targets must remain visible without changing bearing.");
            Capture(canvasHost.GetComponent<Canvas>(), compass, scene, "Same-bearing", report);
            int pool = strip.childCount;
            data.collected.Add(a.Id); progress.objectiveCount = 1; Call(compass, "UpdateTrackedObjective", session, 0f);
            Check(Icons().Length == 1 && Distances().Length == 1 && Distances()[0].GetComponent<TMP_Text>().text == "170 m", "Defeated target must disappear, remaining enemy stays tracked.");
            data.collected.Add(b.Id); session.CollectTrackedQuestTargets(targets); Check(targets.Count == 0, "No stale single-position fallback after every cached enemy is defeated.");
            progress.objectiveCount = 2; progress.completed = true; progress.turnInNpcId = npc.Id;
            Call(compass, "UpdateTrackedObjective", session, 0f); session.CollectTrackedQuestTargets(targets);
            Check(Icons().Length == 1 && targets.Count == 1 && targets[0].worldPointId == npc.Id, "Completed objective must return to one quest-giver marker.");
            progress.rewardsClaimed = true; Call(compass, "UpdateTrackedObjective", session, 0f); Check(Icons().Length == 0 && Distances().Length == 0, "Claimed quest markers must hide.");
            progress.rewardsClaimed = false; data.trackedQuestId = string.Empty; Call(compass, "UpdateTrackedObjective", session, 0f); Check(Icons().Length == 0, "Untracking must hide every marker.");
            Check(strip.childCount == pool && strip.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "Marker pool must be reused without blocking input.");
            data.trackedQuestId = quest.Id; progress.completed = false; progress.hasObjectivePosition = true; progress.objectiveMapScene = active.name; progress.objectivePosition = new Vector3(10, 0, 20); progress.objectivePositions.Clear();
            Set(session, "worldPoints", Array.Empty<WorldInteraction>()); Check(session.TryGetTrackedQuestPosition(out var legacy) && legacy == progress.objectivePosition, "Old saved single-position fallback remains compatible.");
            progress.hasObjectivePosition = false; quest.useFixedObjectivePosition = true; quest.objectivePosition = new Vector3(100, 0, 200);
            Check(session.TryGetTrackedQuestPosition(out var fixedPosition) && fixedPosition == quest.objectivePosition, "Explicit fixed destination remains available.");
            quest.objectiveMapScene = "different-map"; session.CollectTrackedQuestTargets(targets); Check(targets.Count == 0, "Objectives from another map must stay hidden.");
            report.AppendLine("PASS: VI/EN two-marker UI, distinct distances, same-bearing stacking, 2->1 after defeat, stale-position removal, one NPC marker at completion, claimed/untracked cleanup, pooled noninteractive UI, old save/fixed-position compatibility and cross-map filtering.");
        }
        catch (Exception error) { File.WriteAllText(Output + "/Validation.txt", report + "\nFAIL: " + error); throw; }
        finally
        {
            instance.SetValue(null, previous); languageField.SetValue(null, language); EditorSceneManager.ClosePreviewScene(scene);
            if (quest != null) UnityEngine.Object.DestroyImmediate(quest); if (config != null) UnityEngine.Object.DestroyImmediate(config);
            Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty, "User scene and unsaved state must remain unchanged.");
        }
        File.WriteAllText(Output + "/Validation.txt", report + "\nEdit Mode only; no player save files or preferences written.\n"); Debug.Log("[Quest Multi Marker] PASS: " + Output + "/Validation.txt");
    }
    static void Capture(Canvas canvas, AdventureCompassHUD compass, Scene scene, string name, StringBuilder report)
    {
        var host = new GameObject("Compass preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(host, scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .085f, .10f); camera.transform.position = Vector3.back * 10;
        var texture = new RenderTexture(1280, 720, 24); var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = texture; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); Call(compass, "ResizeStrip");
            var position = typeof(AdventureCompassHUD).GetMethod("Position", Private);
            foreach (var list in new[] { "ticks", "directions" })
                foreach (var mark in (System.Collections.IEnumerable)typeof(AdventureCompassHUD).GetField(list, Private).GetValue(compass)) position.Invoke(compass, new[] { mark, 0f, list == "ticks" ? 5f : 15f });
            Call(compass, "UpdateTrackedObjective", CampaignSession.Instance, 0f); Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string path = Output + "/" + name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png"; File.WriteAllBytes(path, pixels.EncodeToPNG()); report.AppendLine("Preview: " + path);
        }
        finally { camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(host); }
    }
}
