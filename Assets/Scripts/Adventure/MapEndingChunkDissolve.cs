using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Temporary material swaps on the visible edge of the ending's shrinking map.</summary>
internal sealed class MapEndingChunkDissolve : IDisposable
{
    sealed class Surface
    {
        public Renderer renderer;
        public float near, far;
        public bool hidden;
        public Material[] original;
    }

    const float EdgeWidth = 1.65f;
    static readonly int FrontId = Shader.PropertyToID("_EndingChunkFront");
    readonly List<Surface> surfaces = new List<Surface>();
    readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
    readonly Shader shader;
    readonly Vector3 center;
    readonly float innerRadius;
    float outerRadius;

    public MapEndingChunkDissolve(MapChunkStreamer streamer, Shader shader, Vector3 center, float innerRadius)
    {
        this.shader = shader;
        this.center = center;
        this.innerRadius = Mathf.Max(8f, innerRadius);
        outerRadius = this.innerRadius + EdgeWidth;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var renderers = new List<Renderer>();
        foreach (var chunk in streamer.chunks)
        {
            if (chunk == null || string.IsNullOrEmpty(chunk.scenePath) || !paths.Add(chunk.scenePath)) continue;
            Scene scene = SceneManager.GetSceneByPath(chunk.scenePath);
            if (!scene.IsValid() || !scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(false, renderers);
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled || renderer.forceRenderingOff) continue;
                    Bounds bounds = renderer.bounds;
                    Vector3 closest = bounds.ClosestPoint(new Vector3(center.x, bounds.center.y, center.z));
                    float near = new Vector2(closest.x - center.x, closest.z - center.z).magnitude;
                    var distanceToFarCorner = new Vector2(
                        Mathf.Abs(bounds.center.x - center.x) + bounds.extents.x,
                        Mathf.Abs(bounds.center.z - center.z) + bounds.extents.z);
                    float far = distanceToFarCorner.magnitude;
                    if (far < this.innerRadius - EdgeWidth) continue;
                    surfaces.Add(new Surface { renderer = renderer, near = near, far = far });
                    outerRadius = Mathf.Max(outerRadius, far + EdgeWidth);
                }
            }
        }
        Sample(0f);
    }

    public void Sample(float progress)
    {
        float radius = Mathf.Lerp(outerRadius, innerRadius, progress);
        Shader.SetGlobalVector(FrontId, new Vector4(center.x, center.y, center.z, radius));
        foreach (var surface in surfaces)
        {
            if (surface.hidden || surface.renderer == null) continue;
            if (surface.near > radius + EdgeWidth)
            {
                surface.hidden = true;
                surface.renderer.forceRenderingOff = true;
                if (surface.original != null) surface.renderer.sharedMaterials = surface.original;
                surface.original = null;
            }
            else if (surface.far > radius - EdgeWidth && surface.original == null &&
                shader != null && shader.isSupported && (surface.renderer is MeshRenderer || surface.renderer is SkinnedMeshRenderer))
            {
                surface.original = surface.renderer.sharedMaterials;
                var replacements = new Material[surface.original.Length];
                for (int i = 0; i < replacements.Length; i++)
                {
                    var material = surface.original[i];
                    replacements[i] = material != null ? DissolveMaterial(material) : null;
                }
                surface.renderer.sharedMaterials = replacements;
            }
        }
    }

    Material DissolveMaterial(Material source)
    {
        if (copies.TryGetValue(source, out var copy)) return copy;
        copy = new Material(shader) { name = source.name + " (Ending)", enableInstancing = source.enableInstancing };
        copy.CopyPropertiesFromMaterial(source);
        if (!source.HasProperty("_BaseMap") || !source.HasProperty("_BaseColor"))
            BattleUnit.CopySurfaceProperties(source, copy);

        // The source may use a different local keyword space. Keep the supported
        // URP Lit keywords and enable common maps on converted legacy materials.
        foreach (var keyword in shader.keywordSpace.keywords)
        {
            if (source.IsKeywordEnabled(keyword.name)) copy.EnableKeyword(keyword.name);
            else copy.DisableKeyword(keyword.name);
        }
        if (copy.HasProperty("_BumpMap") && copy.GetTexture("_BumpMap") != null &&
            (source.IsKeywordEnabled("_NORMALMAP") || source.GetTexturePropertyNames().Length > 0 &&
                !source.HasProperty("_BaseMap")))
            copy.EnableKeyword("_NORMALMAP");
        if ((source.renderQueue >= 2450 && source.renderQueue < 2500) ||
            (copy.HasProperty("_AlphaClip") && copy.GetFloat("_AlphaClip") > .5f))
            copy.EnableKeyword("_ALPHATEST_ON");
        copies.Add(source, copy);
        return copy;
    }

    public void Dispose()
    {
        foreach (var surface in surfaces)
        {
            if (surface.renderer == null) continue;
            if (surface.original != null) surface.renderer.sharedMaterials = surface.original;
            if (surface.hidden) surface.renderer.forceRenderingOff = false;
        }
        foreach (var material in copies.Values) UnityEngine.Object.Destroy(material);
        copies.Clear();
        surfaces.Clear();
        Shader.SetGlobalVector(FrontId, Vector4.zero);
    }
}
