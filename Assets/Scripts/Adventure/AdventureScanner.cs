using System;
using System.Collections.Generic;
using LineworkLite.Common.Utils;
using LineworkLite.FreeOutline;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using ScanOutline = LineworkLite.FreeOutline.Outline;

/// <summary>One scanner on the exploration player; it never moves or consumes an interaction.</summary>
[DisallowMultipleComponent]
public sealed class AdventureScanner : MonoBehaviour
{
    public AdventureScanSettings settings;
    public Transform scanOrigin;
    public Camera scanCamera;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Optional keyboard/gamepad action. If empty, use Scan Key from settings.")]
    public InputActionReference scanAction;
    bool ownsInputAction;
#endif
    public UnityEvent onScanStarted = new UnityEvent();
    public UnityEvent<AdventureScanTarget> onTargetDetected = new UnityEvent<AdventureScanTarget>();

    readonly List<AdventureScanTarget> pending = new List<AdventureScanTarget>();
    readonly List<Reveal> revealed = new List<Reveal>();
    readonly List<OutlineState> outlineStates = new List<OutlineState>();
    AdventureScanHUD hud;
    AudioSource audioSource;
    Vector3 origin;
    float pulseAge, cooldownUntil;
    bool scanning, ownsSettings;
    uint outlineMask;
    public float CooldownRemaining => Mathf.Max(0, cooldownUntil - Time.unscaledTime);
    public bool IsScanning => scanning;

    sealed class Reveal
    {
        public AdventureScanTarget target;
        public Renderer[] renderers;
        public uint[] previousBits;
        public float expiresAt;
    }
    sealed class OutlineState
    {
        public FreeOutline feature;
        public FreeOutlineSettings original, runtime;
        public ScanOutline outline;
    }

    public static AdventureScanner EnsureForMap(PlayerScript player)
    {
        if (player == null) return null;
        var scanner = player.GetComponent<AdventureScanner>();
        if (scanner == null) scanner = player.gameObject.AddComponent<AdventureScanner>();
        return scanner;
    }

    void OnEnable()
    {
        if (scanOrigin == null) scanOrigin = transform;
        if (settings == null) settings = Resources.Load<AdventureScanSettings>("AdventureScanSettings");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<AdventureScanSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            ownsSettings = true;
        }
        outlineMask = 1u << Mathf.Clamp(settings.outlineRenderingLayer, 0, 31);
        hud = GetComponent<AdventureScanHUD>();
        if (hud == null) hud = gameObject.AddComponent<AdventureScanHUD>();
        hud.Initialize(settings);
#if ENABLE_INPUT_SYSTEM
        if (scanAction != null && scanAction.action != null && !scanAction.action.enabled)
        { scanAction.action.Enable(); ownsInputAction = true; }
#endif
    }

    bool CanScanNow()
    {
        var session = CampaignSession.Instance;
        return settings != null && settings.scanEnabled && isActiveAndEnabled && Time.timeScale > 0f &&
            session != null && session.OnMap && session.MapPlayer != null &&
            session.MapPlayer.gameObject == gameObject && !CampaignSession.InputBlocked;
    }

    bool InputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (scanAction != null && scanAction.action != null)
            return scanAction.action.WasPressedThisFrame();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(settings.scanKey);
#elif ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Enum.TryParse(settings.scanKey.ToString(), out Key key) &&
            key != Key.None && Keyboard.current[key].wasPressedThisFrame;
#else
        return false;
