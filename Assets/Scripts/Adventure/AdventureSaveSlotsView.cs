using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Layout can be authored on the Canvas. Missing UI is created once as a fallback;
// opening the menu only changes content/visibility, not authored RectTransforms.
[DisallowMultipleComponent]
public sealed class AdventureSaveSlotsView : MonoBehaviour
{
    [Header("Save / Load list")]
    public GameObject slotsPanel;
    public TMP_Text title;
    public TMP_Text statusText;
    public ScrollRect slotScroll;
    public RectTransform slotContent;
    public Button slotTemplate;
    public Button backButton;
    [Header("Confirmation")]
    public GameObject confirmationPanel;
    public TMP_Text confirmationQuestion;
    public TMP_Text confirmationDetails;
    public Button yesButton;
    public Button noButton;
    [Header("Fallback layout")]
    public Sprite rowBackground;
    public Sprite confirmationBackground;
    [Min(64f)] public float rowHeight = 116f;
    [Min(0f)] public float rowSpacing = 12f;

    sealed class Row
    {
        public Button button;
        public TMP_Text date, location, playCaption, playTime;
        public CanvasGroup group;
    }
    readonly List<Row> rows = new List<Row>();
    AdventureCanvasRoot canvasRoot;
    CampaignSession session;
    TMP_Text fontSource;
    CanvasGroup listGroup;
    AdventureMenu openingMenu;
    CampaignSaveSlotInfo selectedInfo;
    GameObject lastFocused;
    bool saving, committing;
    int selectedSlot;
    bool IsEnglish => GameLocalization.IsEnglish;
    public bool IsOpen => slotsPanel != null && slotsPanel.activeSelf;

    public void Bind(AdventureCanvasRoot view, CampaignSession campaign, Button buttonTemplate)
    {
        session = campaign;
        EnsureHierarchy(view, buttonTemplate);
        backButton.onClick.RemoveListener(Close);
        backButton.onClick.AddListener(Close);
        yesButton.onClick.RemoveListener(Confirm);
        yesButton.onClick.AddListener(Confirm);
        noButton.onClick.RemoveListener(CancelConfirmation);
        noButton.onClick.AddListener(CancelConfirmation);
        Close();
    }

