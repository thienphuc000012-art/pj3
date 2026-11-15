using UnityEngine;

/// <summary>Scene-owned portal glow; frame meshes/collision remain after the energy shuts down.</summary>
public sealed class PortalArrivalVisual : MonoBehaviour
{
    public Renderer[] energySurfaces = System.Array.Empty<Renderer>();
    public Renderer[] runeSurfaces = System.Array.Empty<Renderer>();
    public Light[] glowLights = System.Array.Empty<Light>();
    float[] lightIntensities;
    MaterialPropertyBlock block;
    public float Charge { get; private set; } = 1f;
    void Awake() => Capture();
    void Capture()
    {
        if (block == null) block = new MaterialPropertyBlock();
        if (lightIntensities == null || lightIntensities.Length != glowLights.Length)
        {
            lightIntensities = new float[glowLights.Length];
            for (int i = 0; i < glowLights.Length; i++) if (glowLights[i] != null) lightIntensities[i] = glowLights[i].intensity;
        }
    }
    public void SetCharge(float value)
    {
        Capture(); Charge = Mathf.Clamp01(value);
        foreach (var surface in energySurfaces)
        {
            if (surface == null) continue;
            surface.enabled = Charge > .001f;
            surface.GetPropertyBlock(block); block.SetFloat("_Charge", Charge); surface.SetPropertyBlock(block);
        }
        foreach (var surface in runeSurfaces)
        {
            if (surface == null) continue;
            var material = surface.sharedMaterial;
            if (material == null || !material.HasProperty("_EmissionColor")) continue;
            surface.GetPropertyBlock(block); block.SetColor("_EmissionColor", material.GetColor("_EmissionColor") * Charge); surface.SetPropertyBlock(block);
        }
        for (int i = 0; i < glowLights.Length; i++)
        {
            var light = glowLights[i]; if (light == null) continue;
            light.intensity = lightIntensities[i] * Charge; light.enabled = Charge > .001f;
        }
    }
}
