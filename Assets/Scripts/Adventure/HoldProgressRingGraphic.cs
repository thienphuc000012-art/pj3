using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws a circular or rectangular hold meter without a texture asset.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HoldProgressRingGraphic : MaskableGraphic
{
    const int Segments = 48;
    [SerializeField] bool followRect;
    [SerializeField] Color trackColor = new Color(.76f, .69f, .55f, .42f);
    [SerializeField] Color fillColor = new Color(1f, .87f, .60f, .98f);
    float progress;

    public void ConfigureOutline(Color track, Color fill)
    {
        followRect = true;
        trackColor = track;
        fillColor = fill;
        SetVerticesDirty();
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (followRect)
        {
            if (trackColor.a > 0f) AddOutline(vh, rect, 1f, trackColor);
            if (progress > 0f) AddOutline(vh, rect, progress, fillColor);
            return;
        }

        float radius = Mathf.Min(rect.width, rect.height) * .48f;
        float thickness = Mathf.Clamp(radius * .12f, 2f, 4f);
        Vector2 center = rect.center;
        AddArc(vh, center, radius, thickness, 1f, trackColor);
        if (progress > 0f) AddArc(vh, center, radius, thickness, progress, fillColor);
    }

    static void AddOutline(VertexHelper vh, Rect outer, float amount, Color32 tint)
    {
        float size = Mathf.Min(outer.width, outer.height);
        if (size <= 0f) return;

        float inset = Mathf.Min(2.5f, size * .25f);
        Rect inner = Rect.MinMaxRect(outer.xMin + inset, outer.yMin + inset, outer.xMax - inset, outer.yMax - inset);
        var outerBottomRight = new Vector2(outer.xMax, outer.yMin);
        var innerBottomRight = new Vector2(inner.xMax, inner.yMin);
        var outerTopLeft = new Vector2(outer.xMin, outer.yMax);
        var innerTopLeft = new Vector2(inner.xMin, inner.yMax);
        float width = outer.width - inset;
        float height = outer.height - inset;
        float remaining = (width + height) * 2f * amount;

        AddSide(vh, outer.min, outerBottomRight, inner.min, innerBottomRight, width, tint, ref remaining);
        AddSide(vh, outerBottomRight, outer.max, innerBottomRight, inner.max, height, tint, ref remaining);
        AddSide(vh, outer.max, outerTopLeft, inner.max, innerTopLeft, width, tint, ref remaining);
        AddSide(vh, outerTopLeft, outer.min, innerTopLeft, inner.min, height, tint, ref remaining);
    }

    static void AddSide(VertexHelper vh, Vector2 outerStart, Vector2 outerEnd, Vector2 innerStart, Vector2 innerEnd,
        float length, Color32 tint, ref float remaining)
    {
        if (remaining <= 0f) return;

        float fraction = Mathf.Min(remaining / length, 1f);
        AddQuad(vh, outerStart, Vector2.LerpUnclamped(outerStart, outerEnd, fraction),
            Vector2.LerpUnclamped(innerStart, innerEnd, fraction), innerStart, tint);
        remaining -= length;
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
            AddQuad(vh, outer0, outer1, inner1, inner0, color);
        }
    }

    static void AddQuad(VertexHelper vh, Vector2 outer0, Vector2 outer1, Vector2 inner1, Vector2 inner0, Color32 tint)
    {
        int first = vh.currentVertCount;
        vh.AddVert(outer0, tint, Vector4.zero);
        vh.AddVert(outer1, tint, Vector4.zero);
        vh.AddVert(inner1, tint, Vector4.zero);
        vh.AddVert(inner0, tint, Vector4.zero);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first + 2, first + 3, first);
    }
}
