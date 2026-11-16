using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates editable audio assets and the shared Options canvas without entering Play Mode.</summary>
public static class GameAudioSetup
{
    const string Request = "Library/CombatValidation/GameAudio.request";
    const string Output = "Tools/GameAudio";
    const string AudioFolder = "Assets/Resources/Adventure/Audio";
    const string OptionsPath = "Assets/Resources/Adventure/UI/GameOptions.prefab";
    const string AdventurePath = "Assets/Resources/Adventure/UI/AdventureCanvas.prefab";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/GameAudioSetup.cs")) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/GameAudioValidation.cs")) return;
        foreach (var source in new[] { "GameAudio", "GameOptionsView", "GameAudioSourceBus", "ActorAudio", "ActorAudioProfile", "GameAudioSettings" })
            if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp.dll") < File.GetLastWriteTimeUtc("Assets/Scripts/Adventure/" + source + ".cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); }
        catch (IOException) { return; } // A request may still be finishing its atomic write.
        try { if (command == "setup") Setup(); else if (command == "style") { StyleOptions(); GameAudioValidation.Run(); } else GameAudioValidation.Run(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/Adventure/Audio/Select Audio Settings")]
    static void SelectSettings() { Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(AudioFolder + "/GameAudioSettings.asset"); }
    [MenuItem("Tools/Adventure/Audio/Setup Audio and Options")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring audio assets.");
        Directory.CreateDirectory(Output); Directory.CreateDirectory(AudioFolder); AssetDatabase.Refresh();
        string backup = Output + "/Before-AudioOptions-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        foreach (var path in new[] { OptionsPath, AdventurePath, "Assets/Scenes/Title.unity", "Assets/Resources/Adventure/LocalizationTable.asset" })
            if (File.Exists(path)) File.Copy(path, backup + "/" + Path.GetFileName(path), true);
        var settings = LoadOrCreate<GameAudioSettings>(AudioFolder + "/GameAudioSettings.asset");
        if (settings.player == null) settings.player = LoadOrCreate<ActorAudioProfile>(AudioFolder + "/PlayerAudio.asset");
        if (settings.enemy == null) settings.enemy = LoadOrCreate<ActorAudioProfile>(AudioFolder + "/EnemyAudio.asset");
        EditorUtility.SetDirty(settings);
        AddTranslations();
        if (!File.Exists(OptionsPath)) BuildOptions();
        StyleOptions();
        AddAdventureButton(backup);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Output + "/Setup.txt", "Created editable GameAudioSettings, Player/EnemyAudio profiles, shared Options prefab, and Menu Options button.\nNo music, footsteps or voices assigned: user will add clips. Existing scan/parry/UI fallback retained.\nBackup: " + backup);
        GameAudioValidation.Run();
    }
    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
    }
    static void AddTranslations()
    {
        var table = AssetDatabase.LoadAssetAtPath<GameLocalizationTable>("Assets/Resources/Adventure/LocalizationTable.asset");
        var pairs = new[] { ("Tùy chọn", "Options"), ("Âm lượng", "Volume"), ("Tổng", "Master"), ("Nhạc nền", "Music"), ("Giọng nói", "Voice"), ("Giao diện", "User Interface"), ("Hiệu ứng âm thanh", "Special effects"), ("Ngôn ngữ", "Language"), ("Trở lại", "Back") };
        foreach (var pair in pairs)
            if (!table.entries.Any(e => e != null && (e.vietnamese == pair.Item1 || e.english == pair.Item2)))
                table.entries.Add(new GameLocalizationEntry { key = "audio." + pair.Item2, vietnamese = pair.Item1, english = pair.Item2 });
        EditorUtility.SetDirty(table);
        typeof(GameLocalizationTable).GetMethod("BuildIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(table, null);
    }
    static void StyleOptions()
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/asset/Ui/skillbackground.png").OfType<Sprite>().First();
        var prefab = PrefabUtility.LoadPrefabContents(OptionsPath);
        try
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Adventure/UI/OptionsFont.asset");
            if (font == null || font.material == null || font.atlasTextures == null || font.atlasTextures.Any(t => t == null))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(AdventurePath).GetComponent<AdventureCanvasRoot>().languageFont;
                var generated = TMP_FontAsset.CreateFontAsset(source); generated.name = "Options Vietnamese English";
                generated.TryAddCharacters("Tùy chọnÂm lượngTổngNhạc nềnGiọng nóiGiao diệnHiệu ứng âm thanhNgôn ngữTrở lạiTiếng ViệtMasterMusicVoiceUser InterfaceSpecial effectsLanguageBackVolumeOptionsEnglish0123456789%‹›");
                if (font == null) { font = generated; AssetDatabase.CreateAsset(font, "Assets/Resources/Adventure/UI/OptionsFont.asset"); }
                else { EditorUtility.CopySerialized(generated, font); UnityEngine.Object.DestroyImmediate(generated); }
            }
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSharedMaterial = font.material; }
            foreach (var row in prefab.GetComponent<GameOptionsView>().rows)
            {
                var image = row.slider.transform.parent.GetComponent<Image>(); image.sprite = sprite; image.color = Color.white;
                row.decrease.GetComponent<Image>().color = row.increase.GetComponent<Image>().color = new Color(0, 0, 0, .01f);
            }
            foreach (var button in new[] { prefab.GetComponent<GameOptionsView>().languageButton, prefab.GetComponent<GameOptionsView>().backButton })
            { var image = button.GetComponent<Image>(); image.sprite = sprite; image.color = Color.white; }
            foreach (var button in prefab.GetComponentsInChildren<Button>(true)) GameButtonSound.Ensure(button);
            PrefabUtility.SaveAsPrefabAsset(prefab, OptionsPath);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (var atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }
    static void BuildOptions()
    {
        var root = new GameObject("Options", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PreserveAuthoredUI), typeof(GameOptionsView));
        try
        {
            Stretch((RectTransform)root.transform); root.GetComponent<Image>().color = new Color(.006f, .008f, .014f, .97f);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(AdventurePath).GetComponent<AdventureCanvasRoot>();
            var font = source.defaultFont;
            var theme = AssetDatabase.LoadAssetAtPath<GameUIThemeSettings>("Assets/Resources/Adventure/UI/GameUIThemeSettings.asset");
            var panel = Rect(root.transform, "Volume Panel", new Vector2(.13f, .20f), new Vector2(.87f, .82f));
            Text(panel, "Title", "Tùy chọn", font, 44, new Vector2(0, .87f), Vector2.one);
            Text(panel, "Volume Heading", "Âm lượng", font, 29, new Vector2(0, .76f), new Vector2(1, .86f));
            string[] labels = { "Tổng", "Nhạc nền", "Giọng nói", "Giao diện", "Hiệu ứng âm thanh" };
            var view = root.GetComponent<GameOptionsView>(); view.rows = new GameVolumeRow[5];
            for (int i = 0; i < 5; i++)
            {
                float top = .75f - i * .125f;
                var row = Rect(panel, ((GameAudioBus)i).ToString(), new Vector2(0, top - .105f), new Vector2(1, top));
                var background = row.gameObject.AddComponent<Image>(); background.sprite = theme.buttonBackground; background.type = Image.Type.Simple; background.color = new Color(.55f, .52f, .47f, .8f); background.raycastTarget = false;
                Text(row, "Label", labels[i], font, 27, new Vector2(.035f, 0), new Vector2(.40f, 1));
                var minus = Button(row, "Decrease", "‹", font, null, new Vector2(.40f, .10f), new Vector2(.46f, .90f));
                var plus = Button(row, "Increase", "›", font, null, new Vector2(.84f, .10f), new Vector2(.90f, .90f));
                var sliderRect = Rect(row, "Slider", new Vector2(.46f, .27f), new Vector2(.84f, .73f));
                var slider = sliderRect.gameObject.AddComponent<Slider>();
                var track = sliderRect.gameObject.AddComponent<Image>(); track.color = new Color(.17f, .18f, .20f, 1);
                var fillArea = Rect(sliderRect, "Fill Area", new Vector2(.01f, .08f), new Vector2(.99f, .92f));
                var fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.one); var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = new Color(.91f, .89f, .83f, 1); fillImage.raycastTarget = false;
                var handles = Rect(sliderRect, "Handle Area", new Vector2(.015f, 0), new Vector2(.985f, 1));
                var handle = Rect(handles, "Handle", new Vector2(.8f, -.15f), new Vector2(.8f, 1.15f)); handle.sizeDelta = new Vector2(6, 0);
                var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;
                slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage; slider.direction = Slider.Direction.LeftToRight;
                slider.navigation = new Navigation { mode = Navigation.Mode.Automatic }; slider.value = i < 2 ? .8f : 1f;
                var percentage = Text(row, "Percentage", i < 2 ? "80%" : "100%", font, 25, new Vector2(.9f, 0), new Vector2(.99f, 1));
                view.rows[i] = new GameVolumeRow { bus = (GameAudioBus)i, slider = slider, percentage = percentage, decrease = minus, increase = plus };
            }
            Text(panel, "Language Label", "Ngôn ngữ", font, 27, new Vector2(.035f, -.075f), new Vector2(.4f, .025f));
            view.languageButton = Button(panel, "Language", "Tiếng Việt", font, theme.buttonBackground, new Vector2(.46f, -.075f), new Vector2(.99f, .025f));
            view.languageValue = view.languageButton.GetComponentInChildren<TMP_Text>();
            view.backButton = Button(root.transform, "Back", "Trở lại", font, theme.buttonBackground, new Vector2(.68f, .075f), new Vector2(.87f, .145f));
            PrefabUtility.SaveAsPrefabAsset(root, OptionsPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void AddAdventureButton(string backup)
    {
        void Add(GameObject canvas)
        {
            var overview = canvas.transform.Find("Menu/Party/Overview"); if (overview == null || overview.Find("Options") != null) return;
            var template = overview.Find("LoadGame") ?? overview.Find("Inventory");
            var button = UnityEngine.Object.Instantiate(template.gameObject, overview); button.name = "Options";
            var rect = (RectTransform)button.transform;
            var origin = (RectTransform)template;
            rect.anchoredPosition = origin.anchoredPosition + Vector2.down * (template.name == "LoadGame" ? 88f : 264f);
            button.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
            button.GetComponentInChildren<TMP_Text>(true).text = "Tùy chọn";
            var back = overview.Find("ReturnToTitle") as RectTransform;
            if (back != null) back.anchoredPosition = rect.anchoredPosition + Vector2.down * 88f;
        }
        var prefab = PrefabUtility.LoadPrefabContents(AdventurePath);
        try { Add(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, AdventurePath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        // Existing map scenes contain authored copies; update those currently open too.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i); if (!scene.isLoaded || (scene.name != "m" && scene.name != "mapgame")) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<AdventureCanvasRoot>(true))
                {
                    if (canvas.kind != AdventureCanvasKind.Adventure || canvas.transform.Find("Menu/Party/Overview/Options") != null) continue;
                    if (File.Exists(scene.path)) File.Copy(scene.path, backup + "/" + scene.name + "-Disk.unity", true);
                    EditorSceneManager.SaveScene(scene, backup + "/" + scene.name + "-Current.unity", true);
                    Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Add audio Options"); Add(canvas.gameObject); EditorSceneManager.MarkSceneDirty(scene);
                }
        }
    }
    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static TMP_Text Text(Transform parent, string name, string text, TMP_FontAsset font, float size, Vector2 min, Vector2 max)
    {
        var rect = Rect(parent, name, min, max); var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = new Color(.91f, .89f, .83f, 1);
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap; return label;
    }
    static Button Button(Transform parent, string name, string text, TMP_FontAsset font, Sprite sprite, Vector2 min, Vector2 max)
    {
        var rect = Rect(parent, name, min, max); var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
        image.color = sprite != null ? Color.white : new Color(1, 1, 1, .025f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var label = Text(rect, "Label", text, font, 27, new Vector2(.02f, 0), new Vector2(.98f, 1)); label.alignment = TextAlignmentOptions.Center;
        return button;
    }
}