    // Also callable by an Editor builder with no live campaign session.
    public void EnsureHierarchy(AdventureCanvasRoot view, Button buttonTemplate)
    {
        if (view == null) return;
        canvasRoot = view;
        fontSource = buttonTemplate != null ? buttonTemplate.GetComponentInChildren<TMP_Text>(true) : view.GetComponentInChildren<TMP_Text>(true);
        var settings = Resources.Load<GameUIThemeSettings>("Adventure/UI/GameUIThemeSettings");
        if (rowBackground == null && settings != null) rowBackground = settings.skillBackground;
        if (confirmationBackground == null) confirmationBackground = rowBackground;
        var menu = view.transform.Find("Menu");
        if (menu == null) return;
        if (slotsPanel == null)
        {
            var existing = menu.Find("Save Slots");
            slotsPanel = existing != null ? existing.gameObject : Panel(menu, "Save Slots", Vector2.zero, Vector2.one, new Color(.008f, .011f, .015f, .97f));
        }
        listGroup = slotsPanel.GetComponent<CanvasGroup>();
        if (listGroup == null) listGroup = slotsPanel.AddComponent<CanvasGroup>();
        var box = ChildPanel(slotsPanel.transform, "List Panel", new Vector2(.17f, .12f), new Vector2(.83f, .88f), new Color(.02f, .022f, .025f, .75f));
        if (title == null) title = Text(box, "Title", "", new Vector2(.05f, .88f), new Vector2(.95f, .99f), 36f, TextAlignmentOptions.MidlineLeft);
        if (statusText == null) statusText = Text(box, "Status", "", new Vector2(.05f, .105f), new Vector2(.95f, .18f), 19f, TextAlignmentOptions.MidlineLeft);
        if (slotScroll == null)
        {
            var existing = box.Find("Slot List");
            if (existing != null) slotScroll = existing.GetComponent<ScrollRect>();
            if (slotScroll == null)
            {
                var list = new GameObject("Slot List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                Rect(list, box, new Vector2(.045f, .2f), new Vector2(.955f, .865f));
                list.GetComponent<Image>().color = Color.clear;
                slotScroll = list.GetComponent<ScrollRect>();
                slotScroll.viewport = (RectTransform)list.transform;
                slotScroll.horizontal = false;
                slotScroll.movementType = ScrollRect.MovementType.Clamped;
                slotScroll.scrollSensitivity = 30f;
            }
        }
        if (slotContent == null) slotContent = slotScroll.content;
        if (slotContent == null)
        {
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            slotContent = Rect(content, slotScroll.viewport != null ? slotScroll.viewport : slotScroll.transform, new Vector2(0, 1), Vector2.one);
            slotContent.pivot = new Vector2(.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = rowSpacing;
            layout.padding = new RectOffset(18, 18, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            slotScroll.content = slotContent;
        }
        if (slotTemplate == null)
        {
            var existing = slotContent.Find("Template");
            if (existing != null) slotTemplate = existing.GetComponent<Button>();
            if (slotTemplate == null)
            {
                slotTemplate = Button(slotContent, "Template", "", Vector2.zero, Vector2.one, null);
                var size = slotTemplate.gameObject.AddComponent<LayoutElement>();
                size.preferredHeight = rowHeight; size.minHeight = rowHeight;
                var unused = slotTemplate.transform.Find("Label");
                if (unused != null) unused.gameObject.SetActive(false);
            }
        }
        var previewRow = EnsureRow(slotTemplate);
        if (!Application.isPlaying)
        {
            if (string.IsNullOrEmpty(previewRow.date.text)) previewRow.date.text = "02/11/2026  12:00:00";
            if (string.IsNullOrEmpty(previewRow.location.text)) previewRow.location.text = "Điểm nghỉ";
            if (string.IsNullOrEmpty(previewRow.playCaption.text)) previewRow.playCaption.text = "Thời gian chơi";
            if (string.IsNullOrEmpty(previewRow.playTime.text)) previewRow.playTime.text = "01:02:37";
        }
        slotTemplate.gameObject.SetActive(false);
        if (backButton == null) backButton = ExistingButton(box, "Back") ?? Button(box, "Back", "", new Vector2(.67f, .025f), new Vector2(.95f, .1f), null);
        if (confirmationPanel == null)
        {
            var existing = menu.Find("Save Slot Confirmation");
            confirmationPanel = existing != null ? existing.gameObject : Panel(menu, "Save Slot Confirmation", Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, .72f));
        }
        var confirmBox = ChildPanel(confirmationPanel.transform, "Confirmation Panel", new Vector2(.27f, .32f), new Vector2(.73f, .68f), Color.white);
        var confirmImage = confirmBox.GetComponent<Image>();
        if (confirmImage != null && confirmImage.sprite == null)
        {
            confirmImage.sprite = confirmationBackground;
            confirmImage.color = confirmationBackground != null ? Color.white : new Color(.025f, .025f, .025f, 1f);
        }
        if (confirmationQuestion == null) confirmationQuestion = Text(confirmBox, "Question", "", new Vector2(.08f, .66f), new Vector2(.92f, .92f), 32f, TextAlignmentOptions.Center);
        if (confirmationDetails == null) confirmationDetails = Text(confirmBox, "Details", "", new Vector2(.08f, .36f), new Vector2(.92f, .66f), 20f, TextAlignmentOptions.Center);
        if (yesButton == null) yesButton = ExistingButton(confirmBox, "Yes") ?? Button(confirmBox, "Yes", "", new Vector2(.09f, .1f), new Vector2(.46f, .29f), null);
        if (noButton == null) noButton = ExistingButton(confirmBox, "No") ?? Button(confirmBox, "No", "", new Vector2(.54f, .1f), new Vector2(.91f, .29f), null);
        ConfigureTextHover(yesButton);
        ConfigureTextHover(noButton);
        ConfigureTextHover(backButton);
        yesButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = noButton, selectOnDown = noButton, selectOnLeft = noButton, selectOnRight = noButton };
        noButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = yesButton, selectOnDown = yesButton, selectOnLeft = yesButton, selectOnRight = yesButton };
        if (!Application.isPlaying)
        {
            PreviewText(title, "LƯU GAME");
            PreviewText(backButton.GetComponentInChildren<TMP_Text>(true), "Trở lại");
            Set(confirmationQuestion, "Lưu game");
            PreviewText(confirmationDetails, "02/11/2026  12:00:00\nThời gian chơi: 01:02:37");
            PreviewText(yesButton.GetComponentInChildren<TMP_Text>(true), "Có");
            PreviewText(noButton.GetComponentInChildren<TMP_Text>(true), "Không");
            slotsPanel.SetActive(false); confirmationPanel.SetActive(false);
        }
    }

