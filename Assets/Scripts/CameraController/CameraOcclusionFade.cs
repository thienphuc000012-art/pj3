using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Runs on the real output camera, after Cinemachine has positioned it.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(10000)]
public sealed class CameraOcclusionFade : MonoBehaviour
{
    [Header("Objects blocking the view")]
    public LayerMask obstructionLayers = Physics.DefaultRaycastLayers & ~(1 << 5) & ~(1 << 7);
    [Range(0f, 1f)] public float occludedAlpha = .16f;
    [Min(.01f)] public float fadeOutSeconds = .16f;
    [Min(.01f)] public float fadeInSeconds = .25f;
    [Min(0f)] public float clearDelay = .1f;
    [Min(.01f)] public float checkInterval = .06f;
    [Min(.01f)] public float castRadius = .16f;
    [Min(1f)] public float maximumDistance = 100f;
    [Min(0f)] public float verticalSampleOffset = .35f;
    [Tooltip("Material URP Lit trong suốt dự phòng cho cây/đá có shader riêng; giữ variant trong suốt trong build.")]
    public Material fallbackTemplate;
    [Header("Optional manual target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.4f, 0);

    Camera output;
    Transform primary, secondary;
    Vector3 primaryPoint, secondaryPoint;
    bool hasFocus;
    float elapsed, nextScan, nextCacheCleanup;
    RaycastHit[] hits = new RaycastHit[32];
    Collider[] overlaps = new Collider[16];
    readonly Dictionary<Collider, Renderer[]> colliderCache = new Dictionary<Collider, Renderer[]>();
    readonly Dictionary<Renderer, FadedRenderer> fading = new Dictionary<Renderer, FadedRenderer>();
    readonly HashSet<Renderer> blocked = new HashSet<Renderer>();
    readonly List<Renderer> finished = new List<Renderer>();
    readonly List<Collider> deadColliders = new List<Collider>();
    public int FadingRendererCount => fading.Count;

    public static CameraOcclusionFade Ensure(Camera camera)
    {
        if (camera == null) return null;
        return camera.GetComponent<CameraOcclusionFade>() ?? camera.gameObject.AddComponent<CameraOcclusionFade>();
    }

    public static CameraOcclusionFade EnsureForScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var brain in root.GetComponentsInChildren<CinemachineBrain>(true))
                if (brain.TryGetComponent<Camera>(out var camera) && camera.CompareTag("MainCamera")) return Ensure(camera);
        return null;
    }

    public void SetFocus(Transform main, Vector3 mainPoint, Transform other = null, Vector3 otherPoint = default)
    {
        if (primary != main || secondary != other) nextScan = 0;
        primary = main; primaryPoint = mainPoint;
        secondary = other; secondaryPoint = otherPoint;
        hasFocus = main != null;
    }

    void Awake()
    {
        output = GetComponent<Camera>();
        if (fallbackTemplate == null) fallbackTemplate = Resources.Load<Material>("CameraOcclusion/FadeTemplate");
    }
    void LateUpdate() => Tick(Time.unscaledDeltaTime);

    // Public for deterministic validation without entering a real campaign.
    public void Tick(float deltaTime)
    {
        if (output == null) output = GetComponent<Camera>();
        elapsed += Mathf.Max(0f, deltaTime);
        bool valid = output != null && output.enabled && (target != null || hasFocus && primary != null);
        if (elapsed >= nextScan || !valid)
        {
            nextScan = elapsed + Mathf.Max(.01f, checkInterval);
            blocked.Clear();
            if (valid) Scan();
            foreach (var renderer in blocked)
            {
                if (!fading.TryGetValue(renderer, out var state))
                {
                    state = new FadedRenderer(renderer, fallbackTemplate);
                    fading.Add(renderer, state);
                }
                state.lastSeen = elapsed;
            }
        }
        finished.Clear();
        foreach (var pair in fading)
        {
            var state = pair.Value;
            bool hide = valid && obstructionLayers.value != 0 && elapsed - state.lastSeen <= clearDelay + checkInterval;
            float goal = hide ? Mathf.Clamp01(occludedAlpha) : 1f;
            state.alpha = Mathf.MoveTowards(state.alpha, goal, Mathf.Max(0, deltaTime) /
                Mathf.Max(.01f, hide ? fadeOutSeconds : fadeInSeconds));
            state.Apply();
            if (pair.Key == null || !hide && state.alpha >= .9999f) finished.Add(pair.Key);
        }
        foreach (var renderer in finished) { fading[renderer].Restore(); fading.Remove(renderer); }
        if (elapsed >= nextCacheCleanup)
        {
            nextCacheCleanup = elapsed + 3f;
            deadColliders.Clear();
            foreach (var pair in colliderCache) if (pair.Key == null) deadColliders.Add(pair.Key);
            foreach (var collider in deadColliders) colliderCache.Remove(collider);
        }
    }

