using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns only temporary materials; imported/prefab materials remain untouched.</summary>
public sealed class MapDeathDissolve : IDisposable
{
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Material[]> originals = new List<Material[]>(), runtime = new List<Material[]>();
    readonly List<MaterialPropertyBlock[]> blocks = new List<MaterialPropertyBlock[]>();
    readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    public MapDeathDissolve(Transform model, Material template)
    {
        if (model == null || template == null) return;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            var source = renderer.sharedMaterials; var copies = new Material[source.Length]; var saved = new MaterialPropertyBlock[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                saved[i] = new MaterialPropertyBlock(); renderer.GetPropertyBlock(saved[i], i);
                copies[i] = new Material(template) { name = template.name + " (Map death)" };
                BattleUnit.CopySurfaceProperties(source[i], copies[i]); copies[i].SetFloat("_DissolveAmount", 0);
                // Imported rigs use different mesh units. Give each body/weapon a visible
                // vertical dissolve instead of inheriting slices meant for a unit cube.
                if (copies[i].HasProperty("_SliceDensity") && copies[i].HasProperty("_AxisMode"))
                {
                    var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    float height = Mathf.Max(.001f, mesh != null ? mesh.bounds.size.y : 1);
                    copies[i].SetFloat("_AxisMode", 1); copies[i].SetFloat("_SliceDensity", Mathf.Max(1, 12 / height));
                    copies[i].SetFloat("_NoiseStrength", height * .004f); copies[i].SetFloat("_NoiseScale", 6 / height);
                }
            }
            renderers.Add(renderer); originals.Add(source); runtime.Add(copies); blocks.Add(saved); renderer.sharedMaterials = copies;
        }
    }
    public void Sample(float value)
    {
        for (int r = 0; r < renderers.Count; r++)
            if (renderers[r] != null)
                for (int i = 0; i < runtime[r].Length; i++)
                { renderers[r].GetPropertyBlock(block, i); block.SetFloat("_DissolveAmount", Mathf.Clamp01(value)); renderers[r].SetPropertyBlock(block, i); }
    }
    public void Dispose()
    {
        for (int r = 0; r < renderers.Count; r++)
        {
            if (renderers[r] != null) { renderers[r].sharedMaterials = originals[r]; for (int i = 0; i < blocks[r].Length; i++) renderers[r].SetPropertyBlock(blocks[r][i], i); }
            foreach (var material in runtime[r]) if (material != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(material); else UnityEngine.Object.DestroyImmediate(material); }
        }
        renderers.Clear(); originals.Clear(); runtime.Clear(); blocks.Clear();
    }
}
