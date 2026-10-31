using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Brush banner focus and click feedback without moving the button hit area.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class CombatActionButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, ISubmitHandler,
    ISelectHandler, IDeselectHandler
{
    [Min(.02f)] public float pressDuration = .10f;
    [Min(.02f)] public float ribbonDuration = .15f;
    public Vector2 bannerPadding = new Vector2(8f, 4f);
    [Range(0f, 16f)] public float focusTextOffset = 6f;
    [Range(0f, .1f)] public float entranceStagger = .025f;
    [Range(0f, 1f)] public float idleBannerAlpha = .68f;
    [Range(0f, 1f)] public float idleTextAlpha = .85f;

    RectTransform rect;
    RectTransform motionRoot;
    BattleMenuButtonSlide slideMotion;
    Button button;
    CanvasGroup group;
    Image ribbon, sweep, flash;
    CombatButtonFocusGraphic marker;
    readonly List<RectTransform> content = new List<RectTransform>();
    readonly List<Vector3> contentOrigins = new List<Vector3>();
    readonly List<TMP_Text> labels = new List<TMP_Text>();
    readonly List<float> labelAlphas = new List<float>();
    float hover, pulse, enabledAt;
    bool initialized, capturePending;
    static CombatActionButtonFeedback pointerFocus;
    static AudioSource audioSource;
    static AudioClip fallbackClick;

    public void Configure(Sprite sprite)
    {
        if (initialized) { SetSprite(sprite); return; }
        Initialize();
        // The Canvas owns size and angle. Only the Motion child is translated.
        EnsureMotionRoot();
        slideMotion = GetComponent<BattleMenuButtonSlide>();
        if (slideMotion == null) slideMotion = gameObject.AddComponent<BattleMenuButtonSlide>();
        slideMotion.SetMotionTarget(motionRoot);
        if (button.navigation.mode == Navigation.Mode.None)
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image.name.ToLowerInvariant().Contains("icon")) continue;
            image.color = Color.clear;
        }
        var hitArea = GetComponent<Image>();
        if (hitArea == null) hitArea = gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        button.targetGraphic = hitArea;
        ribbon = Layer("Action Ribbon", sprite, Vector2.zero, Vector2.one, -bannerPadding, bannerPadding);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            // Typography and the label rect belong to the authored Canvas.
            // Track only its position for the small focus animation.
            TrackContent(label.rectTransform);
        }
        BuildFeedbackLayers(sprite);
    }

    void EnsureMotionRoot()
    {
        motionRoot = transform.Find("Motion") as RectTransform;
        if (motionRoot == null)
        {
            var go = new GameObject("Motion", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.layer = gameObject.layer;
            motionRoot = (RectTransform)go.transform;
            motionRoot.SetParent(transform, false);
            motionRoot.anchorMin = Vector2.zero;
            motionRoot.anchorMax = Vector2.one;
            motionRoot.offsetMin = motionRoot.offsetMax = Vector2.zero;
            var children = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != motionRoot && child is RectTransform) children.Add(child);
            }
            foreach (var child in children) child.SetParent(motionRoot, false);
        }
        // Both the original slot and the moved visual remain clickable.
        var hitArea = motionRoot.GetComponent<Image>();
        if (hitArea == null) hitArea = motionRoot.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
    }

    public void ConfigureRow(Image background, ActionButtonUI row)
    {
        if (initialized) { SetSprite(background.sprite); return; }
        Initialize();
        ribbon = background;
        TrackContent(row.iconImage != null ? row.iconImage.rectTransform : null);
        TrackContent(row.nameText != null ? row.nameText.rectTransform : null);
        TrackContent(row.infoText != null ? row.infoText.rectTransform : null);
        TrackContent(row.resourceText != null ? row.resourceText.rectTransform : null);
        BuildFeedbackLayers(background.sprite);
    }

    void Initialize()
    {
        rect = (RectTransform)transform;
        button = GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    void TrackContent(RectTransform target)
    {
        if (target != null && !content.Contains(target)) content.Add(target);
    }

    void BuildFeedbackLayers(Sprite sprite)
    {
        // Overlays share the banner's parent and rect; they never shrink the base.
        sweep = Layer("Ribbon Sweep", sprite, ribbon.rectTransform.anchorMin, ribbon.rectTransform.anchorMax,
            ribbon.rectTransform.offsetMin, ribbon.rectTransform.offsetMax);
        sweep.type = Image.Type.Filled;
        sweep.fillMethod = Image.FillMethod.Horizontal;
        sweep.fillOrigin = (int)Image.OriginHorizontal.Left;
        flash = Layer("Press Flash", null, new Vector2(.07f, .15f), new Vector2(.93f, .15f),
            new Vector2(0f, -.5f), new Vector2(0f, .5f));
        ribbon.transform.SetAsFirstSibling();
        sweep.transform.SetSiblingIndex(1);
        flash.transform.SetSiblingIndex(2);
        Transform visualParent = motionRoot != null ? motionRoot : transform;
        var existing = visualParent.Find("Focus Diamond");
        var go = existing != null ? existing.gameObject :
            new GameObject("Focus Diamond", typeof(RectTransform), typeof(CanvasRenderer), typeof(CombatButtonFocusGraphic));
        go.layer = gameObject.layer;
        go.transform.SetParent(visualParent, false);
        marker = go.GetComponent<CombatButtonFocusGraphic>();
        marker.raycastTarget = false;
        var mr = (RectTransform)go.transform;
        mr.anchorMin = mr.anchorMax = new Vector2(0f, .5f);
        mr.pivot = new Vector2(.5f, .5f);
        mr.sizeDelta = new Vector2(12f, 18f);
        mr.anchoredPosition = new Vector2(8f, 0f);
        initialized = true;
        capturePending = true;
        ResetVisuals();
    }

    Image Layer(string name, Sprite sprite, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform visualParent = motionRoot != null ? motionRoot : transform;
        var existing = visualParent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(visualParent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
        r.localScale = Vector3.one;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    void SetSprite(Sprite sprite)
    {
        ribbon.sprite = sweep.sprite = sprite;
    }

    void CaptureContent()
    {
        if (!capturePending) return;
        // FitFixedSlot runs after Setup. Capture the final row layout in LateUpdate.
        contentOrigins.Clear();
        labels.Clear();
        labelAlphas.Clear();
        foreach (var target in content)
        {
            contentOrigins.Add(target != null ? target.anchoredPosition3D : Vector3.zero);
            var label = target != null ? target.GetComponent<TMP_Text>() : null;
            if (label != null) { labels.Add(label); labelAlphas.Add(label.color.a); }
        }
        capturePending = false;
    }

    void LateUpdate()
    {
        if (!initialized) return;
        CaptureContent();
        bool focused = slideMotion != null ? slideMotion.IsFocused :
            pointerFocus != null ? pointerFocus == this :
                EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        focused &= button.IsInteractable();
        hover = Mathf.MoveTowards(hover, focused ? 1f : 0f, Time.unscaledDeltaTime / ribbonDuration);
        pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime / pressDuration);
        float eased = 1f - Mathf.Pow(1f - hover, 3f);
        float entrance = Mathf.Clamp01((Time.unscaledTime - enabledAt - Mathf.Min(transform.GetSiblingIndex(), 5) * entranceStagger) / .16f);
        group.alpha = slideMotion != null ? 1f : 1f - Mathf.Pow(1f - entrance, 3f);
        float enabledAlpha = button.IsInteractable() ? 1f : .45f;
        ribbon.color = new Color(1f, 1f, 1f, Mathf.Lerp(idleBannerAlpha, 1f, eased) * enabledAlpha);
        sweep.fillAmount = eased;
        sweep.color = new Color(1f, 1f, 1f, .22f * enabledAlpha);
        flash.color = new Color(1f, 1f, 1f, pulse * .38f);
        marker.color = new Color(1f, 1f, 1f, eased * enabledAlpha);
        marker.rectTransform.localScale = Vector3.one * Mathf.Lerp(.8f, 1f, eased);
        for (int i = 0; i < content.Count; i++)
            if (content[i] != null) content[i].anchoredPosition3D = contentOrigins[i] +
                Vector3.right * (slideMotion != null ? 0f : focusTextOffset * eased - pulse * 1.5f);
        for (int i = 0; i < labels.Count; i++)
            if (labels[i] != null)
            {
                var color = labels[i].color;
                color.a = labelAlphas[i] * Mathf.Lerp(idleTextAlpha, 1f, eased) * enabledAlpha;
                labels[i].color = color;
            }
    }

    void Press()
    {
        if (!initialized || !button.IsInteractable()) return;
        pulse = 1f;
        flash.color = new Color(1f, 1f, 1f, .38f);
        PlayClick();
    }

    static void PlayClick()
    {
        if (audioSource == null)
        {
            var go = new GameObject("Combat UI Audio", typeof(AudioSource));
            audioSource = go.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
        }
        var clip = GameUITheme.CombatButtonPressSound;
        if (clip == null)
        {
            if (fallbackClick == null)
            {
                const int rate = 44100;
                var samples = new float[2205];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / rate;
                    samples[i] = Mathf.Sin(t * 2f * Mathf.PI * 1100f) * Mathf.Exp(-t * 145f) * .3f;
                }
                fallbackClick = AudioClip.Create("UI Soft Click", samples.Length, 1, rate, false);
                fallbackClick.SetData(samples, 0);
            }
            clip = fallbackClick;
        }
        audioSource.PlayOneShot(clip, .28f);
    }

    void ResetVisuals()
    {
        if (pointerFocus == this) pointerFocus = null;
        hover = pulse = 0f;
        enabledAt = Time.unscaledTime;
        if (!initialized) return;
        ribbon.color = new Color(1f, 1f, 1f, idleBannerAlpha);
        sweep.fillAmount = 0f;
        flash.color = marker.color = Color.clear;
        if (!capturePending)
        {
            for (int i = 0; i < content.Count; i++)
                if (content[i] != null) content[i].anchoredPosition3D = contentOrigins[i];
            for (int i = 0; i < labels.Count; i++)
                if (labels[i] != null)
                {
                    var color = labels[i].color; color.a = labelAlphas[i]; labels[i].color = color;
                }
        }
        if (group != null) group.alpha = 1f;
    }

    void OnEnable() => ResetVisuals();
    void OnDisable() => ResetVisuals();
    public void OnPointerEnter(PointerEventData e) { if (button != null && button.IsInteractable()) pointerFocus = this; }
    public void OnPointerExit(PointerEventData e) { if (pointerFocus == this) pointerFocus = null; }
    public void OnSelect(BaseEventData e) { }
    public void OnDeselect(BaseEventData e) { }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Press(); }
    public void OnSubmit(BaseEventData e) => Press();
}
