using System;
using System.Collections;
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

[InitializeOnLoad]
public static class MArrivalDialogueSetup
{
    const string Request = "Library/CombatValidation/MArrivalDialogue.request";
    const string Output = "Tools/MArrivalDialogue";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static MArrivalDialogueSetup() { EditorApplication.update += Poll; }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MArrivalDialogueSetup.cs")) return;
        if (new[] { "MExplorationTutorial", "DiscoveryAreaBanner", "WorldInteraction" }.Any(n => File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/Adventure/" + n + ".cs"))) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try { if (command == "setup") Setup(); else Validate(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    static MExplorationTutorial Authored()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        Check(scene.IsValid() && scene.isLoaded, "Open scene m in Edit Mode first.");
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MExplorationTutorial>(true)).Single();
    }
    [MenuItem("Tools/Adventure/Tutorial/Setup m Arrival Dialogue")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Apply outside Play Mode.");
        var tutorial = Authored(); var scene = tutorial.gameObject.scene;
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity");
        Check(EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true), "Could not back up the current scene.");
        tutorial.arrivalDialogue = new[] { "dialog1", "dialog2" }.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/asset/audio/Main Dialog/" + n + ".mp3")).ToArray();
        Check(tutorial.arrivalDialogue.All(c => c != null), "Both supplied dialogue clips must import.");
        tutorial.dialogueGapSeconds = .18f; tutorial.tutorialDelaySeconds = 2f;
        if (tutorial.arrivalVoice == null)
        {
            var host = new GameObject("Arrival Dialogue Audio"); host.transform.SetParent(tutorial.transform, false);
            tutorial.arrivalVoice = host.AddComponent<AudioSource>(); tutorial.arrivalVoice.volume = .9f;
        }
        var source = tutorial.arrivalVoice; source.playOnAwake = false; source.loop = false; source.pitch = 1; source.spatialBlend = 0;
        source.ignoreListenerPause = true; source.priority = 48;
        var route = source.GetComponent<GameAudioSourceBus>(); if (route == null) route = source.gameObject.AddComponent<GameAudioSourceBus>(); route.bus = GameAudioBus.Voice;
        var exit = tutorial.walkExit.position;
        if (tutorial.arrivalDiscoveryArea == null)
            tutorial.arrivalDiscoveryArea = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorldInteraction>(true))
                .Where(a => a.kind == WorldInteractionKind.Discovery && a.gameObject.activeInHierarchy && Vector3.Distance(a.transform.position, exit) <= a.range)
                .OrderBy(a => (a.transform.position - exit).sqrMagnitude).FirstOrDefault();
        Check(tutorial.arrivalDiscoveryArea != null, "The arrival exit must be inside an existing Discovery Area.");
        if (tutorial.arrivalDiscoveryBanner == null)
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab").GetComponent<DiscoveryAreaBanner>();
            Check(template != null && template.panel != null, "Existing discovery banner template is missing.");
            var host = new GameObject("Arrival Discovery", typeof(RectTransform)); host.transform.SetParent(tutorial.tutorialCanvas.transform, false);
            var rect = host.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var panel = UnityEngine.Object.Instantiate(template.panel, host.transform, false); panel.name = "Discovery Area";
            var banner = host.AddComponent<DiscoveryAreaBanner>();
            banner.panel = panel;
            banner.areaName = Rebind(template.areaName, template.panel.transform, panel.transform);
            banner.nameGroup = Rebind(template.nameGroup, template.panel.transform, panel.transform);
            banner.topReveal = Rebind(template.topReveal, template.panel.transform, panel.transform);
            banner.topDivider = Rebind(template.topDivider, template.panel.transform, panel.transform);
            banner.frameRevealSeconds = template.frameRevealSeconds; banner.titleDelaySeconds = template.titleDelaySeconds;
            banner.titleFadeSeconds = template.titleFadeSeconds; banner.holdSeconds = template.holdSeconds; banner.fadeOutSeconds = template.fadeOutSeconds; banner.titleRise = template.titleRise;
            banner.Cancel(); tutorial.arrivalDiscoveryBanner = banner;
        }
        EditorUtility.SetDirty(tutorial); EditorUtility.SetDirty(source); EditorUtility.SetDirty(route);
        EditorSceneManager.MarkSceneDirty(scene); Check(EditorSceneManager.SaveScene(scene), "Could not save the updated scene m.");
        File.WriteAllText(Output + "/Setup.txt", "Backup: " + backup + "\nOrder: arrival camera → dialog1 + dialog2 and Discovery Area title, with cinematic bars → wait 2 seconds → exploration lessons.\nArea: " + tutorial.arrivalDiscoveryArea.displayName + "\nBanner is on the tutorial canvas, independent of the hidden HUD.\n");
        Validate();
    }
    static T Rebind<T>(T source, Transform originalRoot, Transform cloneRoot) where T : Component
    {
        string path = AnimationUtility.CalculateTransformPath(source.transform, originalRoot);
        return (path.Length == 0 ? cloneRoot : cloneRoot.Find(path)).GetComponent<T>();
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    [MenuItem("Tools/Adventure/Tutorial/Validate m Arrival Dialogue")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Validate in Edit Mode.");
        var authored = Authored(); var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        Check(authored.arrivalDialogue.Length == 2 && authored.arrivalDialogue.All(c => c != null), "Exactly two authored recordings.");
        Check(authored.arrivalDialogue.Select(c => c.name).SequenceEqual(new[] { "dialog1", "dialog2" }), "dialog1 must precede dialog2.");
        Check(authored.tutorialDelaySeconds >= 0 && authored.arrivalVoice.GetComponent<GameAudioSourceBus>().bus == GameAudioBus.Voice, "Authored tutorial delay and Voice category.");
        Check(authored.arrivalDiscoveryArea.kind == WorldInteractionKind.Discovery && authored.arrivalDiscoveryBanner.transform.IsChildOf(authored.tutorialCanvas.transform), "Scene-owned banner, independent of AdventureCanvas HUD.");
        Check(Vector3.Distance(authored.walkExit.position, authored.arrivalDiscoveryArea.transform.position) <= authored.arrivalDiscoveryArea.range, "Discovery rewards/entry can be registered at the real arrival position.");
        var previous = CampaignSession.Instance; var instanceField = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var preview = EditorSceneManager.NewPreviewScene(); CampaignConfig config = null; MExplorationTutorial fixture = null;
        var report = new StringBuilder();
        try
        {
            var host = new GameObject("Isolated arrival dialogue fixture"); SceneManager.MoveGameObjectToScene(host, preview);
            var inactive = new GameObject("Inactive campaign"); inactive.SetActive(false); inactive.transform.SetParent(host.transform);
            var session = inactive.AddComponent<CampaignSession>(); config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = active.name;
            Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", JsonUtility.FromJson<CampaignSave>("{\"version\":5,\"currentMapScene\":\"m\"}"));
            var actor = new GameObject("Player"); actor.transform.SetParent(host.transform); var player = actor.AddComponent<PlayerScript>(); player.enabled = false;
            player.CC = actor.AddComponent<CharacterController>(); Set(session, "<MapPlayer>k__BackingField", player); instanceField.SetValue(null, session);
            var controller = new GameObject("Tutorial"); controller.transform.SetParent(host.transform); fixture = controller.AddComponent<MExplorationTutorial>(); fixture.enabled = false; fixture.player = player; fixture.persistProgress = false;
            Set(fixture, "session", session); Set(fixture, "progress", new ExplorationTutorialProgress { id = MExplorationTutorial.ProgressId, curriculumVersion = MExplorationTutorial.CurriculumVersion });
            var canvas = UnityEngine.Object.Instantiate(authored.tutorialCanvas, host.transform, false); fixture.tutorialCanvas = canvas;
            fixture.lessons = canvas.GetComponentInChildren<GuidedCombatTutorialView>(true); fixture.lessons.Close();
            fixture.cinematicBars = canvas.transform.Find("Cinematic Bars").gameObject;
            fixture.arrivalDiscoveryBanner = canvas.GetComponentInChildren<DiscoveryAreaBanner>(true);
            fixture.keyHints = authored.keyHints.Select(h => canvas.transform.Find(AnimationUtility.CalculateTransformPath(h, authored.tutorialCanvas.transform)) as RectTransform).ToArray();
            fixture.worldFocus = canvas.transform.Find("World Focus") as RectTransform; fixture.worldFocusLabel = fixture.worldFocus.GetComponentInChildren<TMP_Text>(true);
            fixture.arrivalVoice = controller.AddComponent<AudioSource>(); fixture.arrivalVoice.playOnAwake = false; fixture.arrivalDialogue = authored.arrivalDialogue;
            fixture.dialogueGapSeconds = authored.dialogueGapSeconds; fixture.tutorialDelaySeconds = authored.tutorialDelaySeconds;
            var areaHost = new GameObject("Out-of-range discovery fixture; never awards or saves"); areaHost.transform.SetParent(host.transform); areaHost.transform.position = Vector3.one * 10000;
            fixture.arrivalDiscoveryArea = areaHost.AddComponent<WorldInteraction>(); fixture.arrivalDiscoveryArea.kind = WorldInteractionKind.Discovery;
            fixture.arrivalDiscoveryArea.range = 1; fixture.arrivalDiscoveryArea.displayName = authored.arrivalDiscoveryArea.displayName; fixture.arrivalDiscoveryArea.savedWorldId = "test-arrival-discovery";
            Check(session.TryBeginPresentation(fixture), "Acquire the same cinematic lock used by Arrival."); Set(fixture, "locked", true); fixture.cinematicBars.SetActive(true);
            var sequence = (IEnumerator)Call(fixture, "ArrivalDialogue"); float elapsed = 0;
            Check(sequence.MoveNext() && sequence.Current is WaitForSecondsRealtime && fixture.arrivalVoice.clip == authored.arrivalDialogue[0], "First line begins before any lesson.");
            Check(fixture.arrivalDiscoveryBanner.IsPlaying && fixture.arrivalDiscoveryBanner.panel.gameObject.activeInHierarchy && !fixture.lessons.IsOpen, "Discovery title appears with the first line while tutorial stays closed.");
            Capture(canvas, preview, fixture.arrivalDiscoveryBanner, report);
            void Wait(float seconds)
            {
                Check(sequence.Current is WaitForSecondsRealtime wait && Mathf.Abs(wait.waitTime - seconds) < .0001f, "Incorrect narration/delay duration.");
                elapsed += seconds; fixture.arrivalDiscoveryBanner.Tick(seconds);
                Check(fixture.cinematicBars.activeSelf && session.PresentationActive && CampaignSession.InputBlocked && !fixture.IsPracticing && !fixture.lessons.IsOpen, "Bars/input lock must remain until the complete post-cutscene phase ends.");
            }
            Wait(authored.arrivalDialogue[0].length);
            Check(sequence.MoveNext(), "Gap after line one."); Wait(authored.dialogueGapSeconds);
            Check(sequence.MoveNext() && fixture.arrivalVoice.clip == authored.arrivalDialogue[1], "Second line follows the first without overlap."); Wait(authored.arrivalDialogue[1].length);
            Check(sequence.MoveNext() && sequence.Current == null && fixture.arrivalDiscoveryBanner.IsPlaying, "Wait for the title's complete reveal/hold/fade.");
            fixture.arrivalDiscoveryBanner.Tick(fixture.arrivalDiscoveryBanner.Duration + .01f);
            Check(sequence.MoveNext(), "Delay follows both voice and discovery animation."); Wait(authored.tutorialDelaySeconds);
            Check(!sequence.MoveNext(), "Tutorial delay finishes once.");
            Call(fixture, "RestorePresentation"); Check(!fixture.cinematicBars.activeSelf && !session.PresentationActive && fixture.arrivalVoice.clip == null && !fixture.arrivalDiscoveryBanner.IsPlaying, "Normal handoff releases bars, audio, discovery and input.");
            Call(fixture, "BeginPractice"); Check(fixture.IsPracticing && fixture.lessons.IsOpen && fixture.lessons.IsHandsOn, "The real first exploration lesson opens after handoff.");
            Check(fixture.lessons.GetComponentsInChildren<Graphic>(true).Where(g => g.gameObject.activeInHierarchy).All(g => !g.raycastTarget), "Exploration hints still permit menu/rest clicks.");
            fixture.lessons.Close(); Set(fixture, "practicing", false); Check(session.TryBeginPresentation(fixture), "Reacquire for interruption test.");
            Set(fixture, "locked", true); fixture.cinematicBars.SetActive(true); var interrupted = (IEnumerator)Call(fixture, "ArrivalDialogue"); Check(interrupted.MoveNext(), "Interrupted sequence begins.");
            Call(fixture, "RestorePresentation"); Call(fixture, "HideAll"); (interrupted as IDisposable)?.Dispose();
            Check(fixture.arrivalVoice.clip == null && !fixture.arrivalVoice.isPlaying && !fixture.arrivalDiscoveryBanner.IsPlaying && !fixture.cinematicBars.activeSelf && !session.PresentationActive, "Interrupted arrival must not leak audio, banner or input lock.");
            session.SetMenu(AdventureMenu.Main); Check(session.Menu == AdventureMenu.Main, "P/main menu remains usable after cleanup."); session.SetMenu(AdventureMenu.None);
            report.AppendLine("PASS: dialog1 then dialog2, full clip lengths at pitch 1, authored gap, discovery reveal in parallel, bars and input lock retained, then authored " + authored.tutorialDelaySeconds + "s delay before the real hands-on lesson.");
            report.AppendLine("PASS: normal and interrupted cleanup stop voice/discovery/bars, release controls and permit menu/rest UI input. Edit Mode only; isolated out-of-range discovery prevents XP and campaign file writes.");
        }
        finally
        {
            if (fixture != null) Call(fixture, "RestorePresentation"); instanceField.SetValue(null, previous);
            EditorSceneManager.ClosePreviewScene(preview); if (config != null) UnityEngine.Object.DestroyImmediate(config);
            Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty, "User scene/dirty state must be preserved by validation.");
        }
        File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[m Arrival Dialogue] PASS: " + Output + "/Validation.txt");
    }
    static void Capture(Canvas canvas, Scene scene, DiscoveryAreaBanner banner, StringBuilder report)
    {
        var host = new GameObject("Arrival UI preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(host, scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .12f, .14f); camera.transform.position = new Vector3(0, 0, -10);
        var target = new RenderTexture(1280, 720, 24); var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            banner.Sample(1.2f); Canvas.ForceUpdateCanvases(); banner.areaName.ForceMeshUpdate(); Check(!banner.areaName.isTextOverflowing, "Area title must fit below the top cinematic bar.");
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string file = Output + "/Cinematic-Discovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png"; File.WriteAllBytes(file, pixels.EncodeToPNG()); report.AppendLine("UI preview: " + file);
        }
        finally
        {
            camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; RenderTexture.active = previous;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
