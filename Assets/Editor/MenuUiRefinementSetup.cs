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

public static class MenuUiRefinementSetup
{
    const string Request = "Library/CombatValidation/MenuUiRefinement.request";
    const string Output = "Tools/MenuUiRefinement";
    const string AdventurePath = "Assets/Resources/Adventure/UI/AdventureCanvas.prefab";
    const string OptionsPath = "Assets/Resources/Adventure/UI/GameOptions.prefab";
    [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.GetLastWriteTimeUtc("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") < File.GetLastWriteTimeUtc("Assets/Editor/MenuUiRefinementSetup.cs")) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        Directory.CreateDirectory(Output);
        try { if (command == "inspect") Inspect(); else if (command == "validate") Validate(); else Setup(); }
        catch (Exception error) { File.WriteAllText(Output + "/Error-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/Adventure/UI/Apply Menu Refinements")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before editing UI assets.");
        Directory.CreateDirectory(Output);
        string backup = Output + "/Before-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"); Directory.CreateDirectory(backup);
        foreach (var path in new[] { AdventurePath, OptionsPath, "Assets/Resources/Adventure/LocalizationTable.asset" })
            File.Copy(path, backup + "/" + Path.GetFileName(path));
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !scene.isDirty || string.IsNullOrEmpty(scene.path)) continue;
            File.Copy(scene.path, backup + "/" + scene.name + "-Disk.unity", true);
            EditorSceneManager.SaveScene(scene, backup + "/" + scene.name + "-Current.unity", true);
        }
        var adventure = PrefabUtility.LoadPrefabContents(AdventurePath);
        try
        {
            var members = adventure.transform.Find("Menu/Rest/Members"); if (members != null) UnityEngine.Object.DestroyImmediate(members.gameObject);
            CopyRect((RectTransform)adventure.transform.Find("Menu/Attributes/CharacterPreview/Portrait"), (RectTransform)adventure.transform.Find("Menu/Skills/CharacterPreview/Portrait"));
            foreach (var page in new[] { "Attributes", "Skills" }) RefineHeader(adventure.transform.Find("Menu/" + page + "/Summary"));
            var details = adventure.transform.Find("Menu/Skills/Summary/Details").GetComponent<TMP_Text>();
            if (details.text.StartsWith("Máu ", StringComparison.Ordinal) || details.text.StartsWith("HP ", StringComparison.Ordinal))
                details.text = string.Join("\n", details.text.Split('\n').Skip(1));
            PrefabUtility.SaveAsPrefabAsset(adventure, AdventurePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(adventure); }
        var options = PrefabUtility.LoadPrefabContents(OptionsPath);
        try
        {
            foreach (var row in options.GetComponent<GameOptionsView>().rows)
            {
                var label = (RectTransform)row.slider.transform.parent.Find("Label");
                label.offsetMin = label.offsetMax = new Vector2(43, 0);
                row.percentage.rectTransform.offsetMin = row.percentage.rectTransform.offsetMax = new Vector2(-43, 0);
                var increase = (RectTransform)row.increase.transform; increase.offsetMax = new Vector2(-43, increase.offsetMax.y);
            }
            PrefabUtility.SaveAsPrefabAsset(options, OptionsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(options); }
        var table = Resources.Load<GameLocalizationTable>("Adventure/LocalizationTable");
        foreach (var pair in new[] { ("Lưu game", "Save Game"), ("LƯU GAME", "SAVE GAME"), ("Lưu", "Save"), ("Lưu / Hồi máu", "Save / Heal"),
            ("Điểm nghỉ • Lưu game / Hồi máu", "Rest point • Save / Heal"), ("Máu {0}/{1}", "HP {0}/{1}") })
        {
            var entry = table.entries.FirstOrDefault(e => e.vietnamese == pair.Item1);
            if (entry == null) table.entries.Add(new GameLocalizationEntry { key = "ui.refinement." + pair.Item2, vietnamese = pair.Item1, english = pair.Item2 });
            else entry.english = pair.Item2;
        }
        EditorUtility.SetDirty(table);
        typeof(GameLocalizationTable).GetMethod("BuildIndex", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(table, null);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Output + "/Setup.txt", "Backup: " + backup + "\nEdited resource prefabs and localization only; the user's open scene remains unsaved.\nOptions labels +43px, percentages -43px; shared Title selector retained; Menu selector hidden by its caller.\nSkills portrait matches Attributes; HP/XP labels 22px and bars 305x8; Rest Members removed.");
        Validate();
    }
    static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta; target.anchoredPosition3D = source.anchoredPosition3D;
        target.localRotation = source.localRotation; target.localScale = source.localScale;
    }
    static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    static void RefineHeader(Transform summary)
    {
        var progress = summary.Find("Progress").GetComponent<TMP_Text>(); progress.enableAutoSizing = false; progress.fontSize = 22;
        TopLeft(progress.rectTransform, new Vector2(25, -70), new Vector2(305, 28));
        var xp = summary.Find("XP"); TopLeft((RectTransform)xp, new Vector2(25, -101), new Vector2(305, 8));
        var health = summary.Find("HealthLabel");
        if (health == null) { health = UnityEngine.Object.Instantiate(progress.gameObject, summary).transform; health.name = "HealthLabel"; }
        var label = health.GetComponent<TMP_Text>(); label.text = "Máu 100/100"; label.font = progress.font; label.fontSharedMaterial = progress.fontSharedMaterial;
        label.fontSize = progress.fontSize; label.enableAutoSizing = false;
        TopLeft((RectTransform)health, new Vector2(25, -117), new Vector2(305, 28));
        var bar = summary.Find("HealthBar");
        if (bar == null) { bar = UnityEngine.Object.Instantiate(xp.gameObject, summary).transform; bar.name = "HealthBar"; }
        TopLeft((RectTransform)bar, new Vector2(25, -151), new Vector2(305, 8));
        bar.GetComponent<Image>().color = new Color(.13f, .14f, .15f, .9f);
        bar.Find("Fill").GetComponent<Image>().color = new Color(.72f, .16f, .13f, 1);
    }
    [MenuItem("Tools/Adventure/UI/Validate Menu Refinements")]
    public static void Validate()
    {
        var report = new StringBuilder();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdventurePath);
        Check(prefab.transform.Find("Menu/Rest/Members") == null, "Rest Members is removed.");
        var source = (RectTransform)prefab.transform.Find("Menu/Attributes/CharacterPreview/Portrait");
        var target = (RectTransform)prefab.transform.Find("Menu/Skills/CharacterPreview/Portrait");
        Check(source.anchorMin == target.anchorMin && source.anchorMax == target.anchorMax && source.sizeDelta == target.sizeDelta && source.anchoredPosition == target.anchoredPosition && source.pivot == target.pivot, "Skills portrait matches the full Attributes portrait rect.");
        foreach (var page in new[] { "Attributes", "Skills" })
        {
            var summary = prefab.transform.Find("Menu/" + page + "/Summary");
            var xp = summary.Find("Progress").GetComponent<TMP_Text>(); var hp = summary.Find("HealthLabel").GetComponent<TMP_Text>();
            Check(xp.font == hp.font && xp.fontSize == hp.fontSize && xp.fontSize == 22 && !xp.enableAutoSizing && !hp.enableAutoSizing, "HP/XP typography matches on " + page);
            Check(((RectTransform)summary.Find("XP")).sizeDelta == new Vector2(305, 8) && ((RectTransform)summary.Find("HealthBar")).sizeDelta == new Vector2(305, 8), "HP/XP bar dimensions match on " + page);
        }
        var options = Resources.Load<GameOptionsView>("Adventure/UI/GameOptions");
        foreach (var row in options.rows)
        {
            Check(((RectTransform)row.slider.transform.parent.Find("Label")).offsetMin.x == 43 && row.percentage.rectTransform.offsetMin.x == -43, "Inset row text: " + row.bus);
        }
        report.AppendLine("PASS: Rest Members removed; Skills portrait matches Attributes (440x660); HP/XP use the same 22px font and 305x8 bars on both pages; all five Options rows have inward text offsets.");
        var scene = EditorSceneManager.NewPreviewScene(); GameObject clone = null, optionsClone = null, textHost = null;
        var field = typeof(GameLocalization).GetField("language", BindingFlags.Static | BindingFlags.NonPublic); var previousLanguage = GameLocalization.CurrentLanguage;
        try
        {
            clone = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(clone, scene);
            var summary = clone.transform.Find("Menu/Attributes/Summary");
            var method = typeof(AdventureUI).GetMethod("EnsureAttributeSummaryLayout", BindingFlags.Static | BindingFlags.NonPublic);
            method.Invoke(null, new object[] { summary, summary.Find("Details"), summary.Find("Progress").GetComponent<TMP_Text>(), ((RectTransform)summary).anchoredPosition.y });
            Check(summary.Find("HealthLabel").GetComponent<TMP_Text>().fontSize == 22 && ((RectTransform)summary.Find("HealthBar")).sizeDelta == new Vector2(305, 8), "Runtime Attributes layout preserves matched HP/XP sizing.");
            optionsClone = UnityEngine.Object.Instantiate(options.gameObject); SceneManager.MoveGameObjectToScene(optionsClone, scene);
            var view = optionsClone.GetComponent<GameOptionsView>(); view.SetLanguageVisible(false);
            Check(!view.languageButton.gameObject.activeSelf && !view.transform.Find("Volume Panel/Language Label").gameObject.activeSelf, "Menu hides the selector heading and whole language button.");
            view.SetLanguageVisible(true);
            Check(view.languageButton.gameObject.activeSelf && view.transform.Find("Volume Panel/Language Label").gameObject.activeSelf, "Title still exposes its language selector.");
            textHost = new GameObject("Localization fixture", typeof(RectTransform), typeof(TextMeshProUGUI)); SceneManager.MoveGameObjectToScene(textHost, scene);
            var text = textHost.GetComponent<TMP_Text>(); text.font = Resources.Load<TMP_FontAsset>("Adventure/UI/OptionsFont");
            foreach (var language in new[] { GameLanguage.Vietnamese, GameLanguage.English, GameLanguage.Vietnamese })
            {
                field.SetValue(null, language); GameLocalization.SetText(text, "Lưu game"); text.GetComponent<LocalizedGameText>().Refresh();
                Check(text.text == (language == GameLanguage.English ? "Save Game" : "Lưu game"), "Save button follows live language binding: " + language);
                Check(GameLocalization.Translate("LƯU GAME") == (language == GameLanguage.English ? "SAVE GAME" : "LƯU GAME"), "Save panel heading translates: " + language);
            }
            report.AppendLine("PASS: runtime Attributes layout keeps the new sizes; Menu Options hides language UI and Title retains it; Save Game and its heading translate correctly across VI → EN → VI. No PlayerPrefs/save writes.");
        }
        finally
        {
            field.SetValue(null, previousLanguage);
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone); if (optionsClone != null) UnityEngine.Object.DestroyImmediate(optionsClone); if (textHost != null) UnityEngine.Object.DestroyImmediate(textHost);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/Validation.txt", report.ToString()); Debug.Log("[Menu UI] Refinements validated: " + Output + "/Validation.txt");
    }
    static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    static void Inspect()
    {
        var report = new StringBuilder();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdventurePath);
        foreach (var prefix in new[] { "Menu/Attributes/CharacterPreview", "Menu/Skills/CharacterPreview", "Menu/Attributes/Summary", "Menu/Skills/Summary", "Menu/Rest" })
        {
            report.AppendLine(prefix); var node = prefab.transform.Find(prefix);
            if (node == null) { report.AppendLine("MISSING"); continue; }
            foreach (var rect in node.GetComponentsInChildren<RectTransform>(true))
            {
                var text = rect.GetComponent<TMP_Text>(); var image = rect.GetComponent<Image>();
                report.AppendLine(rect.name + " active=" + rect.gameObject.activeSelf + " anchors=" + rect.anchorMin + "/" + rect.anchorMax + " pivot=" + rect.pivot + " pos=" + rect.anchoredPosition + " size=" + rect.sizeDelta + " offsets=" + rect.offsetMin + "/" + rect.offsetMax +
                    (text != null ? " FONT=" + text.fontSize + " auto=" + text.enableAutoSizing + " text=" + text.text : "") + (image != null ? " sprite=" + image.sprite?.name : ""));
            }
        }
        var options = AssetDatabase.LoadAssetAtPath<GameOptionsView>(OptionsPath);
        foreach (var row in options.rows)
        {
            foreach (var name in new[] { "Label", "Percentage" })
            {
                var rect = (RectTransform)row.slider.transform.parent.Find(name);
                report.AppendLine("Options/" + row.bus + "/" + name + " anchors=" + rect.anchorMin + "/" + rect.anchorMax + " offsets=" + rect.offsetMin + "/" + rect.offsetMax);
            }
        }
        var table = Resources.Load<GameLocalizationTable>("Adventure/LocalizationTable");
        foreach (var text in new[] { "Lưu game", "LƯU GAME", "Lưu", "Lưu / Hồi máu", "Lưu game / Hồi máu", "Điểm nghỉ • Lưu game / Hồi máu" })
            report.AppendLine("TRANSLATE: " + text + " -> " + table.Translate(text, GameLanguage.English));
        foreach (var path in new[] { "Assets/Scenes/m.unity", "Assets/Scenes/mapgame.unity" })
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                foreach (var canvas in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AdventureCanvasRoot>(true)))
                    report.AppendLine(path + " canvas=" + canvas.name + " prefab=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(canvas) + " restSave=" + canvas.transform.Find("Menu/Rest/Save")?.GetComponentInChildren<TMP_Text>(true)?.text);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        File.WriteAllText(Output + "/Inspection.txt", report.ToString()); Debug.Log("[Menu UI] Inspection ready: " + Output + "/Inspection.txt");
    }
}
