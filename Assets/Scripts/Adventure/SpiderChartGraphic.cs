using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a five-axis attribute chart with an optional pending-upgrade preview.</summary>
[DisallowMultipleComponent]
public sealed class SpiderChartGraphic : MaskableGraphic
{
    const int AxisCount = 5;
    readonly float[] currentValues = new float[AxisCount];
    readonly float[] previewValues = new float[AxisCount];
    bool hasPreview;

    static readonly Color GridColor = new Color(.76f, .79f, .82f, .36f);
    static readonly Color CurrentColor = new Color(.91f, .72f, .40f, 1f);
    static readonly Color CurrentFill = new Color(.91f, .72f, .40f, .13f);
    static readonly Color PreviewColor = new Color(.29f, .82f, 1f, 1f);
    static readonly Color PreviewFill = new Color(.29f, .82f, 1f, .20f);

    public void SetValues(float[] current, float[] preview)
    {
        hasPreview = current != null && preview != null && current.Length >= AxisCount && preview.Length >= AxisCount;
        for (int i = 0; i < AxisCount; i++)
        {
            currentValues[i] = hasPreview ? Mathf.Clamp01(current[i]) : 0f;
            previewValues[i] = hasPreview ? Mathf.Clamp01(preview[i]) : 0f;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width * .22f, rect.height * .27f);
        Vector2 center = rect.center + Vector2.down * (rect.height * .025f);

        // Five nested pentagons and their spokes form the neutral web.
        for (int ring = 1; ring <= 5; ring++)
        {
            Vector2[] points = Points(center, radius * ring / 5f, null);
            for (int i = 0; i < AxisCount; i++)
                AddLine(mesh, points[i], points[(i + 1) % AxisCount], 1f, GridColor);
        }
        Vector2[] outer = Points(center, radius, null);
        for (int i = 0; i < AxisCount; i++) AddLine(mesh, center, outer[i], 1f, GridColor);

        Vector2[] current = Points(center, radius, currentValues);
        AddFill(mesh, center, current, CurrentFill);
        bool changed = false;
        for (int i = 0; i < AxisCount; i++)
            if (!Mathf.Approximately(currentValues[i], previewValues[i])) { changed = true; break; }

        Vector2[] preview = Points(center, radius, previewValues);
        if (hasPreview && changed) AddFill(mesh, center, preview, PreviewFill);

        AddOutline(mesh, current, CurrentColor);
        if (hasPreview && changed) AddOutline(mesh, preview, PreviewColor);
        AddMarkers(mesh, current, CurrentColor);
        if (hasPreview && changed) AddMarkers(mesh, preview, PreviewColor);
    }

    static Vector2[] Points(Vector2 center, float radius, float[] values)
    {
        var points = new Vector2[AxisCount];
        for (int i = 0; i < AxisCount; i++)
        {
            float angle = (90f - 72f * i) * Mathf.Deg2Rad;
            float scale = values == null ? 1f : values[i];
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * scale);
        }
        return points;
    }

    static void AddFill(VertexHelper mesh, Vector2 center, Vector2[] points, Color tint)
    {
        int centerIndex = mesh.currentVertCount;
        AddVertex(mesh, center, tint);
        for (int i = 0; i < points.Length; i++) AddVertex(mesh, points[i], tint);
        for (int i = 0; i < points.Length; i++)
            mesh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + (i + 1) % points.Length);
    }

    static void AddOutline(VertexHelper mesh, Vector2[] points, Color tint)
    {
        for (int i = 0; i < points.Length; i++) AddLine(mesh, points[i], points[(i + 1) % points.Length], 2f, tint);
    }

    static void AddMarkers(VertexHelper mesh, Vector2[] points, Color tint)
    {
        const int sides = 8;
        const float radius = 3f;
        foreach (Vector2 point in points)
        {
            int center = mesh.currentVertCount;
            AddVertex(mesh, point, tint);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                AddVertex(mesh, point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
            }
            for (int i = 0; i < sides; i++) mesh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % sides);
        }
    }

    static void AddLine(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < .0001f) return;
        Vector2 offset = new Vector2(-direction.y, direction.x).normalized * (width * .5f);
        int first = mesh.currentVertCount;
        AddVertex(mesh, from - offset, tint);
        AddVertex(mesh, from + offset, tint);
        AddVertex(mesh, to + offset, tint);
        AddVertex(mesh, to - offset, tint);
        mesh.AddTriangle(first, first + 1, first + 2);
        mesh.AddTriangle(first, first + 2, first + 3);
    }

    static void AddVertex(VertexHelper mesh, Vector2 point, Color tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = point;
        vertex.color = tint;
        mesh.AddVert(vertex);
    }
}
