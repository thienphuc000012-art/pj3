using UnityEngine;
using UnityEngine.UI;

/// <summary>A textured ink field with its feathered edge facing into the game view.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class TutorialLessonBackground : MonoBehaviour
{
    public TutorialLessonStyle style;
    [SerializeField] Image ink;
    [SerializeField] TutorialLessonOrnament ornament;
    public void Build(TutorialLessonStyle value)
    {
        if (value == null || value.inkBrush == null || value.inkMaterial == null) return;
        style = value;
        if (TryGetComponent<Image>(out var plate)) plate.color = Color.clear;
        foreach (string name in new[] { "Pattern", "Frame", "Trim" })
        { var old = transform.Find(name); if (old != null) old.gameObject.SetActive(false); }
        if (ink == null) ink = transform.Find("Ink Background")?.GetComponent<Image>();
        if (ink == null) ink = Child("Ink Background").gameObject.AddComponent<Image>();
        ink.transform.SetAsFirstSibling(); ink.sprite = style.inkBrush; ink.material = style.inkMaterial;
        ink.color = new Color(1, 1, 1, style.opacity); ink.raycastTarget = false; ink.type = Image.Type.Simple; ink.useSpriteMesh = false;
        if (ornament == null) ornament = transform.Find("Corner Ornament")?.GetComponent<TutorialLessonOrnament>();
        if (ornament == null) ornament = Child("Corner Ornament").gameObject.AddComponent<TutorialLessonOrnament>();
        ornament.transform.SetSiblingIndex(1); ornament.raycastTarget = false;
        ornament.color = style.ornamentColor; ornament.borderColor = style.borderColor; ornament.SetVerticesDirty();
        Align();
    }
    RectTransform Child(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)); root.layer = gameObject.layer;
        root.transform.SetParent(transform, false); var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    public void Align()
    {
        if (ink == null || ornament == null || style == null || !(transform.parent is RectTransform parent)) return;
        var page = (RectTransform)transform;
        ink.color = new Color(1, 1, 1, style.opacity);
        if (ink.sprite != style.inkBrush) ink.sprite = style.inkBrush;
        if (ink.material != style.inkMaterial) ink.material = style.inkMaterial;
        ornament.color = style.ornamentColor;
        if (ornament.borderColor != style.borderColor) { ornament.borderColor = style.borderColor; ornament.SetVerticesDirty(); }
        bool right = parent.InverseTransformPoint(page.TransformPoint(page.rect.center)).x >= parent.rect.center.x;
        var rect = ink.rectTransform;
        rect.anchorMin = new Vector2(right ? -style.brushOverhang : 0, -.012f);
        rect.anchorMax = new Vector2(right ? 1 : 1 + style.brushOverhang, 1.012f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = new Vector3(right ? -1 : 1, 1, 1);
        ornament.rectTransform.localScale = new Vector3(right ? -1 : 1, 1, 1);
    }
    void LateUpdate() => Align();
}
