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

    public void CopyShape(PartyHUDSlantedFill source)
    {
        bottomLeft = source.bottomLeft; topLeft = source.topLeft;
        topRight = source.topRight; bottomRight = source.bottomRight;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (fillAmount <= 0) return;
        Rect rect = GetPixelAdjustedRect();
        Add(mesh, rect, bottomLeft);
        Add(mesh, rect, topLeft);
        Add(mesh, rect, Vector2.Lerp(topLeft, topRight, fillAmount));
        Add(mesh, rect, Vector2.Lerp(bottomLeft, bottomRight, fillAmount));
        mesh.AddTriangle(0, 1, 2);
        mesh.AddTriangle(2, 3, 0);
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
