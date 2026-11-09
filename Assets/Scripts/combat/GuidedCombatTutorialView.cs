using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Modal combat lesson with a spotlight cut out of the dimming layer.</summary>
[DisallowMultipleComponent]
public sealed class GuidedCombatTutorialView : MonoBehaviour, IPointerClickHandler, ISubmitHandler, ICancelHandler
{
    static readonly Color Ink = new Color(.035f, .031f, .029f, .98f);
    static readonly Color Gold = new Color(.87f, .73f, .49f);
    static readonly Color Ivory = new Color(.96f, .92f, .84f);
    static readonly Rect Full = new Rect(0f, 0f, 1f, 1f);

    readonly RectTransform[] shade = new RectTransform[4];
    readonly Vector3[] corners = new Vector3[4];
    Canvas sourceCanvas;
    RectTransform overlay;
    RectTransform spotlight;
    RectTransform page;
    TMP_Text counter;
    TMP_Text heading;
    TMP_Text content;
    TMP_Text footer;
    Button requiredButton;
    bool requiresAction;
    bool allowCancel;
    IReadOnlyList<RectTransform> focusTargets;
    Action onDismissed;
    GameObject previousSelection;
    int currentStep;
    int openedFrame;

    public bool IsOpen => isActiveAndEnabled;

    public static GuidedCombatTutorialView Create(Canvas canvas, TMP_FontAsset font)
    {
        if (canvas == null) return null;
        var root = new GameObject("Guided Combat Tutorial", typeof(RectTransform),
            typeof(Canvas), typeof(GraphicRaycaster), typeof(PreserveAuthoredUI));
        root.SetActive(false);
        root.transform.SetParent(canvas.rootCanvas.transform, false);
        var overlayCanvas = root.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.worldCamera = canvas.rootCanvas.worldCamera;
        overlayCanvas.sortingLayerID = canvas.rootCanvas.sortingLayerID;
        overlayCanvas.sortingOrder = (short)Mathf.Clamp(
            Mathf.Max(2400, canvas.rootCanvas.sortingOrder + 20), short.MinValue, short.MaxValue);
        var view = root.AddComponent<GuidedCombatTutorialView>();
        view.sourceCanvas = canvas.rootCanvas;
        view.overlay = (RectTransform)root.transform;
        SetRect(view.overlay, Full);
        view.Build(font != null ? font : TMP_Settings.defaultFontAsset);
        return view;
    }

    public void Show(int step, IReadOnlyList<RectTransform> focus, string title, string body,
        string continueLabel, Action dismissed, Button requiredButton = null, bool allowCancel = false)
    {
        if (!IsOpen && EventSystem.current != null)
            previousSelection = EventSystem.current.currentSelectedGameObject;

        focusTargets = focus;
        this.requiredButton = requiredButton;
        requiresAction = requiredButton != null;
        this.allowCancel = allowCancel;
        currentStep = step;
        counter.text = step.ToString("00") + "  /  07";
        heading.text = title;
        content.text = body;
        footer.text = continueLabel;
        onDismissed = dismissed;
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        UpdateLayout();
        EventSystem.current?.SetSelectedGameObject(gameObject);
    }

    public void Close()
    {
        onDismissed = null;
        focusTargets = null;
        requiredButton = null;
        requiresAction = false;
        allowCancel = false;
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
        {
            var selection = previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null;
            eventSystem.SetSelectedGameObject(selection);
        }
        previousSelection = null;
    }

    void Update()
    {
        if (Time.frameCount <= openedFrame) return;
        if (allowCancel && CancelPressed())
        {
            Dismiss();
            return;
        }
        if (SubmitPressed()) OnSubmit(null);
    }

