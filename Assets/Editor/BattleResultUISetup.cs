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

/// <summary>Authors the existing result prefab. Preview validation never starts gameplay or writes a campaign save.</summary>
public static class BattleResultUISetup
{
    const string Prefab = "Assets/Resources/Adventure/UI/BattleResultCanvas.prefab";
    const string Table = "Assets/Resources/Adventure/LocalizationTable.asset";
    const string MaterialPath = "Assets/Resources/Adventure/UI/BattleDefeatBackdrop.mat";
    const string Request = "Library/CombatValidation/BattleResultUI.request";
    const string Output = "Tools/BattleResults";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/BattleResultUISetup.cs")) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if (command == "setup") Setup(); else Validate(); }
        catch (Exception ex) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", ex.ToString()); Debug.LogException(ex); }
    }
    [MenuItem("Tools/Adventure/UI/Setup Battle Result Presentation")]
    public static void Setup()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before editing the result prefab.");
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        File.Copy(Prefab, backup + "/BattleResultCanvas.prefab"); File.Copy(Table, backup + "/LocalizationTable.asset");
        if (File.Exists(MaterialPath)) File.Copy(MaterialPath, backup + "/BattleDefeatBackdrop.mat");
        var obj = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var view = obj.GetComponent<AdventureCanvasRoot>();
            var presentation = obj.GetComponent<BattleResultPresentation>() ?? obj.AddComponent<BattleResultPresentation>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab").transform.Find("Menu/Pattern");
            Check(source != null, "Menu/Pattern is missing.");
            var pattern = obj.transform.Find("Pattern");
            if (pattern == null) { pattern = UnityEngine.Object.Instantiate(source.gameObject, obj.transform).transform; pattern.name = "Pattern"; }
            pattern.SetSiblingIndex(1); pattern.gameObject.SetActive(true); Protect(pattern.gameObject);
            Stretch((RectTransform)pattern); presentation.diamondPattern = pattern.gameObject;
            var experience = view.Component<TMP_Text>("Experience");
            Protect(experience.gameObject); experience.color = presentation.levelUpColor;
            experience.text = "0 KN"; experience.alignment = TextAlignmentOptions.Right;
            experience.enableAutoSizing = true; experience.fontSizeMin = 34; experience.fontSizeMax = 64;

            var template = obj.transform.Find("Members/Viewport/Content/Template");
            var levelUp = template.Find("Level Up")?.GetComponent<TMP_Text>();
            if (levelUp == null) levelUp = Label("Level Up", template, view.defaultFont, 24f, presentation.levelUpColor);
            Protect(levelUp.gameObject);
            var badge = levelUp.rectTransform; badge.anchorMin = badge.anchorMax = badge.pivot = Vector2.one;
            badge.anchoredPosition = new Vector2(-24, -15); badge.sizeDelta = new Vector2(185, 38);
            levelUp.alignment = TextAlignmentOptions.Right; levelUp.text = "LÊN CẤP!"; levelUp.gameObject.SetActive(false);
            var name = template.Find("Name") as RectTransform;
            if (name != null) name.sizeDelta = new Vector2(290, name.sizeDelta.y);

            var defeat = obj.transform.Find("Defeat") as RectTransform;
            if (defeat == null) defeat = Rect("Defeat", obj.transform);
            Stretch(defeat); Protect(defeat.gameObject); presentation.defeatPanel = defeat.gameObject;
            var backdrop = defeat.Find("Backdrop")?.GetComponent<RawImage>();
            if (backdrop == null) backdrop = Rect("Backdrop", defeat).gameObject.AddComponent<RawImage>();
            Stretch(backdrop.rectTransform); backdrop.raycastTarget = false;
            var shader = Shader.Find("UI/Adventure/Battle Defeat Backdrop"); Check(shader != null, "Defeat background shader missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = shader; material.SetFloat("_BlurSize", 3f); EditorUtility.SetDirty(material);
            backdrop.material = material; presentation.defeatBackdrop = backdrop;
            var geometry = defeat.Find("Red Pattern")?.GetComponent<BattleDefeatPattern>();
            if (geometry == null) geometry = Rect("Red Pattern", defeat).gameObject.AddComponent<BattleDefeatPattern>();
            if (geometry.GetComponent<CanvasRenderer>() == null) geometry.gameObject.AddComponent<CanvasRenderer>();
            Stretch(geometry.rectTransform); geometry.color = new Color(.68f, .15f, .12f, .8f); geometry.raycastTarget = false;
            var title = defeat.Find("Title")?.GetComponent<TMP_Text>() ?? Label("Title", defeat, view.defaultFont, 94, new Color(.68f, .13f, .1f));
            Center(title.rectTransform, new Vector2(0, 30), new Vector2(1200, 270));
            title.text = "HÀNH TRÌNH\nTHẤT BẠI"; title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Normal; title.characterSpacing = 5f; title.lineSpacing = 6f;
            var back = defeat.Find("Back")?.GetComponent<Button>();
            if (back == null)
            {
                var buttonObj = UnityEngine.Object.Instantiate(obj.transform.Find("Continue").gameObject, defeat); buttonObj.name = "Back";
                back = buttonObj.GetComponent<Button>();
            }
            Center((RectTransform)back.transform, new Vector2(0, -350), new Vector2(330, 58));
            var buttonImage = back.GetComponent<Image>();
            buttonImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/asset/Ui/skillbackground.png");
            buttonImage.color = Color.white; buttonImage.type = Image.Type.Simple;
            foreach (var wipe in back.GetComponentsInChildren<UIButtonWipeReveal>(true)) UnityEngine.Object.DestroyImmediate(wipe);
            var goldLine = back.transform.Find("GoldLine"); if (goldLine != null) goldLine.gameObject.SetActive(false);
            var label = back.transform.Find("Label").GetComponent<TMP_Text>(); Stretch(label.rectTransform);
            label.text = "Quay về"; label.alignment = TextAlignmentOptions.Center; label.fontSize = 28; label.enableAutoSizing = false;
            var hover = back.GetComponent<DialogueChoiceTextHover>() ?? back.gameObject.AddComponent<DialogueChoiceTextHover>();
            hover.Configure(label, new Color(.6f, .6f, .6f), Color.white, true, true);
            back.transition = Selectable.Transition.None;
            back.onClick = new Button.ButtonClickedEvent();
            presentation.returnButton = back;
            presentation.victoryContent = obj.transform.Cast<Transform>()
                .Where(t => t != defeat && t != pattern && t.name != "Error").Select(t => t.gameObject).ToArray();
            defeat.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(obj, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(obj); }
        var table = AssetDatabase.LoadAssetAtPath<GameLocalizationTable>(Table);
        Translate(table, "battle.defeat", "HÀNH TRÌNH\nTHẤT BẠI", "EXPEDITION\nFAILED");
        Translate(table, "battle.return", "Quay về", "Return");
        Translate(table, "battle.levelup", "LÊN CẤP!", "LEVEL UP!");
        EditorUtility.SetDirty(table);
        typeof(GameLocalizationTable).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(table, null);
        AssetDatabase.SaveAssets();
        Validate();
    }
    static void Translate(GameLocalizationTable table, string key, string vi, string en)
    {
        var entry = table.entries.FirstOrDefault(e => e.vietnamese == vi);
        if (entry == null) { entry = new GameLocalizationEntry { key = key, vietnamese = vi }; table.entries.Add(entry); }
        entry.english = en;
    }
    static void Protect(GameObject obj) { if (obj.GetComponent<PreserveAuthoredUI>() == null) obj.AddComponent<PreserveAuthoredUI>(); }
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.fontSize = size; label.enableAutoSizing = false; label.color = color; label.raycastTarget = false; return label;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = Vector2.one * .5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    [MenuItem("Tools/Adventure/UI/Validate Battle Result Presentation")]
    public static void Validate()
    {
        var report = new StringBuilder(); Directory.CreateDirectory(Output);
        ValidateExperience(report);
        Scene preview = EditorSceneManager.NewPreviewScene();
        var languageField = typeof(GameLocalization).GetField("language", BindingFlags.Static | BindingFlags.NonPublic);
        GameLanguage oldLanguage = GameLocalization.CurrentLanguage;
        float previousTimeScale = Time.timeScale;
        Texture2D reference = null, combatFrame = null;
        try
        {
            var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            SceneManager.MoveGameObjectToScene(go, preview);
            var view = go.GetComponent<AdventureCanvasRoot>(); var presentation = go.GetComponent<BattleResultPresentation>();
            Check(presentation != null && presentation.defeatPanel != null && presentation.returnButton != null, "Missing authored result references.");
            Check(presentation.defeatPanel.GetComponentInChildren<BattleDefeatPattern>(true).GetComponent<CanvasRenderer>() != null,
                "Defeat geometry needs its own CanvasRenderer.");
            Check(!ShaderUtil.ShaderHasError(presentation.defeatBackdrop.material.shader), "Defeat shader failed to compile.");
            var sourcePattern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Adventure/UI/AdventureCanvas.prefab").transform.Find("Menu/Pattern");
            Check(presentation.diamondPattern.transform.childCount == sourcePattern.childCount, "Victory must reuse all Menu diamonds.");
            var cameraGo = new GameObject("Battle result preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraGo, preview);
            var camera = cameraGo.GetComponent<Camera>(); camera.enabled = false; camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.orthographic = true; camera.orthographicSize = 540; camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .08f, .075f);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.enabled = false;
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(1920, 1080);
            presentation.captureCombatBackground = false;
            var rows = view.Rows("Members/Viewport/Content", 2);
            var style = Resources.Load<PartyHUDStyle>("PartyHUDStyle");
            for (int i = 0; i < rows.Count; i++)
            {
                AdventureCanvasRoot.RowText(rows[i], "Name", i == 0 ? "Kakaka" : "Lauren");
                AdventureCanvasRoot.RowPortrait(rows[i], "Portrait", style.portraits[i].waiting);
            }
            var changes = new[] { new BattleExperienceChange(4, 225, 6, 245), new BattleExperienceChange(2, 140, 5, 110) };
            var rewards = new List<ItemStack> { new ItemStack("test", 12) };
            var loot = view.Rows("Loot/Viewport/Content", 1); AdventureCanvasRoot.RowText(loot[0], "Name", "Đá nâng cấp");
            view.Text("Description", ""); view.Active("Description", false);
            var totals = new[] { 14560, 651, 270, 3, 3, 95 };
            if (File.Exists(Output + "/ReferenceVideo.png"))
            {
                // Only a preview fixture extracted from the supplied video; never added to game assets.
                reference = new Texture2D(2, 2); reference.LoadImage(File.ReadAllBytes(Output + "/ReferenceVideo.png"));
                combatFrame = new Texture2D(640, 360); combatFrame.SetPixels(reference.GetPixels(0, 1080, 640, 360)); combatFrame.Apply();
            }
            foreach (GameLanguage language in new[] { GameLanguage.Vietnamese, GameLanguage.English })
            {
                languageField.SetValue(null, language); GameLocalization.BindHierarchy(go);
                presentation.Configure(true); Time.timeScale = 0;
                presentation.Begin(view, totals, 570, rows, changes, loot, rewards);
                Check(view.Component<TMP_Text>("Stats/Damage").text == "0", "Counters must begin at zero.");
                presentation.Sample(.5f);
                string halfway = view.Component<TMP_Text>("Stats/Damage").text;
                Check(halfway != "0" && halfway != totals[0].ToString("N0"), "Statistics must count progressively while timeScale is zero.");
                presentation.Sample(1.95f);
                Check(rows.All(row => row.transform.Find("Level Up").gameObject.activeSelf), "Level-up badge must appear after crossing a level.");
                Render(camera, canvas, Output + "/Win-LevelUp-" + language + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png");
                presentation.Complete(); Check(!presentation.IsAnimating, "Skipping must finish presentation immediately.");
                Check(view.Component<TMP_Text>("Experience").text == (language == GameLanguage.English ? "570 XP" : "570 KN"),
                    "Total XP must remain readable in both languages.");
                Check(view.Component<TMP_Text>("Stats/Damage").text == totals[0].ToString("N0") && view.Component<TMP_Text>("Stats/Time").text == "1:35", "Final statistics must match actual totals.");
                Check(rows[0].transform.Find("Progress").GetComponent<TMP_Text>().text.Contains("245 / 350"), "Final XP must equal the saved post-battle value.");
                Check(rows[0].transform.Find("Portrait").localScale == Vector3.one, "Level-up pulse must settle to its authored scale.");
                presentation.Configure(false); GameUITheme.Apply(go); GameLocalization.BindHierarchy(go);
                presentation.defeatBackdrop.texture = combatFrame;
                presentation.defeatBackdrop.color = Color.white;
                Check(!view.Node("Stats").gameObject.activeSelf && !view.Node("Members").gameObject.activeSelf && presentation.defeatPanel.activeSelf, "Defeat must hide victory statistics and show its own layout.");
                var title = presentation.defeatPanel.transform.Find("Title").GetComponent<TMP_Text>();
                Check(title.color.r > title.color.g * 2f, "Defeat title must keep its authored red through localization and theme changes.");
                Check(title.text.Contains(language == GameLanguage.English ? "FAILED" : "THẤT BẠI"), "Defeat title must follow the selected language.");
                Render(camera, canvas, Output + "/Lose-" + language + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".png");
            }
            int clicked = 0; presentation.returnButton.onClick.AddListener(() => clicked++); presentation.returnButton.onClick.Invoke();
            Check(clicked == 1 && presentation.returnButton.interactable, "Return button must be clickable.");
            report.AppendLine("PASS: native VI/EN win/lose Canvas previews; Menu diamond hierarchy reused; red defeat title/pattern survives theme and localization; return button clickable.");
            report.AppendLine("PASS: count-up at timeScale=0, staggered XP bars and level-up pulses, final XP/statistics/time, and skip-to-complete. No gameplay reward mutation or scene/save writes.");
            File.WriteAllText(Output + "/Validation.txt", report.ToString());
            Debug.Log("[Battle Result UI] " + report);
        }
        finally
        {
            languageField.SetValue(null, oldLanguage); Time.timeScale = previousTimeScale;
            EditorSceneManager.ClosePreviewScene(preview);
            if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
            if (combatFrame != null) UnityEngine.Object.DestroyImmediate(combatFrame);
        }
    }
    static void ValidateExperience(StringBuilder report)
    {
        foreach (var example in new[] { (1, 0, 0), (1, 20, 30), (1, 90, 20), (4, 225, 570), (99, 100, 20000), (100, 5040, 10000) })
        {
            var progress = new PartyMemberProgress { level = example.Item1, xp = example.Item2 };
            progress.GainXP(example.Item3);
            var change = new BattleExperienceChange(example.Item1, example.Item2, progress.level, progress.xp);
            int previousLevel = example.Item1, previousXP = example.Item2;
            for (float time = 0; time < change.Duration(1.8f, .2f) + 1f; time += .017f)
            {
                change.Sample(time, 1.8f, .2f, .7f, out int level, out int xp, out float fill, out float pulse);
                Check(level > previousLevel || level == previousLevel && xp >= previousXP, "Animated XP must never move backwards inside a level.");
                Check(fill >= 0 && fill <= 1.0001f && pulse >= 0 && pulse <= 1, "Animation values out of range.");
                previousLevel = level; previousXP = xp;
            }
            change.Sample(100f, 1.8f, .2f, .7f, out int finalLevel, out int finalXP, out _, out _);
            Check(finalLevel == progress.level && finalXP == progress.xp, "Animation must match authoritative PartyMemberProgress.GainXP, including level cap.");
        }
        var boundary = new BattleExperienceChange(1, 90, 2, 10);
        boundary.Sample(.95f, 1.8f, .2f, .7f, out int heldLevel, out int heldXP, out float heldFill, out _);
        Check(heldLevel == 1 && heldXP == 100 && heldFill == 1f, "A completed XP bar must remain full briefly before the next level.");
        report.AppendLine("PASS: zero/small XP, single/multiple level-ups, full-bar hold, level-100 overflow cap, and final values match gameplay XP rules.");
    }
    static void Render(Camera camera, Canvas canvas, string path)
    {
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        canvas.scaleFactor = target.width / 1920f;
        var old = RenderTexture.active; Texture2D image = null;
        try
        {
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true)) graphic.SetAllDirty();
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true)) label.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new UnityEngine.Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = old; camera.targetTexture = null;
            if (image != null) UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
