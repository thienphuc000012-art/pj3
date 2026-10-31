using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public class MapChunkStreamer : MonoBehaviour
{
    [Serializable] public class Chunk { public string scenePath; public Bounds bounds; }
    public List<Chunk> chunks = new List<Chunk>();
    [Min(10)] public float loadDistance = 65;
    [Min(20)] public float unloadDistance = 100;
    [Min(2)] public float safetyDistance = 15;
    [Tooltip("Show the full-screen safety overlay if the player reaches an unloaded chunk during exploration.")]
    public bool showOverlayWhileStreaming;
    [Min(0)] public int shadowFaceBudget = 6;
    [Min(1)] public float localShadowDistance = 25;
    [Tooltip("Main-thread integration budget while exploring. Initial loading keeps the current priority so the loading screen can finish promptly.")]
    public UnityEngine.ThreadPriority explorationLoadingPriority = UnityEngine.ThreadPriority.Low;
    public static MapChunkStreamer Instance { get; private set; }
    public static bool MovementBlocked => Instance != null && Instance.Blocked;
    public bool Ready { get; private set; }
    public float InitialLoadProgress { get; private set; }
    public bool Blocked { get; private set; } = true;
    public bool SuppressOverlay { get; set; }
    public string Error { get; private set; }
    PlayerScript player;
    bool stopping, working;
    GameObject overlay;
    Text label;
    readonly HashSet<string> failed = new HashSet<string>();
    readonly Dictionary<Light, LightShadows> shadowLights = new Dictionary<Light, LightShadows>();
    readonly HashSet<string> loadedScenePaths = new HashSet<string>();
    readonly List<Chunk> initialChunks = new List<Chunk>();
    readonly List<GameObject> sceneRoots = new List<GameObject>();
    readonly List<Light> sceneLights = new List<Light>();
    readonly List<Light> deadLights = new List<Light>();
    struct ShadowCandidate { public Light light; public LightShadows shadows; public float distanceSquared; }
    readonly List<ShadowCandidate> shadowCandidates = new List<ShadowCandidate>();
    static readonly Comparison<ShadowCandidate> NearestShadowFirst = (a, b) => a.distanceSquared.CompareTo(b.distanceSquared);
    Vector3 initialPosition;
    string overlayMessage;
    bool ownsLoadingPriority;
    UnityEngine.ThreadPriority savedLoadingPriority;
    float nextShadowUpdate;
    int unloadedSinceCollection;
    int initialChunkCount;
    int initialChunksLoaded;
    Chunk pendingChunk;

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded) loadedScenePaths.Add(scene.path);
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => loadedScenePaths.Add(scene.path);
    void OnSceneUnloaded(Scene scene) => loadedScenePaths.Remove(scene.path);
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        RestoreLoadingPriority();
        if (Instance == this) Instance = null;
    }
    public IEnumerator Initialize(PlayerScript target)
    {
        player = target;
        RegisterSceneLights(gameObject.scene);
        // Chunks opened additively in the Editor may already be resident.
        for (int i = 0; i < chunks.Count; i++)
            if (Loaded(chunks[i])) RegisterSceneLights(SceneManager.GetSceneByPath(chunks[i].scenePath));
        CreateOverlay();
        yield return null;
        initialPosition = player.transform.position;
        initialChunks.Clear();
        float loadDistanceSquared = loadDistance * loadDistance;
        for (int i = 0; i < chunks.Count; i++)
            if (chunks[i] != null && DistanceSquared(chunks[i], initialPosition) <= loadDistanceSquared)
                initialChunks.Add(chunks[i]);
        initialChunks.Sort((a, b) => DistanceSquared(a, initialPosition).CompareTo(DistanceSquared(b, initialPosition)));
        initialChunkCount = initialChunks.Count;
        initialChunksLoaded = 0;
        InitialLoadProgress = initialChunkCount == 0 ? 1f : 0f;
        foreach (var chunk in initialChunks)
        {
            yield return Load(chunk);
            if (Error != null) yield break;
            initialChunksLoaded++;
            InitialLoadProgress = (float)initialChunksLoaded / initialChunkCount;
        }
        Physics.SyncTransforms();
        Ready = true;
        UpdateShadows();
        Update();
        StartCoroutine(Stream());
    }
    public static float Distance(Chunk chunk, Vector3 position)
        => Mathf.Sqrt(DistanceSquared(chunk, position));
    static float DistanceSquared(Chunk chunk, Vector3 position)
    {
        // Vertical map layers belong to the same streaming column.
        position.y = chunk.bounds.center.y;
        return chunk.bounds.SqrDistance(position);
    }
    bool Loaded(Chunk chunk) => chunk != null && loadedScenePaths.Contains(chunk.scenePath);
    void Update()
    {
        if (player == null) return;
        Blocked = !Ready || Error != null;
        if (!Blocked)
        {
            Vector3 position = player.transform.position;
            float safetyDistanceSquared = safetyDistance * safetyDistance;
            for (int i = 0; i < chunks.Count; i++)
                if (chunks[i] != null && (!Loaded(chunks[i]) || chunks[i] == pendingChunk) && DistanceSquared(chunks[i], position) <= safetyDistanceSquared)
                { Blocked = true; break; }
        }
        bool showBlockingOverlay = !Ready || showOverlayWhileStreaming || Error != null;
        bool showOverlay = Blocked && showBlockingOverlay && !stopping && !SuppressOverlay;
        if (overlay != null && overlay.activeSelf != showOverlay) overlay.SetActive(showOverlay);
        if (label != null && showOverlay)
        {
            string message = GameLocalization.Translate(Error ?? "Đang tải khu vực...");
            if (overlayMessage != message) { overlayMessage = message; label.text = message; }
        }
        if (Time.unscaledTime >= nextShadowUpdate)
        {
            nextShadowUpdate = Time.unscaledTime + .5f;
            UpdateShadows();
        }
    }
    IEnumerator Load(Chunk chunk)
    {
        if (Loaded(chunk) || failed.Contains(chunk.scenePath)) yield break;
        if (!Application.CanStreamedLevelBeLoaded(chunk.scenePath))
        {
            Error = "Thiếu scene khu vực trong Build Settings: " + chunk.scenePath;
            failed.Add(chunk.scenePath); Debug.LogError(Error, this); yield break;
        }
        working = true;
        pendingChunk = chunk;
        try
        {
            if (Ready)
            {
                savedLoadingPriority = Application.backgroundLoadingPriority;
                ownsLoadingPriority = true;
                Application.backgroundLoadingPriority = explorationLoadingPriority;
            }
            // Give the loading UI a render opportunity before scene integration.
            yield return null;
            var operation = SceneManager.LoadSceneAsync(chunk.scenePath, LoadSceneMode.Additive);
            if (operation != null) yield return operation;
            var loadedScene = SceneManager.GetSceneByPath(chunk.scenePath);
            if (!loadedScene.isLoaded)
            { Error = "Không tải được khu vực: " + chunk.scenePath; failed.Add(chunk.scenePath); yield break; }
            loadedScenePaths.Add(chunk.scenePath);
            // Do not pile scene activation, light discovery and world scans into
            // the same frame. Initial collider transforms are synced once below.
            yield return null;
            if (Ready) Physics.SyncTransforms();
            RegisterSceneLights(loadedScene);
            if (Ready)
            {
                UpdateShadows();
                yield return null;
                CampaignSession.Instance?.RefreshWorld(loadedScene);
            }
            yield return null;
        }
        finally
        {
            RestoreLoadingPriority();
            pendingChunk = null;
            working = false;
        }
    }

    void RestoreLoadingPriority()
    {
        if (!ownsLoadingPriority) return;
        Application.backgroundLoadingPriority = savedLoadingPriority;
        ownsLoadingPriority = false;
    }
    IEnumerator Stream()
    {
        var wait = new WaitForSecondsRealtime(.2f);
        while (!stopping && player != null)
        {
            Vector3 position = player.transform.position;
            float closestDistanceSquared = loadDistance * loadDistance;
            Chunk next = null;
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                if (chunk == null || Loaded(chunk) || failed.Contains(chunk.scenePath)) continue;
                float distanceSquared = DistanceSquared(chunk, position);
                if (distanceSquared <= closestDistanceSquared)
                { next = chunk; closestDistanceSquared = distanceSquared; }
            }
            if (next != null) { yield return Load(next); continue; }
            float farDistance = Mathf.Max(loadDistance + 20, unloadDistance);
            Chunk far = null;
            for (int i = 0; i < chunks.Count; i++)
                if (Loaded(chunks[i]) && DistanceSquared(chunks[i], position) > farDistance * farDistance)
                { far = chunks[i]; break; }
            if (far != null)
            {
                working = true;
                try
                {
                    var unloadingScene = SceneManager.GetSceneByPath(far.scenePath);
                    CampaignSession.Instance?.ForgetWorldScene(unloadingScene);
                    var operation = SceneManager.UnloadSceneAsync(far.scenePath);
                    if (operation != null) yield return operation;
                    unloadedSinceCollection++;
                }
                finally { working = false; }
            }
            // Unloading scenes removes objects/colliders, not necessarily their shared assets.
            // Reclaim unreferenced meshes/textures while the map is paused in a menu.
            if (!stopping && unloadedSinceCollection >= 4 && CampaignSession.Instance != null && CampaignSession.Instance.Menu != AdventureMenu.None)
            {
                working = true;
                try
                {
                    yield return Resources.UnloadUnusedAssets();
                    unloadedSinceCollection = 0;
                }
                finally { working = false; }
            }
            yield return wait;
        }
    }
    void RegisterSceneLights(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        sceneRoots.Clear();
        scene.GetRootGameObjects(sceneRoots);
        foreach (var root in sceneRoots)
        {
            sceneLights.Clear();
            root.GetComponentsInChildren(true, sceneLights);
            foreach (var light in sceneLights)
                if ((light.type == LightType.Point || light.type == LightType.Spot) && light.shadows != LightShadows.None && !shadowLights.ContainsKey(light))
                    shadowLights.Add(light, light.shadows);
        }
    }
    void UpdateShadows()
    {
        if (player == null) return;
        deadLights.Clear();
        shadowCandidates.Clear();
        Vector3 position = player.transform.position;
        float shadowDistanceSquared = localShadowDistance * localShadowDistance;
        foreach (var pair in shadowLights)
        {
            var light = pair.Key;
            if (light == null) { deadLights.Add(light); continue; }
            float distanceSquared = (light.transform.position - position).sqrMagnitude;
            if (!light.isActiveAndEnabled || distanceSquared > shadowDistanceSquared)
            {
                if (light.shadows != LightShadows.None) light.shadows = LightShadows.None;
                continue;
            }
            shadowCandidates.Add(new ShadowCandidate { light = light, shadows = pair.Value, distanceSquared = distanceSquared });
        }
        foreach (var light in deadLights) shadowLights.Remove(light);
        shadowCandidates.Sort(NearestShadowFirst);
        int remaining = shadowFaceBudget;
        foreach (var candidate in shadowCandidates)
        {
            var light = candidate.light;
            int cost = light.type == LightType.Point ? 6 : 1;
            bool use = remaining >= cost;
            var desired = use ? candidate.shadows : LightShadows.None;
            if (light.shadows != desired) light.shadows = desired;
            if (use) remaining -= cost;
        }
    }
    public void TransitionTo(string scene)
    {
        stopping = true;
        StartCoroutine(Transition(scene));
    }
    IEnumerator Transition(string scene)
    {
        while (working) yield return null;
        SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
    void CreateOverlay()
    {
        overlay = new GameObject("Chunk Loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
        var panel = new GameObject("Background", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(overlay.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(.015f, .025f, .04f, .97f);
        var text = new GameObject("Status", typeof(RectTransform), typeof(Text)); text.transform.SetParent(panel.transform, false);
        rect = text.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.1f,.3f); rect.anchorMax = new Vector2(.9f,.7f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 24; label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
        overlay.SetActive(false);
    }
}
