using UnityEngine;
using UnityEngine.UI;

/// <summary>Visible inventory grid cells, including unoccupied slots.</summary>
[DisallowMultipleComponent]
public sealed class InventorySlotFrame : MaskableGraphic
{
    public Color fillColor = new Color(.04f, .05f, .065f, .38f);
    public Color borderColor = new Color(.65f, .68f, .72f, .7f);
    [Min(.5f)] public float borderWidth = 1.2f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        float width = Mathf.Min(borderWidth, Mathf.Min(r.width, r.height) * .5f);
        Quad(vh, r.xMin, r.yMin, r.xMax, r.yMax, fillColor);
        Quad(vh, r.xMin, r.yMax - width, r.xMax, r.yMax, borderColor);
        Quad(vh, r.xMin, r.yMin, r.xMax, r.yMin + width, borderColor);
        Quad(vh, r.xMin, r.yMin + width, r.xMin + width, r.yMax - width, borderColor);
        Quad(vh, r.xMax - width, r.yMin + width, r.xMax, r.yMax - width, borderColor);
    }

    static void Quad(VertexHelper vh, float left, float bottom, float right, float top, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(left, bottom, 0f), tint, Vector2.zero);
        vh.AddVert(new Vector3(left, top, 0f), tint, Vector2.zero);
        vh.AddVert(new Vector3(right, top, 0f), tint, Vector2.zero);
        vh.AddVert(new Vector3(right, bottom, 0f), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
