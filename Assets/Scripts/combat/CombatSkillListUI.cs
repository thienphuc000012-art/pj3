using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shared skill-description panel and authored template for four skills + ultimate.
[DisallowMultipleComponent]
public sealed class CombatSkillListUI : MonoBehaviour
{
    [Header("Canvas references")]
    public GameObject presentationRoot;
    public RectTransform rowsRoot;
    public ActionButtonUI rowTemplate;
    public Image descriptionBackground;
    public TMP_Text descriptionText;

    CombatSkillRowUI focusedRow;
    public CombatSkillRowUI FocusedRow => focusedRow;

    public void EnsureHierarchy(AdvancedUIManager manager)
    {
        if (manager == null || manager.subMenuPanel == null) return;
        if (presentationRoot == null)
        {
            var existing = manager.subMenuPanel.transform.Find("Skill Presentation");
            presentationRoot = existing != null ? existing.gameObject : CreateRect("Skill Presentation", manager.subMenuPanel.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        }
        if (rowsRoot == null)
        {
            rowsRoot = presentationRoot.transform.Find("Rows") as RectTransform;
            if (rowsRoot == null) rowsRoot = CreateRect("Rows", presentationRoot.transform,
                new Vector2(.035f, .30f), new Vector2(.965f, .98f), Vector2.zero, Vector2.zero);
        }
        if (descriptionBackground == null)
        {
            var existing = presentationRoot.transform.Find("Description Panel");
            descriptionBackground = existing != null ? existing.GetComponent<Image>() : null;
            if (descriptionBackground == null)
            {
                var panel = CreateRect("Description Panel", presentationRoot.transform,
                    new Vector2(.035f, .025f), new Vector2(.965f, .27f), Vector2.zero, Vector2.zero);
                descriptionBackground = panel.gameObject.AddComponent<Image>();
                descriptionBackground.color = new Color(.012f, .016f, .023f, .9f);
            }
        }
        descriptionBackground.raycastTarget = false;
        if (descriptionText == null)
        {
            var existing = descriptionBackground.transform.Find("Description");
            descriptionText = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (descriptionText == null)
            {
                var textRect = CreateRect("Description", descriptionBackground.transform,
                    Vector2.zero, Vector2.one, new Vector2(20f, 14f), new Vector2(-20f, -14f));
                descriptionText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
                var prototype = manager.actionButtonPrefab != null ? manager.actionButtonPrefab.GetComponent<ActionButtonUI>() : null;
                if (prototype != null && prototype.nameText != null) descriptionText.font = prototype.nameText.font;
                descriptionText.fontSize = 24f;
                descriptionText.enableAutoSizing = true;
                descriptionText.fontSizeMin = 17f;
                descriptionText.fontSizeMax = 24f;
                descriptionText.alignment = TextAlignmentOptions.TopLeft;
                descriptionText.textWrappingMode = TextWrappingModes.Normal;
                descriptionText.overflowMode = TextOverflowModes.Ellipsis;
                descriptionText.color = Color.white;
                descriptionText.text = "";
            }
        }
        descriptionText.raycastTarget = false;
        if (rowTemplate == null)
        {
            var existing = rowsRoot.Find("Skill Row Template");
            if (existing != null) rowTemplate = existing.GetComponent<ActionButtonUI>();
            if (rowTemplate == null && manager.actionButtonPrefab != null)
            {
                var obj = Instantiate(manager.actionButtonPrefab, rowsRoot, false);
                obj.name = "Skill Row Template";
                rowTemplate = obj.GetComponent<ActionButtonUI>();
                if (rowTemplate != null) ConfigureNewTemplate(rowTemplate);
            }
        }
        if (rowTemplate != null)
        {
            var row = rowTemplate.GetComponent<CombatSkillRowUI>();
            if (row == null) row = rowTemplate.gameObject.AddComponent<CombatSkillRowUI>();
            row.EnsureVisuals(rowTemplate);
            if (rowTemplate.infoText != null) rowTemplate.infoText.gameObject.SetActive(false);
            rowTemplate.gameObject.SetActive(false);
        }
    }

    static void ConfigureNewTemplate(ActionButtonUI action)
    {
        // This runs only for a freshly created template. Rebinding existing
        // Canvas elements never changes their typography or RectTransforms.
        foreach (var effect in action.GetComponents<CombatActionButtonFeedback>()) RemoveEffect(effect);
        foreach (var effect in action.GetComponents<BattleMenuButtonSlide>()) RemoveEffect(effect);
        foreach (var effect in action.GetComponents<SmoothButtonHoverScale>()) RemoveEffect(effect);
        foreach (var effect in action.GetComponents<UIButtonWipeReveal>()) RemoveEffect(effect);
        foreach (var effect in action.GetComponents<DynamicButtonUI>()) RemoveEffect(effect);
        foreach (var effect in action.GetComponentsInChildren<UIHoverOutline>(true)) RemoveEffect(effect);
        foreach (var effect in action.GetComponentsInChildren<GameUIFrameGraphic>(true)) RemoveEffect(effect);
        var fitter = action.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;
        var rect = (RectTransform)action.transform;
        CombatSkillRowUI.SetRect(rect, new Vector2(0f, .8f), Vector2.one, new Vector2(0f, 5f), new Vector2(0f, -5f));
        foreach (var child in action.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child == action.transform) continue;
            if (child.name == "Combat Banner Background" || child.name == "Action Ribbon" || child.name == "Ribbon Sweep" ||
                child.name == "Press Flash" || child.name == "Focus Diamond" || child.name == "Wipe Reveal" || child.name == "Wipe Reveal Edge")
            { child.gameObject.SetActive(false); if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject); }
        }
        var row = action.GetComponent<CombatSkillRowUI>();
        if (row == null) row = action.gameObject.AddComponent<CombatSkillRowUI>();
        row.EnsureVisuals(action);
        if (row.focusBackground != null) row.focusBackground.gameObject.SetActive(false);
        if (action.infoText != null) action.infoText.gameObject.SetActive(false);
        if (action.iconImage != null)
        {
            CombatSkillRowUI.SetRect(action.iconImage.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f),
                new Vector2(12f, -23f), new Vector2(58f, 23f));
            action.iconImage.preserveAspect = true;
        }
        if (action.nameText != null)
        {
            CombatSkillRowUI.SetRect(action.nameText.rectTransform, Vector2.zero, new Vector2(.65f, 1f),
                new Vector2(74f, 5f), new Vector2(-10f, -5f));
            Style(action.nameText, 25f, 17f, TextAlignmentOptions.MidlineLeft);
        }
        if (action.resourceText != null)
        {
            CombatSkillRowUI.SetRect(action.resourceText.rectTransform, new Vector2(.65f, 0f), new Vector2(.80f, 1f),
                new Vector2(0f, 5f), new Vector2(-3f, -5f));
            Style(action.resourceText, 27f, 16f, TextAlignmentOptions.MidlineRight);
        }
        if (row.resourceLabel != null) Style(row.resourceLabel, 19f, 14f, TextAlignmentOptions.MidlineLeft);
    }

    static void Style(TMP_Text text, float size, float minimum, TextAlignmentOptions alignment)
    {
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimum;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = alignment;
        text.margin = Vector4.zero;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    static void RemoveEffect(Behaviour effect)
    {
        effect.enabled = false;
        if (Application.isPlaying) Destroy(effect);
        else DestroyImmediate(effect);
    }

    public void SetVisible(bool visible)
    {
        if (!visible) ClearFocus();
        if (presentationRoot != null) presentationRoot.SetActive(visible);
    }

    public void Focus(CombatSkillRowUI row)
    {
        if (row == null || !row.HasAction || !row.gameObject.activeInHierarchy) return;
        if (focusedRow != null && focusedRow != row) focusedRow.SetFocused(false);
        focusedRow = row;
        row.SetFocused(true);
        if (descriptionText != null)
        {
            var action = row.Source.CurrentAction;
            GameLocalization.SetText(descriptionText, !string.IsNullOrWhiteSpace(action.description)
                ? VietnameseGameUIText.ActionDescription(action) : "Chưa có mô tả kỹ năng.");
        }
    }

    public void Release(CombatSkillRowUI row)
    {
        if (focusedRow != row) return;
        focusedRow = null;
        if (row != null) row.SetFocused(false);
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var navigationRow = selected != null ? selected.GetComponent<CombatSkillRowUI>() : null;
        if (navigationRow != null && navigationRow.gameObject.activeInHierarchy && navigationRow.HasNavigationFocus &&
            rowsRoot != null && navigationRow.transform.IsChildOf(rowsRoot)) Focus(navigationRow);
    }

    public void FocusFirstRow()
    {
        if (rowsRoot == null) return;
        CombatSkillRowUI first = null;
        foreach (var row in rowsRoot.GetComponentsInChildren<CombatSkillRowUI>())
        {
            if (!row.HasAction) continue;
            if (first == null) first = row;
            var button = row.GetComponent<Button>();
            if (button != null && button.IsInteractable()) { first = row; break; }
        }
        if (first == null) { ClearFocus(); return; }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        first.SetNavigationFocus();
    }

    public void ClearFocus()
    {
        var old = focusedRow;
        focusedRow = null;
        if (old != null) old.SetFocused(false);
        if (descriptionText != null) GameLocalization.SetText(descriptionText, "");
    }

    // The authored Canvas may be visible while editing. Runtime visibility is
    // opt-in through PopulateSkillSubMenu, never inherited from the saved scene.
    void Awake() => SetVisible(false);
    void OnDisable() => SetVisible(false);

    static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        CombatSkillRowUI.SetRect(rect, min, max, offsetMin, offsetMax);
        return rect;
    }
}