    void LateUpdate() => UpdateLayout();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (allowCancel) Dismiss();
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (requiresAction)
        {
            if (!RequiredButtonAvailable()) return;
            var rect = (RectTransform)requiredButton.transform;
            var targetCanvas = requiredButton.GetComponentInParent<Canvas>();
            var rootCanvas = targetCanvas != null ? targetCanvas.rootCanvas : sourceCanvas;
            var camera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, camera)) return;
            if (eventData.pointerPress == gameObject &&
                !RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.pressPosition, camera)) return;
        }
        Dismiss();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!requiresAction || RequiredButtonAvailable()) Dismiss();
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!allowCancel) return;
        Dismiss();
        eventData?.Use();
    }

    bool RequiredButtonAvailable() => requiredButton != null && requiredButton.isActiveAndEnabled &&
        requiredButton.IsInteractable();

    void Dismiss()
    {
        if (!IsOpen || Time.frameCount <= openedFrame) return;
        var callback = onDismissed;
        Close();
        callback?.Invoke();
    }

    void Build(TMP_FontAsset font)
    {
        // The whole overlay owns the click, including the spotlight. Action
        // lessons validate the click against the required command above.
        Plate("Pointer Block", overlay, Full, Color.clear, true);
        var veil = new Color(.012f, .012f, .016f, .63f);
        for (int i = 0; i < shade.Length; ++i)
            shade[i] = Plate("Shade " + i, overlay, Full, veil).rectTransform;

        spotlight = Element("Spotlight", overlay, Full);
        Frame(spotlight, new Color(Gold.r, Gold.g, Gold.b, .76f));

        page = Plate("Lesson", overlay, Full, Ink).rectTransform;
        Plate("Trim", page, Rect.MinMaxRect(.105f, .925f, .895f, .927f), Gold);
        Plate("Divider", page, Rect.MinMaxRect(.105f, .598f, .895f, .600f),
            new Color(Gold.r, Gold.g, Gold.b, .63f));
        counter = Label("Step", page, Rect.MinMaxRect(.105f, .728f, .895f, .775f),
            font, 23, 14, Gold, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        heading = Label("Title", page, Rect.MinMaxRect(.105f, .615f, .895f, .727f),
            font, 54, 25, Gold, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        content = Label("Instruction", page, Rect.MinMaxRect(.105f, .207f, .895f, .565f),
            font, 31, 18, Ivory, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        footer = Label("Input Hint", page, Rect.MinMaxRect(.105f, .075f, .895f, .151f),
            font, 24, 14, Gold, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
    }

    void UpdateLayout()
    {
        if (overlay.rect.width <= 1f || overlay.rect.height <= 1f) return;
        bool hasFocus = TryGetFocus(out Rect focus);
        spotlight.gameObject.SetActive(hasFocus);
        if (hasFocus)
        {
            SetRect(spotlight, focus);
            SetRect(shade[0], Rect.MinMaxRect(0f, focus.yMax, 1f, 1f));
            SetRect(shade[1], Rect.MinMaxRect(0f, 0f, 1f, focus.yMin));
            SetRect(shade[2], Rect.MinMaxRect(0f, focus.yMin, focus.xMin, focus.yMax));
            SetRect(shade[3], Rect.MinMaxRect(focus.xMax, focus.yMin, 1f, focus.yMax));
        }
        else
        {
            SetRect(shade[0], Full);
            for (int i = 1; i < shade.Length; ++i) SetRect(shade[i], default);
        }

        float aspect = overlay.rect.width / overlay.rect.height;
        if (aspect < 1.1f)
        {
            var lower = Rect.MinMaxRect(0f, 0f, 1f, .54f);
            var upper = Rect.MinMaxRect(0f, .46f, 1f, 1f);
            SetRect(page, hasFocus && IntersectionArea(upper, focus) < IntersectionArea(lower, focus) ? upper : lower);
            return;
        }

        const float minWidth = .23f;
        const float maxWidth = .36f;
        float leftRoom = hasFocus ? focus.xMin - .012f : maxWidth;
        float rightRoom = hasFocus ? 1f - focus.xMax - .012f : maxWidth;
        // Place the lesson card on the side with more free room, keeping the
        // highlighted HUD or control visible for every step (including the
        // party HP / Ultimate page at the end).
        bool useLeft = leftRoom > rightRoom;
        if (useLeft && leftRoom < minWidth && rightRoom > leftRoom) useLeft = false;
        if (!useLeft && rightRoom < minWidth && leftRoom > rightRoom) useLeft = true;
        float width = Mathf.Clamp(useLeft ? leftRoom : rightRoom, minWidth, maxWidth);
        SetRect(page, useLeft
            ? Rect.MinMaxRect(0f, 0f, width, 1f)
            : Rect.MinMaxRect(1f - width, 0f, 1f, 1f));
    }

    bool TryGetFocus(out Rect area)
    {
        area = default;
        if (sourceCanvas == null || focusTargets == null) return false;

        Rect overlayRect = overlay.rect;
        var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var camera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
        bool found = false;
        for (int i = 0; i < focusTargets.Count; ++i)
        {
            var target = focusTargets[i];
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            var targetCanvas = target.GetComponentInParent<Canvas>();
            var targetRoot = targetCanvas != null ? targetCanvas.rootCanvas : sourceCanvas;
            var targetCamera = targetRoot.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetRoot.worldCamera;
            target.GetWorldCorners(corners);
            var targetMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var targetMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < corners.Length; ++corner)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, corners[corner]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, camera, out Vector2 local))
                    continue;
                targetMin = Vector2.Min(targetMin, local);
                targetMax = Vector2.Max(targetMax, local);
            }
            if (targetMax.x <= overlayRect.xMin || targetMin.x >= overlayRect.xMax ||
                targetMax.y <= overlayRect.yMin || targetMin.y >= overlayRect.yMax) continue;
            minimum = Vector2.Min(minimum, targetMin);
            maximum = Vector2.Max(maximum, targetMax);
            found = true;
        }

        if (!found) return false;
        const float padding = 13f;
        area = Rect.MinMaxRect(
            Mathf.Clamp01((minimum.x - padding - overlayRect.xMin) / overlayRect.width),
            Mathf.Clamp01((minimum.y - padding - overlayRect.yMin) / overlayRect.height),
            Mathf.Clamp01((maximum.x + padding - overlayRect.xMin) / overlayRect.width),
            Mathf.Clamp01((maximum.y + padding - overlayRect.yMin) / overlayRect.height));
        return area.width > .002f && area.height > .002f;
    }

    static float IntersectionArea(Rect first, Rect second)
    {
        return Mathf.Max(0f, Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin)) *
            Mathf.Max(0f, Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin));
    }

    static bool SubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        if ((keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) ||
            (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }

    static bool CancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    static void Frame(RectTransform parent, Color tint)
    {
        Edge(parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), tint);
        Edge(parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), tint);
        Edge(parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), tint);
        Edge(parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-2f, 0f), tint);
    }

    static void Edge(RectTransform parent, Vector2 min, Vector2 max, Vector2 inset, Color tint)
    {
        var line = Plate("Rule", parent, Rect.MinMaxRect(min.x, min.y, max.x, max.y), tint).rectTransform;
        line.offsetMin = Vector2.Min(Vector2.zero, inset);
        line.offsetMax = Vector2.Max(Vector2.zero, inset);
    }

    static Image Plate(string name, RectTransform parent, Rect bounds, Color tint, bool receivesPointer = false)
    {
        var image = Element(name, parent, bounds).gameObject.AddComponent<Image>();
        image.color = tint;
        image.raycastTarget = receivesPointer;
        return image;
    }

    static TMP_Text Label(string name, RectTransform parent, Rect bounds, TMP_FontAsset font, int size,
        int minSize, Color tint, FontStyles style, TextAlignmentOptions alignment)
    {
        var text = Element(name, parent, bounds).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.color = tint;
        text.fontStyle = style;
        text.fontSize = size;
        text.fontSizeMin = minSize;
        text.fontSizeMax = size;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform Element(string name, RectTransform parent, Rect bounds)
    {
        var element = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        element.SetParent(parent, false);
        SetRect(element, bounds);
        return element;
    }

    static void SetRect(RectTransform rect, Rect bounds)
    {
        if (rect.anchorMin == bounds.min && rect.anchorMax == bounds.max &&
            rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero) return;
        rect.anchorMin = bounds.min;
        rect.anchorMax = bounds.max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
