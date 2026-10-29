using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class SmoothButtonHoverScale : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    RectTransform rectTransform;
    Vector3 baseScale;
    Vector3 scaleVelocity;
    float hoverScale = 1.045f;
    float smoothTime = 0.12f;
    bool hovered;
    bool configured;

    void Awake()
    {
        rectTransform = transform as RectTransform;
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
        // Keyboard focus can remain selected while the pointer is elsewhere.
        // This component responds only to actual pointer hover events.
        Vector3 target = baseScale * (hovered ? hoverScale : 1f);
        rectTransform.localScale = Vector3.SmoothDamp(
            rectTransform.localScale, target, ref scaleVelocity, smoothTime,
            Mathf.Infinity, Time.unscaledDeltaTime);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    void OnDisable()
    {
        hovered = false;
        scaleVelocity = Vector3.zero;
        if (rectTransform != null) rectTransform.localScale = baseScale;
    }
}
