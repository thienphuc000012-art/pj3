using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An opt-in scan target. Enabled targets are registered without requiring a collider.</summary>
[DisallowMultipleComponent]
public sealed class AdventureScanTarget : MonoBehaviour
{
    [Tooltip("Optional interaction whose name and collected state are used by the scanner.")]
    public WorldInteraction interaction;
    public bool detectable = true;
    public bool showMarker = true;
    [Tooltip("Optional marker artwork. An empty icon uses the scanner's default marker.")]
    public Sprite markerIcon;
    [Tooltip("Optional world point used for radius detection. Defaults to the object pivot until renderers are cached.")]
    public Transform scanAnchor;
    [Tooltip("Optional marker point. Defaults to the top of the visible model bounds.")]
    public Transform markerAnchor;
    public Vector3 markerOffset = new Vector3(0f, 0.35f, 0f);

    static readonly HashSet<AdventureScanTarget> activeTargets = new HashSet<AdventureScanTarget>();
    Renderer[] highlightRenderers = Array.Empty<Renderer>();
    bool renderersCached;

    public bool IsAvailable => detectable && isActiveAndEnabled && gameObject.activeInHierarchy &&
        (interaction == null || (interaction.isActiveAndEnabled && !interaction.IsConsumed));

    public string DisplayName => interaction != null && !string.IsNullOrWhiteSpace(interaction.displayName)
        ? interaction.displayName : gameObject.name;

    public Vector3 ScanPosition
    {
        get
        {
            if (scanAnchor != null) return scanAnchor.position;
            return TryGetVisibleBounds(out Bounds bounds) ? bounds.center : transform.position;
        }
    }

    public Vector3 MarkerPosition
    {
        get
        {
            if (markerAnchor != null) return markerAnchor.position + markerOffset;
            if (TryGetVisibleBounds(out Bounds bounds))
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + markerOffset;
            return transform.position + Vector3.up * 1.5f + markerOffset;
        }
    }

    /// <summary>Clears and fills a caller-owned buffer with currently available, loaded targets.</summary>
    public static void CopyActiveTargets(List<AdventureScanTarget> destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        foreach (AdventureScanTarget target in activeTargets)
            if (target != null && target.IsAvailable) destination.Add(target);
    }

    /// <summary>Preserves an authored component and its settings; creates one only if absent.</summary>
    public static AdventureScanTarget EnsureForInteraction(WorldInteraction source)
    {
        if (source == null) return null;
        AdventureScanTarget target = source.GetComponent<AdventureScanTarget>();
        if (target == null) target = source.gameObject.AddComponent<AdventureScanTarget>();
        target.interaction = source;
        if (target.isActiveAndEnabled) activeTargets.Add(target);
        return target;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() => activeTargets.Clear();

    void OnEnable()
    {
        if (interaction == null) interaction = GetComponent<WorldInteraction>();
        activeTargets.Add(this);
    }

    void OnDisable() => activeTargets.Remove(this);
    void OnDestroy() => activeTargets.Remove(this);
    void OnTransformChildrenChanged() => renderersCached = false;

    /// <summary>Renderer discovery runs on reveal, after interaction models have been instantiated.</summary>
    public Renderer[] GetHighlightRenderers()
    {
        if (!renderersCached) RefreshRenderers();
        return highlightRenderers;
    }

    [ContextMenu("Refresh Scan Renderers")]
    public void RefreshRenderers()
    {
        Renderer[] candidates = GetComponentsInChildren<Renderer>(true);
        var renderers = new List<Renderer>(candidates.Length);
        foreach (Renderer candidate in candidates)
        {
            if (candidate == null || !(candidate is MeshRenderer || candidate is SkinnedMeshRenderer)) continue;
            // A nested interaction owns its own reveal and must not be highlighted twice.
            AdventureScanTarget owner = candidate.GetComponentInParent<AdventureScanTarget>();
            if (owner != this) continue;
            renderers.Add(candidate);
        }
        highlightRenderers = renderers.ToArray();
        renderersCached = true;
    }

    bool TryGetVisibleBounds(out Bounds bounds)
    {
        bounds = default;
        if (!renderersCached) return false;
        bool found = false;
        foreach (Renderer candidate in highlightRenderers)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = candidate.bounds; found = true; }
            else bounds.Encapsulate(candidate.bounds);
        }
        return found;
    }
}
