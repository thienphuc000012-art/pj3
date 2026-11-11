using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
internal sealed class BossPhasePips : MaskableGraphic
{
    static readonly Color Border = new Color(.12f, .07f, .04f, .98f);

    int count;
    int litCount;
    float size;
    float spacing;
    Color litColor;
    Color spentColor;

    internal void SetPhases(int phaseCount, int remainingPhases, float pipSize, float gap, Color lit, Color spent)
    {
        pipSize = Mathf.Max(4f, pipSize);
        gap = Mathf.Max(0f, gap);
        if (count == phaseCount && litCount == remainingPhases && size == pipSize && spacing == gap &&
            litColor == lit && spentColor == spent) return;

        count = phaseCount;
        litCount = remainingPhases;
        size = pipSize;
        spacing = gap;
        litColor = lit;
        spentColor = spent;
        rectTransform.sizeDelta = new Vector2(count * size + (count - 1) * spacing, size);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float halfSize = size * .5f;
        for (int i = 0; i < count; i++)
        {
            bool lit = i < litCount;
            Color tint = lit ? litColor : spentColor;
            Vector2 center = new Vector2(rect.xMin + halfSize + i * (size + spacing), rect.center.y);
            Color edge = Color.Lerp(tint, new Color(1f, .93f, .74f, tint.a), lit ? .7f : .16f);
            AddDiamond(mesh, center, halfSize, Border, Border);
            AddDiamond(mesh, center, halfSize * .83f, edge, tint);
            AddDiamond(mesh, center, halfSize * .64f, tint, new Color(tint.r * .62f, tint.g * .62f, tint.b * .62f, tint.a));
        }
    }

    static void AddDiamond(VertexHelper mesh, Vector2 center, float radius, Color light, Color shade)
    {
        int vertex = mesh.currentVertCount;
        mesh.AddVert(center + Vector2.left * radius, light, Vector2.zero);
        mesh.AddVert(center + Vector2.up * radius, light, Vector2.zero);
        mesh.AddVert(center + Vector2.right * radius, shade, Vector2.zero);
        mesh.AddVert(center + Vector2.down * radius, shade, Vector2.zero);
        mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
        mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
    }
}
