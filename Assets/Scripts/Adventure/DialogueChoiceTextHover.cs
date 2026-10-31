using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueChoiceTextHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    Button button;
    bool hovered;
    Color normalColor = new Color(.5f, .5f, .5f, 1f);
    Color hoverColor = Color.white;

    public Color TextColor => hovered && (button == null || button.IsInteractable()) ? hoverColor : normalColor;

    public void Configure(TMP_Text label, Color normal, Color highlight)
    {
        button = GetComponent<Button>();
        normalColor = normal;
        hoverColor = highlight;
        hovered = false;
        GameUITextColor.Apply(label)?.SetChoiceHover(this);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    void OnDisable() => hovered = false;
}
