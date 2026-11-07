using System.Collections;
using Coffee.UISoftMask;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fixed cumulative mask + independent ink artwork. Never scales the panel or its children.</summary>
[DisallowMultipleComponent]
public sealed class BrushRevealController : MonoBehaviour
{
    public enum SweepDirection { RightToLeft, LeftToRight }

    [Header("Cumulative mask")]
    public bool preferSoftMask = true;
    [Min(0f)] public float edgeRoughness = 28f;
    [Min(.1f)] public float featherWidth = 5f;
    [Range(8, 128)] public int edgeSegments = 64;

    [Header("Decorative ink — not the reveal mask")]
    [Tooltip("Optional ink Sprite fitted to the full height of Skill Presentation. Leave empty for the procedural front.")]
    public Sprite visualBrushSprite;
    [Tooltip("Horizontal thickness of the moving ink front. Height follows Skill Presentation automatically.")]
    [Min(1f)] public float inkFrontWidth = 126f;
    [Tooltip("Leans the visible ink head in degrees. A positive angle leans the top to the right.")]
    [Range(-30f, 30f)] public float inkHeadAngle;
    [Tooltip("Mirrors the visible ink head horizontally while it travels in the original direction.")]
    public bool flipInkHead;
    [ColorUsage(false)] public Color brushTint = new Color(.015f, .017f, .022f, .98f);
    [Min(.01f)] public float standaloneDuration = .26f;

    public bool IsInitialized => panel != null && maskRect != null;
    public float Progress { get; private set; }
    public RectTransform MaskRoot => maskRect;
    public SoftMask Mask => mask;

    RectTransform panel, maskRect, overlayRect;
    SoftMask mask;
    InkRevealGraphic maskGraphic, strokeGraphic;
    Coroutine playback;
    bool rightToLeft;
    bool warnedFallback;

    public void CopyStyleFrom(BrushRevealController source)
    {
        preferSoftMask = source.preferSoftMask;
        edgeRoughness = source.edgeRoughness;
        featherWidth = source.featherWidth;
        edgeSegments = source.edgeSegments;
        visualBrushSprite = source.visualBrushSprite;
        inkFrontWidth = source.inkFrontWidth;
        inkHeadAngle = source.inkHeadAngle;
        flipInkHead = source.flipInkHead;
        brushTint = source.brushTint;
    }

    public bool Initialize(RectTransform content, SweepDirection direction = SweepDirection.RightToLeft)
    {
        if (content == null || content.parent == null) return false;
        if (IsInitialized)
        {
            if (content != panel)
            {
                Debug.LogWarning("Use a separate BrushRevealController for each panel.", this);
                return false;
            }
            ConfigureGraphics(direction);
            return true;
        }
        panel = content;
        Transform originalParent = panel.parent;
        int originalSibling = panel.GetSiblingIndex();
        var originalPose = new RectPose(panel);

        maskRect = NewRect("BrushMask", originalParent);
        originalPose.Apply(maskRect);
        maskRect.SetSiblingIndex(originalSibling);
        var existingLayout = panel.GetComponent<LayoutElement>();
        if (existingLayout != null)
        {
            var layout = maskRect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = existingLayout.ignoreLayout;
            layout.minWidth = existingLayout.minWidth;
            layout.minHeight = existingLayout.minHeight;
            layout.preferredWidth = existingLayout.preferredWidth;
            layout.preferredHeight = existingLayout.preferredHeight;
            layout.flexibleWidth = existingLayout.flexibleWidth;
            layout.flexibleHeight = existingLayout.flexibleHeight;
            layout.layoutPriority = existingLayout.layoutPriority;
        }
        maskGraphic = maskRect.gameObject.AddComponent<InkRevealGraphic>();
        mask = maskRect.gameObject.AddComponent<SoftMask>();
        mask.showMaskGraphic = false;
        mask.alphaHitTest = false;
        mask.downSamplingRate = SoftMask.DownSamplingRate.x1;
        bool softSupported = preferSoftMask && SupportsChildMaterials();
        mask.maskingMode = softSupported ? SoftMask.MaskingMode.SoftMasking : SoftMask.MaskingMode.Normal;
        if (preferSoftMask && !softSupported && !warnedFallback)
        {
            warnedFallback = true;
            Debug.LogWarning("Ink reveal uses stencil fallback: import SoftMask TextMeshPro Support (Unity 6) or supply a SoftMaskable variant for custom UI materials. Text remains readable.", this);
        }
        panel.SetParent(maskRect, false);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.pivot = originalPose.pivot;
        panel.sizeDelta = Vector2.zero;
        panel.anchoredPosition3D = Vector3.zero;
        panel.localRotation = Quaternion.identity;
        panel.localScale = Vector3.one;

        overlayRect = NewRect("InkBrushOverlay", originalParent);
        originalPose.Apply(overlayRect);
        overlayRect.SetSiblingIndex(maskRect.GetSiblingIndex() + 1);
        var ignored = overlayRect.gameObject.AddComponent<LayoutElement>();
        ignored.ignoreLayout = true;
        var strokeRect = NewRect("Full Height Ink Front", overlayRect);
        Stretch(strokeRect);
        strokeGraphic = strokeRect.gameObject.AddComponent<InkRevealGraphic>();
        ConfigureGraphics(direction);
        SetProgress(0f);
        SetBrushOpacity(0f);
        return true;
    }

