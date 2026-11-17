using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A non-blocking, Canvas-authored area title. Discovery/save ownership stays in WorldInteraction.</summary>
[DisallowMultipleComponent]
public sealed class DiscoveryAreaBanner : MonoBehaviour
{
    [Header("Discovery Area UI")]
    public CanvasGroup panel;
    public TMP_Text areaName;
    public CanvasGroup nameGroup;
    public RectTransform topReveal;
    public Image topDivider;
    [Header("Animation")]
    [Min(.05f)] public float frameRevealSeconds = .7f;
    [Min(0f)] public float titleDelaySeconds = .3f;
    [Min(.05f)] public float titleFadeSeconds = .65f;
    [Min(0f)] public float holdSeconds = 3.2f;
    [Min(.05f)] public float fadeOutSeconds = .7f;
    [Min(0f)] public float titleRise = 8f;

    readonly Queue<(string id, string title)> pending = new Queue<(string, string)>();
    readonly HashSet<string> queuedIds = new HashSet<string>();
    string activeId;
    Vector2 titlePosition;
    float topWidth, elapsed;
    bool cached, playing;
    public bool IsPlaying => playing;
    public int PendingCount => pending.Count;
    public float Duration => Mathf.Max(frameRevealSeconds, titleDelaySeconds + titleFadeSeconds) + holdSeconds + fadeOutSeconds;

    public static void Announce(WorldInteraction area)
    {
        if (area == null || area.kind != WorldInteractionKind.Discovery || !area.showDiscoveryBanner) return;
        var canvas = AdventureCanvasRoot.Acquire(AdventureCanvasKind.Adventure);
        var banner = canvas.GetComponent<DiscoveryAreaBanner>();
        if (banner == null) { Debug.LogWarning("[Discovery Area] AdventureCanvas is missing its DiscoveryAreaBanner component.", area); return; }
        banner.Enqueue(area.Id, string.IsNullOrWhiteSpace(area.displayName) ? area.name : area.displayName);
    }

    void Awake() { CacheLayout(); Hide(); }

    bool CacheLayout()
    {
        if (cached) return true;
        if (panel == null || areaName == null || nameGroup == null || topReveal == null || topDivider == null) return false;
        titlePosition = areaName.rectTransform.anchoredPosition;
        topWidth = topDivider.rectTransform.rect.width;
        cached = true;
        panel.blocksRaycasts = panel.interactable = false;
        nameGroup.blocksRaycasts = nameGroup.interactable = false;
        foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        return true;
    }

    public bool Enqueue(string id, string title)
    {
        if (!CacheLayout() || string.IsNullOrWhiteSpace(title)) return false;
        string key = string.IsNullOrEmpty(id) ? title : id;
        if (!queuedIds.Add(key)) return false;
        pending.Enqueue((key, title));
        if (!playing) StartNext();
        return true;
    }

    void StartNext()
    {
        if (activeId != null) { queuedIds.Remove(activeId); activeId = null; }
        if (pending.Count == 0) { Hide(); return; }
        var notice = pending.Dequeue();
        activeId = notice.id;
        GameLocalization.SetText(areaName, notice.title);
        playing = true; elapsed = 0f;
        panel.gameObject.SetActive(true);
        Sample(0f);
    }

    void Update()
    {
        // Menus hide HUD; pause the announcement until exploration is visible again.
        if (playing && panel != null && panel.gameObject.activeInHierarchy) Tick(Time.unscaledDeltaTime);
    }

    public void Tick(float seconds)
    {
        if (!playing) return;
        elapsed += Mathf.Max(0f, seconds);
        Sample(elapsed);
        if (elapsed >= Duration) StartNext();
    }

    // Deterministic visual sampling also permits native Edit Mode previews without running a campaign.
    public void Sample(float seconds)
    {
        if (!CacheLayout()) return;
        float frame = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seconds / Mathf.Max(.05f, frameRevealSeconds)));
        float name = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((seconds - titleDelaySeconds) / Mathf.Max(.05f, titleFadeSeconds)));
        float exitStart = Mathf.Max(frameRevealSeconds, titleDelaySeconds + titleFadeSeconds) + holdSeconds;
        float exit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((seconds - exitStart) / Mathf.Max(.05f, fadeOutSeconds)));
        panel.alpha = Mathf.Clamp01(seconds / .12f) * (1f - exit);
        topReveal.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, topWidth * frame);
        nameGroup.alpha = name;
        areaName.rectTransform.anchoredPosition = titlePosition + Vector2.down * (titleRise * (1f - name));
    }

    void Hide()
    {
        playing = false;
        if (panel != null) { panel.alpha = 0f; panel.gameObject.SetActive(false); }
    }
    public void Cancel() { pending.Clear(); queuedIds.Clear(); activeId = null; Hide(); }
    void OnDisable() => Cancel();
}
