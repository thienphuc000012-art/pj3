using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// In-memory UI checks. Does not load a campaign, change a scene, or touch player saves.
public static class SaveSlotInteractionValidation
{
    const string Request = "Library/CombatValidation/SaveSlotInteractionValidation.request";
    const string Result = "Library/CombatValidation/SaveSlotInteractionValidation.result.txt";
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

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
        Validate();
    }

    [MenuItem("Tools/Adventure/UI/Validate Save Slot Interactions")]
    public static void Validate()
    {
        GameObject contents = null, row = null;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before validating UI.");
            contents = PrefabUtility.LoadPrefabContents("Assets/Resources/Adventure/UI/AdventureCanvas.prefab");
            var view = contents.GetComponent<AdventureSaveSlotsView>();
            var root = contents.GetComponent<AdventureCanvasRoot>();
            var template = contents.transform.Find("Menu/Rest/Save").GetComponent<Button>();
            view.EnsureHierarchy(root, template);
            bool english = GameLocalization.IsEnglish;
            CheckQuestion(view, true, false, true, false, english ? "Save Game" : "Lưu game");
            CheckQuestion(view, true, false, false, false, english ? "Confirm Overwrite" : "Xác nhận ghi đè");
            CheckQuestion(view, true, false, false, true, english ? "Confirm Overwrite" : "Xác nhận ghi đè");
            CheckQuestion(view, true, true, false, false, english ? "Delete Save?" : "Xóa bản lưu?");
            CheckQuestion(view, false, false, false, false, english ? "Load Game" : "Tải game");
            view.Bind(root, null, template);
            view.slotsPanel.SetActive(true);
            view.confirmationPanel.SetActive(true);
            Set(view, "deleting", true);
            view.noButton.onClick.Invoke();
            Check(view.slotsPanel.activeSelf && !view.confirmationPanel.activeSelf, "No must return to the list.");
            Check(!(bool)Get(view, "deleting"), "Cancel must clear the pending delete action.");
            Check(view.yesButton.GetComponent<SmoothButtonHoverScale>() == null && view.noButton.GetComponent<SmoothButtonHoverScale>() == null,
                "Confirmation buttons must retain text-only hover.");
            CheckTitleLoad(view, template);

            row = new GameObject("Save Slot Input Test", typeof(RectTransform), typeof(Image), typeof(Button), typeof(SaveSlotContextClick));
            int saves = 0, deletes = 0;
            var button = row.GetComponent<Button>();
            button.onClick.AddListener(() => saves++);
            row.GetComponent<SaveSlotContextClick>().Configure(() => deletes++);
            var pointer = new PointerEventData(EventSystem.current);
            pointer.button = PointerEventData.InputButton.Left;
            ExecuteEvents.Execute(row, pointer, ExecuteEvents.pointerClickHandler);
            Check(saves == 1 && deletes == 0, "Left click must only request saving/loading.");
            pointer.button = PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(row, pointer, ExecuteEvents.pointerClickHandler);
            Check(saves == 1 && deletes == 1, "Right click must only request deletion confirmation.");
            button.interactable = false;
            ExecuteEvents.Execute(row, pointer, ExecuteEvents.pointerClickHandler);
            Check(deletes == 1, "Disabled rows must ignore right click.");

            var settings = Resources.Load<GameUIThemeSettings>("Adventure/UI/GameUIThemeSettings");
            Check(settings != null && settings.npcChatIcon != null, "The shared NPC Chat sprite must resolve.");
            Check(AssetDatabase.GetAssetPath(settings.npcChatIcon) == "Assets/asset/Ui/chat.png", "The NPC marker must use the supplied Chat asset.");
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "PASS: save/overwrite/corrupt/delete/load questions; Title autosave and manual slot confirmation; cancel returns to list and clears delete; fixed-scale text hover; left/right click separation and disabled rows; NPC Chat sprite reference resolves. No live saves or scenes changed.");
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Result));
            File.WriteAllText(Result, "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (row != null) UnityEngine.Object.DestroyImmediate(row);
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    static void CheckTitleLoad(AdventureSaveSlotsView templateView, Button templateButton)
    {
        var canvas = new GameObject("Title Save Slots Test", typeof(RectTransform), typeof(Canvas));
        try
        {
            var view = canvas.AddComponent<AdventureSaveSlotsView>();
            var entries = new[]
            {
                new CampaignSaveSlotInfo { slot = AdventureSaveSlotsView.AutosaveSlot, playTimeSeconds = 60 },
                new CampaignSaveSlotInfo { slot = 1, isEmpty = true },
                new CampaignSaveSlotInfo { slot = 2, playTimeSeconds = 120 }
            };
            int loadedSlot = -1;
            int closed = 0;
            bool Load(int slot, out string error) { loadedSlot = slot; error = null; return true; }
            view.BindTitle(templateView, templateButton, () => entries, Load, () => closed++);
            view.OpenTitle();
            Check(!view.slotContent.Find("Slot 01").GetComponent<Button>().interactable, "Empty Title slots must be disabled.");
            view.slotContent.Find("Slot 02").GetComponent<Button>().onClick.Invoke();
            Check(view.confirmationPanel.activeSelf && loadedSlot == -1, "Selecting a Title slot must request confirmation before loading.");
            Check(view.HandleBack() && view.IsOpen && !view.confirmationPanel.activeSelf && loadedSlot == -1, "Cancel must return to the Title slot list.");
            view.slotContent.Find("Autosave").GetComponent<Button>().onClick.Invoke();
            view.yesButton.onClick.Invoke();
            Check(loadedSlot == AdventureSaveSlotsView.AutosaveSlot && !view.IsOpen && closed == 1, "Title confirmation must load the chosen autosave.");
            view.OpenTitle();
            view.slotContent.Find("Slot 02").GetComponent<Button>().onClick.Invoke();
            view.yesButton.onClick.Invoke();
            Check(loadedSlot == 2 && !view.IsOpen && closed == 2, "Title confirmation must load the chosen manual slot.");
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }

    static void CheckQuestion(AdventureSaveSlotsView view, bool saving, bool deleting, bool empty, bool corrupt, string expected)
    {
        Set(view, "saving", saving);
        Set(view, "deleting", deleting);
        Set(view, "selectedInfo", new CampaignSaveSlotInfo { slot = 1, isEmpty = empty, isCorrupt = corrupt });
        typeof(AdventureSaveSlotsView).GetMethod("ShowConfirmationText", PrivateInstance).Invoke(view, null);
        Check(view.confirmationQuestion.text == expected, "Unexpected confirmation question: " + view.confirmationQuestion.text);
    }
    static object Get(AdventureSaveSlotsView view, string field) => typeof(AdventureSaveSlotsView).GetField(field, PrivateInstance).GetValue(view);
    static void Set(AdventureSaveSlotsView view, string field, object value) => typeof(AdventureSaveSlotsView).GetField(field, PrivateInstance).SetValue(view, value);
    static void Check(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
}
