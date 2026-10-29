using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Applies pending attribute points only after the confirm button is held.</summary>
[DisallowMultipleComponent]
public sealed class HoldToConfirmButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Min(.2f)] float holdDuration = .85f;

    Button button;
    CanvasGroup buttonGroup;
    UIButtonWipeReveal reveal;
    UIHoverOutline outline;
    HoldProgressRingGraphic ring;
    GameObject badge;
    Action confirmed;
    float progress;
    bool available;
    bool pointerHeld;
    bool wasHeld;
    bool completedThisHold;

    public void Bind(Action callback)
    {
        button = GetComponent<Button>();
        buttonGroup = GetComponent<CanvasGroup>();
        if (buttonGroup == null) buttonGroup = gameObject.AddComponent<CanvasGroup>();
        reveal = GetComponent<UIButtonWipeReveal>();
        outline = GetComponentInChildren<UIHoverOutline>(true);
        confirmed = callback;
        EnsureBadge();
        SetAvailable(false);
    }

    public void SetAvailable(bool value)
    {
        available = value;
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.interactable = value;
        if (buttonGroup == null) buttonGroup = GetComponent<CanvasGroup>();
        if (buttonGroup != null)
        {
            buttonGroup.alpha = value ? 1f : .38f;
            buttonGroup.interactable = value;
            buttonGroup.blocksRaycasts = true;
        }
        if (reveal == null) reveal = GetComponent<UIButtonWipeReveal>();
        reveal?.SetForcedReveal(value);
        if (outline == null) outline = GetComponentInChildren<UIHoverOutline>(true);
        outline?.SetSelected(value);
        // Keep the key/ring badge visible as an affordance, dimming it together
        // with the button until there are pending points to apply.
        if (badge != null) badge.SetActive(true);
        if (!value) ResetHold();
    }

    void EnsureBadge()
    {
        if (badge != null) return;
        Transform existing = transform.Find("Hold Badge");
        if (existing != null)
        {
            badge = existing.gameObject;
            ring = existing.GetComponentInChildren<HoldProgressRingGraphic>(true);
            return;
        }

        badge = new GameObject("Hold Badge", typeof(RectTransform));
        badge.transform.SetParent(transform, false);
        badge.transform.SetAsLastSibling();
        var badgeRect = (RectTransform)badge.transform;
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(.5f, 0f);
        badgeRect.pivot = new Vector2(.5f, .5f);
        badgeRect.anchoredPosition = new Vector2(0f, -27f);
        badgeRect.sizeDelta = new Vector2(44f, 44f);

        var ringObject = new GameObject("Hold Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(HoldProgressRingGraphic));
        ringObject.transform.SetParent(badge.transform, false);
        var ringRect = (RectTransform)ringObject.transform;
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = ringRect.offsetMax = Vector2.zero;
        ring = ringObject.GetComponent<HoldProgressRingGraphic>();
        ring.raycastTarget = false;

        var labelObject = new GameObject("Hold Key", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(badge.transform, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        TMP_Text source = GetComponentInChildren<TMP_Text>(true);
        if (source != null)
        {
            label.font = source.font;
            label.fontSharedMaterial = source.fontSharedMaterial;
            label.fontSize = Mathf.Clamp(source.fontSize, 15f, 20f);
            label.color = new Color(.94f, .86f, .68f, 1f);
        }
        label.text = "A";
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        badge.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!available || (button != null && !button.interactable)) return;
        pointerHeld = true;
    }

    public void OnPointerUp(PointerEventData eventData) => pointerHeld = false;

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData != null && eventData.pointerPress == gameObject) pointerHeld = false;
    }

    void Update()
    {
        bool keyboardHeld = available && IsUpgradeKeyHeld();
        bool held = available && (pointerHeld || keyboardHeld);
        if (held)
        {
            if (!wasHeld) { progress = 0f; completedThisHold = false; }
            progress = Mathf.Min(1f, progress + Time.unscaledDeltaTime / holdDuration);
            if (ring != null) ring.SetProgress(progress);
            if (progress >= 1f && !completedThisHold)
            {
                completedThisHold = true;
                confirmed?.Invoke();
            }
        }
        else if (wasHeld)
        {
            progress = 0f;
            completedThisHold = false;
            if (ring != null) ring.SetProgress(0f);
        }
        wasHeld = held;
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
        pointerHeld = false;
        wasHeld = false;
        completedThisHold = false;
        progress = 0f;
        if (ring != null) ring.SetProgress(0f);
    }

    void OnDisable() => ResetHold();
}
