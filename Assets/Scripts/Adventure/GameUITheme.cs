using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Applies the shared neutral menu palette and edge-only interaction highlight.</summary>
public static class GameUITheme
{
    static readonly Color Background = new Color(0.012f, 0.014f, 0.017f, 1f);
    static readonly Color Panel = new Color(0.035f, 0.038f, 0.042f, 0.94f);
    static readonly Color Divider = new Color(0.25f, 0.26f, 0.28f, 1f);
    static readonly Color Hover = new Color(0.88f, 0.9f, 0.93f, 0.95f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallSceneTheme()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.gameObject.scene != scene) continue;
            var parent = canvas.transform.parent != null ? canvas.transform.parent.GetComponentInParent<Canvas>() : null;
            if (parent == null) Apply(canvas.gameObject);
        }
    }

    public static void Apply(GameObject root)
    {
        if (root == null) return;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            string n = image.gameObject.name.ToLowerInvariant();
            if (Contains(n, "hp", "health", "fill", "icon", "portrait", "preview", "buff", "debuff", "ultimate", "energy")) continue;
            if (Contains(n, "background", "backdrop"))
            {
                image.color = WithAlpha(Background, image.color.a);
                continue;
            }
            if (Contains(n, "divider", "separator", "line", "edge", "rule"))
            {
                image.color = WithAlpha(Divider, image.color.a);
                continue;
            }
            if (Contains(n, "panel", "card", "row", "cell", "dialog", "container", "header", "footer", "statsbackground", "skillcontext", "skilldialog", "slots"))
                image.color = WithAlpha(Panel, image.color.a);
        }

        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null) continue;
            Color c = text.color;
            // Preserve the original emphasis while removing the old gold/cyan hue.
            float gray = Mathf.Clamp01(c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f);
            gray = Mathf.Lerp(0.48f, 0.94f, gray);
            text.color = new Color(gray, gray, gray, c.a);
        }

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (button == null) continue;
            Graphic graphic = button.GetComponent<Graphic>();
            if (graphic == null) graphic = button.targetGraphic;
            if (graphic == null || graphic.name.ToLowerInvariant().Contains("icon")) continue;
            button.transition = Selectable.Transition.None;
            var edge = graphic.GetComponent<UIHoverOutline>();
            if (edge == null) edge = graphic.gameObject.AddComponent<UIHoverOutline>();
            edge.Configure(Hover);
        }
    }

    static bool Contains(string value, params string[] terms)
    {
        foreach (var term in terms) if (value.Contains(term)) return true;
        return false;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}

[DisallowMultipleComponent]
public sealed class UIHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Outline outline;
    bool hovered;
    bool selected;

    void Awake() => EnsureOutline();

    public void Configure(Color color)
    {
        EnsureOutline();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = true;
        outline.enabled = hovered || selected;
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (outline != null) outline.enabled = hovered || selected;
    }

    void EnsureOutline()
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.9f, 0.93f, 0.95f);
        outline.effectDistance = new Vector2(2f, 2f);
        outline.useGraphicAlpha = true;
        outline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; if (outline != null) outline.enabled = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; if (outline != null) outline.enabled = selected; }
    public void OnSelect(BaseEventData eventData) { selected = true; if (outline != null) outline.enabled = true; }
    public void OnDeselect(BaseEventData eventData) { selected = false; if (outline != null) outline.enabled = hovered; }
}
