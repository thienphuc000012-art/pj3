using UnityEngine;
using UnityEngine.UI;

// A small animated glint rendered on filled Stain slots. No texture or raycast needed.
public sealed class StainSparkleGraphic : MaskableGraphic
{
    int slot;
    bool lit;

    public static StainSparkleGraphic Attach(Image icon, int index)
    {
        var existing = icon.transform.Find("Stain Sparkle");
        if (existing != null && existing.TryGetComponent(out StainSparkleGraphic found)) return found;
        var go = new GameObject("Stain Sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(StainSparkleGraphic));
        go.layer = icon.gameObject.layer;
        go.transform.SetParent(icon.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var graphic = go.GetComponent<StainSparkleGraphic>();
        graphic.slot = index;
        graphic.raycastTarget = false;
        graphic.color = new Color(1f, .91f, .7f, .85f);
        graphic.enabled = false;
        return graphic;
    }

    public void SetLit(bool value)
    {
        lit = value;
        if (enabled != value) enabled = value;
    }

    void Update()
    {
        if (lit) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!lit) return;
        Rect rect = rectTransform.rect;
        float phase = Mathf.Repeat(Time.unscaledTime * .4f + slot * .17f, 1f);
        float pulse = Mathf.Pow(Mathf.Sin(phase * Mathf.PI), 6f);
        Color32 tint = new Color(color.r, color.g, color.b, color.a * pulse);
        float size = Mathf.Min(rect.width, rect.height) * (.045f + .035f * pulse);
        Vector2 center = new Vector2(rect.xMin + rect.width * .66f, rect.yMin + rect.height * .68f);
        int first = mesh.currentVertCount;
        mesh.AddVert(center + Vector2.up * size, tint, Vector2.zero);
        mesh.AddVert(center + Vector2.right * (size * .2f), tint, Vector2.zero);
        mesh.AddVert(center + Vector2.down * size, tint, Vector2.zero);
        mesh.AddVert(center + Vector2.left * (size * .2f), tint, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        first = mesh.currentVertCount;
        mesh.AddVert(center + Vector2.left * size, tint, Vector2.zero);
        mesh.AddVert(center + Vector2.up * (size * .2f), tint, Vector2.zero);
        mesh.AddVert(center + Vector2.right * size, tint, Vector2.zero);
        mesh.AddVert(center + Vector2.down * (size * .2f), tint, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
    }
}
