using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws restrained corner filigree for the game's dark fantasy panels.</summary>
[DisallowMultipleComponent]
public sealed class GameUIFrameGraphic : MaskableGraphic
{
    Color tint = new Color(.83f, .67f, .38f, .44f);
    bool ornate;

    public void Configure(Color color, bool useOrnament)
    {
        tint = color;
        ornate = useOrnament;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float inset = Mathf.Clamp(Mathf.Min(r.width, r.height) * .035f, 3f, 7f);
        float length = Mathf.Clamp(Mathf.Min(r.width, r.height) * (ornate ? .11f : .14f), 6f, ornate ? 22f : 14f);
        float thickness = ornate ? 1.25f : 1f;

        DrawCorner(vh, r.xMin + inset, r.yMin + inset, 1, 1, length, thickness);
        DrawCorner(vh, r.xMax - inset, r.yMin + inset, -1, 1, length, thickness);
        DrawCorner(vh, r.xMin + inset, r.yMax - inset, 1, -1, length, thickness);
        DrawCorner(vh, r.xMax - inset, r.yMax - inset, -1, -1, length, thickness);

        if (ornate)
        {
            // A small cut-stone motif and two hairlines make large panels read
            // as game menus while leaving their content and background visible.
            float middle = (r.xMin + r.xMax) * .5f;
            float top = r.yMax - inset;
            float diamond = 3.2f;
            Vector2 a = new Vector2(middle, top);
            Vector2 b = a + Vector2.right * diamond;
            Vector2 c = a + Vector2.down * diamond;
            Vector2 d = a + Vector2.left * diamond;
            Vector2 e = a + Vector2.up * diamond;
            AddLine(vh, b, c, thickness, tint);
            AddLine(vh, c, d, thickness, tint);
            AddLine(vh, d, e, thickness, tint);
            AddLine(vh, e, b, thickness, tint);
            AddBar(vh, new Vector2(middle - 25f, top), new Vector2(middle - 7f, top), .7f, tint);
            AddBar(vh, new Vector2(middle + 7f, top), new Vector2(middle + 25f, top), .7f, tint);
        }
    }

    void DrawCorner(VertexHelper vh, float x, float y, int xDirection, int yDirection, float length, float thickness)
    {
        Vector2 origin = new Vector2(x, y);
        Vector2 horizontalEnd = origin + Vector2.right * (xDirection * length);
        Vector2 verticalEnd = origin + Vector2.up * (yDirection * length);
        AddBar(vh, origin, horizontalEnd, thickness, tint);
        AddBar(vh, origin, verticalEnd, thickness, tint);

        Vector2 inner = origin + new Vector2(xDirection * 2.4f, yDirection * 2.4f);
        AddBar(vh, inner, inner + Vector2.right * (xDirection * length * .62f), .65f, tint);
        AddBar(vh, inner, inner + Vector2.up * (yDirection * length * .62f), .65f, tint);
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color) => AddBar(vh, a, b, width, color);

    static void AddBar(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < .0001f) return;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
        int start = vh.currentVertCount;
        AddVertex(vh, a - normal, color);
        AddVertex(vh, a + normal, color);
        AddVertex(vh, b + normal, color);
        AddVertex(vh, b - normal, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
