using System;
using System.Collections.Generic;
using LineworkLite.Common.Utils;
using LineworkLite.FreeOutline;
using UnityEngine;
using UnityEngine.UI;
using Outline = LineworkLite.FreeOutline.Outline;

[Serializable]
public sealed class CombatTargetStyle
{
    [ColorUsage(false, true)] public Color outlineColor = Color.red;
    [Range(0f, 30f)] public float width = 4f;
    [Min(0f), Tooltip("Độ sáng lóe lúc bắt đầu chọn, trong Enter Duration.")]
    public float enterBrightness = 2f;
    [Min(0f)] public float selectedBrightness = 1f;
    [Range(0f, .8f)] public float pulseAmount = .12f;
    [Min(0f)] public float pulseCyclesPerSecond = 1.2f;
}

/// <summary>Selection presentation only; never changes materials, physics layers or targeting rules.</summary>
[DisallowMultipleComponent]
public sealed class CombatTargetSelection : MonoBehaviour
{
    const int ChannelCount = 4;
    [Header("References")]
    public CombatManager combat;
    public Canvas canvas;
    public FreeOutline outlineFeature;
    public Camera targetCamera;
    public Sprite enemyTargetSprite;
    public Sprite playerTargetSprite;

    [Header("Enemy — Red")]
    public CombatTargetStyle enemyStyle = new CombatTargetStyle { outlineColor = new Color(1f, .075f, .045f) };
    [Header("Player — Blue")]
    public CombatTargetStyle playerStyle = new CombatTargetStyle { outlineColor = new Color(.055f, .38f, 1f) };
    [Header("Outline Timing (unscaled seconds)")]
    [Min(0f)] public float enterDuration = .1f;
    [Min(0f)] public float exitDuration = .16f;
    [Header("Outline Details")]
    public Occlusion occlusion = Occlusion.WhenNotOccluded;
    public OutlineRenderQueue renderQueue = OutlineRenderQueue.Opaque;
    public ExtrusionMethod extrusionMethod = ExtrusionMethod.ClipSpaceNormalVector;
    public MaskingStrategy maskingStrategy = MaskingStrategy.Stencil;
    public bool scaleWithResolution = true;
    [Range(8, 28), Tooltip("Dành riêng 4 Rendering Layer liên tiếp. Mặc định 24–27. Không phải GameObject Layer; chỉnh trước Play Mode.")]
    public int firstRenderingLayer = 24;

    [Header("Target Icon (Canvas units)")]
    public bool showMarker;
    [Min(1f)] public float markerSize = 150f;
    [Min(1f)] public float enterScalePeak = 1.45f;
    [Min(0f)] public float markerEnterDuration = .28f;
    public float rotationSpeed = 45f;
    [Range(0f, 1f)] public float markerOpacity = 1f;
    public Vector3 centerOffset;

    sealed class Channel
    {
        public Outline outline;
        public uint mask;
        public BattleUnit unit;
        public CombatTargetAnchor anchor;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<uint> originalBits = new List<uint>();
        public RectTransform marker;
        public Image image;
        public float elapsed, exitElapsed, strength, exitStrength;
        public bool leaving;
    }

    readonly List<Channel> channels = new List<Channel>(ChannelCount);
    FreeOutlineSettings originalSettings, runtimeSettings;
    RectTransform markerRoot;
    bool initialized;
    public BattleUnit CurrentTarget { get; private set; }
    public int ActiveVisualCount
    {
        get { int count = 0; foreach (var channel in channels) if (channel.unit != null) count++; return count; }
    }

