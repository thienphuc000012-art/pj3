using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Skill-only presentation. The hit area, icon, name and resource word stay still.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class CombatSkillRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, IMoveHandler, ISubmitHandler
{
    [Header("Skill row visuals")]
    public Image focusBackground;
    public TMP_Text resourceLabel;
    [ColorUsage(false)] public Color focusTint = new Color(1f, 1f, 1f, .98f);
    [Header("Focused motion")]
    [Range(0f, .5f)] public float backgroundScaleAmount = .035f;
    [Range(0f, .5f)] public float numberScaleAmount = .10f;
    [Min(0f)] public float pulseCyclesPerSecond = .9f;
    [Range(0f, 1f)] public float unavailableAlpha = .5f;

    CombatSkillListUI owner;
    ActionButtonUI source;
    Button button;
    CanvasGroup rowGroup;
    Vector2 backgroundOrigin, numberOrigin;
    Vector3 backgroundScale = Vector3.one, numberScale = Vector3.one;
    bool captured, focused, pointerInside, navigationFocused;
    float focusStarted;
    float transitionAlpha = 1f;
    bool transitionInputEnabled = true;
    string previousNumber, previousLabel;

    public bool IsFocused => focused;
    public ActionButtonUI Source => source != null ? source : GetComponent<ActionButtonUI>();
    public bool HasAction => Source != null && Source.CurrentAction != null;
    public bool HasNavigationFocus => navigationFocused && EventSystem.current != null &&
        EventSystem.current.currentSelectedGameObject == gameObject;
    public bool TransitionInputEnabled => transitionInputEnabled;

    // Keep transition opacity separate from resource availability opacity.
    // Otherwise this row's LateUpdate would overwrite the entrance fade.
    public void SetTransitionAlpha(float value)
    {
        transitionAlpha = Mathf.Clamp01(value);
        ApplyOpacity();
    }

    public void SetTransitionInputEnabled(bool value)
    {
        transitionInputEnabled = value;
        if (!value)
        {
            pointerInside = navigationFocused = false;
            if (owner != null) owner.Release(this);
            SetFocused(false);
        }
    }

    void ApplyOpacity()
    {
        if (rowGroup != null)
            rowGroup.alpha = transitionAlpha * (button != null && !button.interactable ? unavailableAlpha : 1f);
    }

    public void Bind(CombatSkillListUI list, ActionButtonUI action)
    {
        RestoreMotion();
        owner = list;
        source = action;
        button = GetComponent<Button>();
        EnsureVisuals(action);
        if (source != null && source.infoText != null) source.infoText.gameObject.SetActive(false);
        rowGroup = GetComponent<CanvasGroup>();
        if (rowGroup == null) rowGroup = gameObject.AddComponent<CanvasGroup>();
        backgroundOrigin = focusBackground != null ? focusBackground.rectTransform.anchoredPosition : Vector2.zero;
        numberOrigin = source != null && source.resourceText != null ? source.resourceText.rectTransform.anchoredPosition : Vector2.zero;
        backgroundScale = focusBackground != null ? focusBackground.rectTransform.localScale : Vector3.one;
        numberScale = source != null && source.resourceText != null ? source.resourceText.rectTransform.localScale : Vector3.one;
        captured = true;
        pointerInside = navigationFocused = false;
        SetFocused(false);
        if (!HasAction) SetResource("", "");
    }

    // Creates missing visual elements only. Existing Canvas rects remain authored.
    public void EnsureVisuals(ActionButtonUI action)
    {
        if (action == null) return;
        if (focusBackground == null)
        {
            var existing = transform.Find("Focus Background");
            focusBackground = existing != null ? existing.GetComponent<Image>() : null;
            if (focusBackground == null)
            {
                var obj = new GameObject("Focus Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.layer = gameObject.layer;
                obj.transform.SetParent(transform, false);
                focusBackground = obj.GetComponent<Image>();
                SetRect(focusBackground.rectTransform, Vector2.zero, Vector2.one, new Vector2(-4f, 0f), new Vector2(4f, 0f));
                focusBackground.sprite = GameUITheme.CombatSubMenuBackgroundSprite;
                focusBackground.type = Image.Type.Simple;
                focusBackground.color = focusTint;
            }
        }
        focusBackground.raycastTarget = false;
        focusBackground.transform.SetAsFirstSibling();
        if (resourceLabel == null)
        {
            var existing = transform.Find("Resource Label");
            resourceLabel = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (resourceLabel == null)
            {
                var obj = new GameObject("Resource Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                obj.layer = gameObject.layer;
                obj.transform.SetParent(transform, false);
                resourceLabel = obj.GetComponent<TMP_Text>();
                if (action.resourceText != null) resourceLabel.font = action.resourceText.font;
                resourceLabel.fontSize = 19f;
                resourceLabel.alignment = TextAlignmentOptions.MidlineLeft;
                resourceLabel.textWrappingMode = TextWrappingModes.NoWrap;
                resourceLabel.overflowMode = TextOverflowModes.Ellipsis;
                resourceLabel.color = Color.white;
                SetRect(resourceLabel.rectTransform, new Vector2(.8f, 0f), Vector2.one, new Vector2(7f, 5f), new Vector2(-14f, -5f));
            }
        }
        resourceLabel.raycastTarget = false;
        if (action.nameText != null) action.nameText.raycastTarget = false;
        if (action.resourceText != null) action.resourceText.raycastTarget = false;
        if (action.iconImage != null) action.iconImage.raycastTarget = false;
        var hitArea = GetComponent<Image>();
        if (hitArea != null) { hitArea.color = Color.clear; hitArea.raycastTarget = true; }
        var rowButton = GetComponent<Button>();
        rowButton.transition = Selectable.Transition.None;
        if (hitArea != null) rowButton.targetGraphic = hitArea;
    }

    public void SetResource(string number, string label)
    {
        if (source == null) source = GetComponent<ActionButtonUI>();
        if (source != null && source.resourceText != null && previousNumber != number)
        {
            GameLocalization.SetText(source.resourceText, number ?? "");
            previousNumber = number;
        }
        if (resourceLabel != null && previousLabel != label)
        {
            GameLocalization.SetText(resourceLabel, label ?? "");
            previousLabel = label;
        }
    }

    public void SetFocused(bool value)
    {
        bool next = value && transitionInputEnabled && HasAction && isActiveAndEnabled;
        if (next && !focused) focusStarted = Time.unscaledTime;
        focused = next;
        if (!captured)
        {
            var action = Source;
            backgroundOrigin = focusBackground != null ? focusBackground.rectTransform.anchoredPosition : Vector2.zero;
            numberOrigin = action != null && action.resourceText != null ? action.resourceText.rectTransform.anchoredPosition : Vector2.zero;
            backgroundScale = focusBackground != null ? focusBackground.rectTransform.localScale : Vector3.one;
            numberScale = action != null && action.resourceText != null ? action.resourceText.rectTransform.localScale : Vector3.one;
            captured = true;
        }
        ApplyMotion(0f);
    }

    // Deterministic sampling also supports an Editor preview without Play Mode.
    public void ApplyMotion(float elapsed)
    {
        float wave = focused ? Mathf.Sin(elapsed * Mathf.PI * 2f * pulseCyclesPerSecond) : 0f;
        if (focusBackground != null)
        {
            focusBackground.gameObject.SetActive(focused);
            focusBackground.color = focusTint;
            focusBackground.rectTransform.anchoredPosition = backgroundOrigin;
            focusBackground.rectTransform.localScale = backgroundScale * (1f + wave * backgroundScaleAmount);
        }
        var action = Source;
        if (action != null && action.resourceText != null)
        {
            action.resourceText.rectTransform.anchoredPosition = numberOrigin;
            action.resourceText.rectTransform.localScale = numberScale * (1f + wave * numberScaleAmount);
        }
    }

    public void SetNavigationFocus()
    {
        if (!transitionInputEnabled) return;
        navigationFocused = true;
        if (owner != null) owner.Focus(this);
    }

    void LateUpdate()
    {
        if (!captured) return;
        ApplyMotion(Time.unscaledTime - focusStarted);
        ApplyOpacity();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!transitionInputEnabled || !HasAction || owner == null) return;
        pointerInside = true;
        navigationFocused = false;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject, eventData);
        owner.Focus(this);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (owner != null) owner.Release(this);
    }
    public void OnSelect(BaseEventData eventData)
    {
        if (!transitionInputEnabled) return;
        navigationFocused = eventData is AxisEventData;
        if ((pointerInside || navigationFocused) && owner != null) owner.Focus(this);
    }
    public void OnDeselect(BaseEventData eventData)
    {
        pointerInside = navigationFocused = false;
        if (owner != null) owner.Release(this);
    }
    public void OnMove(AxisEventData eventData)
    {
        if (!transitionInputEnabled) return;
        if (eventData.moveDir != MoveDirection.None && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            SetNavigationFocus();
    }
    public void OnSubmit(BaseEventData eventData)
    {
        if (!transitionInputEnabled) return;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject) SetNavigationFocus();
    }
    void OnDisable()
    {
        transitionAlpha = 1f;
        transitionInputEnabled = true;
        pointerInside = navigationFocused = false;
        if (owner != null) owner.Release(this);
        focused = false;
        RestoreMotion();
    }
    void RestoreMotion()
    {
        if (!captured) return;
        if (focusBackground != null)
        {
            focusBackground.rectTransform.anchoredPosition = backgroundOrigin;
            focusBackground.rectTransform.localScale = backgroundScale;
            focusBackground.gameObject.SetActive(false);
        }
        if (source != null && source.resourceText != null)
        {
            source.resourceText.rectTransform.anchoredPosition = numberOrigin;
            source.resourceText.rectTransform.localScale = numberScale;
        }
    }

    public static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
