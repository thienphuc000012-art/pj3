using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a restrained circular hold meter without requiring a texture asset.</summary>
public sealed class HoldProgressRingGraphic : MaskableGraphic
{
    const int Segments = 48;
    float progress;

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * .48f;
        float thickness = Mathf.Clamp(radius * .12f, 2f, 4f);
        Vector2 center = rect.center;
        Color track = new Color(.76f, .69f, .55f, .42f);
        Color fill = new Color(1f, .87f, .60f, .98f);
        AddArc(vh, center, radius, thickness, 1f, track);
        if (progress > 0f) AddArc(vh, center, radius, thickness, progress, fill);
    }

    static void AddArc(VertexHelper vh, Vector2 center, float radius, float thickness, float amount, Color color)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(Segments * amount));
        float startAngle = -90f;
        for (int i = 0; i < count; i++)
        {
            float t0 = amount * i / count;
            float t1 = amount * (i + 1) / count;
            float a0 = (startAngle + 360f * t0) * Mathf.Deg2Rad;
            float a1 = (startAngle + 360f * t1) * Mathf.Deg2Rad;
            Vector2 outer0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
            Vector2 outer1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            Vector2 inner1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (radius - thickness);
            Vector2 inner0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (radius - thickness);
            int first = vh.currentVertCount;
            Color32 tint = color;
            vh.AddVert(outer0, tint, Vector4.zero);
            vh.AddVert(outer1, tint, Vector4.zero);
            vh.AddVert(inner1, tint, Vector4.zero);
            vh.AddVert(inner0, tint, Vector4.zero);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first + 2, first + 3, first);
        }
    }
}
