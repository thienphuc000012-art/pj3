using System.Collections.Generic;
using UnityEngine;

/// <summary>Session-owned entry/cooldown state. Survives Canvas and streamed chunk recreation.</summary>
public sealed class DiscoveryAreaTriggerState
{
    sealed class Entry
    {
        public string map;
        public Vector3 position;
        public float radius, nextAllowed;
        public bool inside;
    }
    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    public void ObservePlayer(string map, Vector3 position)
    {
        // Use cached world bounds even while the area's chunk is unloaded.
        foreach (var entry in entries.Values)
            if (entry.map != map || (position - entry.position).sqrMagnitude > entry.radius * entry.radius)
                entry.inside = false;
    }

    // Caller has already checked the area's interaction radius and exploration state.
    public bool TryEnter(WorldInteraction area, string map, float now)
    {
        if (area == null || area.kind != WorldInteractionKind.Discovery || !area.isActiveAndEnabled) return false;
        string key = map + "|" + area.Id;
        if (!entries.TryGetValue(key, out var entry))
        {
            entry = new Entry { map = map };
            entries.Add(key, entry);
        }
        entry.position = area.transform.position; entry.radius = Mathf.Max(0f, area.range);
        if (entry.inside) return false;
        entry.inside = true;
        if (now < entry.nextAllowed) return false;
        entry.nextAllowed = now + Mathf.Max(0f, area.discoveryCooldownSeconds);
        return true;
    }

    public void Clear() => entries.Clear();
}
