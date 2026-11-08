using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a white-default rectangular or elliptical caption plate with a black outline.</summary>
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Manga Speech Bubble Graphic")]
public sealed class MangaSpeechBubbleGraphic : MaskableGraphic
{
    public bool ellipse;
    [Min(0f)] public float borderWidth = 2f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        int count = ellipse ? 64 : 4;
        float border = Mathf.Max(0f, borderWidth);
        Vector2 half = rect.size * 0.5f;
        Vector2 innerHalf = new Vector2(Mathf.Max(0f, half.x - border), Mathf.Max(0f, half.y - border));
        Color outline = new Color(0f, 0f, 0f, color.a);
        bool hasInterior = innerHalf.x > 0f && innerHalf.y > 0f;

        if (!hasInterior)
        {
            AddVertex(vh, rect.center, outline);
            for (int i = 0; i < count; ++i)
                AddVertex(vh, Point(rect.center, half, i, count), outline);
            for (int i = 0; i < count; ++i)
                vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
            return;
        }

        // The ring and interior meet without overlapping, preserving CanvasGroup fades.
        for (int i = 0; i < count; ++i)
        {
            AddVertex(vh, Point(rect.center, half, i, count), outline);
            AddVertex(vh, Point(rect.center, innerHalf, i, count), outline);
        }
        for (int i = 0; i < count; ++i)
        {
            int outer = i * 2;
            int nextOuter = ((i + 1) % count) * 2;
            vh.AddTriangle(outer, nextOuter, nextOuter + 1);
            vh.AddTriangle(outer, nextOuter + 1, outer + 1);
        }

        int center = vh.currentVertCount;
        AddVertex(vh, rect.center, color);
        for (int i = 0; i < count; ++i)
            AddVertex(vh, Point(rect.center, innerHalf, i, count), color);
        for (int i = 0; i < count; ++i)
            vh.AddTriangle(center, center + i + 1, center + (i + 1) % count + 1);
    }

    Vector2 Point(Vector2 center, Vector2 half, int index, int count)
    {
        if (ellipse)
        {
            float angle = index * Mathf.PI * 2f / count;
            return center + new Vector2(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y);
        }
        switch (index)
        {
            case 0: return center + new Vector2(-half.x, -half.y);
            case 1: return center + new Vector2(half.x, -half.y);
            case 2: return center + new Vector2(half.x, half.y);
            default: return center + new Vector2(-half.x, half.y);
        }
    }

    static void AddVertex(VertexHelper vh, Vector2 point, Color vertexColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = vertexColor;
        vertex.uv0 = Vector2.zero;
        vh.AddVert(vertex);
    }
}
