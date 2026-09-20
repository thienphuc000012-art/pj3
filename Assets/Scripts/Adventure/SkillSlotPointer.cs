using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SkillSlotPointer : MonoBehaviour, IPointerClickHandler
{
    public Action rightClick;
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Right) rightClick?.Invoke();
    }
}
