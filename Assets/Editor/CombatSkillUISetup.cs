using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Explicit Canvas authoring and isolated UI checks. Never enters combat or uses player saves.
public static class CombatSkillUISetup
{
    const string ScenePath = "Assets/Scenes/combattest.unity";
    const string ResultPath = "Library/CombatValidation/CombatSkillUISetup.result.txt";
    const string PreviewPath = "Tools/CanvasUI/SkillListPreview.png";
    const string RequestPath = "Library/CombatValidation/CombatSkillUISetup.request";
    const string ValidationRequestPath = "Library/CombatValidation/CombatSkillUIValidate.request";
    const string MigrationRequestPath = "Library/CombatValidation/CombatSkillUIPulseMigration.request";

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(RequestPath)) EditorApplication.delayCall += RunRequested;
        if (File.Exists(ValidationRequestPath)) EditorApplication.delayCall += ValidateRequested;
        if (File.Exists(MigrationRequestPath)) EditorApplication.delayCall += MigrateRequested;
    }

    static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        if (!File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        Setup();
    }

    [MenuItem("Tools/Adventure/UI/Setup Combat Skill List")]
    public static void Setup() => Run(false);
    public static void SetupBatch() => Run(true);

    static void MigrateRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += MigrateRequested; return; }
        if (!File.Exists(MigrationRequestPath)) return;
        File.Delete(MigrationRequestPath);
        MigratePulseVisibility();
    }

    [MenuItem("Tools/Adventure/UI/Update Skill Pulse and Visibility")]
    public static void MigratePulseVisibility()
    {
        Scene scene = default;
        bool opened = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before updating the skill UI.");
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save the open combattest scene before updating; unsaved changes will not be overwritten.");
            string original = File.ReadAllText(ScenePath);
            string backup = "Tools/CanvasUI/BeforeSkillPulse-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, backup + "/combattest.unity");
            File.Copy(ScenePath + ".meta", backup + "/combattest.unity.meta");
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
            var manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            var view = manager.skillListUI != null ? manager.skillListUI : manager.subMenuPanel.GetComponent<CombatSkillListUI>();
            Check(view != null && view.presentationRoot != null && view.rowTemplate != null, "Set up the skill Canvas before updating its pulse.");
            var poses = view.presentationRoot.GetComponentsInChildren<RectTransform>(true).ToDictionary(rect => rect, rect => new RectPose(rect));
            // Pixel bob amplitudes must not become scale multipliers. Only the
            // old saved schema receives the new percent-based defaults.
            if (original.Contains("backgroundBobAmplitude:") || original.Contains("numberBobAmplitude:"))
                foreach (var row in view.presentationRoot.GetComponentsInChildren<CombatSkillRowUI>(true))
                {
                    row.backgroundScaleAmount = .035f;
                    row.numberScaleAmount = .10f;
                    row.pulseCyclesPerSecond = .9f;
                }
            view.SetVisible(false);
            foreach (var pose in poses) Check(pose.Value.Same(pose.Key), "Pulse update must preserve authored skill RectTransforms.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save skill UI defaults.");
            ValidateAndRender(manager);
            File.WriteAllText(ResultPath, "PASS: skill background/number scale pulse; fixed positions; description hidden outside the skill menu; isolated input/item/render checks. Backup: " + backup + ". No live combat or saves used.");
            Debug.Log(File.ReadAllText(ResultPath));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally { if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    static void ValidateRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += ValidateRequested; return; }
        if (!File.Exists(ValidationRequestPath)) return;
        File.Delete(ValidationRequestPath);
        ValidateOnly();
    }

    [MenuItem("Tools/Adventure/UI/Validate Combat Skill List")]
    public static void ValidateOnly()
    {
        Scene scene = default;
        bool opened = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validating the skill list.");
            string sourceHash = Hash(ScenePath);
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
            var manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            Check(manager.skillListUI != null, "Set up the skill Canvas before running validation.");
            ValidateAndRender(manager);
            Check(sourceHash == Hash(ScenePath), "Validation must not write the combat scene.");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "PASS: validation-only; four skills plus one ultimate; pointer/navigation descriptions; single focused banner; only banner/number scale pulse; fixed positions/icon/name/resource word/hit area; description visibility lifecycle; item presentation preserved; nonblank isolated preview: " + PreviewPath + ". No source scene, combat or live saves changed.");
            Debug.Log(File.ReadAllText(ResultPath));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally { if (opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    static void Run(bool batch)
    {
        Scene scene = default;
        bool opened = false;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before setting up the skill list.");
            scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save the open combattest scene before setup; unsaved changes will not be overwritten.");
            string backup = "Tools/CanvasUI/BeforeSkillList-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            File.Copy(ScenePath, backup + "/combattest.unity");
            File.Copy(ScenePath + ".meta", backup + "/combattest.unity.meta");
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }
            var manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AdvancedUIManager>(true)).Single();
            string itemPrefabPath = AssetDatabase.GetAssetPath(manager.actionButtonPrefab);
            string itemPrefabHash = Hash(itemPrefabPath);
            var panelRect = (RectTransform)manager.subMenuPanel.transform;
            var parentPose = new RectPose(panelRect);
            var view = manager.subMenuPanel.GetComponent<CombatSkillListUI>();
            if (view == null) view = manager.subMenuPanel.AddComponent<CombatSkillListUI>();
            manager.skillListUI = view;
            view.EnsureHierarchy(manager);
            Check(parentPose.Same(panelRect), "Setup must preserve the authored SubMenuPanel size and position.");
            var authored = view.presentationRoot.GetComponentsInChildren<RectTransform>(true)
                .ToDictionary(rect => rect, rect => new RectPose(rect));
            view.EnsureHierarchy(manager);
            foreach (var pair in authored) Check(pair.Value.Same(pair.Key), "Repeat setup changed an authored skill UI RectTransform: " + pair.Key.name);
            Check(view.rowTemplate != null && view.rowsRoot != null && view.descriptionText != null, "Skill UI references must be authored on the Canvas.");
            view.rowTemplate.gameObject.SetActive(false);
            view.SetVisible(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the combat skill Canvas.");
            ValidateAndRender(manager);
            Check(itemPrefabHash == Hash(itemPrefabPath), "The shared item/button prefab must remain unchanged.");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "PASS: authored skill Canvas hidden by default; repeat setup preserves rects; four normal skills plus one ultimate; shared description on pointer/navigation; single focused banner; only banner/number scale pulse; no persistent mouse hover; item presentation preserved. Isolated preview: " + PreviewPath + ". Backup: " + backup + ". No combat or live saves used.");
            Debug.Log(File.ReadAllText(ResultPath));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "FAIL: " + exception);
            Debug.LogException(exception);
            if (batch) throw;
        }
        finally
        {
            if (batch && opened && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void ValidateAndRender(AdvancedUIManager source)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        var actions = new List<ActionData>();
        var oldEventSystem = EventSystem.current;
        EventSystem events = null;
        bool registeredEvents = false;
        Camera camera = null;
        RenderTexture renderTexture = null;
        Texture2D image = null;
        try
        {
            var holder = new GameObject("Isolated Skill UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(AdvancedUIManager));
            SceneManager.MoveGameObjectToScene(holder, preview);
            holder.layer = 5;
            var canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            var scaler = holder.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            var manager = holder.GetComponent<AdvancedUIManager>();
            manager.enabled = false;
            manager.partyHUDList = new List<PartyHUDUnit>();
            var panel = UnityEngine.Object.Instantiate(source.subMenuPanel, holder.transform, false);
            panel.name = "Skill List Preview";
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.localRotation = Quaternion.identity;
            panelRect.localScale = Vector3.one;
            panelRect.sizeDelta = ((RectTransform)source.subMenuPanel.transform).rect.size;
            manager.subMenuPanel = panel;
            manager.subMenuContent = panel.transform.Find(RelativePath(source.subMenuContent, source.subMenuPanel.transform));
            manager.actionButtonPrefab = source.actionButtonPrefab;
            manager.skillListUI = panel.GetComponent<CombatSkillListUI>();
            var view = manager.skillListUI;
            view.EnsureHierarchy(manager);
            // Ordinary MonoBehaviour lifecycle callbacks do not run in Edit
            // Mode, so sample the runtime startup contract explicitly.
            view.presentationRoot.SetActive(true);
            InvokeLifecycle(view, "Awake");
            Check(!view.presentationRoot.activeSelf, "Runtime initialization must hide the authored skill presentation.");
            manager.HideSubMenu();
            Check(!panel.activeSelf && !view.presentationRoot.activeSelf, "Initial action UI must not show the description panel.");
            panel.SetActive(true);
            Check(!view.presentationRoot.activeSelf, "Merely enabling the submenu must not open skills.");
            var eventsObject = new GameObject("Isolated UI EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(eventsObject, preview);
            events = eventsObject.GetComponent<EventSystem>();
            // EventSystem is not ExecuteAlways; its normal OnEnable is not
            // guaranteed to register it when created by an Edit Mode check.
            if (!RegisteredEvents().Contains(events))
            {
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
                registeredEvents = true;
            }
            EventSystem.current = events;
            Check(EventSystem.current == events, "The isolated EventSystem must be registered for real navigation checks.");

            Sprite icon = AssetDatabase.FindAssets("t:ActionData", new[] { "Assets/Adventure/Prefabs/player" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ActionData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(action => action != null && action.icon != null).Select(action => action.icon).FirstOrDefault();
            string[] names = { "Liên trảm", "Hỏa cầu", "Băng kích", "Phục hồi", "Tuyệt kỹ" };
            for (int i = 0; i < 6; i++)
            {
                var action = ScriptableObject.CreateInstance<ActionData>();
                action.name = "Skill UI Check " + i;
                action.type = ActionData.ActionType.Skill;
                action.actionName = names[Mathf.Min(i, 4)];
                action.description = i == 0 ? "Chém liên tiếp hai lần vào một mục tiêu. Mỗi nhát chém gây sát thương vật lý." : "Mô tả riêng của kỹ năng " + i + ".";
                action.stainChange = -i - 1;
                action.isUltimate = i == 4;
                action.icon = icon;
                actions.Add(action);
            }
            manager.PopulateSkillSubMenu(new List<ActionData> { actions[0], actions[4], actions[1], null, actions[2], actions[3], actions[5] });
            var rows = ActiveRows(view);
            Check(rows.Length == 5, "Skills must remain fixed at four normal slots and one ultimate.");
            var expected = new[] { actions[0], actions[1], actions[2], actions[3], actions[4] };
            for (int i = 0; i < rows.Length; i++) Check(rows[i].GetComponent<ActionButtonUI>().CurrentAction == expected[i], "Skill/ultimate slot mapping is incorrect.");
            Check(!manager.subMenuContent.gameObject.activeSelf && !panel.GetComponent<ScrollRect>().enabled, "Skill list must not be draggable as a ScrollRect.");
            Check(rows.All(row => row.GetComponent<ActionButtonUI>().infoText == null || !row.GetComponent<ActionButtonUI>().infoText.gameObject.activeSelf), "Row descriptions must move to the shared panel.");

            var pointer = new PointerEventData(events);
            ExecuteEvents.Execute(rows[1].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Check(rows[1].IsFocused && !rows[0].IsFocused, "Pointer hover must focus exactly one row.");
            Check(view.descriptionText.text == actions[1].description, "Pointer hover must update the shared description.");
            ExecuteEvents.Execute(rows[1].gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Check(!rows[1].IsFocused, "Pointer exit must clear mouse-only focus.");
            var nav = new AxisEventData(events) { moveDir = MoveDirection.Down };
            events.SetSelectedGameObject(rows[0].gameObject, nav);
            ExecuteEvents.Execute(rows[0].gameObject, nav, ExecuteEvents.selectHandler);
            Check(rows[0].IsFocused && view.descriptionText.text == actions[0].description, "Navigation selection must update the shared description.");
            for (int i = 0; i < 20; i++) view.Focus(rows[i % rows.Length]);
            Check(rows.Count(row => row.IsFocused) == 1, "Rapid focus changes must not leave multiple backgrounds active.");
            view.Focus(rows[0]);
            ValidateMotion(rows[0]);
            view.Focus(rows[2]);
            Check(!rows[0].IsFocused && rows[2].IsFocused, "Focus transfer must clear the old row.");
            view.ClearFocus();
            Check(rows.All(row => !row.IsFocused), "ClearFocus must reset all rows.");
            var normal = rows[0].GetComponent<ActionButtonUI>();
            rows[0].SetResource("3", "Tinh lực");
            Check(normal.resourceText.text == "3" && rows[0].resourceLabel.text.Length > 0, "Number and stationary resource word must be separate labels.");

            // The shared prefab is still used by items without the skill-only presenter.
            var item = ScriptableObject.CreateInstance<ActionData>(); actions.Add(item);
            item.type = ActionData.ActionType.Item; item.actionName = "Potion UI Check"; item.description = "Item description remains in the row.";
            var itemObject = UnityEngine.Object.Instantiate(source.actionButtonPrefab, holder.transform, false);
            var itemUI = itemObject.GetComponent<ActionButtonUI>(); itemUI.Setup(item);
            Check(itemObject.GetComponent<CombatSkillRowUI>() == null && itemUI.infoText.gameObject.activeSelf && itemUI.resourceText.text.StartsWith("x"), "Item quantity/description presentation must remain intact.");
            UnityEngine.Object.DestroyImmediate(itemObject);

            manager.PopulateSubMenu(new List<ActionData> { item });
            Check(!view.presentationRoot.activeSelf && manager.subMenuContent.gameObject.activeSelf,
                "Opening Items must hide the skill description panel.");
            manager.PopulateSkillSubMenu(actions);
            manager.ShowActionMenu(true);
            Check(!panel.activeSelf && !view.presentationRoot.activeSelf,
                "Returning to the action menu must close the skill description.");
            manager.PopulateSkillSubMenu(actions);
            manager.ToggleAllUI(false);
            Check(!panel.activeSelf && !view.presentationRoot.activeSelf,
                "Hiding battle UI must close the skill description.");
            manager.PopulateSkillSubMenu(actions);
            panel.SetActive(false);
            InvokeLifecycle(view, "OnDisable");
            panel.SetActive(true);
            Check(!view.presentationRoot.activeSelf,
                "Closing and externally reopening the submenu must not revive the old description.");
            manager.PopulateSkillSubMenu(actions);
            rows = ActiveRows(view);
            view.Focus(rows[0]);
            rows[0].ApplyMotion(.25f);
            var cameraObject = new GameObject("Skill UI Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            camera = cameraObject.GetComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .05f, 1f);
            camera.cullingMask = 1 << 5;
            camera.orthographic = true;
            camera.orthographicSize = 500f;
            camera.transform.position = new Vector3(0f, 0f, -20f);
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            renderTexture = new RenderTexture(900, 1000, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject.activeInHierarchy) graphic.SetAllDirty();
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var oldTexture = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                image = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
                File.WriteAllBytes(PreviewPath, image.EncodeToPNG());
                var pixels = image.GetPixels32();
                Color32 backdrop = pixels[0];
                int contentPixels = 0, brightPixels = 0;
                foreach (var pixel in pixels)
                {
                    int difference = Math.Abs(pixel.r - backdrop.r) + Math.Abs(pixel.g - backdrop.g) + Math.Abs(pixel.b - backdrop.b);
                    if (difference > 24) contentPixels++;
                    if (difference > 60 && pixel.r > 170 && pixel.g > 170 && pixel.b > 170) brightPixels++;
                }
                Check(contentPixels > 1000 && brightPixels > 100,
                    "The skill preview rendered blank or without visible text. Content pixels=" + contentPixels + ", bright pixels=" + brightPixels);
            }
            finally { RenderTexture.active = oldTexture; }
        }
        finally
        {
            if (events != null && registeredEvents)
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
            if (oldEventSystem != null && RegisteredEvents().Contains(oldEventSystem)) EventSystem.current = oldEventSystem;
            if (camera != null) camera.targetTexture = null;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (renderTexture != null) { renderTexture.Release(); UnityEngine.Object.DestroyImmediate(renderTexture); }
            EditorSceneManager.ClosePreviewScene(preview);
            foreach (var action in actions) if (action != null) UnityEngine.Object.DestroyImmediate(action);
        }
    }

    static CombatSkillRowUI[] ActiveRows(CombatSkillListUI view) => view.rowsRoot.GetComponentsInChildren<CombatSkillRowUI>(true)
        .Where(row => row.gameObject.activeSelf && row.gameObject != view.rowTemplate.gameObject).OrderBy(row => row.transform.GetSiblingIndex()).ToArray();

    static void ValidateMotion(CombatSkillRowUI row)
    {
        var source = row.GetComponent<ActionButtonUI>();
        var rowPose = new RectPose((RectTransform)row.transform);
        var namePose = new RectPose(source.nameText.rectTransform);
        var iconPose = new RectPose(source.iconImage.rectTransform);
        var wordPose = new RectPose(row.resourceLabel.rectTransform);
        row.ApplyMotion(0f);
        Vector2 numberPosition = source.resourceText.rectTransform.anchoredPosition;
        Vector2 bannerPosition = row.focusBackground.rectTransform.anchoredPosition;
        Vector3 baseNumberScale = source.resourceText.rectTransform.localScale;
        Vector3 baseBannerScale = row.focusBackground.rectTransform.localScale;
        row.ApplyMotion(.25f);
        Vector3 firstNumber = source.resourceText.rectTransform.localScale;
        Vector3 firstBanner = row.focusBackground.rectTransform.localScale;
        row.ApplyMotion(.75f);
        Check((firstNumber - source.resourceText.rectTransform.localScale).sqrMagnitude > .00001f, "Resource number must scale while focused.");
        Check((firstBanner - row.focusBackground.rectTransform.localScale).sqrMagnitude > .00001f, "Focused background must scale.");
        Check(numberPosition == source.resourceText.rectTransform.anchoredPosition && bannerPosition == row.focusBackground.rectTransform.anchoredPosition,
            "The resource number and background must keep their positions while scaling.");
        Check(rowPose.Same((RectTransform)row.transform) && namePose.Same(source.nameText.rectTransform) && iconPose.Same(source.iconImage.rectTransform) && wordPose.Same(row.resourceLabel.rectTransform),
            "Icon, name, resource word and button hit rectangle must stay still.");
        Check(!row.focusBackground.raycastTarget && !source.resourceText.raycastTarget && !row.resourceLabel.raycastTarget, "Animated visuals must not capture pointer input.");
        row.SetFocused(false);
        Check(source.resourceText.rectTransform.localScale == baseNumberScale && row.focusBackground.rectTransform.localScale == baseBannerScale,
            "Removing focus must restore the authored number and background scales.");
        row.SetFocused(true);
    }

    static string RelativePath(Transform child, Transform root)
    {
        var segments = new List<string>();
        for (var current = child; current != null && current != root; current = current.parent) segments.Insert(0, current.name);
        return string.Join("/", segments);
    }
    static List<EventSystem> RegisteredEvents() => (List<EventSystem>)typeof(EventSystem)
        .GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    static void InvokeLifecycle(CombatSkillListUI view, string method) => typeof(CombatSkillListUI)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)); }

    readonly struct RectPose
    {
        readonly Vector2 position, size, min, max, pivot;
        readonly Vector3 scale;
        readonly Quaternion rotation;
        public RectPose(RectTransform rect) { position = rect.anchoredPosition; size = rect.sizeDelta; min = rect.anchorMin; max = rect.anchorMax; pivot = rect.pivot; scale = rect.localScale; rotation = rect.localRotation; }
        public bool Same(RectTransform rect) => position == rect.anchoredPosition && size == rect.sizeDelta && min == rect.anchorMin && max == rect.anchorMax && pivot == rect.pivot && scale == rect.localScale && rotation == rect.localRotation;
    }
}