    void ConfigureGraphics(SweepDirection direction)
    {
        rightToLeft = direction == SweepDirection.RightToLeft;
        maskGraphic.ConfigureMask(edgeRoughness, featherWidth, edgeSegments, rightToLeft);
        strokeGraphic.ConfigureInkFront(edgeRoughness, featherWidth, edgeSegments, rightToLeft);
    }

    bool SupportsChildMaterials()
    {
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is MaskableGraphic maskable && !maskable.maskable) continue;
            var material = graphic.material;
            var shader = material != null ? material.shader : null;
            if (shader == null) continue;
            string shaderName = shader.name;
            if (shaderName.Contains("SoftMaskable")) continue;
            string variantName = "Hidden/" + shaderName + " (SoftMaskable)";
            if (Shader.Find(variantName) == null) return false;
        }
        return true;
    }

    public void SetProgress(float value)
    {
        Progress = Mathf.Clamp01(value);
        if (!IsInitialized || mask == null || maskGraphic == null) return;
        bool needsMask = Progress < 1f;
        // At 100%, release the offscreen soft-mask buffer and stop touching child
        // materials. Existing focus art can extend beyond the panel normally.
        maskGraphic.SetMaskProgress(Progress);
        if (needsMask)
        {
            if (!maskGraphic.enabled) maskGraphic.enabled = true;
            if (!mask.enabled) mask.enabled = true;
            mask.SetSoftMaskDirty();
        }
        else
        {
            if (mask.enabled) mask.enabled = false;
            if (maskGraphic.enabled) maskGraphic.enabled = false;
        }
    }

    public void SetBrushOpacity(float opacity)
    {
        if (!IsInitialized || overlayRect == null) return;
        opacity = Mathf.Clamp01(opacity);
        bool visible = opacity > .001f && Progress > 0f && Progress < 1f;
        overlayRect.gameObject.SetActive(visible);
        if (!visible) return;
        // Use the mask's current pose so the art tracks its bounds with any Canvas Scaler or layout.
        new RectPose(maskRect).Apply(overlayRect);
        Color tint = brushTint;
        tint.a *= opacity;
        strokeGraphic.SetVisualFront(Progress, inkFrontWidth, tint, inkHeadAngle, flipInkHead, visualBrushSprite);
    }

    // Standalone preview APIs. The main transition samples progress directly so
    // all tracks share one clock and never compete with these coroutines.
    public void PlayBrushReveal() => PlayStandalone(1f);
    public void PlayReverseBrushReveal() => PlayStandalone(0f);
    void PlayStandalone(float target)
    {
        if (!IsInitialized || !isActiveAndEnabled) return;
        CancelPlayback();
        playback = StartCoroutine(AnimateStandalone(target));
    }
    IEnumerator AnimateStandalone(float target)
    {
        float from = Progress;
        float elapsed = 0f;
        while (elapsed < standaloneDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, standaloneDuration));
            float p = Mathf.Lerp(from, target, 1f - Mathf.Pow(1f - t, 3f));
            SetProgress(p);
            SetBrushOpacity(Mathf.Min(t * 10f, (1f - t) * 10f));
            yield return null;
        }
        SetProgress(target);
        SetBrushOpacity(0f);
        playback = null;
    }
    public void CancelPlayback()
    {
        if (playback != null) StopCoroutine(playback);
        playback = null;
    }
    public void ResetTransition()
    {
        CancelPlayback();
        // The owner hides the panel. Leaving no active mask avoids per-frame
        // rendering/allocation while Skills is closed or Items is open.
        SetProgress(1f);
        SetBrushOpacity(0f);
    }
    // The generated wrapper and overlay belong to the panel hierarchy. Unity
    // disposes them with it. Reparenting the viewport during scene teardown fails.
    void OnDisable() => ResetTransition();
    static RectTransform NewRect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }
    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    readonly struct RectPose
    {
        readonly Vector2 min, max, size;
        readonly Vector3 position, scale;
        readonly Quaternion rotation;
        public readonly Vector2 pivot;
        public RectPose(RectTransform rect)
        {
            min = rect.anchorMin; max = rect.anchorMax; size = rect.sizeDelta;
            pivot = rect.pivot; position = rect.anchoredPosition3D;
            scale = rect.localScale; rotation = rect.localRotation;
        }
        public void Apply(RectTransform rect)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.sizeDelta = size; rect.anchoredPosition3D = position;
            rect.localScale = scale; rect.localRotation = rotation;
        }
    }
}
