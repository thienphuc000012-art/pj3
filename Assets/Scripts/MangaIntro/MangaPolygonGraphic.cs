using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a simple polygon using points normalized to this RectTransform.</summary>
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Manga Polygon Graphic")]
public sealed class MangaPolygonGraphic : MaskableGraphic
{
    [SerializeField]
    Vector2[] points =
    {
        new Vector2(0f, 0f), new Vector2(1f, 0f),
        new Vector2(1f, 1f), new Vector2(0f, 1f)
    };

    const float Epsilon = 0.000001f;
    readonly List<Vector2> polygon = new List<Vector2>();
    readonly List<int> remaining = new List<int>();
    readonly List<int> triangles = new List<int>();

    /// <summary>Copies a simple convex or concave polygon, in either winding, in normalized coordinates.</summary>
    public void SetPolygon(Vector2[] normalizedPoints)
    {
        points = normalizedPoints == null ? null : (Vector2[])normalizedPoints.Clone();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        polygon.Clear();
        remaining.Clear();
        triangles.Clear();
        if (points == null || points.Length < 3) return;

        foreach (Vector2 point in points)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y)) return;
            if (polygon.Count == 0 || (point - polygon[polygon.Count - 1]).sqrMagnitude > Epsilon * Epsilon)
                polygon.Add(point);
        }
        if (polygon.Count > 1 && (polygon[0] - polygon[polygon.Count - 1]).sqrMagnitude <= Epsilon * Epsilon)
            polygon.RemoveAt(polygon.Count - 1);

        // Redundant collinear vertices can otherwise prevent an ear from being found.
        bool removed = true;
        while (removed && polygon.Count > 3)
        {
            removed = false;
            for (int i = 0; i < polygon.Count; ++i)
            {
                Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                if (Mathf.Abs(Cross(current - previous, next - current)) > Epsilon) continue;
                polygon.RemoveAt(i);
                removed = true;
                break;
            }
        }
        if (polygon.Count < 3) return;

        float area = 0f;
        for (int i = 0; i < polygon.Count; ++i)
            area += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
        if (Mathf.Abs(area) <= Epsilon) return;
        for (int i = 0; i < polygon.Count; ++i)
            remaining.Add(area > 0f ? i : polygon.Count - 1 - i);

        while (remaining.Count > 3)
        {
            bool foundEar = false;
            for (int i = 0; i < remaining.Count; ++i)
            {
                int previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                int current = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];
                Vector2 a = polygon[previous];
                Vector2 b = polygon[current];
                Vector2 c = polygon[next];
                if (Cross(b - a, c - b) <= Epsilon) continue;

                bool containsPoint = false;
                foreach (int index in remaining)
                {
                    if (index == previous || index == current || index == next) continue;
                    Vector2 p = polygon[index];
                    if (Cross(b - a, p - a) >= -Epsilon &&
                        Cross(c - b, p - b) >= -Epsilon &&
                        Cross(a - c, p - c) >= -Epsilon)
                    {
                        containsPoint = true;
                        break;
                    }
                }
                if (containsPoint) continue;
                triangles.Add(previous);
                triangles.Add(current);
                triangles.Add(next);
                remaining.RemoveAt(i);
                foundEar = true;
                break;
            }

            // An invalid or self-intersecting outline should produce no partial mask.
            if (!foundEar) return;
        }
        triangles.AddRange(remaining);

        Rect rect = rectTransform.rect;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        foreach (Vector2 point in polygon)
        {
            vertex.position = new Vector3(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height);
            vertex.uv0 = point;
            vh.AddVert(vertex);
        }
        for (int i = 0; i < triangles.Count; i += 3)
            vh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
