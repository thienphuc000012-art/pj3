using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Reveals a grunge button plate from left to right on hover or selection.</summary>
[DisallowMultipleComponent]
public sealed class UIButtonWipeReveal : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    const float RevealSeconds = .28f;
    const float HideSeconds = .18f;
    const float BaseAlpha = 0f;

    Image plate;
    Image reveal;
    Image revealEdge;
    Selectable selectable;
    float progress;
    bool hovered;
    bool selected;
    bool forcedReveal;

    public void Configure(Image background, Sprite sprite)
        => Configure(background, sprite, new Color(.70f, .58f, .34f, .96f));

    public void Configure(Image background, Sprite sprite, Color revealColor)
    {
        if (background == null || sprite == null) return;
        plate = background;
        selectable = GetComponent<Selectable>();
        plate.sprite = sprite;
        plate.type = Image.Type.Simple;
        plate.preserveAspect = false;
        plate.color = new Color(1f, 1f, 1f, BaseAlpha);
        plate.raycastTarget = true;

        if (reveal == null)
        {
            Transform existing = plate.transform.Find("Wipe Reveal");
            if (existing != null) reveal = existing.GetComponent<Image>();
            if (reveal == null)
            {
                var go = new GameObject("Wipe Reveal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(plate.transform, false);
                go.transform.SetAsFirstSibling();
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                reveal = go.GetComponent<Image>();
            }
        }

        reveal.sprite = sprite;
        reveal.type = Image.Type.Filled;
        reveal.fillMethod = Image.FillMethod.Horizontal;
        reveal.fillOrigin = (int)Image.OriginHorizontal.Left;
        reveal.fillAmount = Mathf.SmoothStep(0f, 1f, progress);
        reveal.color = revealColor;
        reveal.raycastTarget = false;
        reveal.maskable = plate.maskable;
        reveal.preserveAspect = false;
        reveal.transform.SetAsFirstSibling();

        if (revealEdge == null)
        {
            Transform existingEdge = plate.transform.Find("Wipe Reveal Edge");
            if (existingEdge != null) revealEdge = existingEdge.GetComponent<Image>();
            if (revealEdge == null)
            {
                var edgeObject = new GameObject("Wipe Reveal Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                edgeObject.transform.SetParent(plate.transform, false);
                var edgeRect = (RectTransform)edgeObject.transform;
                edgeRect.anchorMin = edgeRect.anchorMax = new Vector2(0f, .5f);
                edgeRect.pivot = new Vector2(.5f, .5f);
                edgeRect.sizeDelta = new Vector2(2f, 0f);
                edgeRect.offsetMin = new Vector2(0f, -6f);
                edgeRect.offsetMax = new Vector2(0f, 6f);
                revealEdge = edgeObject.GetComponent<Image>();
            }
        }
        revealEdge.color = new Color(1f, .88f, .63f, .86f);
        revealEdge.raycastTarget = false;
        revealEdge.maskable = plate.maskable;
        revealEdge.transform.SetSiblingIndex(reveal.transform.GetSiblingIndex() + 1);
        UpdateRevealEdge();
    }

    public void SetForcedReveal(bool value) => forcedReveal = value;

    void Update()
    {
        if (reveal == null) return;
        bool canReveal = selectable == null || selectable.IsInteractable();
        float target = canReveal && (hovered || selected || forcedReveal) ? 1f : 0f;
        float seconds = target > progress ? RevealSeconds : HideSeconds;
        progress = Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / seconds);
        reveal.fillAmount = Mathf.SmoothStep(0f, 1f, progress);
        UpdateRevealEdge();
    }

    void UpdateRevealEdge()
    {
        if (revealEdge == null || plate == null) return;
        revealEdge.gameObject.SetActive(progress > .015f && progress < .985f);
        var rect = (RectTransform)revealEdge.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(Mathf.SmoothStep(0f, 1f, progress), .5f);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    void OnDisable()
    {
        hovered = false;
        selected = false;
        progress = 0f;
        if (reveal != null) reveal.fillAmount = 0f;
        if (revealEdge != null) revealEdge.gameObject.SetActive(false);
    }
}
