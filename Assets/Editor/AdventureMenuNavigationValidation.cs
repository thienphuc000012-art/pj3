using System;
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

// Builds an isolated UI fixture. Never opens a campaign or writes player saves.
public static class AdventureMenuNavigationValidation
{
    const string Request = "Library/CombatValidation/MenuNavigation.request";
    const string Result = "Library/CombatValidation/MenuNavigation.result.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += Requested;
    }

    static void Requested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Requested; return; }
        File.Delete(Request);
        Validate();
    }

    [MenuItem("Tools/Adventure/UI/Validate Quest Navigation")]
    public static void Validate()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var instance = typeof(CampaignSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var originalSession = CampaignSession.Instance;
        float timeScale = Time.timeScale;
        CampaignConfig config = null;
        AdventureQuest quest = null;
        Camera camera = null;
        RenderTexture render = null;
        try
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run UI validation in Edit Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab");
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var root = canvas.GetComponent<AdventureCanvasRoot>();
            var menu = root.transform.Find("Menu");
            menu.gameObject.SetActive(true);
            foreach (Transform child in menu) child.gameObject.SetActive(false);
            root.transform.Find("HUD").gameObject.SetActive(false);
            root.transform.Find("Toast").gameObject.SetActive(false);
            var authoredReturn = menu.Find("Party/Overview/ReturnToTitle");
            Check(authoredReturn != null && authoredReturn.GetComponent<Button>() != null, "ReturnToTitle must be authored in Party.");

            var holder = new GameObject("Isolated Quest Menu Controller");
            SceneManager.MoveGameObjectToScene(holder, preview);
            var session = holder.AddComponent<CampaignSession>();
            var controller = holder.AddComponent<AdventureUI>();
            config = ScriptableObject.CreateInstance<CampaignConfig>();
            quest = ScriptableObject.CreateInstance<AdventureQuest>();
            quest.questId = "navigation-test";
            quest.displayName = "Nhiệm vụ kiểm tra";
            quest.useFixedObjectivePosition = true;
            quest.objectivePosition = new Vector3(140f, 0f, 130f);
            config.quests.Add(quest);
            var data = new CampaignSave { trackedQuestId = quest.Id };
            data.quests.Add(new AdventureQuestProgress { questId = quest.Id });
            SetBacking(session, "Config", config);
            SetBacking(session, "Data", data);
            SetBacking(session, "Menu", AdventureMenu.Tasks);
            instance.SetValue(null, session);
            Set(controller, "view", root);
            Set(controller, "travelButtonTemplate", root.Component<Button>("Menu/Rest/Save"));
            Call(controller, "BuildQuestUI");
            Call(controller, "BuildReturnToTitleUI");
            Check(menu.Find("Party/Overview").GetComponentsInChildren<Button>(true).Count(b => b.name == "ReturnToTitle") == 1,
                "Binding must preserve a single authored ReturnToTitle button.");
            SetBacking(session, "Menu", AdventureMenu.Main);
            authoredReturn.GetComponent<Button>().onClick.Invoke();
            var titleModal = menu.Find("Return To Title Confirmation");
            Check(titleModal.gameObject.activeSelf && CampaignSession.Instance == session,
                "ReturnToTitle must open confirmation before ending the session.");
            Check(!menu.Find("Party/Overview").GetComponent<CanvasGroup>().interactable,
                "Return confirmation must block menu commands underneath.");
            var noHover = titleModal.Find("Confirmation Panel/No").GetComponent<DialogueChoiceTextHover>();
            Check(noHover != null, "Return No must have text feedback.");
            noHover.OnSelect(new BaseEventData(EventSystem.current));
            Check(noHover.TextColor.r == .5f, "Automatically focusing No must not look like mouse hover.");
            noHover.OnPointerEnter(new PointerEventData(EventSystem.current));
            Check(noHover.TextColor == Color.white, "Hovering No must brighten its text.");
            noHover.OnPointerExit(new PointerEventData(EventSystem.current));
            Check(noHover.TextColor.r == .5f, "No must dim after pointer exit even if still selected.");
            noHover.OnSelect(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Left });
            Check(noHover.TextColor == Color.white, "Actual keyboard/gamepad navigation must retain visible focus.");
            noHover.OnPointerEnter(new PointerEventData(EventSystem.current));
            noHover.OnPointerExit(new PointerEventData(EventSystem.current));
            Check(noHover.TextColor.r == .5f, "Returning to mouse hover must clear navigation glow on exit.");
            titleModal.Find("Confirmation Panel/No").GetComponent<Button>().onClick.Invoke();
            Check(!titleModal.gameObject.activeSelf && menu.Find("Party/Overview").GetComponent<CanvasGroup>().interactable,
                "No must restore the menu.");
            authoredReturn.GetComponent<Button>().onClick.Invoke();
            Check(controller.HandleBack() && !titleModal.gameObject.activeSelf && CampaignSession.Instance == session,
                "Back must dismiss return confirmation.");
            SetBacking(session, "Menu", AdventureMenu.Tasks);
            Call(controller, "RefreshQuestList");
            var row = menu.Find("Tasks/Quest List/Quest Scroll/Content/Quest " + quest.Id);
            Check(Mathf.Abs(row.GetComponent<Image>().color.a - 100f / 255f) < .001f, "Normal quest alpha must be 100/255.");
            row.GetComponent<QuestButtonHighlight>().OnPointerEnter(new PointerEventData(EventSystem.current));
            Check(Mathf.Abs(row.GetComponent<Image>().color.a - 100f / 255f) < .001f, "Hovered quest alpha must be 100/255.");

            row.GetComponent<Button>().onClick.Invoke();
            var modal = menu.Find("Quest Track Confirmation");
            var prompt = modal.Find("Panel/Prompt").GetComponent<TMP_Text>();
            Check(modal.gameObject.activeSelf && data.trackedQuestId == quest.Id,
                "Clicking the tracked quest must open confirmation without immediately untracking.");
            Check(prompt.text.Contains(GameLocalization.Translate("Hủy theo dõi nhiệm vụ?")), "Untracking needs its own question.");
            Check(!menu.Find("Tasks").GetComponent<CanvasGroup>().interactable, "The modal must block the quest list.");
            modal.Find("Panel/No").GetComponent<Button>().onClick.Invoke();
            Check(!modal.gameObject.activeSelf && data.trackedQuestId == quest.Id &&
                menu.Find("Tasks").GetComponent<CanvasGroup>().interactable, "No must preserve tracking and restore the list.");

            data.trackedQuestId = string.Empty;
            row.GetComponent<Button>().onClick.Invoke();
            Check(prompt.text.Contains(GameLocalization.Translate("Theo dõi nhiệm vụ?")), "An untracked quest must request tracking.");
            // If tracking changed while the modal was open, Yes must never toggle it off.
            data.trackedQuestId = quest.Id;
            modal.Find("Panel/Yes").GetComponent<Button>().onClick.Invoke();
            Check(data.trackedQuestId == quest.Id && !modal.gameObject.activeSelf, "Stale confirmation must not invert current tracking.");

            foreach (Transform child in menu) child.gameObject.SetActive(false);
            root.transform.Find("HUD").gameObject.SetActive(true);
            var compass = root.GetComponent<AdventureCompassHUD>() ?? root.gameObject.AddComponent<AdventureCompassHUD>();
            Call(compass, "Awake");
            var strip = root.transform.Find("HUD/Horizontal Compass HUD");
            var positionMark = typeof(AdventureCompassHUD).GetMethod("Position", Private);
            foreach (var tick in (System.Collections.IEnumerable)typeof(AdventureCompassHUD).GetField("ticks", Private).GetValue(compass))
                positionMark.Invoke(compass, new[] { tick, 0f, 5f });
            foreach (var direction in (System.Collections.IEnumerable)typeof(AdventureCompassHUD).GetField("directions", Private).GetValue(compass))
                positionMark.Invoke(compass, new[] { direction, 0f, 15f });
            var marker = strip.Find("Tracked Quest Marker").GetComponent<RectTransform>();
            var distance = strip.Find("Tracked Quest Distance").GetComponent<RectTransform>();
            Check(marker.childCount == 10 && marker.GetComponent<TMP_Text>() == null, "Quest marker needs a drawn hexagon and inner diamond.");
            Check(distance.anchoredPosition.y > marker.anchoredPosition.y, "Distance must be above the marker.");
            Check(strip.Find("Compass Line Left") != null && strip.Find("Compass Line Right") != null, "Compass needs its horizontal guide.");
            marker.anchoredPosition = new Vector2(-240f, 0f);
            distance.anchoredPosition = new Vector2(-240f, 30f);
            marker.gameObject.SetActive(true);
            distance.gameObject.SetActive(true);
            distance.GetComponent<TMP_Text>().text = "191 m";

            var cameraGo = new GameObject("Quest Compass Validation Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGo, preview);
            camera = cameraGo.GetComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 540f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f, .12f, .14f);
            var view = root.GetComponent<Canvas>();
            view.renderMode = RenderMode.ScreenSpaceCamera;
            view.worldCamera = camera;
            view.planeDistance = 1f;
            render = new RenderTexture(1920, 1080, 24);
            render.Create();
            camera.targetTexture = render;
            Canvas.ForceUpdateCanvases();
            foreach (var text in strip.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previousRender = RenderTexture.active;
            var image = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                image.Apply();
                File.WriteAllBytes("Library/CombatValidation/QuestCompass.png", image.EncodeToPNG());
            }
            finally { RenderTexture.active = previousRender; UnityEngine.Object.DestroyImmediate(image); }
            File.WriteAllText(Result, "PASS: ReturnToTitle confirmation, No and Back; No is dim on automatic focus and pointer exit, bright on hover or actual keyboard/gamepad navigation; quest alpha 100 normal/hover; untrack modal and No; stale confirmation; hexagon marker and distance above compass line. No player save files written.");
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception exception)
        {
            File.WriteAllText(Result, "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            instance.SetValue(null, originalSession);
            Time.timeScale = timeScale;
            if (camera != null) camera.targetTexture = null;
            if (render != null) { render.Release(); UnityEngine.Object.DestroyImmediate(render); }
            EditorSceneManager.ClosePreviewScene(preview);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            if (quest != null) UnityEngine.Object.DestroyImmediate(quest);
        }
    }

    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static void SetBacking(CampaignSession target, string property, object value) => Set(target, "<" + property + ">k__BackingField", value);
    static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
