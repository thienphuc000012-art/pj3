using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

/// <summary>
/// A cumulative reveal silhouette, or an independent decorative ink stroke.
/// SoftMask reads texture alpha and UVs, so feathering uses a texture, not vertex alpha.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Effects/Ink Reveal Graphic")]
public sealed class InkRevealGraphic : MaskableGraphic
{
    public enum GraphicMode { CumulativeMask, InkFront }

    [SerializeField] GraphicMode graphicMode;
    [SerializeField, Range(0f, 1f)] float progress;
    [SerializeField, Range(8, 128)] int edgeSegments = 64;
    [SerializeField, Min(0f)] float edgeRoughness = 28f;
    [SerializeField, Min(0.1f)] float featherWidth = 5f;

    Texture2D featherTexture;
    bool rightToLeft;
    float inkWidth, headAngle;
    bool flipHead;
    Color inkTint;
    Sprite inkSprite;

    public override Texture mainTexture => graphicMode == GraphicMode.CumulativeMask
        ? GetFeatherTexture() : inkSprite != null ? inkSprite.texture : Texture2D.whiteTexture;

    public void ConfigureMask(float roughness, float feather, int segments, bool reverse)
    {
        graphicMode = GraphicMode.CumulativeMask;
        edgeRoughness = Mathf.Max(0f, roughness);
        featherWidth = Mathf.Max(0.1f, feather);
        edgeSegments = Mathf.Clamp(segments, 8, 128);
        rightToLeft = reverse;
        color = Color.white;
        raycastTarget = false;
        SetAllDirty();
    }

