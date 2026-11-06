using UnityEngine;
using UnityEngine.UI;

/// <summary>A sprite-backed frame or an empty angular ring around a turn-order portrait.</summary>
[AddComponentMenu("UI/Turn Order Portrait Frame")]
public sealed class TurnOrderPortraitFrame : Image
{
    [Min(0)] public float thickness = 2f;
    [Min(0)] public float cornerCut = 5f;

    public static TurnOrderPortraitFrame Apply(RectTransform portrait, Sprite sprite, Color color,
        Vector2 padding, float thickness)
    {
        if (portrait == null) return null;
        Transform child = portrait.Find("Portrait Frame");
        if (child == null)
        {
            child = new GameObject("Portrait Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(TurnOrderPortraitFrame)).transform;
            child.SetParent(portrait, false);
        }
        var frame = child.GetComponent<TurnOrderPortraitFrame>();
        if (frame == null) frame = child.gameObject.AddComponent<TurnOrderPortraitFrame>();
        child.gameObject.layer = portrait.gameObject.layer;
        var rect = frame.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = -padding; rect.offsetMax = padding;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        child.SetAsLastSibling();
        frame.sprite = sprite;
        frame.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Type.Sliced : Type.Simple;
        frame.color = color; frame.thickness = Mathf.Max(0f, thickness);
        frame.raycastTarget = false; frame.preserveAspect = false;
        frame.enabled = true; child.gameObject.SetActive(true);
        frame.SetAllDirty();
        return frame;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        if (sprite != null) { base.OnPopulateMesh(mesh); return; }
        mesh.Clear();
        Rect outer = GetPixelAdjustedRect();
        float smallest = Mathf.Min(outer.width, outer.height);
        if (smallest <= 0f || thickness <= 0f) return;
        float edge = Mathf.Min(thickness, smallest * .49f);
        float cut = Mathf.Clamp(cornerCut, 0f, smallest * .5f);
        Rect inner = Rect.MinMaxRect(outer.xMin + edge, outer.yMin + edge, outer.xMax - edge, outer.yMax - edge);
        float innerCut = Mathf.Clamp(cut - (2f - Mathf.Sqrt(2f)) * edge, 0f, Mathf.Min(inner.width, inner.height) * .5f);
        for (int i = 0; i < 8; i++)
        {
            Add(mesh, Corner(outer, cut, i));
            Add(mesh, Corner(inner, innerCut, i));
        }
        for (int i = 0; i < 8; i++)
        {
            int next = ((i + 1) % 8) * 2;
            mesh.AddTriangle(i * 2, i * 2 + 1, next + 1);
            mesh.AddTriangle(i * 2, next + 1, next);
        }
    }

    static Vector2 Corner(Rect rect, float cut, int index)
    {
        switch (index)
        {
            case 0: return new Vector2(rect.xMin + cut, rect.yMin);
            case 1: return new Vector2(rect.xMax - cut, rect.yMin);
            case 2: return new Vector2(rect.xMax, rect.yMin + cut);
            case 3: return new Vector2(rect.xMax, rect.yMax - cut);
            case 4: return new Vector2(rect.xMax - cut, rect.yMax);
            case 5: return new Vector2(rect.xMin + cut, rect.yMax);
            case 6: return new Vector2(rect.xMin, rect.yMax - cut);
            default: return new Vector2(rect.xMin, rect.yMin + cut);
        }
    }

    void Add(VertexHelper mesh, Vector2 position)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position; vertex.color = color; vertex.uv0 = new Vector2(.5f, .5f);
        mesh.AddVert(vertex);
    }
}
