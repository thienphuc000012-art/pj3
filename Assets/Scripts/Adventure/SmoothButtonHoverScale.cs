using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SmoothButtonHoverScale : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    RectTransform rectTransform;
    Button button;
    DynamicButtonUI entrance;
    Vector3 baseScale;
    float scaleVelocity;
    float hoverScale = 1.045f;
    float smoothTime = 0.12f;
    bool hovered;
    bool configured;
    bool appliedDirectScale;
    public float Multiplier { get; private set; } = 1f;

    public static void Ensure(Button button)
    {
        if (button == null) return;
        var effect = button.GetComponent<SmoothButtonHoverScale>();
        var canvas = button.GetComponentInParent<AdventureCanvasRoot>(true);
        var memberPanel = canvas != null && canvas.kind == AdventureCanvasKind.Adventure
            ? canvas.transform.Find("Menu/Party/Member")
            : null;
        if (memberPanel != null && button.transform.IsChildOf(memberPanel))
        {
            if (effect != null)
            {
                effect.enabled = false;
                if (Application.isPlaying) Destroy(effect);
            }
            return;
        }
        if (effect == null) effect = button.gameObject.AddComponent<SmoothButtonHoverScale>();
        effect.enabled = true;
    }

    void Awake()
    {
        rectTransform = transform as RectTransform;
        button = GetComponent<Button>();
        if (TryGetComponent(out entrance)) entrance.SetHoverScale(this);
        CaptureBaseScale();
    }

    public void Configure(float targetScale, float transitionTime)
    {
        if (rectTransform == null) rectTransform = transform as RectTransform;
        CaptureBaseScale();
        hoverScale = Mathf.Max(1f, targetScale);
        smoothTime = Mathf.Max(0.01f, transitionTime);
    }

    void CaptureBaseScale()
    {
        if (configured || rectTransform == null) return;
        baseScale = rectTransform.localScale;
        configured = true;
    }

    void Update()
    {
        if (rectTransform == null) return;
        bool canHover = hovered && (button == null || (button.IsActive() && button.IsInteractable()));
        float target = canHover ? hoverScale : 1f;
        if (Mathf.Abs(Multiplier - target) < .0001f)
        {
            if (Multiplier == target) return;
            Multiplier = target;
            scaleVelocity = 0f;
        }
        else Multiplier = Mathf.SmoothDamp(
            Multiplier, target, ref scaleVelocity, smoothTime,
            Mathf.Infinity, Time.unscaledDeltaTime);

        // DynamicButtonUI owns scale while its entrance animation is enabled.
        if (entrance == null || !entrance.isActiveAndEnabled)
        {
            rectTransform.localScale = baseScale * Multiplier;
            appliedDirectScale = Multiplier != 1f;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    void OnDisable()
    {
        hovered = false;
        scaleVelocity = 0f;
        Multiplier = 1f;
        if (appliedDirectScale && rectTransform != null) rectTransform.localScale = baseScale;
        appliedDirectScale = false;
    }
}
