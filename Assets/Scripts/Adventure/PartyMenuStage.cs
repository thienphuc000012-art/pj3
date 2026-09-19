using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// An isolated, still-life render of the game's own party prefabs.
public class PartyMenuStage : MonoBehaviour
{
    GameObject stage;
    Camera previewCamera;
    RenderTexture texture;
    string key;
    bool needsRender;
    Coroutine build;
    readonly Dictionary<string, RenderTexture> snapshots = new Dictionary<string, RenderTexture>();
    public Texture Texture => texture;
    public bool Ready => key != null && snapshots.ContainsKey(key);
    public bool ReadyFor(BattleUnit unit) => unit != null && key == unit.GetInstanceID().ToString() && Ready;
    public void Show(IEnumerable<BattleUnit> templates)
    {
        var units = templates.Where(x => x != null).ToArray();
        string next = string.Join(",", units.Select(x => x.GetInstanceID()));
        if (key == next) return;
        Hide(); key = next;
        if (snapshots.TryGetValue(next, out texture)) return;
        build = StartCoroutine(Build(units, next));
    }
    IEnumerator Build(BattleUnit[] units, string snapshotKey)
    {
        // Let the menu and its input appear before preparing character meshes.
        yield return null;
        stage = new GameObject("Menu character stage"); stage.transform.SetParent(transform);
        stage.transform.position = new Vector3(0, -1000, 0); stage.SetActive(false);
        for (int i = 0; i < units.Length; i++)
        {
            var model = Instantiate(units[i].gameObject, stage.transform);
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) { script.enabled = false; Destroy(script); }
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var camera in model.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var particles in model.GetComponentsInChildren<ParticleSystem>(true)) { var main = particles.main; main.playOnAwake = false; particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            foreach (var renderer in model.GetComponentsInChildren<ParticleSystemRenderer>(true)) renderer.enabled = false;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
                var clip = animator.runtimeAnimatorController?.animationClips.FirstOrDefault(x => x.name.ToLowerInvariant().Contains("idle"));
                if (clip != null) clip.SampleAnimation(animator.gameObject, 0);
            }
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.SetActive(true); // Parent remains inactive until gameplay scripts are removed.
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length > 0)
            {
                foreach (var renderer in renderers) renderer.updateWhenOffscreen = true;
                Bounds bounds = ModelBounds(renderers);
                if (bounds.size.y > .05f) model.transform.localScale *= 3.3f / bounds.size.y;
                bounds = ModelBounds(renderers);
                model.transform.position += new Vector3((i - (units.Length - 1) * .5f) * 1.65f - (bounds.center.x - stage.transform.position.x), stage.transform.position.y - bounds.min.y, -(bounds.center.z - stage.transform.position.z));
            }
            yield return null;
        }
        var cameraObject = new GameObject("Menu portrait camera"); cameraObject.transform.SetParent(stage.transform, false);
        previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.enabled = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.006f, .012f, .014f, 1);
        previewCamera.cullingMask = 1 << 30; previewCamera.orthographic = true;
        var cameraData = previewCamera.GetUniversalAdditionalCameraData();
        cameraData.renderShadows = false; cameraData.renderPostProcessing = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off; cameraData.requiresDepthOption = CameraOverrideOption.Off;
        previewCamera.allowHDR = false; previewCamera.allowMSAA = false; previewCamera.useOcclusionCulling = false;
        int width = units.Length == 1 ? 576 : 768, height = units.Length == 1 ? 768 : 576;
        previewCamera.aspect = (float)width / height;
        var meshes = stage.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var framing = meshes.Length > 0 ? ModelBounds(meshes) : new Bounds(stage.transform.position + Vector3.up * 1.65f, new Vector3(3, 3.3f, 1));
        previewCamera.orthographicSize = Mathf.Max(.5f, Mathf.Max(framing.extents.y, framing.extents.x / previewCamera.aspect) * 1.15f);
        previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = Mathf.Max(30, framing.size.z + 10);
        previewCamera.transform.position = framing.center + Vector3.forward * (framing.extents.z + 8);
        previewCamera.transform.localRotation = Quaternion.Euler(0, 180, 0);
        texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); texture.Create();
        var previousTarget = RenderTexture.active;
        try { RenderTexture.active = texture; GL.Clear(true, true, previewCamera.backgroundColor); }
        finally { RenderTexture.active = previousTarget; }
        previewCamera.targetTexture = texture;
        AddLight("Portrait key", new Vector3(-3, 4, 4), 8.4f, new Color(1, .86f, .68f));
        AddLight("Portrait rim", new Vector3(3, 4, -3), 6f, new Color(.55f, .72f, 1));
        stage.SetActive(true); needsRender = true;
        RenderPipelineManager.endCameraRendering += FinishedRendering;
        previewCamera.enabled = true;
        // Render through the normal pipeline, avoiding a synchronous nested Canvas rebuild.
        yield return new WaitForEndOfFrame();
        if (needsRender && GraphicsSettings.currentRenderPipeline == null) needsRender = false;
        while (needsRender) yield return null;
        if (snapshots.Count >= 8)
        {
            var oldest = snapshots.First(); snapshots.Remove(oldest.Key);
            oldest.Value.Release(); Destroy(oldest.Value);
        }
        snapshots[snapshotKey] = texture;
        ReleaseStage(); build = null;
    }
    void AddLight(string label, Vector3 position, float intensity, Color color)
    {
        var obj = new GameObject(label); obj.transform.SetParent(stage.transform, false); obj.transform.localPosition = position;
        // A preview directional light can become URP's main light for every camera.
        // Local lights at the isolated stage cannot change the map's main light.
        var light = obj.AddComponent<Light>(); light.type = LightType.Point; light.range = 12; light.intensity = intensity;
        light.color = color; light.cullingMask = 1 << 30; light.shadows = LightShadows.None;
    }
    static Bounds ModelBounds(SkinnedMeshRenderer[] renderers)
    {
        // Use the sampled pose, not imported bind-pose bounds (which may frame only the feet).
        var result = new Bounds(); bool initialized = false;
        foreach (var renderer in renderers)
        {
            var baked = new Mesh(); renderer.BakeMesh(baked);
            Bounds local = baked.vertexCount > 0 ? baked.bounds : renderer.localBounds;
            Destroy(baked);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var point = renderer.transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                        if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                        else result.Encapsulate(point);
                    }
        }
        return result;
    }
    void FinishedRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != previewCamera) return;
        needsRender = false; previewCamera.enabled = false;
    }
    void ReleaseStage()
    {
        RenderPipelineManager.endCameraRendering -= FinishedRendering;
        if (stage != null) { stage.SetActive(false); Destroy(stage); }
        stage = null; previewCamera = null; needsRender = false;
    }
    public void Hide()
    {
        if (build != null) { StopCoroutine(build); build = null; }
        ReleaseStage();
        if (texture != null && !snapshots.ContainsValue(texture)) { texture.Release(); Destroy(texture); }
        texture = null; key = null;
    }
    public void Clear()
    {
        Hide();
        foreach (var cached in snapshots.Values) { cached.Release(); Destroy(cached); }
        snapshots.Clear();
    }
    void OnDestroy() { Clear(); }
}
