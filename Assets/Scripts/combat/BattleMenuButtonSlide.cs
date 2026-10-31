using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Interruptible horizontal focus/entrance motion for a combat menu button.</summary>
/// <remarks>Keep the Button on a stable slot and animate a child RectTransform.</remarks>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class BattleMenuButtonSlide : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
    IMoveHandler, ISubmitHandler
{
    [Header("Motion Target")]
    [Tooltip("Child containing the banner, text and icon. Its authored angle, size and scale are preserved.")]
    public RectTransform motionTarget;
    [Tooltip("Optional CanvasGroup on Motion Target, used only for entrance fading.")]
    public CanvasGroup entranceGroup;

    [Header("Focus Slide")]
    public float slideDistance = 50f;
    [Min(.01f)] public float slideOutDuration = .24f;
    public Ease slideOutEase = Ease.OutBack;
    [Range(0f, 4f)] public float backOvershoot = 1.4f;
    [Min(0f), Tooltip("Extra travel before the final settle, in root Canvas units.")]
    public float overshootExtraPixels = 3f;
    [Min(.01f)] public float slideInDuration = .18f;
    public Ease slideInEase = Ease.InOutCubic;

    [Header("Entrance")]
    [Tooltip("Enable for standalone use. Leave off when BattleMenuStagger drives entrances.")]
    public bool playEntranceOnEnable;
    public Vector2 entryOffset = new Vector2(-70f, 0f);
    [Min(.01f)] public float entryDuration = .22f;
    public Ease entryEase = Ease.OutCubic;

    Button button;
    RectTransform cachedTarget;
    Vector2 authoredPosition;
    Vector2 horizontalAxis;
    Vector2 entranceOffset;
    float focusOffset;
    float authoredAlpha = 1f;
    bool initialized, focused, entrancePlayed;
    bool pointerInside, navigationFocused;
    Sequence focusTween;
    Sequence entranceTween;

    public bool IsFocused => focused;

    void Awake() => button = GetComponent<Button>();

    public void SetMotionTarget(RectTransform target)
    {
        if (initialized && cachedTarget == target) return;
        ResetImmediately();
        initialized = false;
        motionTarget = target;
        EnsureInitialized();
    }

    bool EnsureInitialized()
    {
        if (initialized && cachedTarget == motionTarget && cachedTarget != null) return true;
        if (motionTarget == null) return false;
        if (button == null) button = GetComponent<Button>();
        cachedTarget = motionTarget;
        authoredPosition = cachedTarget.anchoredPosition;
        if (entranceGroup == null || entranceGroup.transform != cachedTarget)
            entranceGroup = cachedTarget.GetComponent<CanvasGroup>();
        authoredAlpha = entranceGroup != null ? entranceGroup.alpha : 1f;
        horizontalAxis = ToTargetParentSpace(Vector2.right);
        initialized = true;
        return true;
    }

    Vector2 ToTargetParentSpace(Vector2 canvasOffset)
    {
        var canvas = GetComponentInParent<Canvas>();
        var parent = cachedTarget != null ? cachedTarget.parent : null;
        if (parent == null) return canvasOffset;
        Transform reference = canvas != null ? canvas.rootCanvas.transform : parent;
        Vector3 world = reference.TransformVector(new Vector3(canvasOffset.x, canvasOffset.y, 0f));
        Vector3 local = parent.InverseTransformVector(world);
        return new Vector2(local.x, local.y);
    }

    public void SetFocused(bool value)
    {
        if (!EnsureInitialized()) return;
        value &= isActiveAndEnabled && (button == null || button.IsInteractable());
        // An early input should not wait behind this button's stagger delay.
        // Finish its remaining entrance from its current pose without snapping.
        if (value && entranceTween != null && entranceTween.IsActive())
            StartEntranceTween(0f, .08f);
        if (focused == value) return;
        focused = value;
        KillFocusTween();
        horizontalAxis = ToTargetParentSpace(Vector2.right);
        float target = value ? slideDistance : 0f;
        focusTween = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        if (value && overshootExtraPixels > 0f && Mathf.Abs(slideDistance) > .001f)
        {
            // The first leg gives a small visible overshoot; the final leg lands
            // exactly on Slide Distance. Both continue from the current offset.
            float peak = target + Mathf.Sign(slideDistance == 0f ? 1f : slideDistance) * overshootExtraPixels;
            focusTween.Append(DOTween.To(() => focusOffset, SetFocusOffset, peak, Mathf.Max(.01f, slideOutDuration * .8f))
                .SetEase(slideOutEase, backOvershoot));
            focusTween.Append(DOTween.To(() => focusOffset, SetFocusOffset, target, Mathf.Max(.01f, slideOutDuration * .2f))
                .SetEase(Ease.InOutCubic));
        }
        else
        {
            var tween = DOTween.To(() => focusOffset, SetFocusOffset, target,
                Mathf.Max(.01f, value ? slideOutDuration : slideInDuration));
            if (value) tween.SetEase(slideOutEase, backOvershoot);
            else tween.SetEase(slideInEase);
            focusTween.Append(tween);
        }
    }

    void SetFocusOffset(float value)
    {
        focusOffset = value;
        ApplyPosition();
    }

    void ApplyPosition()
    {
        if (cachedTarget != null)
            cachedTarget.anchoredPosition = authoredPosition + horizontalAxis * focusOffset + entranceOffset;
    }

    public void PlayEntrance(float delay)
    {
        if (!isActiveAndEnabled || !EnsureInitialized()) return;
        if (!entrancePlayed)
        {
            entranceOffset = ToTargetParentSpace(entryOffset);
            if (entranceGroup != null) entranceGroup.alpha = 0f;
            entrancePlayed = true;
        }
        ApplyPosition();
        StartEntranceTween(delay, entryDuration);
    }

    void StartEntranceTween(float delay, float duration)
    {
        KillEntranceTween();
        entranceTween = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        entranceTween.AppendInterval(Mathf.Max(0f, delay));
        entranceTween.Append(DOTween.To(() => entranceOffset, value =>
        {
            entranceOffset = value;
            ApplyPosition();
        }, Vector2.zero, Mathf.Max(.01f, duration)).SetEase(entryEase));
        if (entranceGroup != null)
        {
            var group = entranceGroup;
            entranceTween.Join(DOTween.To(() => group.alpha, value =>
            {
                if (group != null) group.alpha = value;
            }, authoredAlpha, Mathf.Max(.01f, duration)).SetEase(Ease.OutCubic));
        }
    }

    public void ResetImmediately()
    {
        KillFocusTween();
        KillEntranceTween();
        focused = false;
        pointerInside = navigationFocused = false;
        focusOffset = 0f;
        entranceOffset = Vector2.zero;
        entrancePlayed = false;
        if (!initialized) return;
        if (cachedTarget != null) cachedTarget.anchoredPosition = authoredPosition;
        if (entranceGroup != null) entranceGroup.alpha = authoredAlpha;
    }

    void KillFocusTween()
    {
        if (focusTween != null) focusTween.Kill(false);
        focusTween = null;
    }

    void KillEntranceTween()
    {
        if (entranceTween != null) entranceTween.Kill(false);
        entranceTween = null;
    }

    void OnEnable()
    {
        if (playEntranceOnEnable) PlayEntrance(0f);
    }

    void Update()
    {
        if (focused && button != null && !button.IsInteractable()) SetFocused(false);
    }

    void OnDisable() => ResetImmediately();
    void OnDestroy() => ResetImmediately();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!EnsureInitialized() || button != null && !button.IsInteractable()) return;
        pointerInside = true;
        navigationFocused = false;
        var events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject != gameObject)
            events.SetSelectedGameObject(gameObject, eventData);
        SetFocused(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        RefreshInputFocus();
    }

    public void OnSelect(BaseEventData eventData)
    {
        // EventSystem also selects on mouse click and when the menu opens.
        // Only a real navigation event should hold focus after the pointer exits.
        navigationFocused = eventData is AxisEventData;
        RefreshInputFocus();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        pointerInside = navigationFocused = false;
        SetFocused(false);
    }

    public void OnMove(AxisEventData eventData)
    {
        // Handles the case where navigation stays on the same selected button.
        if (eventData.moveDir == MoveDirection.None || !IsEventSystemSelected()) return;
        navigationFocused = true;
        RefreshInputFocus();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsEventSystemSelected()) return;
        navigationFocused = true;
        RefreshInputFocus();
    }

    bool IsEventSystemSelected() => EventSystem.current != null &&
        EventSystem.current.currentSelectedGameObject == gameObject;

    public void RefreshInputFocus() => SetFocused(pointerInside || navigationFocused && IsEventSystemSelected());
}