    void Scan()
    {
        var physics = gameObject.scene.GetPhysicsScene();
        Vector3 point = target != null ? target.position + targetOffset : primaryPoint;
        Cast(physics, point);
        if (verticalSampleOffset > 0) Cast(physics, point + Vector3.up * verticalSampleOffset);
        if (target == null && secondary != null && secondary != primary) Cast(physics, secondaryPoint);
        // SphereCast does not report colliders containing its origin.
        int count;
        while ((count = physics.OverlapSphere(transform.position, Mathf.Max(castRadius, output.nearClipPlane),
                   overlaps, obstructionLayers, QueryTriggerInteraction.Ignore)) == overlaps.Length && overlaps.Length < 4096)
            Array.Resize(ref overlaps, overlaps.Length * 2);
        for (int i = 0; i < count; i++) Collect(overlaps[i]);
        foreach (var marker in CameraOcclusionObject.Active)
        {
            if (marker == null || marker.ignoreOcclusion || !marker.isActiveAndEnabled ||
                (obstructionLayers.value & (1 << marker.gameObject.layer)) == 0) continue;
            foreach (var renderer in marker.Renderers)
                if (Eligible(renderer) && (IntersectsPrimary(renderer.bounds, point) ||
                    target == null && secondary != null && Intersects(renderer.bounds, secondaryPoint)))
                    AddGroup(marker.Renderers);
        }
    }

    bool Intersects(Bounds bounds, Vector3 point)
    {
        Vector3 offset = point - transform.position;
        float length = Mathf.Min(maximumDistance, offset.magnitude - .1f);
        if (length <= 0) return false;
        bounds.Expand(castRadius * 2f);
        return bounds.Contains(transform.position) || bounds.IntersectRay(new Ray(transform.position, offset.normalized), out float distance) && distance < length;
    }

    bool IntersectsPrimary(Bounds bounds, Vector3 point) => Intersects(bounds, point) ||
        verticalSampleOffset > 0 && Intersects(bounds, point + Vector3.up * verticalSampleOffset);

    void Cast(PhysicsScene physics, Vector3 point)
    {
        Vector3 ray = point - transform.position;
        float length = Mathf.Min(maximumDistance, ray.magnitude - .1f);
        if (length <= 0) return;
        int count;
        while ((count = physics.SphereCast(transform.position, castRadius, ray.normalized, hits, length,
                   obstructionLayers, QueryTriggerInteraction.Ignore)) == hits.Length && hits.Length < 4096)
            Array.Resize(ref hits, hits.Length * 2);
        for (int i = 0; i < count; i++) Collect(hits[i].collider);
    }

