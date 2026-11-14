using System.Collections.Generic;
using UnityEngine;

/// <summary>Resolves the visible slash plane, including particle birth rotation, once per prefab.</summary>
public static class CombatSlashOrientation
{
    public struct AuthoredFrame
    {
        public Transform meshTransform;
        public ParticleSystemRenderer particles;
        public Mesh mesh;
        public Quaternion basis;
    }
    sealed class Cached { public GameObject prefab; public bool found; public AuthoredFrame frame; }
    static readonly Dictionary<int, Cached> frames = new Dictionary<int, Cached>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => frames.Clear();

    public static Quaternion Rotation(GameObject prefab, Vector3 sweep, Vector3 blade, Vector3 normalHint)
    {
        if (sweep.sqrMagnitude < .000001f) sweep = Vector3.right;
        sweep.Normalize(); blade = Vector3.ProjectOnPlane(blade, sweep);
        if (blade.sqrMagnitude < .000001f) blade = Vector3.ProjectOnPlane(Vector3.up, sweep);
        if (blade.sqrMagnitude < .000001f) blade = Vector3.ProjectOnPlane(Vector3.forward, sweep);
        blade.Normalize();
        var normal = Vector3.Cross(sweep, blade).normalized;
        if (normalHint.sqrMagnitude > .000001f && Vector3.Dot(normal, normalHint) < 0) { blade = -blade; normal = -normal; }
        var worldFrame = Quaternion.LookRotation(normal, blade);
        return TryGetAuthoredFrame(prefab, out var source) ? worldFrame * Quaternion.Inverse(source.basis) : worldFrame;
    }

    public static bool TryGetAuthoredFrame(GameObject prefab, out AuthoredFrame frame)
    {
        frame = default; if (prefab == null) return false;
        int id = prefab.GetInstanceID();
        if (frames.TryGetValue(id, out var cached) && cached.prefab == prefab) { frame = cached.frame; return cached.found; }
        int bestPriority = -1; float bestArea = 0;
        foreach (var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (!renderer.enabled || renderer.renderMode != ParticleSystemRenderMode.Mesh || renderer.mesh == null || !ActiveInPrefab(renderer.transform, prefab.transform)) continue;
            var mesh = renderer.mesh;
            if (!PlanarAxes(mesh, out Vector3 longAxis, out Vector3 shortAxis, out float area)) continue;
            string name = renderer.name.ToLowerInvariant();
            // Distortion meshes are often slightly larger than the visible slash.
            // Sparks, smoke and ground stamps must not define the sword arc's basis.
            bool secondary = name.Contains("disto") || name.Contains("decal") || name.Contains("crack") || name.Contains("trail");
            int priority = secondary ? 0 : renderer.transform == prefab.transform ? 3 : name.Contains("slash") ? 2 : 1;
            if (priority < bestPriority || (priority == bestPriority && area <= bestArea)) continue;
            var main = renderer.GetComponent<ParticleSystem>().main;
            Quaternion birth = Quaternion.Euler(main.startRotation3D ? BirthAngle(main.startRotationX) : 0,
                main.startRotation3D ? BirthAngle(main.startRotationY) : 0, BirthAngle(main.startRotationZ));
            Matrix4x4 relative = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Vector3 x = relative.MultiplyVector(birth * longAxis).normalized;
            Vector3 y = Vector3.ProjectOnPlane(relative.MultiplyVector(birth * shortAxis), x).normalized;
            if (y.sqrMagnitude < .000001f) continue;
            frame = new AuthoredFrame { meshTransform = renderer.transform, particles = renderer, mesh = mesh, basis = Quaternion.LookRotation(Vector3.Cross(x, y), y) };
            bestPriority = priority; bestArea = area;
        }
        if (bestPriority < 0)
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!ActiveInPrefab(filter.transform, prefab.transform) || !PlanarAxes(filter.sharedMesh, out Vector3 x, out Vector3 y, out float area) || area <= bestArea) continue;
                var relative = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                x = relative.MultiplyVector(x).normalized; y = Vector3.ProjectOnPlane(relative.MultiplyVector(y), x).normalized;
                if (y.sqrMagnitude < .000001f) continue;
                frame = new AuthoredFrame { meshTransform = filter.transform, mesh = filter.sharedMesh, basis = Quaternion.LookRotation(Vector3.Cross(x, y), y) };
                bestArea = area;
            }
        bool found = frame.mesh != null;
        frames[id] = new Cached { prefab = prefab, found = found, frame = frame };
        return found;
    }
    static bool ActiveInPrefab(Transform item, Transform root)
    {
        for (var t = item; t != null; t = t.parent) { if (!t.gameObject.activeSelf) return false; if (t == root) return true; }
        return false;
    }
    static float BirthAngle(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.TwoConstants: return (curve.constantMin + curve.constantMax) * .5f * Mathf.Rad2Deg;
            case ParticleSystemCurveMode.Curve: return curve.curve.Evaluate(0) * curve.curveMultiplier * Mathf.Rad2Deg;
            case ParticleSystemCurveMode.TwoCurves: return (curve.curveMin.Evaluate(0) + curve.curveMax.Evaluate(0)) * .5f * curve.curveMultiplier * Mathf.Rad2Deg;
            default: return curve.constant * Mathf.Rad2Deg;
        }
    }
    public static bool PlanarAxes(Mesh mesh, out Vector3 x, out Vector3 y, out float area)
    {
        x = Vector3.right; y = Vector3.up; area = 0; if (mesh == null) return false;
        var e = mesh.bounds.extents;
        int normal = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
        int longest = normal == 0 ? (e.y >= e.z ? 1 : 2) : normal == 1 ? (e.x >= e.z ? 0 : 2) : e.x >= e.y ? 0 : 1;
        int middle = 3 - normal - longest;
        float min = e[normal], max = e[longest]; area = e[middle] * max;
        if (area < .00001f || min > max * .25f) return false;
        Vector3 n = Axis(normal); x = Axis(longest); y = Vector3.Cross(n, x).normalized;
        return true;
    }
    static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
}
