using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
public sealed class CombatActionMenuTransition : MonoBehaviour
{
    [Min(.01f)] public float showDuration = .18f;
    [Min(.01f)] public float hideDuration = .10f;
    public Vector2 slideOffset = new Vector2(-24f, 0f);
    RectTransform rect;
    CanvasGroup group;
    Vector2 origin;
    float blend, fromBlend, elapsed;
    bool visible, initialized, transitioning;

    void Initialize()
    {
        if (initialized) return;
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        origin = rect.anchoredPosition;
        initialized = true;
    }

    public void SetVisible(bool value, bool immediate = false)
    {
        Initialize();
        if (value && !gameObject.activeSelf) gameObject.SetActive(true);
        group.interactable = group.blocksRaycasts = value;
        if (immediate)
        {
            visible = value;
            transitioning = false;
            blend = value ? 1f : 0f;
            Draw();
            if (!value) gameObject.SetActive(false);
            return;
        }
        // Repeated visibility updates must not restart the entrance. If the
        // target changes mid-transition, continue from the current appearance.
        if (visible != value)
        {
            visible = value;
            fromBlend = blend;
            elapsed = 0f;
            transitioning = true;
        }
    }

    void OnEnable()
    {
        Initialize();
        visible = true;
        blend = fromBlend = elapsed = 0f;
        transitioning = true;
        group.interactable = group.blocksRaycasts = true;
        Draw();
    }

    void Update()
    {
        if (!transitioning) return;
        elapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(.01f, visible ? showDuration : hideDuration);
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = visible ? 1f - Mathf.Pow(1f - t, 3f) : Mathf.SmoothStep(0f, 1f, t);
        blend = Mathf.Lerp(fromBlend, visible ? 1f : 0f, eased);
        Draw();
        if (t >= 1f)
        {
            transitioning = false;
            if (!visible) gameObject.SetActive(false);
        }
    }

    void Draw()
    {
        group.alpha = blend;
        rect.anchoredPosition = origin + (GetComponent<BattleMenuStagger>() != null ? Vector2.zero : slideOffset * (1f - blend));
    }

    void OnDisable()
    {
        if (!initialized) return;
        rect.anchoredPosition = origin;
        blend = fromBlend = elapsed = 0f;
        visible = transitioning = false;
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;
    }
}
