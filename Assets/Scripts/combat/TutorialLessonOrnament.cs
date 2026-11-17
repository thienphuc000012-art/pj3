using UnityEngine;
using UnityEngine.UI;

/// <summary>Distressed interlaced corner motifs; the central writing area stays clear.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialLessonOrnament : MaskableGraphic
{
    public Color borderColor = new Color(.80f, .77f, .65f, .42f);
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); var r = rectTransform.rect;
        float size = Mathf.Min(r.width * .24f, r.height * .13f);
        for (int i = 0; i < 4; i++) Motif(mesh, new Vector2(Mathf.Lerp(r.xMin + size * .55f, r.xMax - size * .55f, i / 3f), r.yMax - size * .6f), size, i * 31, 1);
        Motif(mesh, new Vector2(r.xMax - size * .55f, r.yMax - size * 1.65f), size * .9f, 47, .55f);
        Motif(mesh, new Vector2(r.xMax - size * .58f, r.yMin + size * .7f), size * 1.12f, 63, .9f);
        Motif(mesh, new Vector2(r.xMax - size * .65f, r.yMin + size * 1.85f), size * .9f, 72, .4f);
        var a = new Vector2(r.xMin + 15, r.yMax - 24); var b = new Vector2(r.xMax - 38, r.yMax - 24);
        Stroke(mesh, a, b, borderColor, 1.25f, 94);
        Stroke(mesh, a, a + new Vector2(0, -70), borderColor, 1.25f, 97);
        Stroke(mesh, a + new Vector2(0, -55), a + new Vector2(55, 0), borderColor, 1.1f, 99);
    }
    void Motif(VertexHelper mesh, Vector2 center, float size, int seed, float fade)
    {
        var tint = color; tint.a *= fade;
        Diamond(mesh, center, size * .49f, tint, seed);
        Diamond(mesh, center, size * .35f, tint, seed + 3);
        Diamond(mesh, center, size * .20f, tint, seed + 7);
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            float angle = quadrant * Mathf.PI * .5f;
            Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-axis.y, axis.x);
            Vector2 knot = center + axis * size * .27f;
            Diamond(mesh, knot, size * .11f, tint, seed + quadrant * 8);
            Stroke(mesh, knot + side * size * .11f, knot + axis * size * .14f + side * size * .11f, tint, 1.1f, seed + quadrant);
            Stroke(mesh, knot + axis * size * .14f + side * size * .11f, center + axis * size * .41f, tint, 1.1f, seed + quadrant + 12);
        }
    }
    void Diamond(VertexHelper mesh, Vector2 center, float radius, Color tint, int seed)
    {
        var top = center + Vector2.up * radius; var right = center + Vector2.right * radius;
        var bottom = center + Vector2.down * radius; var left = center + Vector2.left * radius;
        Stroke(mesh, top, right, tint, 1.15f, seed); Stroke(mesh, right, bottom, tint, 1.15f, seed + 1);
        Stroke(mesh, bottom, left, tint, 1.15f, seed + 2); Stroke(mesh, left, top, tint, 1.15f, seed + 3);
    }
    void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, Color tint, float width, int seed)
    {
        int pieces = Mathf.Max(3, Mathf.CeilToInt(Vector2.Distance(from, to) / 8f));
        Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
        for (int i = 0; i < pieces; i++)
        {
            float grain = Mathf.Repeat(Mathf.Sin((seed * 13 + i) * 7.31f) * 43758.5453f, 1f);
            if (grain < .16f) continue;
            var a = Vector2.Lerp(from, to, i / (float)pieces); var b = Vector2.Lerp(from, to, (i + .94f) / pieces);
            var c = tint; c.a *= Mathf.Lerp(.3f, 1f, grain); int start = mesh.currentVertCount;
            mesh.AddVert(a - normal, c, Vector2.zero); mesh.AddVert(a + normal, c, Vector2.zero);
            mesh.AddVert(b + normal, c, Vector2.zero); mesh.AddVert(b - normal, c, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
