using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Keeps long generated descriptions readable within their authored rectangle.</summary>
public sealed class ActionDescriptionScroll : ScrollRect, IPointerClickHandler
{
    TMP_Text label;
    string lastText;

    public static void Ensure(TMP_Text text)
    {
        if (text == null || !text.gameObject.activeSelf || text.transform.parent == null || text.transform.parent.GetComponent<ActionDescriptionScroll>() != null) return;
        var original = text.rectTransform;
        var parent = original.parent; int sibling = original.GetSiblingIndex();
        var go = new GameObject(original.name, typeof(RectTransform), typeof(PreserveAuthoredUI), typeof(RectMask2D), typeof(CanvasRenderer), typeof(Image));
        go.layer = text.gameObject.layer;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax; rect.pivot = original.pivot;
        rect.sizeDelta = original.sizeDelta; rect.anchoredPosition3D = original.anchoredPosition3D;
        rect.localRotation = original.localRotation; rect.localScale = original.localScale; rect.SetSiblingIndex(sibling);
        var image = go.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = true;
        var authoredLayout = text.GetComponent<LayoutElement>();
        if (authoredLayout != null)
        {
            var layout = go.AddComponent<LayoutElement>(); layout.minWidth = authoredLayout.minWidth; layout.minHeight = authoredLayout.minHeight;
            layout.preferredWidth = authoredLayout.preferredWidth; layout.preferredHeight = authoredLayout.preferredHeight;
            layout.flexibleWidth = authoredLayout.flexibleWidth; layout.flexibleHeight = authoredLayout.flexibleHeight;
            layout.ignoreLayout = authoredLayout.ignoreLayout;
        }
        original.name = "Text"; original.SetParent(rect, false);
        original.localScale = Vector3.one; original.localRotation = Quaternion.identity;
        original.anchorMin = new Vector2(0, 1); original.anchorMax = Vector2.one; original.pivot = new Vector2(.5f, 1);
        original.offsetMin = Vector2.zero; original.offsetMax = Vector2.zero;
        text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        var fitter = text.GetComponent<ContentSizeFitter>(); if (fitter == null) fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = go.AddComponent<ActionDescriptionScroll>(); scroll.label = text;
        scroll.viewport = rect; scroll.content = original; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = MovementType.Clamped; scroll.inertia = false; scroll.scrollSensitivity = 28;
    }

    protected override void OnEnable() { base.OnEnable(); TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TextChanged); }
    protected override void OnDisable() { TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TextChanged); base.OnDisable(); }
    void TextChanged(Object changed)
    {
        if (label == null || changed != label || lastText == label.text) return;
        lastText = label.text;
        if (content != null) { content.anchoredPosition = Vector2.zero; LayoutRebuilder.MarkLayoutForRebuild(content); }
    }
    public override void OnScroll(PointerEventData eventData)
    {
        if (content != null && viewport != null && content.rect.height > viewport.rect.height + .5f) { base.OnScroll(eventData); return; }
        var ancestor = transform.parent != null ? ExecuteEvents.GetEventHandler<IScrollHandler>(transform.parent.gameObject) : null;
        if (ancestor != null) ExecuteEvents.Execute(ancestor, eventData, ExecuteEvents.scrollHandler);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging || transform.parent == null) return;
        var button = transform.parent.GetComponent<Button>();
        if (button == null) button = transform.parent.Find("Select")?.GetComponent<Button>();
        if (button != null) button.OnPointerClick(eventData);
    }
}
