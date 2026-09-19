using UnityEngine;
using UnityEngine.UI;

// Vertex alpha keeps the bottom of the selected card completely untouched.
public class SelectionFadeGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        float bottom = Mathf.Lerp(r.yMax, r.yMin, .55f);
        Color clear = color; clear.a = 0;
        mesh.AddVert(new Vector3(r.xMin, bottom), clear, Vector2.zero);
        mesh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.up);
        mesh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.one);
        mesh.AddVert(new Vector3(r.xMax, bottom), clear, Vector2.right);
        mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
    }
}