#endif
    }

    void Update()
    {
        bool visible = CanScanNow();
        var session = CampaignSession.Instance;
        if (session != null && session.MapPlayer != null && session.MapPlayer.gameObject != gameObject)
        {
            CancelScan();
            if (hud != null) hud.Tick(transform, scanCamera, false, CooldownRemaining);
            return;
        }
        // A menu/encounter cancels the current reveal instead of resuming an
        // old paused pulse after its real-time cooldown has already elapsed.
        if (!visible) CancelScan();
        if (visible && InputPressed()) TryScan();
        if (scanCamera == null || !scanCamera.isActiveAndEnabled) scanCamera = Camera.main;

        if (scanning && visible)
        {
            pulseAge += Time.deltaTime;
            float travelTime = settings.radius / Mathf.Max(1f, settings.waveSpeed);
            float radius = Mathf.Min(settings.radius, pulseAge * settings.waveSpeed);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                AdventureScanTarget target = pending[i];
                if (target == null || !target.IsAvailable) { pending.RemoveAt(i); continue; }
                if ((target.ScanPosition - origin).sqrMagnitude > radius * radius) continue;
                pending.RemoveAt(i);
                if (HasLineOfSight(target)) RevealTarget(target);
            }
            if (AdventureScanRuntime.Owner == this)
            {
                AdventureScanRuntime.Origin = origin;
                AdventureScanRuntime.Radius = radius;
                AdventureScanRuntime.Strength = 1f - Mathf.Clamp01((pulseAge - travelTime) / Mathf.Max(.05f, settings.fadeDuration));
            }
            if (pulseAge >= travelTime + settings.fadeDuration) { scanning = false; pending.Clear(); }
        }
        if (AdventureScanRuntime.Owner == this)
        {
            AdventureScanRuntime.Active = scanning && visible;
            AdventureScanRuntime.Settings = settings;
            AdventureScanRuntime.TargetCamera = scanCamera;
        }
        for (int i = revealed.Count - 1; i >= 0; i--)
            if (revealed[i].target == null || !revealed[i].target.IsAvailable || Time.unscaledTime >= revealed[i].expiresAt)
            { Release(revealed[i]); revealed.RemoveAt(i); }
        UpdateOutline(visible);
        if (hud != null) hud.Tick(transform, scanCamera, visible, CooldownRemaining);
    }

    public bool TryScan()
    {
        if (!CanScanNow() || CooldownRemaining > 0f) return false;
        if (AdventureScanRuntime.Owner != null && AdventureScanRuntime.Owner != this)
            AdventureScanRuntime.Owner.CancelScan();
        AdventureScanRuntime.Owner = this;
        origin = scanOrigin.position;
        pulseAge = 0f;
        scanning = true;
        cooldownUntil = Time.unscaledTime + settings.cooldown;
        AdventureScanTarget.CopyActiveTargets(pending);
        float radiusSquared = settings.radius * settings.radius;
        pending.RemoveAll(target => (settings.detectionLayers.value & (1 << target.gameObject.layer)) == 0 ||
            (target.ScanPosition - origin).sqrMagnitude > radiusSquared);
        pending.Sort((a, b) => (a.ScanPosition - origin).sqrMagnitude.CompareTo((b.ScanPosition - origin).sqrMagnitude));
        int count = Mathf.Clamp(settings.maxDetectedTargets, 1, 128);
        if (pending.Count > count) pending.RemoveRange(count, pending.Count - count);
        InitializeOutline();
        if (settings.scanSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false; audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(settings.scanSound, settings.volume);
        }
        onScanStarted.Invoke();
        return true;
    }

    bool HasLineOfSight(AdventureScanTarget target)
    {
        if (!settings.requireLineOfSight) return true;
        if (!Physics.Linecast(origin + Vector3.up * .8f, target.MarkerPosition, out RaycastHit hit,
            settings.obstructionLayers, QueryTriggerInteraction.Ignore)) return true;
        return hit.transform == target.transform || hit.transform.IsChildOf(target.transform);
    }

    void RevealTarget(AdventureScanTarget target)
    {
        foreach (Reveal existing in revealed)
            if (existing.target == target)
            {
                existing.expiresAt = Time.unscaledTime + settings.revealDuration;
                hud.RevealTarget(target, existing.expiresAt);
                return;
            }
        // Bound both rendering and UI work even if a designer removes cooldown.
        if (revealed.Count >= Mathf.Clamp(settings.maxDetectedTargets, 1, 128))
        { Release(revealed[0]); revealed.RemoveAt(0); }
        var entry = new Reveal { target = target, renderers = target.GetHighlightRenderers(), expiresAt = Time.unscaledTime + settings.revealDuration };
        entry.previousBits = new uint[entry.renderers.Length];
        for (int i = 0; i < entry.renderers.Length; i++)
        {
            Renderer renderer = entry.renderers[i];
            if (renderer == null) continue;
            entry.previousBits[i] = renderer.renderingLayerMask & outlineMask;
            renderer.renderingLayerMask |= outlineMask;
        }
        revealed.Add(entry);
        hud.RevealTarget(target, entry.expiresAt);
        onTargetDetected.Invoke(target);
    }

    void InitializeOutline()
    {
        if (outlineStates.Count > 0 || !settings.showOutline) return;
        outlineMask = 1u << Mathf.Clamp(settings.outlineRenderingLayer, 0, 31);
        FreeOutline[] features = settings.outlineFeatures;
        if (features == null || features.Length == 0) features = Resources.FindObjectsOfTypeAll<FreeOutline>();
        foreach (FreeOutline feature in features)
        {
            if (feature == null || outlineStates.Exists(state => state.feature == feature)) continue;
            var state = new OutlineState { feature = feature, original = feature.Settings };
            state.runtime = state.original != null ? Instantiate(state.original) : ScriptableObject.CreateInstance<FreeOutlineSettings>();
            state.runtime.name = "Exploration Scan Outlines (Runtime)";
            state.runtime.hideFlags = HideFlags.HideAndDontSave;
            state.outline = ScriptableObject.CreateInstance<ScanOutline>();
            state.outline.hideFlags = HideFlags.HideAndDontSave;
            state.outline.RenderingLayer = outlineMask;
            state.outline.blendMode = BlendingMode.Additive;
            state.outline.occlusion = settings.requireLineOfSight ? Occlusion.WhenNotOccluded : Occlusion.Always;
            state.outline.renderQueue = OutlineRenderQueue.Opaque;
            state.outline.extrusionMethod = ExtrusionMethod.ClipSpaceNormalVector;
            state.outline.maskingStrategy = MaskingStrategy.Stencil;
            state.outline.scaling = Scaling.ConstantScreenSize;
            state.outline.scaleWithResolution = true;
            state.outline.referenceResolution = LineworkLite.FreeOutline.Resolution._1080;
            state.outline.SetActive(false);
            state.runtime.Outlines.Add(state.outline);
            feature.SetSettings(state.runtime);
            outlineStates.Add(state);
        }
    }

    void UpdateOutline(bool visible)
    {
        float pulse = 1f + settings.outlinePulseAmount * Mathf.Sin(Time.time * settings.outlinePulseSpeed * Mathf.PI * 2f);
        foreach (OutlineState state in outlineStates)
        {
            state.outline.color = settings.outlineColor * pulse;
            state.outline.width = settings.outlineWidth;
            state.outline.SetActive(visible && settings.showOutline && revealed.Count > 0);
        }
    }

    void Release(Reveal entry)
    {
        if (hud != null) hud.RemoveTarget(entry.target);
        for (int i = 0; i < entry.renderers.Length; i++)
            if (entry.renderers[i] != null)
                entry.renderers[i].renderingLayerMask = (entry.renderers[i].renderingLayerMask & ~outlineMask) | entry.previousBits[i];
    }

    public void CancelScan()
    {
        scanning = false; pending.Clear();
        if (AdventureScanRuntime.Owner == this)
        { AdventureScanRuntime.Active = false; AdventureScanRuntime.Owner = null; }
        foreach (Reveal entry in revealed) Release(entry);
        revealed.Clear();
        if (settings != null) UpdateOutline(false);
        if (hud != null) hud.Clear();
    }

    void OnDisable()
    {
        CancelScan();
#if ENABLE_INPUT_SYSTEM
        if (ownsInputAction && scanAction != null && scanAction.action != null) scanAction.action.Disable();
        ownsInputAction = false;
#endif
        foreach (OutlineState state in outlineStates)
        {
            if (state.feature != null && state.feature.Settings == state.runtime) state.feature.SetSettings(state.original);
            if (state.outline != null) { Destroy(state.outline.material); Destroy(state.outline); }
            Destroy(state.runtime);
        }
        outlineStates.Clear(); outlineMask = 0;
        if (hud != null) hud.Clear();
    }

    void OnDestroy() { if (ownsSettings && settings != null) Destroy(settings); }

    void OnDrawGizmosSelected()
    {
        if (settings == null) return;
        Gizmos.color = new Color(0f, .8f, 1f, .5f);
        Gizmos.DrawWireSphere(scanOrigin != null ? scanOrigin.position : transform.position, settings.radius);
    }
}