    public void Open(bool forSaving)
    {
        if (session == null || canvasRoot == null || !session.OnMap || session.Busy || slotsPanel == null) return;
        if (forSaving && (session.Menu != AdventureMenu.Rest || !session.CanUpgrade)) return;
        saving = forSaving; openingMenu = session.Menu; committing = false; selectedInfo = null; lastFocused = null;
        confirmationPanel.SetActive(false);
        listGroup.interactable = listGroup.blocksRaycasts = true;
        slotsPanel.SetActive(true); slotsPanel.transform.SetAsLastSibling();
        RefreshRows();
        Canvas.ForceUpdateCanvases();
        if (slotScroll != null) slotScroll.verticalNormalizedPosition = 1f;
        FocusFirstRow();
    }

    public void Close()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            ((slotsPanel != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(slotsPanel.transform)) ||
             (confirmationPanel != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(confirmationPanel.transform))))
            EventSystem.current.SetSelectedGameObject(null);
        if (slotsPanel != null) slotsPanel.SetActive(false);
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (listGroup != null) listGroup.interactable = listGroup.blocksRaycasts = true;
        selectedInfo = null; selectedSlot = 0; committing = false; lastFocused = null;
    }

    public bool HandleBack()
    {
        if (!IsOpen) return false;
        if (confirmationPanel != null && confirmationPanel.activeSelf) CancelConfirmation();
        else Close();
        return true;
    }

    void OnEnable() => GameLocalization.LanguageChanged += RefreshLanguage;
    void OnDisable() { GameLocalization.LanguageChanged -= RefreshLanguage; Close(); }
    void Update()
    {
        if (!IsOpen) return;
        if (session == null || !session.OnMap || session.Busy || LoadingScreen.IsLoading || session.Menu != openingMenu) { Close(); return; }
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != lastFocused)
        {
            lastFocused = selected;
            if (selected != null && selected.transform.IsChildOf(slotContent)) ScrollSelectionIntoView((RectTransform)selected.transform);
        }
    }
    void RefreshLanguage()
    {
        if (!IsOpen || session == null) return;
        RefreshRows();
        if (confirmationPanel.activeSelf) ShowConfirmationText();
    }

    void RefreshRows()
    {
        var entries = session.GetSaveSlots();
        int count = Mathf.Clamp(session.SaveSlotCount, 1, 99);
        while (rows.Count < count)
        {
            int slot = rows.Count + 1;
            var button = Instantiate(slotTemplate, slotContent); button.name = "Slot " + slot.ToString("D2");
            var row = EnsureRow(button);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SelectSlot(slot));
            rows.Add(row);
        }
        bool anyLoadable = false;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]; row.button.gameObject.SetActive(i < count);
            if (i >= count) continue;
            CampaignSaveSlotInfo info = null;
            foreach (var entry in entries) if (entry != null && entry.slot == i + 1) { info = entry; break; }
            info = info ?? new CampaignSaveSlotInfo { slot = i + 1, isEmpty = true };
            bool valid = !info.isEmpty && !info.isCorrupt;
            anyLoadable |= valid;
            row.button.interactable = saving || valid;
            row.group.alpha = saving || valid ? 1f : .48f;
            Set(row.date, info.isEmpty ? Pick("Ô lưu trống", "Empty slot") : info.isCorrupt ? Pick("Bản lưu không đọc được", "Unreadable save") : FormatDate(info));
            Set(row.location, info.isEmpty ? "" : !string.IsNullOrWhiteSpace(info.locationName) ? info.locationName : info.mapScene ?? "");
            Set(row.playCaption, valid ? Pick("Thời gian chơi", "Play Time") : "");
            Set(row.playTime, valid ? FormatPlayTime(info.playTimeSeconds) : "—");
        }
        Set(title, saving ? Pick("LƯU GAME", "SAVE GAME") : Pick("TẢI GAME", "LOAD GAME"));
        Set(backButton.GetComponentInChildren<TMP_Text>(true), Pick("Trở lại", "Back"));
        Set(statusText, !saving && !anyLoadable ? Pick("Chưa có bản lưu để tải.", "There are no saves to load.") : "");
        ConfigureListNavigation();
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(slotContent))
        {
            var selectable = selected.GetComponent<Selectable>();
            if (selectable == null || !selectable.IsInteractable()) FocusFirstRow();
        }
    }

    void SelectSlot(int slot)
    {
        if (!IsOpen || committing) return;
        selectedInfo = null;
        foreach (var entry in session.GetSaveSlots()) if (entry != null && entry.slot == slot) { selectedInfo = entry; break; }
        selectedInfo = selectedInfo ?? new CampaignSaveSlotInfo { slot = slot, isEmpty = true };
        if (!saving && (selectedInfo.isEmpty || selectedInfo.isCorrupt)) return;
        selectedSlot = slot;
        listGroup.interactable = listGroup.blocksRaycasts = false;
        ShowConfirmationText();
        confirmationPanel.SetActive(true); confirmationPanel.transform.SetAsLastSibling();
        yesButton.interactable = noButton.interactable = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }
    void ShowConfirmationText()
    {
        if (selectedInfo == null) return;
        Set(confirmationQuestion, saving ? Pick("Lưu game", "Save Game") : Pick("Tải game", "Load Game"));
        Set(confirmationDetails, selectedInfo.isEmpty ? "" : selectedInfo.isCorrupt
            ? Pick("Bản lưu hiện tại không đọc được. Dữ liệu sẽ được thay thế.", "This save is unreadable. Its data will be replaced.")
            : FormatDate(selectedInfo) + "\n" + Pick("Thời gian chơi: ", "Play Time: ") + FormatPlayTime(selectedInfo.playTimeSeconds));
        Set(yesButton.GetComponentInChildren<TMP_Text>(true), Pick("Có", "Yes"));
        Set(noButton.GetComponentInChildren<TMP_Text>(true), Pick("Không", "No"));
    }
    void CancelConfirmation()
    {
        if (committing) return;
        confirmationPanel.SetActive(false);
        listGroup.interactable = listGroup.blocksRaycasts = true;
        if (selectedSlot > 0 && selectedSlot <= rows.Count && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(rows[selectedSlot - 1].button.gameObject);
    }
    void Confirm()
    {
        if (!IsOpen || committing || selectedSlot < 1 || selectedInfo == null || session == null) return;
        committing = true;
        yesButton.interactable = noButton.interactable = false;
        string error;
        bool success = saving ? session.TrySaveToSlot(selectedSlot, out error) : session.TryLoadFromSlot(selectedSlot, out error);
        committing = false;
        if (success) { Close(); return; }
        yesButton.interactable = noButton.interactable = true;
        CancelConfirmation();
        RefreshRows();
        Set(statusText, string.IsNullOrWhiteSpace(error) ? Pick("Không thể thực hiện. Vui lòng thử lại.", "The operation failed. Please try again.") : error);
    }

    Row EnsureRow(Button button)
    {
        var oldName = button.transform.Find("Slot Name");
        if (oldName != null)
        {
            oldName.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(oldName.gameObject);
            else DestroyImmediate(oldName.gameObject);
        }
        var group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        var row = new Row {
            button = button, group = group,
            date = Text(button.transform, "Saved At", "", new Vector2(.055f, .27f), new Vector2(.7f, .7f), 25f, TextAlignmentOptions.MidlineLeft),
            location = Text(button.transform, "Location", "", new Vector2(.055f, .06f), new Vector2(.7f, .32f), 17f, TextAlignmentOptions.MidlineLeft),
            playCaption = Text(button.transform, "Play Time Label", "", new Vector2(.72f, .58f), new Vector2(.945f, .87f), 17f, TextAlignmentOptions.Center),
            playTime = Text(button.transform, "Play Time", "", new Vector2(.72f, .18f), new Vector2(.945f, .6f), 30f, TextAlignmentOptions.Center)
        };
        ConfigureTextHover(button);
        return row;
    }

    static void ConfigureTextHover(Button button)
    {
        if (button == null) return;
        var scale = button.GetComponent<SmoothButtonHoverScale>();
        if (scale != null)
        {
            scale.enabled = false;
            if (Application.isPlaying) Destroy(scale);
            else DestroyImmediate(scale);
        }
        // Keep the background still; only text responds to pointer hover.
        button.transition = Selectable.Transition.None;
        if (button.targetGraphic != null) button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
        var hover = button.GetComponent<DialogueChoiceTextHover>();
        if (hover == null) hover = button.gameObject.AddComponent<DialogueChoiceTextHover>();
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            hover.Configure(label, new Color(.5f, .5f, .5f, 1f), Color.white);
    }
    void FocusFirstRow()
    {
        if (EventSystem.current == null) return;
        foreach (var row in rows)
            if (row.button.gameObject.activeSelf && row.button.interactable) { EventSystem.current.SetSelectedGameObject(row.button.gameObject); return; }
        EventSystem.current.SetSelectedGameObject(backButton.gameObject);
    }
    void ConfigureListNavigation()
    {
        var enabledRows = new List<Button>();
        foreach (var row in rows) if (row.button.gameObject.activeSelf && row.button.interactable) enabledRows.Add(row.button);
        for (int i = 0; i < enabledRows.Count; i++)
            enabledRows[i].navigation = new Navigation {
                mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? enabledRows[i - 1] : backButton,
                selectOnDown = i + 1 < enabledRows.Count ? enabledRows[i + 1] : backButton
            };
        backButton.navigation = new Navigation {
            mode = Navigation.Mode.Explicit,
            selectOnUp = enabledRows.Count > 0 ? enabledRows[enabledRows.Count - 1] : backButton,
            selectOnDown = enabledRows.Count > 0 ? enabledRows[0] : backButton
        };
    }
    void ScrollSelectionIntoView(RectTransform row)
    {
        if (slotScroll == null || slotScroll.viewport == null || slotContent == null) return;
        Canvas.ForceUpdateCanvases();
        var viewport = slotScroll.viewport;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
        float offset = bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y
            : bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y : 0f;
        if (Mathf.Abs(offset) < .01f) return;
        slotScroll.StopMovement();
        var position = slotContent.anchoredPosition;
        position.y = Mathf.Clamp(position.y + offset, 0f, Mathf.Max(0f, slotContent.rect.height - viewport.rect.height));
        slotContent.anchoredPosition = position;
    }
    string Pick(string vietnamese, string english) => IsEnglish ? english : vietnamese;
    static void PreviewText(TMP_Text label, string text) { if (label != null && string.IsNullOrEmpty(label.text)) label.text = text; }
    static void Set(TMP_Text label, string text) { if (label != null) GameLocalization.SetText(label, text ?? ""); }
    string FormatDate(CampaignSaveSlotInfo info) => info.SavedAtLocal?.ToString(IsEnglish ? "MMM dd, yyyy  h:mm:ss tt" : "dd/MM/yyyy  HH:mm:ss", CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "vi-VN")) ?? "—";
    public static string FormatPlayTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d) seconds = 0d;
        long whole = (long)Math.Min(seconds, 31557600000d);
        return (whole / 3600).ToString("D2") + ":" + (whole / 60 % 60).ToString("D2") + ":" + (whole % 60).ToString("D2");
    }
    static RectTransform Rect(GameObject obj, Transform parent, Vector2 min, Vector2 max)
    {
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    static Button ExistingButton(Transform parent, string name) => parent.Find(name)?.GetComponent<Button>();
    Transform ChildPanel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var existing = parent.Find(name);
        return existing != null ? existing : Panel(parent, name, min, max, color).transform;
    }
    static GameObject Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        Rect(panel, parent, min, max); panel.GetComponent<Image>().color = color;
        return panel;
    }
    TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment)
    {
        var existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent<TMP_Text>(out var authored)) return authored;
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Rect(obj, parent, min, max);
        var text = obj.GetComponent<TMP_Text>();
        if (fontSource != null) text.font = fontSource.font;
        text.fontSize = size; text.color = Color.white; text.alignment = alignment;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false; text.text = value;
        return text;
    }
    Button Button(Transform parent, string name, string value, Vector2 min, Vector2 max, UnityAction action)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        Rect(obj, parent, min, max);
        var image = obj.GetComponent<Image>(); image.sprite = rowBackground;
        image.color = rowBackground != null ? Color.white : new Color(.07f, .075f, .085f, 1f);
        var button = obj.GetComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.selectedColor = new Color(1.12f, 1.12f, 1.12f, 1f); colors.pressedColor = new Color(.8f, .8f, .8f, 1f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        Text(obj.transform, "Label", value, new Vector2(.08f, .08f), new Vector2(.92f, .92f), 23f, TextAlignmentOptions.Center);
        ConfigureTextHover(button);
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
}