    void Collect(Collider collider)
    {
        if (collider == null || collider is TerrainCollider || IsCharacter(collider.transform)) return;
        var group = collider.GetComponentInParent<CameraOcclusionObject>();
        if (group != null)
        {
            if (!group.ignoreOcclusion && group.isActiveAndEnabled) AddGroup(group.Renderers);
            return;
        }
        if (!colliderCache.TryGetValue(collider, out var renderers))
        {
            var lod = collider.GetComponentInParent<LODGroup>();
            if (lod != null) renderers = lod.GetComponentsInChildren<Renderer>(true);
            else if (collider.TryGetComponent<Renderer>(out var own)) renderers = new[] { own };
            else
            {
                renderers = collider.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    var parent = collider.GetComponentInParent<Renderer>();
                    renderers = parent != null ? new[] { parent } : Array.Empty<Renderer>();
                }
            }
            colliderCache.Add(collider, renderers);
        }
        foreach (var renderer in renderers)
            if (Eligible(renderer) && IntersectsPrimary(renderer.bounds, target != null ? target.position + targetOffset : primaryPoint))
                blocked.Add(renderer);
            else if (Eligible(renderer) && secondary != null && Intersects(renderer.bounds, secondaryPoint)) blocked.Add(renderer);
    }

    void AddGroup(Renderer[] renderers) { foreach (var renderer in renderers) if (Eligible(renderer)) blocked.Add(renderer); }
    bool Eligible(Renderer renderer) => renderer != null && renderer.gameObject.activeInHierarchy &&
        (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && !IsCharacter(renderer.transform) &&
        (output.cullingMask & (1 << renderer.gameObject.layer)) != 0;
    bool IsCharacter(Transform node)
    {
        if (node == null) return false;
        if (primary != null && node.IsChildOf(primary) || secondary != null && node.IsChildOf(secondary) ||
            target != null && node.IsChildOf(target)) return true;
        if (node.GetComponentInParent<BattleUnit>() != null || node.GetComponentInParent<PlayerScript>() != null) return true;
        var interaction = node.GetComponentInParent<WorldInteraction>();
        return interaction != null && (interaction.kind == WorldInteractionKind.Encounter || interaction.kind == WorldInteractionKind.DialogueNPC);
    }

    public void RestoreAll()
    {
        foreach (var state in fading.Values) state.Restore();
        fading.Clear(); blocked.Clear(); colliderCache.Clear();
    }
    void OnDisable() => RestoreAll();
    void OnDestroy() => RestoreAll();

    sealed class FadedRenderer
    {
        readonly Renderer renderer;
        readonly Material[] originals, variants;
        readonly MaterialPropertyBlock[] originalBlocks, blocks;
        readonly Color[] colors;
        readonly List<Material> current = new List<Material>();
        public float alpha = 1f, lastSeen;
        public FadedRenderer(Renderer renderer, Material fallback)
        {
            this.renderer = renderer;
            originals = renderer.sharedMaterials;
            variants = new Material[originals.Length];
            colors = new Color[originals.Length];
            blocks = new MaterialPropertyBlock[originals.Length];
            originalBlocks = new MaterialPropertyBlock[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material source = originals[i];
                if (source == null) continue;
                variants[i] = CreateVariant(source, fallback);
                originalBlocks[i] = new MaterialPropertyBlock(); renderer.GetPropertyBlock(originalBlocks[i], i);
                blocks[i] = new MaterialPropertyBlock(); renderer.GetPropertyBlock(blocks[i], i);
                if (blocks[i].isEmpty) renderer.GetPropertyBlock(blocks[i]);
                colors[i] = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                    source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                if (blocks[i].HasColor("_BaseColor")) colors[i] = blocks[i].GetColor("_BaseColor");
                else if (blocks[i].HasColor("_Color")) colors[i] = blocks[i].GetColor("_Color");
            }
            renderer.sharedMaterials = variants;
        }
        public void Apply()
        {
            if (renderer == null) return;
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i] == null) continue;
                Color color = colors[i]; color.a *= alpha;
                blocks[i].SetColor("_BaseColor", color); blocks[i].SetColor("_Color", color);
                renderer.SetPropertyBlock(blocks[i], i);
            }
        }
        public void Restore()
        {
            if (renderer != null)
            {
                renderer.GetSharedMaterials(current);
                bool owned = current.Count == variants.Length;
                for (int i = 0; owned && i < current.Count; i++) owned = current[i] == variants[i];
                if (owned)
                {
                    renderer.sharedMaterials = originals;
                    for (int i = 0; i < originalBlocks.Length; i++)
                        if (originalBlocks[i] != null) renderer.SetPropertyBlock(originalBlocks[i].isEmpty ? null : originalBlocks[i], i);
                }
            }
            foreach (var variant in variants)
                if (variant != null) { if (Application.isPlaying) Destroy(variant); else DestroyImmediate(variant); }
        }
    }

    static Material CreateVariant(Material source, Material fallback)
    {
        bool native = source.shader.name == "Universal Render Pipeline/Lit" ||
            source.shader.name == "Universal Render Pipeline/Simple Lit" || source.shader.name == "Universal Render Pipeline/Unlit";
        Shader shader = fallback != null ? fallback.shader : Shader.Find("Universal Render Pipeline/Lit");
        var material = native || shader == null ? new Material(source) : fallback != null ? new Material(fallback) : new Material(shader);
        material.name = source.name + " (Camera Fade Runtime)"; material.hideFlags = HideFlags.HideAndDontSave;
        if (!native && shader != null)
        {
            CopyTexture(source, material, "_BaseMap", "_BaseMap", "_AlbedoMap", "_MainTex", "_BaseColorMap");
            CopyTexture(source, material, "_BumpMap", "_BumpMap", "_NormalMap");
            CopyTexture(source, material, "_EmissionMap", "_EmissionMap");
            foreach (string property in new[] { "_Metallic", "_Smoothness", "_Cull", "_BumpScale", "_EmissionColor", "_Cutoff", "_AlphaClip" })
                if (source.HasProperty(property) && material.HasProperty(property))
                {
                    if (property == "_EmissionColor") material.SetColor(property, source.GetColor(property));
                    else material.SetFloat(property, source.GetFloat(property));
                }
            if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
        }
        Set("_Surface", 1); Set("_Blend", 0); Set("_BlendModePreserveSpecular", 0);
        Set("_SrcBlend", (float)BlendMode.SrcAlpha); Set("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        Set("_SrcBlendAlpha", (float)BlendMode.One); Set("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        Set("_ZWrite", 0); Set("_AlphaToMask", 0); Set("_AlphaClip", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); material.DisableKeyword("_ALPHAMODULATE_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = (int)RenderQueue.Transparent;
        foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals", "DepthNormalsOnly" }) material.SetShaderPassEnabled(pass, false);
        return material;
        void Set(string property, float value) { if (material.HasProperty(property)) material.SetFloat(property, value); }
    }
    static void CopyTexture(Material source, Material destination, string to, params string[] from)
    {
        if (!destination.HasProperty(to)) return;
        foreach (string name in from)
            if (source.HasProperty(name) && source.GetTexture(name) != null)
            {
                destination.SetTexture(to, source.GetTexture(name));
                destination.SetTextureScale(to, source.GetTextureScale(name));
                destination.SetTextureOffset(to, source.GetTextureOffset(name));
                return;
            }
    }
}
