using UnityEngine;
using UnityEngine.UI;

/// <summary>A clockwise charging ring with a bright head and fading halo, without textures or Bloom.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
public sealed class AdventureScanCooldownGraphic : MaskableGraphic
{
    float progress = 1f, thickness = 2.5f, glowWidth = 7f;
    bool cooling;
    Color tint = new Color(.45f, .92f, 1f);

    protected override void Awake()
    {
        base.Awake();
        useLegacyMeshGeneration = false;
    }

    public void SetState(float ready, bool onCooldown, Color ringTint, float ringThickness, float haloWidth)
    {
        ready = Mathf.Clamp01(ready);
        ringThickness = Mathf.Max(1f, ringThickness);
        haloWidth = Mathf.Max(0f, haloWidth);
        if (Mathf.Abs(progress - ready) < .0001f && cooling == onCooldown && tint == ringTint &&
            Mathf.Approximately(thickness, ringThickness) && Mathf.Approximately(glowWidth, haloWidth)) return;
        progress = ready; cooling = onCooldown; tint = ringTint;
        thickness = ringThickness; glowWidth = haloWidth;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        float halfSize = Mathf.Min(rect.width, rect.height) * .5f;
        float halo = Mathf.Min(glowWidth, halfSize * .22f);
        float radius = halfSize - halo - 1f;
        if (radius <= 1f) return;
        float stroke = Mathf.Clamp(thickness, 1f, radius * .35f);
        Vector2 center = rect.center;
        Disc(vh, center, radius - stroke, new Color(.015f, .04f, .055f, .28f));
        Arc(vh, center, radius, stroke, 0, 360, WithAlpha(tint, .18f));

        float sweep = progress * 360f;
        if (sweep > 0f)
        {
            // Several translucent bands make a soft outer glow on an overlay Canvas.
            for (int band = 3; band >= 1; band--)
                Arc(vh, center, radius + halo * band / 3f, stroke + halo * band / 3f,
                    0f, sweep, WithAlpha(tint, (cooling ? .025f : .045f) * (4 - band)));
            Arc(vh, center, radius, stroke, 0f, sweep, WithAlpha(tint, cooling ? .65f : .95f));
        }
        if (!cooling) return;

        // The leading light travels once around the circle over the whole cooldown.
        float tail = Mathf.Min(65f, sweep);
        Arc(vh, center, radius, stroke * 1.65f, sweep - tail, tail, WithAlpha(tint, .85f), true);
        Vector2 head = center + Direction(sweep) * (radius - stroke * .5f);
        for (int band = 3; band >= 1; band--)
            Disc(vh, head, stroke + halo * band / 3f, WithAlpha(tint, .045f * (4 - band)));
        Disc(vh, head, stroke * .85f, new Color(.9f, 1f, 1f, .98f));
    }

    static Vector2 Direction(float clockwiseDegrees)
    {
        float radians = clockwiseDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    static Color WithAlpha(Color value, float opacity)
    {
        value.a *= opacity;
        return value;
    }

    static void Arc(VertexHelper vh, Vector2 center, float outerRadius, float width,
        float start, float sweep, Color tint, bool fadeTail = false)
    {
        if (sweep <= .001f) return;
        float innerRadius = Mathf.Max(0f, outerRadius - width);
        int segments = Mathf.Max(1, Mathf.CeilToInt(sweep / 3f));
        for (int i = 0; i < segments; i++)
        {
            float from = (float)i / segments, to = (float)(i + 1) / segments;
            Vector2 a = Direction(start + sweep * from), b = Direction(start + sweep * to);
            Color ca = tint, cb = tint;
            if (fadeTail) { ca.a *= from * from; cb.a *= to * to; }
            int index = vh.currentVertCount;
            vh.AddVert(center + a * innerRadius, ca, Vector2.zero);
            vh.AddVert(center + a * outerRadius, ca, Vector2.zero);
            vh.AddVert(center + b * outerRadius, cb, Vector2.zero);
            vh.AddVert(center + b * innerRadius, cb, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }

    static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        const int segments = 32;
        int index = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i <= segments; i++)
            vh.AddVert(center + Direction(i * 360f / segments) * radius, tint, Vector2.zero);
        for (int i = 0; i < segments; i++) vh.AddTriangle(index, index + i + 1, index + i + 2);
    }
}
