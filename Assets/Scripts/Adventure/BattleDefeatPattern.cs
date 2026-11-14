using UnityEngine;
using UnityEngine.UI;

/// <summary>Fine broken red geometric strokes, drawn behind the defeat title.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BattleDefeatPattern : MaskableGraphic
{
    [Min(.1f)] public float lineWidth = 1.2f;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 6; i++)
            {
                float width = 36f + i * 33f;
                Vector2 a = center + new Vector2(-width, side * rect.height * .49f);
                Vector2 b = center + new Vector2(width * .72f, side * rect.height * .27f);
                Vector2 c = center + new Vector2(-width * .45f, side * rect.height * .08f);
                Stroke(mesh, a, b, i); Stroke(mesh, b, c, i + 2);
                Stroke(mesh, new Vector2(2f * center.x - a.x, a.y), new Vector2(2f * center.x - b.x, b.y), i + 1);
                Stroke(mesh, new Vector2(2f * center.x - b.x, b.y), new Vector2(2f * center.x - c.x, c.y), i + 3);
            }
            for (int i = -3; i <= 3; i++)
                Stroke(mesh, center + new Vector2(i * 19f, side * rect.height * .49f),
                    center + new Vector2(i * 10f, side * rect.height * .09f), i + 8);
        }
    }
    void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, int seed)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * lineWidth * .5f;
        for (int i = 0; i < 23; i++)
        {
            float start = i / 23f, end = (i + .82f + .12f * Mathf.Sin(seed + i)) / 23f;
            Vector2 p = Vector2.Lerp(a, b, start), q = Vector2.Lerp(a, b, end);
            int index = mesh.currentVertCount;
            Color shade = color; shade.a *= .45f + .5f * Mathf.PerlinNoise(i * .9f, seed + 20f);
            mesh.AddVert(p - normal, shade, Vector2.zero); mesh.AddVert(p + normal, shade, Vector2.zero);
            mesh.AddVert(q + normal, shade, Vector2.zero); mesh.AddVert(q - normal, shade, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