    public void SetMaskProgress(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(progress, value)) return;
        progress = value;
        SetVerticesDirty();
    }

    public void ConfigureInkFront(float roughness, float feather, int segments, bool reverse)
    {
        ConfigureMask(roughness, feather, segments, reverse);
        graphicMode = GraphicMode.InkFront;
    }

    public void SetVisualFront(float value, float width, Color tint, float angle, bool flip, Sprite sprite)
    {
        progress = Mathf.Clamp01(value);
        inkWidth = Mathf.Max(1f, width);
        inkTint = tint;
        headAngle = Mathf.Clamp(angle, -30f, 30f);
        flipHead = flip;
        if (inkSprite != sprite)
        {
            inkSprite = sprite;
            SetMaterialDirty();
        }
        SetVerticesDirty();
    }

    Texture2D GetFeatherTexture()
    {
        if (featherTexture != null) return featherTexture;
        const int width = 64;
        featherTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false, true)
        {
            name = "Ink Reveal Edge Alpha (runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width];
        for (int i = 0; i < width; i++)
            pixels[i] = new Color32(255, 255, 255,
                (byte)Mathf.RoundToInt(255f * (1f - Mathf.SmoothStep(0f, 1f, i / (width - 1f)))));
        featherTexture.SetPixels32(pixels);
        featherTexture.Apply(false, true);
        return featherTexture;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (graphicMode == GraphicMode.InkFront) DrawInkFront(vh);
        else DrawReveal(vh);
    }

    void DrawReveal(VertexHelper vh)
    {
        if (progress <= 0f) return;
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;
        if (progress >= 1f)
        {
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), Color.white,
                0f, 0f, 0f, 0f);
            return;
        }

        float roughness = Roughness(r);
        float feather = Feather(r);
        float front = FrontPosition(r, progress);
        for (int i = 0; i < edgeSegments; i++)
        {
            float y0 = Mathf.Lerp(r.yMin, r.yMax, i / (float)edgeSegments);
            float y1 = Mathf.Lerp(r.yMin, r.yMax, (i + 1f) / edgeSegments);
            float edge0 = front + EdgeNoise(i) * roughness;
            float edge1 = front + EdgeNoise(i + 1) * roughness;
            float inner0 = Mathf.Clamp(edge0 - feather, r.xMin, r.xMax);
            float inner1 = Mathf.Clamp(edge1 - feather, r.xMin, r.xMax);
            if (inner0 > r.xMin || inner1 > r.xMin)
                Quad(vh, new Vector2(r.xMin, y0), new Vector2(inner0, y0),
                    new Vector2(inner1, y1), new Vector2(r.xMin, y1), Color.white,
                    0f, 0f, 0f, 0f);

            float outer0 = Mathf.Clamp(edge0, r.xMin, r.xMax);
            float outer1 = Mathf.Clamp(edge1, r.xMin, r.xMax);
            if (outer0 <= inner0 && outer1 <= inner1) continue;
            // Clamp geometry to the panel but keep its original feather UVs.
            float u00 = Mathf.Clamp01((inner0 - edge0 + feather) / feather);
            float u10 = Mathf.Clamp01((outer0 - edge0 + feather) / feather);
            float u11 = Mathf.Clamp01((outer1 - edge1 + feather) / feather);
            float u01 = Mathf.Clamp01((inner1 - edge1 + feather) / feather);
            Quad(vh, new Vector2(inner0, y0), new Vector2(outer0, y0),
                new Vector2(outer1, y1), new Vector2(inner1, y1), Color.white,
                u00, u10, u11, u01);
        }
    }

    // Fixed in y, so increasing progress never hides a previously revealed pixel.
    static float EdgeNoise(int i) => Mathf.Sin(i * 1.77f) * 0.47f
        + Mathf.Sin(i * 0.41f + 0.8f) * 0.31f + Mathf.Sin(i * 3.71f) * 0.22f;

    float Roughness(Rect rect) => Mathf.Min(edgeRoughness, rect.width * .15f);
    float Feather(Rect rect) => Mathf.Min(featherWidth, Mathf.Max(.1f, rect.width * .1f));

    float FrontPosition(Rect rect, float value)
    {
        float margin = Roughness(rect) + Feather(rect);
        return Mathf.Lerp(rect.xMin - margin, rect.xMax + margin, value);
    }

    void DrawInkFront(VertexHelper vh)
    {
        Rect r = rectTransform.rect;
        if (inkTint.a <= 0f || r.width <= 0f || r.height <= 0f) return;
        float front = FrontPosition(r, progress);
        float roughness = Roughness(r);
        float width = Mathf.Min(inkWidth, r.width);
        float tailWidth = Mathf.Min(12f, width * .16f);
        // Lean each strip in X instead of rotating the panel or shortening the ink in Y.
        float slope = Mathf.Tan(headAngle * Mathf.Deg2Rad) * (rightToLeft ? -1f : 1f);
        float facing = flipHead ? -1f : 1f;
        Color clear = new Color(inkTint.r, inkTint.g, inkTint.b, 0f);
        Vector4 spriteUV = inkSprite != null ? DataUtility.GetOuterUV(inkSprite) : Vector4.zero;

        for (int i = 0; i < edgeSegments; i++)
        {
            float t0 = i / (float)edgeSegments;
            float t1 = (i + 1f) / edgeSegments;
            float y0 = Mathf.Lerp(r.yMin, r.yMax, t0);
            float y1 = Mathf.Lerp(r.yMin, r.yMax, t1);
            float x0 = front + slope * (y0 - r.center.y);
            float x1 = front + slope * (y1 - r.center.y);
            if (inkSprite != null)
            {
                InkStrip(vh, r, y0, y1, x0 - facing * width * .15f, x1 - facing * width * .15f,
                    x0 + facing * width * .85f, x1 + facing * width * .85f, inkTint, inkTint,
                    spriteUV.x, spriteUV.z, Mathf.Lerp(spriteUV.y, spriteUV.w, t0), Mathf.Lerp(spriteUV.y, spriteUV.w, t1));
                continue;
            }

            float edge0 = x0 + facing * EdgeNoise(i) * roughness;
            float edge1 = x1 + facing * EdgeNoise(i + 1) * roughness;
            float tip0 = edge0 + facing * width * Mathf.Lerp(.52f, 1f, Hash(i + 35));
            float tip1 = edge1 + facing * width * Mathf.Lerp(.52f, 1f, Hash(i + 36));
            Color ink = inkTint;
            ink.a *= Mathf.Lerp(.73f, 1f, Hash(i + 71));

            InkStrip(vh, r, y0, y1, edge0 - facing * tailWidth, edge1 - facing * tailWidth, edge0, edge1, clear, ink);
            InkStrip(vh, r, y0, y1, edge0, edge1, tip0, tip1, ink, clear);

            if (Hash(i + 101) > .53f)
            {
                Color rim = new Color(.88f, .85f, .75f, inkTint.a * .24f);
                InkStrip(vh, r, y0, y1, edge0, edge1, edge0 + facing * 2f, edge1 + facing * 2f, rim, rim);
            }
        }
    }

    void InkStrip(VertexHelper vh, Rect r, float y0, float y1,
        float left0, float left1, float right0, float right1, Color leftTint, Color rightTint,
        float leftU = 0f, float rightU = 0f, float bottomV = 0f, float topV = 0f)
    {
        if (right0 < left0)
        {
            (left0, right0) = (right0, left0);
            (left1, right1) = (right1, left1);
            (leftTint, rightTint) = (rightTint, leftTint);
            (leftU, rightU) = (rightU, leftU);
        }
        float a = Mathf.Clamp(left0, r.xMin, r.xMax);
        float b = Mathf.Clamp(right0, r.xMin, r.xMax);
        float c = Mathf.Clamp(right1, r.xMin, r.xMax);
        float d = Mathf.Clamp(left1, r.xMin, r.xMax);
        if (b <= a && c <= d) return;

        int first = vh.currentVertCount;
        AddInkVertex(a, y0, Mathf.InverseLerp(left0, right0, a), bottomV);
        AddInkVertex(b, y0, Mathf.InverseLerp(left0, right0, b), bottomV);
        AddInkVertex(c, y1, Mathf.InverseLerp(left1, right1, c), topV);
        AddInkVertex(d, y1, Mathf.InverseLerp(left1, right1, d), topV);
        AddTriangles(vh, first);

        void AddInkVertex(float x, float y, float t, float v)
        {
            vh.AddVert(new Vector2(DirectedX(x, r), y), Color.Lerp(leftTint, rightTint, t),
                new Vector2(Mathf.Lerp(leftU, rightU, t), v));
        }
    }

    static float Hash(int value)
    {
        float n = Mathf.Sin(value * 12.9898f + 4.1414f) * 43758.5453f;
        return n - Mathf.Floor(n);
    }

    void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        Color32 tint, float ua = 0f, float ub = 0f, float uc = 0f, float ud = 0f)
    {
        int first = vh.currentVertCount;
        Rect r = rectTransform.rect;
        a.x = DirectedX(a.x, r); b.x = DirectedX(b.x, r);
        c.x = DirectedX(c.x, r); d.x = DirectedX(d.x, r);
        vh.AddVert(a, tint, new Vector2(ua, 0.5f));
        vh.AddVert(b, tint, new Vector2(ub, 0.5f));
        vh.AddVert(c, tint, new Vector2(uc, 0.5f));
        vh.AddVert(d, tint, new Vector2(ud, 0.5f));
        AddTriangles(vh, first);
    }

    float DirectedX(float x, Rect rect) => rightToLeft ? rect.xMin + rect.xMax - x : x;

    void AddTriangles(VertexHelper vh, int first)
    {
        vh.AddTriangle(first, first + (rightToLeft ? 2 : 1), first + (rightToLeft ? 1 : 2));
        vh.AddTriangle(first + 2, first + (rightToLeft ? 0 : 3), first + (rightToLeft ? 3 : 0));
    }

    protected override void OnDestroy()
    {
        if (featherTexture != null)
        {
            if (Application.isPlaying) Destroy(featherTexture);
            else DestroyImmediate(featherTexture);
        }
        base.OnDestroy();
    }
}
