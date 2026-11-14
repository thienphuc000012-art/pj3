using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Recovers the existing convex solids in an imported collision mesh, without
/// making a convex hull around a whole building, arch, bridge or group of branches.</summary>
public static class AuthoredCollisionHulls
{
    const float Epsilon = .0003f;
    public sealed class Part
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Plane[] planes;
        public Bounds bounds;
        public bool box;
        public bool Contains(Vector3 point) => planes.All(p => p.GetDistanceToPoint(point) <= Epsilon);
        public Mesh CreateMesh(string name)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds(); return mesh;
        }
    }
    struct Triangle { public int a, b, c; public Triangle(int x, int y, int z) { a = x; b = y; c = z; } }
    struct Edge : IEquatable<Edge>
    {
        public int a, b;
        public Edge(int x, int y) { a = Mathf.Min(x, y); b = Mathf.Max(x, y); }
        public bool Equals(Edge other) => a == other.a && b == other.b;
        public override bool Equals(object obj) => obj is Edge edge && Equals(edge);
        public override int GetHashCode() => (a * 397) ^ b;
    }

    public static Part[] Decompose(Mesh mesh)
    {
        var points = new List<Vector3>(); var unique = new Dictionary<Vector3Int, int>();
        var vertices = mesh.vertices; var source = mesh.triangles; var indices = new int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var key = Vector3Int.RoundToInt(vertices[i] * 100000);
            if (!unique.TryGetValue(key, out int id)) { id = points.Count; unique.Add(key, id); points.Add(vertices[i]); }
            indices[i] = id;
        }
        var raw = new List<Triangle>();
        for (int i = 0; i < source.Length; i += 3)
        {
            var t = new Triangle(indices[source[i]], indices[source[i + 1]], indices[source[i + 2]]);
            if (Vector3.Cross(points[t.b] - points[t.a], points[t.c] - points[t.a]).sqrMagnitude > 1e-16f) raw.Add(t);
        }
        // Most FBX files keep each authored solid together in the triangle stream.
        var parts = new List<Part>(); var current = new List<Triangle>(); var edgeCounts = new Dictionary<Edge, int>();
        bool streamed = true;
        foreach (var t in raw)
        {
            current.Add(t); AddCount(edgeCounts, new Edge(t.a, t.b)); AddCount(edgeCounts, new Edge(t.b, t.c)); AddCount(edgeCounts, new Edge(t.c, t.a));
            if (current.Count < 4 || edgeCounts.Values.Any(n => n != 2)) continue;
            var part = MakePart(points, current, true);
            if (part == null) { streamed = false; break; }
            parts.Add(part); current.Clear(); edgeCounts.Clear();
        }
        if (streamed && current.Count == 0 && parts.Count > 0) return Checked(points, raw, parts);

        // Some exporters reorder faces by material, or duplicate a complete solid.
        var seen = new HashSet<string>(); var deduplicated = raw.Where(t => seen.Add(TriangleKey(t))).ToList();
        var connected = Components(deduplicated, false);
        parts = connected.Select(g => MakePart(points, g, true)).ToList();
        if (parts.All(p => p != null)) return Checked(points, raw, parts);

        // Touching solids can share coincident faces/edges. Do not join triangles through
        // such ambiguous edges. Planar fragments are duplicate caps, verified below.
        parts = Components(raw, true).Select(g => MakePart(points, g, false)).Where(p => p != null).ToList();
        if (parts.Count == 0) throw new InvalidOperationException("No valid convex solids in " + mesh.name);
        return Checked(points, raw, parts);
    }

    static void AddCount(Dictionary<Edge, int> counts, Edge edge) { counts.TryGetValue(edge, out int n); counts[edge] = n + 1; }
    static string TriangleKey(Triangle t) { var ids = new[] { t.a, t.b, t.c }; Array.Sort(ids); return string.Join(",", ids); }
    static List<List<Triangle>> Components(List<Triangle> triangles, bool breakAmbiguous)
    {
        var owners = new Dictionary<Edge, List<int>>(); var parent = Enumerable.Range(0, triangles.Count).ToArray();
        int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
        void Add(Edge e, int id) { if (!owners.TryGetValue(e, out var list)) owners.Add(e, list = new List<int>()); list.Add(id); }
        for (int i = 0; i < triangles.Count; i++) { var t = triangles[i]; Add(new Edge(t.a, t.b), i); Add(new Edge(t.b, t.c), i); Add(new Edge(t.c, t.a), i); }
        foreach (var ids in owners.Values)
        {
            if (breakAmbiguous && ids.Count != 2) continue;
            for (int i = 1; i < ids.Count; i++) parent[Root(ids[i])] = Root(ids[0]);
        }
        return Enumerable.Range(0, triangles.Count).GroupBy(Root).Select(g => g.Select(i => triangles[i]).ToList()).ToList();
    }

    static Part MakePart(List<Vector3> all, List<Triangle> source, bool requireClosed)
    {
        var ids = source.SelectMany(t => new[] { t.a, t.b, t.c }).Distinct().ToArray();
        if (ids.Length < 4 || ids.Length > 128) return null;
        var v = ids.Select(i => all[i]).ToArray(); var center = v.Aggregate(Vector3.zero, (a, p) => a + p) / v.Length;
        var bounds = new Bounds(v[0], Vector3.zero); foreach (var p in v) bounds.Encapsulate(p);
        bool volume = false;
        for (int i = 1; i < v.Length - 1 && !volume; i++)
        {
            var normal = Vector3.Cross(v[i] - v[0], v[i + 1] - v[0]).normalized;
            volume = normal.sqrMagnitude > .5f && v.Any(p => Mathf.Abs(Vector3.Dot(normal, p - v[0])) > Epsilon);
        }
        if (!volume) return null;
        var planes = new List<Plane>(); var edges = new Dictionary<Edge, int>();
        foreach (var t in source)
        {
            var a = all[t.a]; var normal = Vector3.Cross(all[t.b] - a, all[t.c] - a).normalized;
            bool positive = v.Any(p => Vector3.Dot(normal, p - a) > Epsilon), negative = v.Any(p => Vector3.Dot(normal, p - a) < -Epsilon);
            if (positive && negative) return null;
            if (Vector3.Dot(normal, center - a) > 0) normal = -normal;
            planes.Add(new Plane(normal, a));
            AddCount(edges, new Edge(t.a, t.b)); AddCount(edges, new Edge(t.b, t.c)); AddCount(edges, new Edge(t.c, t.a));
        }
        if (requireClosed && edges.Values.Any(n => n != 2)) return null;
        if (!requireClosed)
        {
            // Add any missing cap planes from the convex point set. Final surface checks
            // ensure the cap was present in the original FBX or hidden by another solid.
            for (int a = 0; a < v.Length - 2; a++) for (int b = a + 1; b < v.Length - 1; b++) for (int c = b + 1; c < v.Length; c++)
            {
                var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized; if (n.sqrMagnitude < .5f) continue;
                bool pos = v.Any(p => Vector3.Dot(n, p - v[a]) > Epsilon), neg = v.Any(p => Vector3.Dot(n, p - v[a]) < -Epsilon);
                if (pos && neg) continue; if (Vector3.Dot(n, center - v[a]) > 0) n = -n;
                planes.Add(new Plane(n, v[a]));
            }
        }
        var remap = ids.Select((id, i) => new { id, i }).ToDictionary(x => x.id, x => x.i);
        bool box = v.Length == 8 && v.All(p => OnEnd(p.x, bounds.min.x, bounds.max.x) && OnEnd(p.y, bounds.min.y, bounds.max.y) && OnEnd(p.z, bounds.min.z, bounds.max.z));
        return new Part { vertices = v, triangles = source.SelectMany(t => new[] { remap[t.a], remap[t.b], remap[t.c] }).ToArray(), planes = planes.ToArray(), bounds = bounds, box = box };
    }
    static bool OnEnd(float value, float a, float b) => Mathf.Min(Mathf.Abs(value - a), Mathf.Abs(value - b)) <= .00003f;
    static Part[] Checked(List<Vector3> points, List<Triangle> triangles, List<Part> input)
    {
        var keys = new HashSet<string>();
        var parts = input.Where(p => keys.Add(string.Join(";", p.vertices.Select(v => Vector3Int.RoundToInt(v * 100000).ToString()).OrderBy(s => s)))).ToArray();
        foreach (var t in triangles)
            if (!parts.Any(p => p.Contains(points[t.a]) && p.Contains(points[t.b]) && p.Contains(points[t.c])))
                throw new InvalidOperationException("A source collision triangle is not covered by the recovered solids.");
        foreach (var part in parts)
        {
            var v = part.vertices;
            for (int a = 0; a < v.Length - 2; a++) for (int b = a + 1; b < v.Length - 1; b++) for (int c = b + 1; c < v.Length; c++)
            {
                var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized; if (n.sqrMagnitude < .5f) continue;
                bool pos = v.Any(p => Vector3.Dot(n, p - v[a]) > Epsilon), neg = v.Any(p => Vector3.Dot(n, p - v[a]) < -Epsilon);
                if (pos && neg) continue;
                foreach (var sample in new[] { (v[a] + v[b] + v[c]) / 3f, (v[a] * 2 + v[b] + v[c]) / 4f })
                {
                    if (triangles.Any(t => OnTriangle(sample, points[t.a], points[t.b], points[t.c]))) continue;
                    if (parts.Any(other => other != part && other.planes.All(p => p.GetDistanceToPoint(sample) < -Epsilon))) continue;
                    throw new InvalidOperationException("A recovered hull would bridge an opening in the source collision geometry.");
                }
            }
        }
        return parts;
    }
    static bool OnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a; var ac = c - a; var normal = Vector3.Cross(ab, ac);
        if (normal.sqrMagnitude < 1e-16f || Mathf.Abs(Vector3.Dot(normal.normalized, p - a)) > Epsilon) return false;
        float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), ba = Vector3.Dot(ab, ac), pa = Vector3.Dot(p - a, ab), pc = Vector3.Dot(p - a, ac);
        float denom = aa * bb - ba * ba; if (Mathf.Abs(denom) < 1e-12f) return false;
        float u = (bb * pa - ba * pc) / denom, v = (aa * pc - ba * pa) / denom;
        return u >= -.0005f && v >= -.0005f && u + v <= 1.0005f;
    }
}
