using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Quest rows react to pointer hover; confirmation actions can also show focus.
[DisallowMultipleComponent]
public sealed class QuestButtonHighlight : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    Button button;
    Image background;
    GameUITextColor[] labels;
    float highlightedBackgroundAlpha;
    float normalBackgroundAlpha;
    bool invertBackground, showSelection, highlightOnHover, hovered, selected;
    Color? authoredBackgroundColor;

    public void Configure(bool invertBackground, bool showSelection, float highlightedBackgroundAlpha = 1f,
        bool highlightOnHover = true, float normalBackgroundAlpha = 1f, bool preserveBackgroundColor = false)
    {
        this.invertBackground = invertBackground;
        this.showSelection = showSelection;
        this.highlightedBackgroundAlpha = Mathf.Clamp01(highlightedBackgroundAlpha);
        this.normalBackgroundAlpha = Mathf.Clamp01(normalBackgroundAlpha);
        this.highlightOnHover = highlightOnHover;
        button = GetComponent<Button>();
        background = invertBackground ? GetComponent<Image>() : null;
        authoredBackgroundColor = preserveBackgroundColor && background != null
            ? background.color : (Color?)null;
        var texts = GetComponentsInChildren<TMP_Text>(true);
        labels = new GameUITextColor[texts.Length];
        for (int i = 0; i < texts.Length; i++) labels[i] = GameUITextColor.Apply(texts[i]);
        hovered = selected = false;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!highlightOnHover) return;
        hovered = true;
        selected = false;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!highlightOnHover) return;
        hovered = selected = false;
        Refresh();
    }
    public void OnSelect(BaseEventData eventData) { selected = showSelection; Refresh(); }
    public void OnDeselect(BaseEventData eventData) { selected = false; Refresh(); }
    void OnDisable() { hovered = selected = false; Refresh(); }

    void Refresh()
    {
        bool active = (hovered || selected) && button != null && button.IsInteractable();
        if (background != null)
        {
            if (authoredBackgroundColor.HasValue)
                background.color = active
                    ? Color.Lerp(authoredBackgroundColor.Value, new Color(.36f, .28f, .19f), .6f)
                    : authoredBackgroundColor.Value;
            else
                background.color = active ? new Color(1f, 1f, 1f, highlightedBackgroundAlpha)
                    : new Color(0f, 0f, 0f, normalBackgroundAlpha);
            background.canvasRenderer.SetColor(Color.white);
        }
        Color text;
        if (authoredBackgroundColor.HasValue)
            text = active ? new Color(1f, .85f, .59f) : Color.white;
        else if (invertBackground)
            text = active ? Color.black : Color.white;
        else
            text = active ? Color.white : new Color(.5f, .5f, .5f, 1f);
        if (labels != null)
            foreach (var label in labels) if (label != null) label.SetColorOverride(text);
    }
}
