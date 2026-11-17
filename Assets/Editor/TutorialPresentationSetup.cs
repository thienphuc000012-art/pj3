using System;
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

/// <summary>Applies the shared lesson decoration and checks tutorial gates without entering Play or saving campaign data.</summary>
[InitializeOnLoad]
public static class TutorialPresentationSetup
{
    const string Request = "Library/CombatValidation/TutorialPresentation.request";
    const string Output = "Tools/TutorialPresentation";
    const string Prefab = "Assets/Resources/Combat/GuidedCombatTutorial.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static TutorialPresentationSetup() { EditorApplication.update += Poll; }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/TutorialPresentationSetup.cs")) return;
        if (new[] { "Assets/Scripts/Adventure/MExplorationTutorial.cs", "Assets/Scripts/combat/GuidedCombatTutorialView.cs", "Assets/Scripts/combat/TutorialInstructionKeys.cs" }
            .Any(file => File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc(file))) return;
        string command; try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        try { if (command == "setup") Setup(); else Validate(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    static MExplorationTutorial Authored()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/m.unity");
        Check(scene.IsValid() && scene.isLoaded, "Open m in Edit Mode first.");
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MExplorationTutorial>(true)).Single();
    }
    static void Decorate(GuidedCombatTutorialView view)
    {
        view.EnsureLessonPresentation();
        var background = view.transform.Find("Lesson").GetComponent<TutorialLessonBackground>();
        Check(background != null, "Shared ink style must be available before decorating lessons.");
        background.Build(Resources.Load<TutorialLessonStyle>("Combat/TutorialLessonStyle"));
        EditorUtility.SetDirty(view); EditorUtility.SetDirty(background);
    }
    static void EnsureInkStyle()
    {
        const string materialPath = "Assets/Resources/Combat/TutorialInkBackdrop.mat";
        const string stylePath = "Assets/Resources/Combat/TutorialLessonStyle.asset";
        var brush = AssetDatabase.LoadAllAssetsAtPath("Assets/asset/Ui/Grunge Ink Brush Stroke Collection.png").OfType<Sprite>().Single(s => s.name.EndsWith("_2"));
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/Combat/TutorialInkBackdrop.shader");
        Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Tutorial ink shader must compile.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(shader); material.name = "Tutorial Ink Backdrop"; AssetDatabase.CreateAsset(material, materialPath); }
        material.shader = shader; material.SetVector("_BrushRect", UnityEngine.Sprites.DataUtility.GetOuterUV(brush)); material.SetTexture("_MainTex", brush.texture);
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        var style = AssetDatabase.LoadAssetAtPath<TutorialLessonStyle>(stylePath);
        if (style == null) { style = ScriptableObject.CreateInstance<TutorialLessonStyle>(); AssetDatabase.CreateAsset(style, stylePath); }
        style.inkBrush = brush; style.inkMaterial = material;
        EditorUtility.SetDirty(style); AssetDatabase.SaveAssetIfDirty(style);
    }
    [MenuItem("Tools/Adventure/Tutorial/Apply Lesson Presentation")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Apply in Edit Mode.");
        var tutorial = Authored(); var scene = tutorial.gameObject.scene;
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(scene.path, backup + "/m.disk.unity"); File.Copy(Prefab, backup + "/GuidedCombatTutorial.prefab");
        Check(EditorSceneManager.SaveScene(scene, backup + "/m.current.unity", true), "Back up unsaved scene edits first.");
        EnsureInkStyle();
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try { Decorate(root.GetComponent<GuidedCombatTutorialView>()); PrefabUtility.SaveAsPrefabAsset(root, Prefab); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach (var view in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GuidedCombatTutorialView>(true)))
        { Decorate(view); }
        EditorSceneManager.MarkSceneDirty(scene); Check(EditorSceneManager.SaveScene(scene), "Save lesson changes to m.");
        const string combatPath = "Assets/Scenes/combattest.unity";
        File.Copy(combatPath, backup + "/combattest.disk.unity");
        var combat = SceneManager.GetSceneByPath(combatPath); bool wasLoaded = combat.IsValid() && combat.isLoaded;
        try
        {
            if (!wasLoaded) combat = EditorSceneManager.OpenScene(combatPath, OpenSceneMode.Additive);
            else Check(EditorSceneManager.SaveScene(combat, backup + "/combattest.current.unity", true), "Back up loaded combat scene first.");
            var views = combat.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GuidedCombatTutorialView>(true)).ToArray();
            Check(views.Length > 0, "Authored combat lesson must exist.");
            foreach (var view in views) Decorate(view);
            EditorSceneManager.MarkSceneDirty(combat); Check(EditorSceneManager.SaveScene(combat), "Save lesson changes to combattest.");
        }
        finally { SceneManager.SetActiveScene(scene); if (!wasLoaded && combat.IsValid() && combat.isLoaded) EditorSceneManager.CloseScene(combat, true); }
        File.WriteAllText(Output + "/Setup.txt", "Backup: " + backup + "\nApplied textured black ink, inward-facing brush feather, distressed corner ornament and open ivory border to shared prefab and m/combattest lessons; retained text, keycaps and placement.\n");
        Validate();
    }
    [MenuItem("Tools/Adventure/Tutorial/Validate Lesson Presentation")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Validate in Edit Mode.");
        Directory.CreateDirectory(Output); var authored = Authored(); var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var languageField = typeof(GameLocalization).GetField("language", Static); var language = GameLocalization.CurrentLanguage;
        var sessionField = typeof(CampaignSession).GetField("<Instance>k__BackingField", Static); var previousSession = CampaignSession.Instance;
        var ownerField = typeof(MExplorationTutorial).GetField("activePractice", Static); var previousOwner = ownerField.GetValue(null);
        var preview = EditorSceneManager.NewPreviewScene(); preview.name = "m";
        float timescale = Time.timeScale; var cursor = Cursor.lockState; bool visible = Cursor.visible;
        CampaignConfig config = null; var report = new StringBuilder();
        try
        {
            var host = new GameObject("Isolated lesson policy fixture"); SceneManager.MoveGameObjectToScene(host, preview);
            var inactive = new GameObject("Inactive campaign"); inactive.SetActive(false); inactive.transform.SetParent(host.transform);
            var session = inactive.AddComponent<CampaignSession>(); config = ScriptableObject.CreateInstance<CampaignConfig>(); config.mapScene = active.name;
            Set(session, "<Config>k__BackingField", config); Set(session, "<Data>k__BackingField", new CampaignSave()); sessionField.SetValue(null, session);
            var actor = new GameObject("Player"); actor.transform.SetParent(host.transform); var player = actor.AddComponent<PlayerScript>(); player.enabled = false;
            Set(session, "<MapPlayer>k__BackingField", player);
            var tutorial = inactive.AddComponent<MExplorationTutorial>(); tutorial.player = player; tutorial.persistProgress = false;
            var progress = new ExplorationTutorialProgress { id = MExplorationTutorial.ProgressId, curriculumVersion = MExplorationTutorial.CurriculumVersion };
            Set(tutorial, "session", session); Set(tutorial, "progress", progress); Set(tutorial, "practicing", true); ownerField.SetValue(null, tutorial);
            for (int step = 0; step < MExplorationTutorial.LessonCount; step++)
            {
                progress.nextLesson = step;
                foreach (ExplorationTutorialAction action in Enum.GetValues(typeof(ExplorationTutorialAction)))
                    Check(MExplorationTutorial.Allows(action, player) == (step >= (int)action), "Gate mismatch: step " + step + ", " + action);
                session.SetMenu(AdventureMenu.Main); Check((session.Menu == AdventureMenu.Main) == (step >= 2), "P/button menu opened before its lesson.");
                session.SetMenu(AdventureMenu.None); Check(session.Menu == AdventureMenu.None && Time.timeScale == timescale, "Menu closing/time restoration.");
                Check(session.CanStartEncounter == (step >= 6), "Encounter attack gate mismatch at step " + step);
            }
            progress.nextLesson = 0; var other = new GameObject("Different actor"); other.transform.SetParent(host.transform); var otherPlayer = other.AddComponent<PlayerScript>(); otherPlayer.enabled = false;
            Check(MExplorationTutorial.Allows(ExplorationTutorialAction.Interact, otherPlayer), "A different actor must remain unaffected.");
            preview.name = "mapgame"; Check(MExplorationTutorial.Allows(ExplorationTutorialAction.Menu, player), "Policy must apply only to scene m."); preview.name = "m";
            session.BeginItemPickup(null); session.OpenRest(null); session.BeginDialogue(null); Check(!session.Busy && session.Menu == AdventureMenu.None, "Future interactions changed state.");
            Check(!session.TravelToMap(null), "Future portal interaction must be rejected.");
            Set(player, "<onSurface>k__BackingField", true); var parkour = actor.AddComponent<FarkourControllerScript>(); parkour.playerScript = player;
            int jumps = player.JumpCount; Call(parkour, "TryJumpOrParkour");
            Check(player.JumpCount == jumps + 1 && !player.playerInAction && !player.playerHanging, "Space must perform a normal jump before Parkour unlocks.");
            progress.complete = true; Check(MExplorationTutorial.Allows(ExplorationTutorialAction.Encounter, player), "Completion must release controls."); progress.complete = false;
            Call(tutorial, "OnDisable"); Check(MExplorationTutorial.Allows(ExplorationTutorialAction.Encounter, player), "Disable/unload must release the owner.");
            report.AppendLine("PASS: all seven stages, progressive scan/menu/parkour/interact/encounter gates, menu close, time restoration, completion, disable, other actor and mapgame isolation. No campaign writes.");

            var canvas = UnityEngine.Object.Instantiate(authored.tutorialCanvas, host.transform, false); var view = GuidedCombatTutorialView.Create(canvas, TMP_Settings.defaultFontAsset);
            tutorial.tutorialCanvas = canvas; tutorial.lessons = view;
            tutorial.keyHints = authored.keyHints.Select(h => canvas.transform.Find(AnimationUtility.CalculateTransformPath(h, authored.tutorialCanvas.transform)) as RectTransform).ToArray();
            tutorial.worldFocus = canvas.transform.Find("World Focus") as RectTransform; tutorial.worldFocusLabel = tutorial.worldFocus.GetComponentInChildren<TMP_Text>(true);
            tutorial.campfire = authored.campfire; tutorial.parkourObstacle = authored.parkourObstacle; tutorial.encounterEnemy = authored.encounterEnemy;
            foreach (var hint in tutorial.keyHints) hint.gameObject.SetActive(true);
            Set(tutorial, "practicing", true); ownerField.SetValue(null, tutorial);
            var page = view.transform.Find("Lesson"); var text = page.Find("Instruction").GetComponent<TMP_Text>();
            Check(page.Find("Pattern/Diamond").GetComponent<TutorialDiamondPattern>() != null && page.Find("Frame") != null, "Lesson diamond pattern and frame.");
            Check(page.GetComponent<Image>().color.a <= .901f, "Lesson background alpha.");
            var backdrop = page.GetComponent<TutorialLessonBackground>();
            Check(backdrop != null && backdrop.style != null && page.Find("Ink Background").GetComponent<Image>().sprite != null, "Authored ink sprite/style.");
            Check(!page.Find("Pattern").gameObject.activeSelf && !page.Find("Frame").gameObject.activeSelf, "Old uniform grid/rectangular frame must be hidden.");
            Check(!ShaderUtil.ShaderHasError(backdrop.style.inkMaterial.shader), "Ink shader errors.");
            var rect = page as RectTransform; var savedMin = rect.anchorMin; var savedMax = rect.anchorMax;
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(.36f, 1); backdrop.Align();
            Check(page.Find("Ink Background").localScale.x == 1 && page.Find("Ink Background").GetComponent<RectTransform>().anchorMax.x > 1, "Left panel brush must feather toward the map.");
            rect.anchorMin = new Vector2(.64f, 0); rect.anchorMax = Vector2.one; backdrop.Align();
            Check(page.Find("Ink Background").localScale.x == -1 && page.Find("Ink Background").GetComponent<RectTransform>().anchorMin.x < 0, "Right panel must mirror only the decoration, not the text.");
            rect.anchorMin = savedMin; rect.anchorMax = savedMax; backdrop.Align();
            foreach (var lang in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            {
                languageField.SetValue(null, lang);
                for (int step = 0; step < MExplorationTutorial.LessonCount; step++)
                {
                    progress.nextLesson = step; Call(tutorial, "ShowLesson"); Call(tutorial, "LateUpdate"); Canvas.ForceUpdateCanvases(); Call(view, "LateUpdate");
                    Check(view.IsHandsOn && view.GetComponentsInChildren<Graphic>(true).Where(g => g.gameObject.activeInHierarchy).All(g => !g.raycastTarget), "Exploration overlay must pass UI clicks.");
                    Check(tutorial.keyHints[2].GetComponent<CanvasGroup>().alpha == (step >= 1 ? 1f : .28f), "Locked Q hint must be dimmed.");
                    Check(tutorial.keyHints[3].GetComponent<CanvasGroup>().alpha == (step >= 2 ? 1f : .28f), "Locked P hint must be dimmed.");
                    Check(tutorial.keyHints[4].GetComponent<CanvasGroup>().alpha == (step >= 4 ? 1f : .28f), "Locked E hint must be dimmed.");
                    Capture(canvas, preview, view, lang + "-m-" + step, report);
                    Check(text.textInfo.linkCount > 0 && text.GetComponentInChildren<TutorialKeyFrameGraphic>().BoxCount == text.textInfo.linkCount, "Key frame count at " + lang + " step " + step + ": links=" + text.textInfo.linkCount + ", boxes=" + text.GetComponentInChildren<TutorialKeyFrameGraphic>().BoxCount + ", rich=" + text.richText + ", active=" + text.isActiveAndEnabled + ", chars=" + text.textInfo.characterCount + ", text=" + text.text);
                }
            }
            view.Close(); view.Show(1, 3, null, "Parry", "Press <b>Space</b> to parry. Q / P / E", "Continue", null);
            Call(view, "LateUpdate"); Check(!view.IsHandsOn && view.transform.Find("Pointer Block").GetComponent<Graphic>().raycastTarget, "Combat lessons must restore modal input.");
            Capture(canvas, preview, view, "Combat", report);
            Check(text.textInfo.linkCount == 4 && text.GetComponentInChildren<TutorialKeyFrameGraphic>().BoxCount == 4, "Combat rich-text keys.");
            view.Close();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Check(template.transform.Find("Lesson/Pattern/Diamond") != null && template.transform.Find("Lesson/Instruction").GetComponent<TutorialInstructionKeys>() != null, "Shared combat prefab must persist authored decorations.");
            report.AppendLine("PASS: VI/EN for all seven lessons, Q/P/E dimming, keycap geometry, textured ink with correct inward-facing feather on both sides, corner ornament, old grid hidden, compiled UI shader, combat modal after exploration. Rendered native 1280x720 previews.");
        }
        finally
        {
            ownerField.SetValue(null, previousOwner); sessionField.SetValue(null, previousSession); languageField.SetValue(null, language);
            EditorSceneManager.ClosePreviewScene(preview); if (config != null) UnityEngine.Object.DestroyImmediate(config);
            Time.timeScale = timescale; Cursor.lockState = cursor; Cursor.visible = visible;
            Check(SceneManager.GetActiveScene() == active && active.isDirty == dirty, "Validation changed user scene state.");
        }
        File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Tutorial Presentation] PASS: " + Output + "/Validation.txt");
        MPortalTutorialValidation.Run(authored, true);
        MArrivalDialogueSetup.Validate();
    }
    static void Capture(Canvas canvas, Scene scene, GuidedCombatTutorialView view, string name, StringBuilder report)
    {
        var host = new GameObject("Lesson preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(host, scene);
        var camera = host.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .17f, .19f); camera.transform.position = new Vector3(0, 0, -10);
        var target = new RenderTexture(1280, 720, 24); var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); Call(view, "LateUpdate"); Canvas.ForceUpdateCanvases();
            foreach (var text in view.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Instruction text overflow: " + name + " " + text.name); }
            camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string file = Output + "/" + name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png"; File.WriteAllBytes(file, pixels.EncodeToPNG()); report.AppendLine("Preview: " + file);
        }
        finally
        {
            camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; RenderTexture.active = previous;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
