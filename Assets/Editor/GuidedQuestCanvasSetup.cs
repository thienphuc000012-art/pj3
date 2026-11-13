using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit authoring request only. Never starts gameplay or touches campaign saves.
public static class GuidedQuestCanvasSetup
{
    const string Request = "Library/CombatValidation/GuidedQuestCanvas.request";
    const string Result = "Library/CombatValidation/GuidedQuestCanvas.result.txt";
    const string Adventure = "Assets/Resources/Adventure/UI/AdventureCanvas.prefab";
    const string Tutorial = "Assets/Resources/Combat/GuidedCombatTutorial.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
    }
    static void RunRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        File.Delete(Request);
        Setup();
    }

    [MenuItem("Tools/Adventure/UI/Setup Guided Tutorial and Quest Canvas")]
    public static void Setup()
    {
        string backup = "Tools/CanvasUI/BeforeGuidedQuest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before authoring UI.");
            Directory.CreateDirectory(backup);
            foreach (var path in new[] { Adventure, Tutorial, "Assets/Scenes/combattest.unity", "Assets/Scenes/Title.unity" })
                if (File.Exists(path)) File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
            AuthorAdventure();
            AuthorTutorialPrefab();
            EditScene("Assets/Scenes/combattest.unity", scene =>
            {
                var canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .First(canvas => canvas.isRootCanvas && canvas.GetComponentInChildren<AdvancedUIManager>(true) != null);
                var tutorial = GuidedCombatTutorialView.Create(canvas, null);
                Check(tutorial != null, "Could not author the tutorial Canvas.");
                tutorial.gameObject.SetActive(false);
            });
            EditScene("Assets/Scenes/Title.unity", scene =>
            {
                var title = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AdventureTitleMenu>(true)).Single();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Adventure).GetComponent<AdventureSaveSlotsView>();
                var view = title.GetComponent<AdventureSaveSlotsView>() ?? title.gameObject.AddComponent<AdventureSaveSlotsView>();
                var titleState = new SerializedObject(title);
                view.BindTitle(source, (Button)titleState.FindProperty("continueButton").objectReferenceValue,
                    () => new List<CampaignSaveSlotInfo>(), RejectLoad, null);
            });
            ValidateAuthoredBehaviour();
            GuidedCombatTutorialValidation.Run();
            AdventureMenuNavigationValidation.Validate();
            Check(File.ReadAllText("Library/CombatValidation/MenuNavigation.result.txt").StartsWith("PASS"), "Quest navigation regression failed.");
            File.WriteAllText(Result, "PASS: authored Guided Tutorial prefab and combattest Canvas; authored Quest button/list/template/confirmation; layout survives rebinding; VI/EN labels and live quest refresh; Title save slot/Yes/No/Back text-only selection under PreserveAuthoredUI; Run Away blocked throughout tutorial. Guided branches and quest navigation regressions passed. Backup: " + backup);
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "FAIL: " + exception + "\nBackup: " + backup);
            Debug.LogException(exception);
        }
    }

    static bool RejectLoad(int slot, out string error) { error = "Validation does not load saves."; return false; }

    static void AuthorAdventure()
    {
        var prefab = PrefabUtility.LoadPrefabContents(Adventure);
        GameObject helper = null;
        try
        {
            helper = new GameObject("Quest Canvas Authoring"); helper.SetActive(false);
            var controller = helper.AddComponent<AdventureUI>();
            var root = prefab.GetComponent<AdventureCanvasRoot>();
            Set(controller, "view", root);
            Set(controller, "travelButtonTemplate", root.Component<Button>("Menu/Rest/Save"));
            Call(controller, "BuildQuestUI");
            var layout = root.transform.Find("Menu/Tasks/Quest List/Quest Scroll/Content").GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            prefab.transform.Find("Menu/Tasks").gameObject.SetActive(false);
            prefab.transform.Find("Menu/Quest Track Confirmation").gameObject.SetActive(false);
            var saves = prefab.GetComponent<AdventureSaveSlotsView>();
            saves.EnsureHierarchy(root, root.Component<Button>("Menu/Rest/Save"));
            Check(PrefabUtility.SaveAsPrefabAsset(prefab, Adventure) != null, "Could not save AdventureCanvas.");
        }
        finally
        {
            if (helper != null) UnityEngine.Object.DestroyImmediate(helper);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    static void AuthorTutorialPrefab()
    {
        if (File.Exists(Tutorial)) return;
        var root = new GameObject("Tutorial Authoring Canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            var font = AssetDatabase.LoadAssetAtPath<GameObject>(Adventure).GetComponent<AdventureCanvasRoot>().defaultFont;
            var view = GuidedCombatTutorialView.Create(root.GetComponent<Canvas>(), font);
            view.SetAuthoringPreview();
            view.Close();
            Check(PrefabUtility.SaveAsPrefabAsset(view.gameObject, Tutorial) != null, "Could not save tutorial prefab.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static void EditScene(string path, Action<Scene> edit)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool alreadyOpen = scene.IsValid() && scene.isLoaded;
        Scene active = SceneManager.GetActiveScene();
        if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            edit(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Check(EditorSceneManager.SaveScene(scene), "Could not save " + path);
        }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void ValidateAuthoredBehaviour()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var originalCombat = CombatManager.Instance;
        var sessionField = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var originalSession = CampaignSession.Instance;
        CampaignConfig config = null;
        AdventureQuest quest = null;
        var languageField = typeof(GameLocalization).GetField("language", BindingFlags.Static | BindingFlags.NonPublic);
        var originalLanguage = GameLocalization.CurrentLanguage;
        try
        {
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Adventure), preview);
            var root = canvas.GetComponent<AdventureCanvasRoot>();
            canvas.transform.Find("Menu").gameObject.SetActive(true);
            var helper = new GameObject("Isolated Authored Quest Check"); helper.SetActive(false);
            SceneManager.MoveGameObjectToScene(helper, preview);
            var controller = helper.AddComponent<AdventureUI>();
            Set(controller, "view", root); Set(controller, "travelButtonTemplate", root.Component<Button>("Menu/Rest/Save"));
            var tasks = canvas.transform.Find("Menu/Tasks");
            var list = tasks.Find("Quest List").GetComponent<RectTransform>();
            list.anchoredPosition += new Vector2(17f, -9f);
            Vector2 position = list.anchoredPosition;
            int count = canvas.GetComponentsInChildren<Transform>(true).Length;
            Call(controller, "BuildQuestUI"); Call(controller, "BuildQuestUI");
            Check(list.anchoredPosition == position && canvas.GetComponentsInChildren<Transform>(true).Length == count,
                "Binding recreated or moved authored quest UI.");
            foreach (var language in new[] { GameLanguage.English, GameLanguage.Vietnamese })
            {
                languageField.SetValue(null, language);
                Call(controller, "BuildQuestUI");
                string text = root.Component<TMP_Text>("Menu/Party/Overview/Quests/Label").text;
                Check(text == (language == GameLanguage.English ? "Quests" : "Nhiệm vụ"), "Quest command did not translate.");
                string heading = tasks.Find("Quest List/Heading").GetComponent<TMP_Text>().text;
                Check(heading == (language == GameLanguage.English ? "AVAILABLE QUESTS" : "NHIỆM VỤ ĐANG CÓ"), "Quest heading did not translate.");
            }
            // Simulate the language-change handler while both the list and its
            // confirmation are open, using only in-memory quest progress.
            var session = helper.AddComponent<CampaignSession>();
            config = ScriptableObject.CreateInstance<CampaignConfig>();
            quest = ScriptableObject.CreateInstance<AdventureQuest>();
            quest.questId = "authored-language-test";
            quest.displayName = "NHIỆM VỤ"; quest.description = "Chưa có nhiệm vụ.";
            config.quests.Add(quest);
            var data = new CampaignSave { trackedQuestId = quest.Id };
            data.quests.Add(new AdventureQuestProgress { questId = quest.Id });
            Set(session, "<Config>k__BackingField", config);
            Set(session, "<Data>k__BackingField", data);
            Set(session, "<Menu>k__BackingField", AdventureMenu.Tasks);
            sessionField.SetValue(null, session);
            tasks.gameObject.SetActive(true);
            Call(controller, "RefreshQuestList"); Call(controller, "OpenQuestDialog", quest);
            foreach (var language in new[] { GameLanguage.English, GameLanguage.Vietnamese })
            {
                languageField.SetValue(null, language);
                Call(controller, "RefreshQuestLanguage");
                var row = tasks.Find("Quest List/Quest Scroll/Content/Quest " + quest.Id);
                Check(row.Find("Label").GetComponent<TMP_Text>().text.StartsWith(language == GameLanguage.English ? "QUESTS" : "NHIỆM VỤ"),
                    "Open quest rows must update language.");
                Check(row.parent.childCount == 2, "Language refresh must not duplicate quest rows or the template.");
                var prompt = root.Component<TMP_Text>("Menu/Quest Track Confirmation/Panel/Prompt");
                Check(prompt.text.Contains(GameLocalization.Translate("Hủy theo dõi nhiệm vụ?")), "Open quest confirmation must update language.");
            }
            // The actual Title ancestry suppresses theme recoloring. Text feedback must still work there.
            var preserve = new GameObject("Title-style Canvas", typeof(RectTransform), typeof(PreserveAuthoredUI));
            SceneManager.MoveGameObjectToScene(preserve, preview);
            var saveTemplate = canvas.GetComponent<AdventureSaveSlotsView>();
            foreach (var source in new[] { saveTemplate.slotTemplate, saveTemplate.yesButton, saveTemplate.noButton, saveTemplate.backButton })
            {
                var button = UnityEngine.Object.Instantiate(source, preserve.transform);
                button.gameObject.SetActive(true);
                AdventureSaveSlotsView.ConfigureTextHover(button);
                var hover = button.GetComponent<DialogueChoiceTextHover>();
                var label = button.GetComponentsInChildren<TMP_Text>(true).First(text => text.gameObject.activeSelf);
                hover.OnPointerEnter(null); Check(label.color.r > .99f, "Hovered save text must brighten under Title.");
                hover.OnPointerExit(null); Check(Mathf.Abs(label.color.r - .5f) < .01f, "Unselected save text must dim.");
                hover.OnSelect(null); Check(label.color.r > .99f, "Selected save text must brighten.");
                hover.OnDeselect(null); Check(Mathf.Abs(label.color.r - .5f) < .01f, "Deselected save text must dim.");
                Check(button.GetComponent<SmoothButtonHoverScale>() == null, "Save controls must not scale.");
            }
            var manager = helper.AddComponent<CombatManager>();
            CombatManager.Instance = manager;
            var tutorialCanvas = root.GetComponent<Canvas>();
            var tutorialType = typeof(CombatManager).Assembly.GetType("GuidedCombatTutorial");
            var uiHost = helper.AddComponent<AdvancedUIManager>();
            uiHost.actionMenuPanel = root.Component<RectTransform>("Menu/Party/Overview");
            object tutorial = tutorialType.GetMethod("Create").Invoke(null, new object[] { uiHost });
            Set(manager, "guidedTutorial", tutorial);
            Check(manager.IsRunAwayLockedByTutorial, "Tutorial must lock Run Away even between lesson pages.");
            var runButton = root.Component<Button>("Menu/Party/Overview/Inventory");
            Set(uiHost, "runAwayButton", runButton);
            uiHost.RefreshRunAwayTutorialLock();
            Check(!runButton.interactable, "Run Away must be disabled visually during the tutorial.");
            manager.OnRunAwayClicked();
            Set(manager, "guidedTutorial", null);
            uiHost.RefreshRunAwayTutorialLock();
            Check(runButton.interactable, "Ordinary encounters must keep Run Away available.");
            var view = tutorialCanvas.GetComponentInChildren<GuidedCombatTutorialView>(true);
            var page = (RectTransform)typeof(GuidedCombatTutorialView).GetField("page", Private).GetValue(view);
            Color authoredColor = page.GetComponent<Image>().color;
            GameUITheme.Apply(canvas);
            Check(page.GetComponent<Image>().color == authoredColor, "Theme must preserve the authored tutorial style.");
            view.automaticLessonPlacement = false; page.anchoredPosition = new Vector2(51f, 23f);
            Call(view, "UpdateLayout");
            Check(page.anchoredPosition == new Vector2(51f, 23f), "Manual tutorial layout must remain unchanged.");
            Check(GuidedCombatTutorialView.Create(tutorialCanvas, null) == view, "Tutorial view must be reused.");
            RenderPreviews(canvas, view);
        }
        finally
        {
            languageField.SetValue(null, originalLanguage);
            sessionField.SetValue(null, originalSession);
            CombatManager.Instance = originalCombat;
            EditorSceneManager.ClosePreviewScene(preview);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            if (quest != null) UnityEngine.Object.DestroyImmediate(quest);
        }
    }

    static void RenderPreviews(GameObject canvasObject, GuidedCombatTutorialView tutorial)
    {
        Scene scene = canvasObject.scene;
        var cameraObject = new GameObject("Authored UI Preview Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true; camera.orthographicSize = 540;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .14f, .17f);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
        foreach (Transform child in canvasObject.transform) child.gameObject.SetActive(false);
        var menu = canvasObject.transform.Find("Menu"); menu.gameObject.SetActive(true);
        foreach (Transform child in menu) child.gameObject.SetActive(child.name == "Tasks" || child.name == "Background");
        var questDialog = menu.Find("Quest Track Confirmation"); questDialog.gameObject.SetActive(false);
        var target = new RenderTexture(1920, 1080, 24); target.Create(); camera.targetTexture = target;
        try
        {
            Capture("QuestAuthoredCanvas.png");
            menu.gameObject.SetActive(false);
            tutorial.SetAuthoringPreview(); tutorial.automaticLessonPlacement = false;
            tutorial.gameObject.SetActive(true);
            Capture("GuidedAuthoredCanvas.png");
        }
        finally { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); }

        void Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvasObject.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render();
            if (name == "QuestAuthoredCanvas.png")
            {
                var content = menu.Find("Tasks/Quest List/Quest Scroll/Content");
                var row = content.Cast<Transform>().First(child => child.gameObject.activeSelf).GetComponent<RectTransform>();
                Check(row.rect.width > 100f && row.rect.height > 1f, "Quest rows must occupy the scroll width.");
            }
            var old = RenderTexture.active;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                File.WriteAllBytes("Library/CombatValidation/" + name, image.EncodeToPNG());
            }
            finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image); }
        }
    }

    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
