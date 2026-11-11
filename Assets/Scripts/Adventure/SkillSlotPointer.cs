using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillSlotPointer : MonoBehaviour, IPointerClickHandler, ISelectHandler
{
    public Action rightClick;
    public Action select;
    Image rowBackground;
    Image selectOverlay;
    GameUITextColor labelStyle;

    public void BindStyle(Image background, TMP_Text label)
    {
        rowBackground = background;
        selectOverlay = GetComponent<Image>();
        labelStyle = GameUITextColor.Apply(label);
    }

    public void SetSelected(bool selected)
    {
        if (rowBackground != null) rowBackground.color = selected ? Color.white : Color.black;
        if (selectOverlay != null) selectOverlay.color = selected ? (Color)new Color32(0, 0, 0, 100) : Color.clear;
        labelStyle?.SetColorOverride(selected ? Color.black : Color.white);
    }

    public void OnSelect(BaseEventData data) => select?.Invoke();
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Right) rightClick?.Invoke();
    }
}
