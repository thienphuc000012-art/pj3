using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueChoiceTextHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
    IMoveHandler, ISubmitHandler
{
    Button button;
    bool hovered;
    bool selected;
    bool navigationFocused;
    [SerializeField] bool respondToSelection;
    [SerializeField] bool navigationSelectionOnly;
    [SerializeField] Color normalColor = new Color(.5f, .5f, .5f, 1f);
    [SerializeField] Color hoverColor = Color.white;
    [SerializeField] List<TMP_Text> labels = new List<TMP_Text>();

    public Color TextColor => (hovered || respondToSelection && selected && (!navigationSelectionOnly || navigationFocused)) &&
        (button == null || button.IsInteractable()) ? hoverColor : normalColor;

    public void Configure(TMP_Text label, Color normal, Color highlight, bool includeSelection = false, bool navigationOnly = false)
    {
        button = GetComponent<Button>();
        normalColor = normal;
        hoverColor = highlight;
        respondToSelection = includeSelection;
        navigationSelectionOnly = navigationOnly;
        navigationFocused = false;
        hovered = false;
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        if (label != null && !labels.Contains(label)) labels.Add(label);
        GameUITextColor.Apply(label)?.SetChoiceHover(this);
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; navigationFocused = false; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; Refresh(); }
    public void OnSelect(BaseEventData eventData) { selected = true; navigationFocused = eventData is AxisEventData; Refresh(); }
    public void OnDeselect(BaseEventData eventData) { selected = navigationFocused = false; Refresh(); }
    public void OnMove(AxisEventData eventData)
    {
        if (eventData == null || eventData.moveDir == MoveDirection.None || !IsSelected()) return;
        selected = navigationFocused = true; Refresh();
    }
    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsSelected()) return;
        selected = navigationFocused = true; Refresh();
    }
    bool IsSelected() => EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
    void OnEnable() { button = GetComponent<Button>(); Refresh(); }
    void OnDisable() { hovered = selected = navigationFocused = false; Refresh(); }
    void LateUpdate() => Refresh();
    void Refresh()
    {
        // Explicit interaction colors also work under PreserveAuthoredUI (Title).
        foreach (var label in labels)
            if (label != null)
            {
                var color = TextColor;
                color.a = label.color.a;
                if (label.color != color) label.color = color;
            }
    }
}
