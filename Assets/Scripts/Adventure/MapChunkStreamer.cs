using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [Min(0)] public int shadowFaceBudget = 6;
    [Min(1)] public float localShadowDistance = 25;
    public static MapChunkStreamer Instance { get; private set; }
    public static bool MovementBlocked => Instance != null && Instance.Blocked;
    public bool Ready { get; private set; }
    public bool Blocked { get; private set; } = true;
    public string Error { get; private set; }
    PlayerScript player;
    bool stopping, working;
    GameObject overlay;
    Text label;
    readonly HashSet<string> failed = new HashSet<string>();
    readonly Dictionary<Light, LightShadows> shadowLights = new Dictionary<Light, LightShadows>();
    float nextShadowUpdate;
    int unloadedSinceCollection;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    public IEnumerator Initialize(PlayerScript target)
    {
        player = target;
        RegisterLights();
        CreateOverlay();
        yield return null;
        foreach (var chunk in chunks.OrderBy(x => Distance(x, player.transform.position)))
        {
            if (Distance(chunk, player.transform.position) <= loadDistance) yield return Load(chunk);
            if (Error != null) yield break;
        }
        Physics.SyncTransforms();
        Ready = true;
        Update();
        StartCoroutine(Stream());
    }
    public static float Distance(Chunk chunk, Vector3 position)
    {
        // Vertical map layers belong to the same streaming column.
        position.y = chunk.bounds.center.y;
        return Mathf.Sqrt(chunk.bounds.SqrDistance(position));
    }
    bool Loaded(Chunk chunk) => SceneManager.GetSceneByPath(chunk.scenePath).isLoaded;
    void Update()
    {
        if (player == null) return;
        Blocked = !Ready || Error != null || chunks.Any(x => !Loaded(x) && Distance(x, player.transform.position) <= safetyDistance);
        if (overlay != null) overlay.SetActive(Blocked && !stopping);
        if (label != null) label.text = Error ?? "Đang tải khu vực...";
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
        var operation = SceneManager.LoadSceneAsync(chunk.scenePath, LoadSceneMode.Additive);
        if (operation != null) yield return operation;
        working = false;
        if (!Loaded(chunk)) { Error = "Không tải được khu vực: " + chunk.scenePath; failed.Add(chunk.scenePath); yield break; }
        Physics.SyncTransforms();
        RegisterLights();
        CampaignSession.Instance?.RefreshWorld();
        yield return null;
    }
    IEnumerator Stream()
    {
        while (!stopping && player != null)
        {
            var next = chunks.Where(x => !Loaded(x) && !failed.Contains(x.scenePath) && Distance(x, player.transform.position) <= loadDistance)
                .OrderBy(x => Distance(x, player.transform.position)).FirstOrDefault();
            if (next != null) { yield return Load(next); continue; }
            var far = chunks.FirstOrDefault(x => Loaded(x) && Distance(x, player.transform.position) > Mathf.Max(loadDistance + 20, unloadDistance));
            if (far != null)
            {
                working = true;
                var operation = SceneManager.UnloadSceneAsync(far.scenePath);
                if (operation != null) yield return operation;
                working = false;
                unloadedSinceCollection++;
                CampaignSession.Instance?.RefreshWorld();
            }
            // Unloading scenes removes objects/colliders, not necessarily their shared assets.
            // Reclaim unreferenced meshes/textures while the map is paused in a menu.
            if (!stopping && unloadedSinceCollection >= 4 && CampaignSession.Instance != null && CampaignSession.Instance.Menu != AdventureMenu.None)
            {
                working = true;
                yield return Resources.UnloadUnusedAssets();
                working = false;
                unloadedSinceCollection = 0;
            }
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
    void RegisterLights()
    {
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if ((light.type == LightType.Point || light.type == LightType.Spot) && light.shadows != LightShadows.None && !shadowLights.ContainsKey(light))
                shadowLights.Add(light, light.shadows);
        UpdateShadows();
    }
    void UpdateShadows()
    {
        foreach (var dead in shadowLights.Keys.Where(x => x == null).ToArray()) shadowLights.Remove(dead);
        if (player == null) return;
        int remaining = shadowFaceBudget;
        foreach (var pair in shadowLights.OrderBy(x => (x.Key.transform.position - player.transform.position).sqrMagnitude))
        {
            var light = pair.Key;
            int cost = light.type == LightType.Point ? 6 : 1;
            bool use = light.isActiveAndEnabled && remaining >= cost && Vector3.Distance(light.transform.position, player.transform.position) <= localShadowDistance;
            light.shadows = use ? pair.Value : LightShadows.None;
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
    }
}
