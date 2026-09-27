using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

/// <summary>Transient live-image distortion. No screenshot, time freeze or scene transforms.</summary>
public sealed class ScreenSpaceCut : MonoBehaviour
{
    private readonly Vector4[] lines = new Vector4[24];
    private readonly int[] started = new int[24];
    private Material material;
    private CutPass pass;
    private float beginTime, burst, angle, pixels;
    private int count, frames;
    private bool playing;
    private bool hold, ownsTime;
    private float duration, restoreStart, previousScale, previousFixed, appliedScale, slowScale;
    private static ScreenSpaceCut timeOwner;

    public static void StopActive() { if (timeOwner != null) timeOwner.Stop(); }

    private void OnEnable() { RenderPipelineManager.beginCameraRendering += Render; }
    private void OnDisable() { RenderPipelineManager.beginCameraRendering -= Render; Stop(); }
    private void OnDestroy() { CoreUtils.Destroy(material); }

    public void Play(ActionData action, float degrees)
    {
        Stop();
        if (material == null)
        {
            var shader = Resources.Load<Shader>("CombatSpaceCut");
            if (shader == null || !shader.isSupported) { Debug.LogWarning("Space cut shader unavailable", this); return; }
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new CutPass(material);
        }
        count = Mathf.Clamp(action.screenSlashCount, 1, 24);
        frames = Mathf.Clamp(action.screenSplitFrames, 1, 6);
        burst = count == 1 ? 0 : Mathf.Clamp(action.screenSlashBurstTime, 0, Mathf.Max(.1f, action.screenSlashDuration) * .75f);
        pixels = Mathf.Clamp(action.screenSplitPixels, 0, 40);
        angle = degrees; beginTime = Time.unscaledTime; playing = true;
        duration = Mathf.Max(.1f, action.screenSlashDuration);
        restoreStart = Mathf.Clamp(action.screenSplitRestoreStart, .5f, .95f);
        hold = action.screenSplitHold;
        slowScale = Mathf.Clamp(action.screenSlashTimeScale, .05f, 1f);
        if (Application.isPlaying && slowScale < 1f && Time.timeScale > 0)
        {
            StopActive();
            previousScale = Time.timeScale; previousFixed = Time.fixedDeltaTime;
            ownsTime = true; timeOwner = this;
            ApplyTime(slowScale);
        }
        for (int i = 0; i < started.Length; i++) started[i] = -1;
        material.SetColor("_EdgeTint", action.screenSlashColor);
    }

    private void ApplyTime(float multiplier)
    {
        appliedScale = previousScale * multiplier;
        Time.timeScale = appliedScale;
        Time.fixedDeltaTime = previousFixed * multiplier;
    }

    public void Stop()
    {
        playing = false;
        if (!ownsTime) return;
        // Do not unpause or overwrite a newer external time-scale change.
        if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = previousScale;
        Time.fixedDeltaTime = previousFixed;
        ownsTime = false;
        if (timeOwner == this) timeOwner = null;
    }

    public static float Closure(float elapsed, float seconds, float start)
    {
        float t = Mathf.InverseLerp(seconds * start, seconds, elapsed);
        return 1f - Mathf.SmoothStep(0, 1, t);
    }

    private void Update()
    {
        if (!playing) return;
        float elapsed = Time.unscaledTime - beginTime;
        if (elapsed >= duration) { Stop(); return; }
        if (ownsTime)
        {
            if (!Mathf.Approximately(Time.timeScale, appliedScale)) { Stop(); return; }
            ApplyTime(Mathf.Lerp(1f, slowScale, Closure(elapsed, duration, restoreStart)));
        }
    }

    private void Render(ScriptableRenderContext context, Camera camera)
    {
        if (!playing || material == null || camera.cameraType != CameraType.Game || camera.targetTexture != null || camera != Camera.main) return;
        int active = 0, completed = 0;
        float elapsed = Time.unscaledTime - beginTime;
        float scale = camera.pixelHeight / 1080f;
        for (int i = 0; i < count; i++)
        {
            float delay = count == 1 ? 0 : burst * i / (count - 1);
            if (elapsed < delay) continue;
            if (started[i] < 0) started[i] = Time.frameCount;
            int age = Time.frameCount - started[i];
            if (!hold && age >= frames) { completed++; continue; }
            bool final = i == count - 1;
            float degrees = count == 1 || final ? angle : angle + i * 137.508f;
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 normal = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
            Vector2 centre = count == 1 || final ? Vector2.zero :
                new Vector2(Mathf.Sin(i * 2.4f + .7f) * camera.pixelWidth * .2f,
                    Mathf.Cos(i * 1.7f + .3f) * camera.pixelHeight * .22f);
            float envelope = hold ? Closure(elapsed, duration, restoreStart) * Mathf.Clamp01((elapsed - delay) / .035f) : 1f - (float)age / frames;
            float strength = pixels * scale * envelope;
            lines[active++] = new Vector4(normal.x, normal.y, Vector2.Dot(normal, centre), strength);
        }
        if (completed == count) Stop();
        if (active == 0) return;
        material.SetInt("_CutCount", active);
        material.SetVectorArray("_Cuts", lines);
        material.SetVector("_CutViewport", new Vector4(camera.pixelWidth, camera.pixelHeight, 1f / camera.pixelWidth, 1f / camera.pixelHeight));
        camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
    }

    private sealed class CutPass : ScriptableRenderPass
    {
        private readonly Material material;
        public CutPass(Material value)
        {
            material = value;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer data)
        {
            var resources = data.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var source = resources.activeColorTexture;
            var descriptor = graph.GetTextureDesc(source);
            descriptor.name = "Transient space cut"; descriptor.clearBuffer = false;
            var destination = graph.CreateTexture(descriptor);
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), passName: "Sword space cut");
            resources.cameraColor = destination;
        }
    }
}
