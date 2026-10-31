using UnityEngine;
using UnityEngine.UI;

/// <summary>A small paint-like diamond for the focused combat command.</summary>
[DisallowMultipleComponent]
public sealed class CombatButtonFocusGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = GetPixelAdjustedRect();
        Vector2 center = r.center;
        vh.AddVert(new Vector3(r.xMin, center.y, 0f), color, Vector2.zero);
        vh.AddVert(new Vector3(center.x, r.yMax, 0f), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, center.y, 0f), color, Vector2.zero);
        vh.AddVert(new Vector3(center.x, r.yMin, 0f), color, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
}
