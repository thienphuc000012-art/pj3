using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Explicit setup, never a migration on ordinary project startup.
public static class SaveSlotsUISetup
{
    const string PrefabPath = "Assets/Resources/Adventure/UI/AdventureCanvas.prefab";
    const string Request = "Library/CombatValidation/SaveSlotsUISetup.request";
    const string Result = "Library/CombatValidation/SaveSlotsUISetup.result.txt";

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
    }

    static void RunRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += RunRequested; return; }
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        Setup();
    }

    [MenuItem("Tools/Adventure/UI/Setup Save and Load Slots")]
    public static void Setup()
    {
        GameObject contents = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before setting up the save UI.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath && stage.scene.isDirty)
                throw new InvalidOperationException("Save the open AdventureCanvas Prefab Stage before setup.");
            string backup = "Tools/CanvasUI/BeforeSaveSlots-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            File.Copy(PrefabPath, backup + "/AdventureCanvas.prefab");
            contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            var root = contents.GetComponent<AdventureCanvasRoot>();
            if (root == null) throw new InvalidOperationException("Missing AdventureCanvasRoot.");
            var template = root.transform.Find("Menu/Rest/Save")?.GetComponent<Button>();
            if (template == null) throw new InvalidOperationException("Missing Rest/Save button.");
            var view = contents.GetComponent<AdventureSaveSlotsView>();
            if (view == null) view = contents.AddComponent<AdventureSaveSlotsView>();
            view.EnsureHierarchy(root, template);
            var overview = root.transform.Find("Menu/Party/Overview");
            var inventory = overview.Find("Inventory").GetComponent<Button>();
            var load = overview.Find("LoadGame")?.GetComponent<Button>();
            if (load == null)
            {
                load = UnityEngine.Object.Instantiate(inventory, overview);
                load.name = "LoadGame";
                ((RectTransform)load.transform).anchoredPosition = ((RectTransform)inventory.transform).anchoredPosition + Vector2.down * 176f;
                load.onClick = new Button.ButtonClickedEvent();
            }
            load.GetComponentInChildren<TMP_Text>(true).text = "Tải game";
            load.gameObject.SetActive(true);
            view.title.text = "LƯU GAME";
            view.backButton.GetComponentInChildren<TMP_Text>(true).text = "Trở lại";
            view.confirmationQuestion.text = "Lưu game";
            view.confirmationDetails.text = "02/11/2026  14:30:00\nThời gian chơi: 01:02:37";
            view.yesButton.GetComponentInChildren<TMP_Text>(true).text = "Có";
            view.noButton.GetComponentInChildren<TMP_Text>(true).text = "Không";
            SetExample(view.slotTemplate.transform, "Saved At", "02/11/2026  14:30:00");
            SetExample(view.slotTemplate.transform, "Location", "Tên điểm nghỉ");
            SetExample(view.slotTemplate.transform, "Play Time Label", "Thời gian chơi");
            SetExample(view.slotTemplate.transform, "Play Time", "01:02:37");
            if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null) throw new IOException("Could not save AdventureCanvas prefab.");
            ValidateView(root, view, template);
            File.WriteAllText(Result, "PASS: authored save/load panels, slot/date/playtime labels, Yes/No modal and LoadGame button; cancel leaves slot list open; elapsed-time formatting and repeat setup preserves authored layout. Backup: " + backup);
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally { if (contents != null) PrefabUtility.UnloadPrefabContents(contents); }
    }

    static void SetExample(Transform row, string name, string value)
    {
        var text = row.Find(name)?.GetComponent<TMP_Text>();
        if (text != null) text.text = value;
    }

    static void ValidateView(AdventureCanvasRoot root, AdventureSaveSlotsView view, Button template)
    {
        if (view.slotsPanel == null || view.confirmationPanel == null || view.slotContent == null || view.slotTemplate == null ||
            view.yesButton == null || view.noButton == null) throw new InvalidOperationException("Missing save UI references.");
        if (!view.slotsPanel.GetComponent<Image>().raycastTarget || !view.confirmationPanel.GetComponent<Image>().raycastTarget)
            throw new InvalidOperationException("Save UI must block pointer input behind its panels.");
        var row = (RectTransform)view.slotTemplate.transform;
        var beforePosition = row.anchoredPosition;
        var beforeSize = row.sizeDelta;
        view.EnsureHierarchy(root, template);
        if (row.anchoredPosition != beforePosition || row.sizeDelta != beforeSize)
            throw new InvalidOperationException("Rebinding changed an authored slot layout.");
        if (AdventureSaveSlotsView.FormatPlayTime(3661.9) != "01:01:01" || AdventureSaveSlotsView.FormatPlayTime(90000) != "25:00:00")
            throw new InvalidOperationException("Play time must preserve hours above 24.");
        // Exercise cancel in memory; no live campaign or save files are involved.
        view.Bind(root, null, template);
        view.slotsPanel.SetActive(true);
        view.confirmationPanel.SetActive(true);
        typeof(AdventureSaveSlotsView).GetField("selectedSlot", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, 1);
        view.noButton.onClick.Invoke();
        if (!view.slotsPanel.activeSelf || view.confirmationPanel.activeSelf)
            throw new InvalidOperationException("Cancel must return to the slot list.");
        view.Close();
    }
}
