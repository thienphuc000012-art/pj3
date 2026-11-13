using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Limits punctual shadows through additive arena loading and combat, without changing illumination.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-1000)]
public sealed class CombatShadowBudget : MonoBehaviour
{
    [Min(0), Tooltip("Point Light dùng 6 shadow faces; Spot Light dùng 1.")]
    public int maximumShadowFaces = 12;
    [Min(1f)] public float shadowDistance = 30f;
    [Range(.1f, .9f), Tooltip("Phần atlas tối đa dành cho shadow cục bộ; giữ chỗ để tránh phân mảnh.")]
    public float atlasUsage = .75f;
    [Tooltip("Thông thường VFX chỉ cần chiếu sáng, không cần tạo thêm shadow map.")]
    public bool allowVfxShadows;

    public static CombatShadowBudget Active { get; private set; }
    public int SelectedShadowFaces { get; private set; }
    public int RegisteredLightCount => entries.Count;
    Vector3 center;
    bool subscribed;
    readonly HashSet<string> chunkPaths = new HashSet<string>();
    readonly Dictionary<Light, Entry> entries = new Dictionary<Light, Entry>();
    readonly List<GameObject> roots = new List<GameObject>();
    readonly List<Light> found = new List<Light>();
    readonly List<Light> dead = new List<Light>();
    readonly List<Entry> candidates = new List<Entry>();
    sealed class Entry
    {
        public Light light;
        public UniversalAdditionalLightData data;
        public LightShadows original;
        public bool vfx;
        public float distanceSquared;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Active = null;

    public static CombatShadowBudget Ensure(Scene owner)
    {
        var budget = Active;
        if (budget == null)
        {
            foreach (var root in owner.GetRootGameObjects())
                if (root.TryGetComponent<CombatShadowBudget>(out budget)) break;
            if (budget == null)
            {
                var root = new GameObject("Combat Shadow Budget");
                SceneManager.MoveGameObjectToScene(root, owner);
                budget = root.AddComponent<CombatShadowBudget>();
            }
        }
        Active = budget;
        budget.Subscribe();
        budget.Adopt(owner);
        return budget;
    }

    public void PrepareArena(CombatAreaScan scan)
    {
        if (scan == null) return;
        center = scan.SpawnPose.position;
        chunkPaths.Clear();
        foreach (string path in scan.ChunkPaths) chunkPaths.Add(path);
        foreach (string path in chunkPaths)
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded) RegisterScene(scene);
        }
        ApplyBudget();
    }

    public void SetCenter(Vector3 position) { center = position; ApplyBudget(); }

    void Adopt(Scene owner)
    {
        if (!owner.IsValid() || !owner.isLoaded) return;
        if (gameObject.scene != owner) SceneManager.MoveGameObjectToScene(gameObject, owner);
        RegisterScene(owner);
        ApplyBudget();
    }

    void OnEnable() { Active = this; Subscribe(); }
    void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        SceneManager.sceneLoaded += SceneLoaded;
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
    }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene == gameObject.scene || chunkPaths.Contains(scene.path))
        { RegisterScene(scene); ApplyBudget(); }
    }
    // No global searches or second camera cull: new chunks/VFX register once.
    void BeforeCamera(ScriptableRenderContext context, Camera camera) => ApplyBudget();

    public void RegisterScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        roots.Clear(); scene.GetRootGameObjects(roots);
        foreach (var root in roots) RegisterHierarchy(root);
    }
    public void RegisterHierarchy(GameObject root, bool vfx = false)
    {
        if (root == null) return;
        found.Clear(); root.GetComponentsInChildren(true, found);
        foreach (var light in found)
        {
            if (light.type != LightType.Point && light.type != LightType.Spot) continue;
            if (entries.TryGetValue(light, out var existing)) { existing.vfx |= vfx; continue; }
            entries.Add(light, new Entry { light = light, original = light.shadows,
                data = light.GetComponent<UniversalAdditionalLightData>(), vfx = vfx });
        }
    }

    public void ApplyBudget()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return;
        candidates.Clear(); dead.Clear();
        float distanceLimit = shadowDistance * shadowDistance;
        foreach (var pair in entries)
        {
            var entry = pair.Value; var light = pair.Key;
            if (light == null) { dead.Add(light); continue; }
            // Directional lights are never part of the punctual budget.
            if (light.type != LightType.Point && light.type != LightType.Spot) continue;
            entry.distanceSquared = (light.transform.position - center).sqrMagnitude;
            if (entry.original == LightShadows.None || !light.isActiveAndEnabled ||
                entry.distanceSquared > distanceLimit || entry.vfx && !allowVfxShadows)
            { if (light.shadows != LightShadows.None) light.shadows = LightShadows.None; continue; }
            candidates.Add(entry);
        }
        foreach (var light in dead) entries.Remove(light);
        candidates.Sort(NearestFirst);
        long capacity = (long)(pipeline.additionalLightsShadowmapResolution * (double)pipeline.additionalLightsShadowmapResolution * atlasUsage);
        int facesLeft = maximumShadowFaces;
        SelectedShadowFaces = 0;
        foreach (var entry in candidates)
        {
            int faces = entry.light.type == LightType.Point ? 6 : 1;
            int resolution = Resolution(entry, pipeline);
            long cost = (long)resolution * resolution * faces;
            bool allowed = resolution >= 128 && Mathf.IsPowerOfTwo(resolution) && faces <= facesLeft && cost <= capacity;
            var desired = allowed ? entry.original : LightShadows.None;
            if (entry.light.shadows != desired) entry.light.shadows = desired;
            if (allowed) { facesLeft -= faces; capacity -= cost; SelectedShadowFaces += faces; }
        }
    }
    static int NearestFirst(Entry a, Entry b)
    {
        int order = a.distanceSquared.CompareTo(b.distanceSquared);
        return order != 0 ? order : a.light.GetInstanceID().CompareTo(b.light.GetInstanceID());
    }
    static int Resolution(Entry entry, UniversalRenderPipelineAsset pipeline)
    {
        int tier = entry.data != null ? entry.data.additionalLightsShadowResolutionTier : 2;
        if (tier == -1) return (int)entry.light.shadowResolution;
        if (tier <= 0) return pipeline.additionalLightsShadowResolutionTierLow;
        return tier == 1 ? pipeline.additionalLightsShadowResolutionTierMedium : pipeline.additionalLightsShadowResolutionTierHigh;
    }
    void OnDisable() => Release();
    void OnDestroy() => Release();

    public void Release()
    {
        if (subscribed)
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            subscribed = false;
        }
        foreach (var entry in entries.Values)
            if (entry.light != null) entry.light.shadows = entry.original;
        entries.Clear();
        if (Active == this) Active = null;
    }
}
