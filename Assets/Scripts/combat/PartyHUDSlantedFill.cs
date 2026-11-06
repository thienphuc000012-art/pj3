using UnityEngine;
using UnityEngine.UI;

// Keeps Image.fillAmount compatible with the existing HP damage-trail system.
[AddComponentMenu("UI/Party HUD Slanted Fill")]
public sealed class PartyHUDSlantedFill : Image
{
    public Vector2 bottomLeft;
    public Vector2 topLeft = new Vector2(.031f, .558f);
    public Vector2 topRight = Vector2.one;
    public Vector2 bottomRight = new Vector2(.963f, .351f);

    [System.Serializable]
    private struct FillSection
    {
        [Range(0f, 1f)] public float x;
        [Range(0f, 1f)] public float bottom;
        [Range(0f, 1f)] public float top;
    }

    [SerializeField, Tooltip("Optional normalized cross sections, ordered from left to right.")]
    private FillSection[] contour;

    public void CopyShape(PartyHUDSlantedFill source)
    {
        bottomLeft = source.bottomLeft; topLeft = source.topLeft;
        topRight = source.topRight; bottomRight = source.bottomRight;
        contour = source.contour;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (fillAmount <= 0) return;
        Rect rect = GetPixelAdjustedRect();
        if (contour != null && contour.Length >= 2)
        {
            PopulateContour(mesh, rect);
            return;
        }
        Add(mesh, rect, bottomLeft);
        Add(mesh, rect, topLeft);
        Add(mesh, rect, Vector2.Lerp(topLeft, topRight, fillAmount));
        Add(mesh, rect, Vector2.Lerp(bottomLeft, bottomRight, fillAmount));
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
    }

    private void PopulateContour(VertexHelper mesh, Rect rect)
    {
        FillSection left = contour[0];
        float fillEnd = Mathf.Lerp(left.x, contour[contour.Length - 1].x, fillAmount);
        Add(mesh, rect, new Vector2(left.x, left.bottom));
        Add(mesh, rect, new Vector2(left.x, left.top));

        for (int i = 1; i < contour.Length; i++)
        {
            FillSection right = contour[i];
            if (right.x <= left.x) continue;

            // Clip the final strip at the HP cutoff, preserving both bevels and the center crest.
            float x = Mathf.Min(right.x, fillEnd);
            float t = Mathf.InverseLerp(left.x, right.x, x);
            int first = mesh.currentVertCount - 2;
            Add(mesh, rect, new Vector2(x, Mathf.Lerp(left.bottom, right.bottom, t)));
            Add(mesh, rect, new Vector2(x, Mathf.Lerp(left.top, right.top, t)));
            mesh.AddTriangle(first, first + 1, first + 3);
            mesh.AddTriangle(first + 3, first + 2, first);
            if (x >= fillEnd) break;
            left = right;
        }
    }

    void Add(VertexHelper mesh, Rect rect, Vector2 point)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = new Vector3(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height);
        vertex.color = color;
        vertex.uv0 = new Vector2(.5f, .5f);
        mesh.AddVert(vertex);
    }
}
