using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Confirms an upgrade after a sustained press and draws progress around the control.</summary>
[DisallowMultipleComponent]
public sealed class HoldToConfirmButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Min(.2f)] float holdDuration = .85f;

    static readonly Color ReadyBorder = new Color(.76f, .69f, .55f, .55f);
    static readonly Color ProgressBorder = new Color(1f, .91f, .73f, 1f);

    Button button;
    CanvasGroup buttonGroup;
    HoldProgressRingGraphic border;
    Action confirmed;
    Action selectSkill;
    Func<bool> canUnlockSkill;
    PointerEventData heldPointer;
    float progress;
    bool available, selected, wasHeld, completedThisHold;

    public void Bind(Action callback)
    {
        button = GetComponent<Button>();
        buttonGroup = GetComponent<CanvasGroup>();
        if (buttonGroup == null) buttonGroup = gameObject.AddComponent<CanvasGroup>();
        confirmed = callback;
        var plate = transform.Find("Skill Background Plate") as RectTransform;
        EnsureBadge(plate);
        border.ConfigureOutline(ReadyBorder, ProgressBorder);
        SetAvailable(false);
    }

    public void BindSkill(Action select, Func<bool> canUnlock, Action callback)
    {
        button = GetComponent<Button>();
        selectSkill = select;
        canUnlockSkill = canUnlock;
        confirmed = callback;
        EnsureBadge(null);
        border.ConfigureOutline(Color.clear, ProgressBorder);
    }

    public void SetAvailable(bool value)
    {
        available = value;
        button.interactable = value;
        buttonGroup.alpha = value ? 1f : .38f;
        buttonGroup.interactable = value;
        buttonGroup.blocksRaycasts = true;
        if (!value) ResetHold();
    }

    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;
        border.ConfigureOutline(selected ? ReadyBorder : Color.clear, ProgressBorder);
        if (!selected) ResetHold();
    }

    void EnsureBadge(RectTransform plate)
    {
        var badge = transform.Find("Hold Badge") as RectTransform;
        if (badge == null)
        {
            var go = new GameObject("Hold Badge", typeof(RectTransform));
            badge = (RectTransform)go.transform;
            badge.SetParent(transform, false);
        }
        foreach (Transform child in badge)
            if (child.name == "Hold Key" || child.name == "Hold Ring") child.gameObject.SetActive(false);
        badge.anchorMin = plate != null ? plate.anchorMin : Vector2.zero;
        badge.anchorMax = plate != null ? plate.anchorMax : Vector2.one;
        Vector2 padding = plate != null ? Vector2.one * 2f : Vector2.zero;
        badge.offsetMin = (plate != null ? plate.offsetMin : Vector2.zero) - padding;
        badge.offsetMax = (plate != null ? plate.offsetMax : Vector2.zero) + padding;
        badge.SetAsLastSibling();
        border = badge.GetComponent<HoldProgressRingGraphic>();
        if (border == null) border = badge.gameObject.AddComponent<HoldProgressRingGraphic>();
        border.raycastTarget = false;
        badge.gameObject.SetActive(true);
    }

    bool CanConfirm() => button != null && button.IsInteractable() &&
        (canUnlockSkill != null ? canUnlockSkill() : available);

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || heldPointer != null ||
            button == null || !button.IsInteractable()) return;
        selectSkill?.Invoke();
        if (CanConfirm()) heldPointer = eventData;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == heldPointer) ResetHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData == heldPointer) ResetHold();
    }

    void Update()
    {
        bool keyboardHeld = canUnlockSkill == null && available && IsUpgradeKeyHeld();
        bool pointerHeld = heldPointer != null && !heldPointer.dragging;
        if ((!pointerHeld && !keyboardHeld) || !CanConfirm())
        {
            if (wasHeld || heldPointer != null) ResetHold();
            return;
        }

        wasHeld = true;
        if (completedThisHold) return;
        progress = Mathf.Min(1f, progress + Time.unscaledDeltaTime / holdDuration);
        border.SetProgress(progress);
        if (progress < 1f) return;

        completedThisHold = true;
        confirmed?.Invoke();
    }

    static bool IsUpgradeKeyHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.aKey.isPressed;
#else
        return Input.GetKey(KeyCode.A);
#endif
    }

    void ResetHold()
    {
        heldPointer = null;
        wasHeld = false;
        completedThisHold = false;
        if (progress == 0f) return;
        progress = 0f;
        if (border != null) border.SetProgress(0f);
    }

    void OnDisable() => ResetHold();
}
