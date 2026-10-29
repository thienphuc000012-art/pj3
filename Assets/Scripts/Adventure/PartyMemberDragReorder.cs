using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PartyMemberDragReorder : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    int memberIndex;
    RectTransform dragRoot;
    Action<int, int> swapMembers;
    Canvas rootCanvas;
    RectTransform dragGhost;
    CanvasGroup sourceCanvasGroup;
    float originalCanvasAlpha;
    Vector2 pointerOffset;
    bool dragging;
    bool suppressNextClick;
    float suppressClickUntil;

    public void Configure(int index, RectTransform memberCard, Action<int, int> onSwap)
    {
        memberIndex = index;
        dragRoot = memberCard;
        swapMembers = onSwap;
        if (dragRoot != null)
        {
            sourceCanvasGroup = dragRoot.GetComponent<CanvasGroup>();
            if (sourceCanvasGroup == null) sourceCanvasGroup = dragRoot.gameObject.AddComponent<CanvasGroup>();
        }
        var canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    public bool SuppressClick()
    {
        return suppressNextClick && Time.unscaledTime <= suppressClickUntil;
    }

    void Update()
    {
        // A drag released outside the picker produces no Button click to consume.
        // Expire the suppression so the next deliberate selection still works.
        if (suppressNextClick && Time.unscaledTime > suppressClickUntil) suppressNextClick = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || dragRoot == null || rootCanvas == null || swapMembers == null)
            return;

        dragging = true;
        suppressNextClick = true;
        suppressClickUntil = Time.unscaledTime + 0.25f;
        originalCanvasAlpha = sourceCanvasGroup.alpha;
        sourceCanvasGroup.alpha = originalCanvasAlpha * 0.32f;

        dragGhost = Instantiate(dragRoot, rootCanvas.transform, false);
        dragGhost.name = "Party Member Card Drag Preview";
        dragGhost.anchorMin = dragGhost.anchorMax = new Vector2(0.5f, 0.5f);
        dragGhost.pivot = dragRoot.pivot;
        dragGhost.sizeDelta = dragRoot.rect.size;
        dragGhost.localScale = dragRoot.localScale;
        var ghostGroup = dragGhost.GetComponent<CanvasGroup>();
        if (ghostGroup == null) ghostGroup = dragGhost.gameObject.AddComponent<CanvasGroup>();
        ghostGroup.alpha = 0.94f;
        ghostGroup.interactable = false;
        ghostGroup.blocksRaycasts = false;
        foreach (var graphic in dragGhost.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        foreach (var selectable in dragGhost.GetComponentsInChildren<Selectable>(true))
            selectable.interactable = false;
        foreach (var dragHandler in dragGhost.GetComponentsInChildren<PartyMemberDragReorder>(true))
            dragHandler.enabled = false;

        var canvasRect = rootCanvas.transform as RectTransform;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
            eventData.position, eventCamera, out Vector2 pointerInCanvas))
        {
            Vector2 sourcePivotInCanvas = canvasRect.InverseTransformPoint(dragRoot.TransformPoint(Vector3.zero));
            pointerOffset = pointerInCanvas - sourcePivotInCanvas;
        }
        MoveGhost(eventData);
        dragGhost.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging) MoveGhost(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!dragging)
        {
            var source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponentInParent<PartyMemberDragReorder>()
                : null;
            if (source != null && source != this && source.dragging)
                source.swapMembers?.Invoke(source.memberIndex, memberIndex);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragging = false;
        if (sourceCanvasGroup != null) sourceCanvasGroup.alpha = originalCanvasAlpha;
        if (dragGhost != null) Destroy(dragGhost.gameObject);
        dragGhost = null;
    }

    void MoveGhost(PointerEventData eventData)
    {
        if (dragGhost == null || rootCanvas == null) return;
        var canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null) return;
        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position,
            eventCamera, out Vector2 localPoint))
            dragGhost.anchoredPosition = localPoint - pointerOffset;
    }
}
