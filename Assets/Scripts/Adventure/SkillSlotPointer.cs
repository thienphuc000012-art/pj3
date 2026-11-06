using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SkillSlotPointer : MonoBehaviour, IPointerClickHandler, ISelectHandler
{
    public Action rightClick;
    public Action select;

    public void SetSelected(bool selected)
    {
        foreach (var wipe in GetComponentsInChildren<UIButtonWipeReveal>(true))
        {
            wipe.SetSelectionOnly(true);
            wipe.SetForcedReveal(selected);
        }
        foreach (var edge in GetComponentsInChildren<UIHoverOutline>(true))
        {
            edge.SetSelectionOnly(true);
            edge.SetSelected(selected);
        }
    }

    public void OnSelect(BaseEventData data) => select?.Invoke();
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Right) rightClick?.Invoke();
    }
}
