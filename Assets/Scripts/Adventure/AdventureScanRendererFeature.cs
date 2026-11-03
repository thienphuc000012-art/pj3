using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Depth-based scan overlay for exploration cameras in URP RenderGraph.</summary>
public sealed class AdventureScanRendererFeature : ScriptableRendererFeature
{
    [Tooltip("Adventure/Exploration Scan shader. Keep this reference assigned so builds retain the shader.")]
    public Shader scanShader;

    Material scanMaterial;
    ScanPass scanPass;

    public override void Create()
    {
        CoreUtils.Destroy(scanMaterial);
        scanMaterial = scanShader != null ? CoreUtils.CreateEngineMaterial(scanShader) : null;
        scanPass = new ScanPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!AdventureScanRuntime.Active || AdventureScanRuntime.Strength <= 0f ||
            AdventureScanRuntime.Settings == null)
            return;

        Camera camera = renderingData.cameraData.camera;
        if (camera == null || camera.cameraType != CameraType.Game ||
            renderingData.cameraData.renderType == CameraRenderType.Overlay ||
            (AdventureScanRuntime.TargetCamera != null && camera != AdventureScanRuntime.TargetCamera))
            return;

        if (scanMaterial == null && scanShader != null)
            scanMaterial = CoreUtils.CreateEngineMaterial(scanShader);
        if (scanMaterial == null) return;
        if (scanPass == null) scanPass = new ScanPass();

        scanPass.Setup(scanMaterial, AdventureScanRuntime.Settings,
            AdventureScanRuntime.Origin, AdventureScanRuntime.Radius, AdventureScanRuntime.Strength);
        renderer.EnqueuePass(scanPass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(scanMaterial);
        scanMaterial = null;
    }

    sealed class ScanPass : ScriptableRenderPass
    {
        static readonly int ColorId = Shader.PropertyToID("_ScanColor");
        static readonly int OriginRadiusId = Shader.PropertyToID("_ScanOriginRadius");
        static readonly int WaveId = Shader.PropertyToID("_ScanWaveParams");
        static readonly int DetailId = Shader.PropertyToID("_ScanDetailParams");
        static readonly int LinesId = Shader.PropertyToID("_ScanLineParams");
        readonly ProfilingSampler sampler = new ProfilingSampler("Adventure Exploration Scan");

        Material material;
        Color color;
        Vector4 originRadius, wave, detail, lines;

        sealed class PassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
        }

        public ScanPass()
        {
            // Render before Bloom/tonemapping so the cyan highlights can glow.
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(Material scanMaterial, AdventureScanSettings settings,
            Vector3 origin, float radius, float strength)
        {
            material = scanMaterial;
            color = settings.scanColor;
            originRadius = new Vector4(origin.x, origin.y, origin.z, Mathf.Max(0f, radius));
            wave = new Vector4(Mathf.Max(.01f, settings.waveWidth), Mathf.Max(0f, settings.waveIntensity),
                Mathf.Max(.01f, settings.revealTrailWidth), Mathf.Max(.01f, settings.gridScale));
            detail = new Vector4(Mathf.Max(0f, settings.gridIntensity), Mathf.Max(0f, settings.edgeIntensity),
                Mathf.Max(0f, settings.environmentTint), Mathf.Max(.01f, settings.hologramScanlineDensity));
            lines = new Vector4(Mathf.Clamp01(settings.hologramScanlineStrength), Mathf.Clamp01(strength), 0f, 0f);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.cameraDepthTexture.IsValid() || !resources.activeColorTexture.IsValid()) return;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                "Adventure Exploration Scan", out var data, sampler))
            {
                data.material = material;
                // Each recorded pass owns its values, including when several cameras render in one frame.
                data.properties = new MaterialPropertyBlock();
                data.properties.SetColor(ColorId, color);
                data.properties.SetVector(OriginRadiusId, originRadius);
                data.properties.SetVector(WaveId, wave);
                data.properties.SetVector(DetailId, detail);
                data.properties.SetVector(LinesId, lines);

                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                // Hardware additive blending reads the existing color without an opaque-color copy.
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData passData, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, passData.material, 0,
                        MeshTopology.Triangles, 3, 1, passData.properties);
                });
            }
        }
    }
}