    void OnEnable() { if (Application.isPlaying) Initialize(); }
    void OnDisable() => Shutdown();
    void OnDestroy() => Shutdown();

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (!initialized && !Initialize()) return;
        SelectTarget(combat != null && combat.IsChoosingTarget ? combat.currentTarget : null);
        Advance(Time.unscaledDeltaTime);
    }

    public bool Initialize()
    {
        if (initialized) return true;
        if (combat == null) combat = GetComponent<CombatManager>();
        if (outlineFeature == null) return false;
        originalSettings = outlineFeature.Settings;
        runtimeSettings = originalSettings != null ? Instantiate(originalSettings) : ScriptableObject.CreateInstance<FreeOutlineSettings>();
        runtimeSettings.name = "Combat Target Outlines (Runtime)";
        runtimeSettings.hideFlags = HideFlags.HideAndDontSave;
        for (int i = 0; i < ChannelCount; i++)
        {
            var channel = new Channel { mask = 1u << (Mathf.Clamp(firstRenderingLayer, 8, 28) + i) };
            channel.outline = ScriptableObject.CreateInstance<Outline>();
            channel.outline.name = "Target Selection " + i;
            channel.outline.hideFlags = HideFlags.HideAndDontSave;
            channel.outline.RenderingLayer = channel.mask;
            channel.outline.blendMode = BlendingMode.Alpha;
            channel.outline.SetActive(false);
            runtimeSettings.Outlines.Add(channel.outline);
            channels.Add(channel);
        }
        outlineFeature.SetSettings(runtimeSettings);
        initialized = true;
        return true;
    }

    public void SelectTarget(BattleUnit target)
    {
        if (!IsValid(target)) target = null;
        if (CurrentTarget == target) return;
        if (!initialized && !Initialize()) return;
        CurrentTarget = target;
        foreach (var channel in channels)
            if (channel.unit != null && channel.unit != target && !channel.leaving)
                BeginExit(channel);
        if (target == null) return;

        Channel chosen = null;
        foreach (var channel in channels) if (channel.unit == target) { chosen = channel; break; }
        if (chosen == null) foreach (var channel in channels) if (channel.unit == null) { chosen = channel; break; }
        if (chosen == null)
        {
            // Fast switching is bounded: retire the oldest outgoing channel.
            chosen = channels[0];
            foreach (var channel in channels)
                if (channel.exitElapsed > chosen.exitElapsed) chosen = channel;
            Release(chosen);
        }
        if (chosen.unit == null) Bind(chosen, target);
        chosen.leaving = false;
        chosen.elapsed = chosen.exitElapsed = 0f;
        chosen.strength = 1f;
        ApplyStyle(chosen);
        PositionMarker(chosen);
    }

    public void ClearSelection(bool immediate = false)
    {
        CurrentTarget = null;
        foreach (var channel in channels)
            if (immediate) Release(channel);
            else if (channel.unit != null && !channel.leaving) BeginExit(channel);
    }

    public void Advance(float deltaTime)
    {
        float dt = Mathf.Max(0f, deltaTime);
        foreach (var channel in channels)
        {
            if (channel.unit == null) { Release(channel); continue; }
            if (!IsValid(channel.unit))
            {
                if (CurrentTarget == channel.unit) CurrentTarget = null;
                Release(channel);
                continue;
            }
            channel.elapsed += dt;
            if (channel.leaving)
            {
                channel.exitElapsed += dt;
                float t = exitDuration <= 0 ? 1 : Mathf.Clamp01(channel.exitElapsed / exitDuration);
                channel.strength = channel.exitStrength * (1f - Mathf.SmoothStep(0, 1, t));
                if (t >= 1f) { Release(channel); continue; }
            }
            ApplyStyle(channel);
            PositionMarker(channel);
        }
    }

    void BeginExit(Channel channel)
    {
        channel.leaving = true;
        channel.exitElapsed = 0f;
        channel.exitStrength = channel.strength;
        if (exitDuration <= 0f) Release(channel);
    }

    void Bind(Channel channel, BattleUnit target)
    {
        channel.unit = target;
        channel.anchor = target.GetComponent<CombatTargetAnchor>();
        Renderer[] candidates = channel.anchor != null && channel.anchor.outlineRenderers != null && channel.anchor.outlineRenderers.Length > 0
            ? channel.anchor.outlineRenderers : target.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in candidates)
        {
            if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                renderer.GetComponentInParent<BattleUnit>() != target || channel.renderers.Contains(renderer)) continue;
            channel.renderers.Add(renderer);
            channel.originalBits.Add(renderer.renderingLayerMask & channel.mask);
            renderer.renderingLayerMask |= channel.mask;
        }
        EnsureMarker(channel);
        if (channel.image != null)
        {
            channel.image.sprite = target.isPlayer ? playerTargetSprite : enemyTargetSprite;
            channel.image.gameObject.SetActive(showMarker && channel.image.sprite != null);
        }
    }

    void ApplyStyle(Channel channel)
    {
        if (channel.unit == null) return;
        CombatTargetStyle style = channel.unit.isPlayer ? playerStyle : enemyStyle;
        float pulse = 1f + style.pulseAmount * Mathf.Sin(channel.elapsed * Mathf.PI * 2f * style.pulseCyclesPerSecond);
        float flash = enterDuration <= 0f ? 1f : Mathf.Clamp01(channel.elapsed / enterDuration);
        float brightness = channel.leaving ? style.selectedBrightness : Mathf.Lerp(style.enterBrightness, style.selectedBrightness * pulse, flash);
        Color color = style.outlineColor * Mathf.Max(0f, brightness);
        color.a = Mathf.Clamp01(style.outlineColor.a * channel.strength);
        channel.outline.color = color;
        channel.outline.occlusion = occlusion;
        channel.outline.renderQueue = renderQueue;
        channel.outline.extrusionMethod = extrusionMethod;
        channel.outline.maskingStrategy = maskingStrategy;
        channel.outline.scaling = Scaling.ConstantScreenSize;
        channel.outline.scaleWithResolution = scaleWithResolution;
        channel.outline.referenceResolution = LineworkLite.FreeOutline.Resolution._1080;
        channel.outline.width = Mathf.Max(0f, style.width);
        channel.outline.SetActive(channel.renderers.Count > 0 && channel.strength > 0f);
        if (channel.image != null) channel.image.color = new Color(1, 1, 1, markerOpacity * channel.strength);
    }

    void EnsureMarker(Channel channel)
    {
        if (!showMarker || canvas == null || channel.marker != null) return;
        if (markerRoot == null)
        {
            var root = new GameObject("Target Selection", typeof(RectTransform));
            markerRoot = root.GetComponent<RectTransform>();
            markerRoot.SetParent(canvas.transform, false);
            markerRoot.anchorMin = Vector2.zero; markerRoot.anchorMax = Vector2.one;
            markerRoot.offsetMin = markerRoot.offsetMax = Vector2.zero;
            markerRoot.gameObject.layer = canvas.gameObject.layer;
        }
        var go = new GameObject("Target Reticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        channel.marker = go.GetComponent<RectTransform>();
        channel.marker.SetParent(markerRoot, false);
        channel.marker.anchorMin = channel.marker.anchorMax = channel.marker.pivot = new Vector2(.5f, .5f);
        go.layer = canvas.gameObject.layer;
        channel.image = go.GetComponent<Image>();
        channel.image.raycastTarget = false;
        channel.image.preserveAspect = true;
    }

    void PositionMarker(Channel channel)
    {
        if (channel.marker == null || canvas == null) return;
        if (!showMarker) { channel.marker.gameObject.SetActive(false); return; }
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null) { channel.marker.gameObject.SetActive(false); return; }
        Vector3 center = channel.unit.transform.position + Vector3.up;
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (var renderer in channel.renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (hasBounds) center = bounds.center;
        if (channel.anchor != null)
        {
            if (channel.anchor.center != null) center = channel.anchor.center.position;
            Transform basis = channel.anchor.center != null ? channel.anchor.center : channel.unit.transform;
            center += basis.TransformVector(channel.anchor.localOffset);
        }
        center += centerOffset;
        Vector3 screen = camera.WorldToScreenPoint(center);
        bool visible = showMarker && channel.image.sprite != null && screen.z > camera.nearClipPlane;
        channel.marker.gameObject.SetActive(visible);
        if (!visible) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRoot, screen, uiCamera, out Vector2 point))
            channel.marker.anchoredPosition = point;
        channel.marker.sizeDelta = Vector2.one * markerSize * (channel.anchor != null ? channel.anchor.markerScale : 1f);
        float t = markerEnterDuration <= 0 ? 1f : Mathf.Clamp01(channel.elapsed / markerEnterDuration);
        float peak = Mathf.Max(1f, enterScalePeak);
        float scale = t < .35f ? Mathf.Lerp(1f, peak, Mathf.SmoothStep(0, 1, t / .35f)) : Mathf.Lerp(peak, 1f, Mathf.SmoothStep(0, 1, (t - .35f) / .65f));
        channel.marker.localScale = Vector3.one * scale;
        channel.marker.localRotation = Quaternion.Euler(0, 0, channel.elapsed * rotationSpeed);
    }

    void Release(Channel channel)
    {
        for (int i = 0; i < channel.renderers.Count; i++)
            if (channel.renderers[i] != null)
                channel.renderers[i].renderingLayerMask = (channel.renderers[i].renderingLayerMask & ~channel.mask) | channel.originalBits[i];
        channel.renderers.Clear(); channel.originalBits.Clear();
        channel.unit = null; channel.anchor = null; channel.leaving = false;
        channel.strength = 0f;
        if (channel.outline != null) channel.outline.SetActive(false);
        if (channel.marker != null) channel.marker.gameObject.SetActive(false);
    }

    public Outline GetOutline(BattleUnit target)
    {
        foreach (var channel in channels) if (target != null && channel.unit == target) return channel.outline;
        return null;
    }

    public RectTransform GetMarker(BattleUnit target)
    {
        foreach (var channel in channels) if (target != null && channel.unit == target) return channel.marker;
        return null;
    }

    public void Shutdown()
    {
        ClearSelection(true);
        if (outlineFeature != null && runtimeSettings != null && outlineFeature.Settings == runtimeSettings)
            outlineFeature.SetSettings(originalSettings);
        foreach (var channel in channels)
        {
            if (channel.outline != null) { DestroyOwned(channel.outline.material); DestroyOwned(channel.outline); }
        }
        channels.Clear();
        DestroyOwned(runtimeSettings);
        if (markerRoot != null) { markerRoot.gameObject.SetActive(false); DestroyOwned(markerRoot.gameObject); }
        runtimeSettings = null; originalSettings = null; markerRoot = null; initialized = false;
    }

    static bool IsValid(BattleUnit unit) => unit != null && unit.isActiveAndEnabled && !unit.IsDead;
    static void DestroyOwned(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
