using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Button.onClick continues to handle left click / Submit. Right click only
// requests a separate confirmation; this component never changes save files.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class SaveSlotContextClick : MonoBehaviour, IPointerClickHandler
{
    Button button;
    UnityAction onRightClick;

    public void Configure(UnityAction action)
    {
        button = GetComponent<Button>();
        onRightClick = action;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button == PointerEventData.InputButton.Right &&
            isActiveAndEnabled && button != null && button.IsInteractable())
            onRightClick?.Invoke();
    }
}
