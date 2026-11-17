using UnityEngine;
using UnityEngine.UI;

/// <summary>Light diamond strokes for lesson backgrounds, clipped by their Pattern container.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialDiamondPattern : MaskableGraphic
{
    [Min(40f)] public float spacing = 150f;
    [Min(.2f)] public float lineWidth = .8f;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var rect = GetPixelAdjustedRect(); float step = Mathf.Max(40, spacing), half = step * .52f;
        for (float y = rect.yMin - half; y < rect.yMax + step; y += step)
            for (float x = rect.xMin - half; x < rect.xMax + step; x += step)
            {
                Vector2 top = new Vector2(x, y + half), right = new Vector2(x + half, y), bottom = new Vector2(x, y - half), left = new Vector2(x - half, y);
                Stroke(mesh, top, right); Stroke(mesh, right, bottom); Stroke(mesh, bottom, left); Stroke(mesh, left, top);
            }
    }
    void Stroke(VertexHelper mesh, Vector2 from, Vector2 to)
    {
        Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * lineWidth * .5f;
        int start = mesh.currentVertCount;
        mesh.AddVert(from - normal, color, Vector2.zero); mesh.AddVert(from + normal, color, Vector2.zero);
        mesh.AddVert(to + normal, color, Vector2.zero); mesh.AddVert(to - normal, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
    }
}
