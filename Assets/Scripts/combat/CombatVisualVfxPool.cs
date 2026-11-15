using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded, scene-owned reuse for visual-only effects. Gameplay/projectile scripts always use their original lifetime.</summary>
public sealed class CombatVisualVfxPool : IDisposable
{
    sealed class Entry
    {
        public GameObject instance, prefab;
        public Transform[] nodes;
        public Vector3[] positions, scales;
        public Quaternion[] rotations;
        public bool[] active;
        public ParticleSystem[] particles;
        public float[] speeds;
        public AudioSource[] audio;
        public int activeIndex = -1;
        public double deadline = double.PositiveInfinity;
    }
    readonly Transform owner;
    readonly int perPrefabLimit, totalLimit;
    readonly Action<GameObject> released;
    readonly Dictionary<GameObject, bool> eligibility = new Dictionary<GameObject, bool>();
    readonly Dictionary<GameObject, Stack<Entry>> available = new Dictionary<GameObject, Stack<Entry>>();
    readonly Dictionary<GameObject, Entry> instances = new Dictionary<GameObject, Entry>();
    readonly List<Entry> activeEntries = new List<Entry>();
    GameObject inactiveRoot;
    public int CachedCount { get; private set; }
    public int CreatedCount { get; private set; }
    public int ReusedCount { get; private set; }
    public int ActiveCount => activeEntries.Count;

    public CombatVisualVfxPool(Transform owner, int perPrefabLimit, int totalLimit, Action<GameObject> released = null)
    {
        this.owner = owner; this.perPrefabLimit = Mathf.Max(0, perPrefabLimit); this.totalLimit = Mathf.Max(0, totalLimit);
        this.released = released;
    }

    public static bool CanPool(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponentInChildren<ParticleSystem>(true) == null) return false;
        foreach (var particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
            if (particles.main.stopAction != ParticleSystemStopAction.None) return false;
        foreach (var component in prefab.GetComponentsInChildren<Component>(true))
        {
            if (component == null) return false;
            if (component is Transform || component is ParticleSystem || component is ParticleSystemRenderer ||
                component is MeshFilter || component is MeshRenderer || component is Light || component is AudioSource || component is LODGroup) continue;
            // In particular, RFX4, damage callbacks, Animator, physics and projectile controllers must not be reused implicitly.
            return false;
        }
        return true;
    }

    public GameObject Create(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null, bool allowPooling = true)
    {
        if (prefab == null) return null;
        if (!allowPooling || perPrefabLimit == 0 || totalLimit == 0)
            return UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
        if (!eligibility.TryGetValue(prefab, out bool safe)) { safe = CanPool(prefab); eligibility.Add(prefab, safe); }
        if (!safe) return UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
        Entry entry = null;
        if (available.TryGetValue(prefab, out var stack))
            while (stack.Count > 0 && entry == null)
            {
                var candidate = stack.Pop(); CachedCount--;
                if (candidate.instance != null) { entry = candidate; ReusedCount++; }
                else instances.Remove(candidate.instance);
            }
        if (entry == null)
        {
            if (inactiveRoot == null)
            {
                inactiveRoot = new GameObject("Cached Visual VFX"); inactiveRoot.transform.SetParent(owner, false); inactiveRoot.SetActive(false);
            }
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, inactiveRoot.transform);
            instance.SetActive(false);
            entry = new Entry { instance = instance, prefab = prefab, nodes = instance.GetComponentsInChildren<Transform>(true),
                particles = instance.GetComponentsInChildren<ParticleSystem>(true), audio = instance.GetComponentsInChildren<AudioSource>(true) };
            entry.positions = new Vector3[entry.nodes.Length]; entry.rotations = new Quaternion[entry.nodes.Length];
            entry.scales = new Vector3[entry.nodes.Length]; entry.active = new bool[entry.nodes.Length];
            for (int i = 0; i < entry.nodes.Length; i++)
            {
                entry.positions[i] = entry.nodes[i].localPosition; entry.rotations[i] = entry.nodes[i].localRotation;
                entry.scales[i] = entry.nodes[i].localScale; entry.active[i] = entry.nodes[i].gameObject.activeSelf;
            }
            entry.scales[0] = prefab.transform.localScale;
            entry.speeds = new float[entry.particles.Length];
            for (int i = 0; i < entry.particles.Length; i++)
            {
                var main = entry.particles[i].main; entry.speeds[i] = main.simulationSpeed;
                // The pool owns return timing; a particle's Destroy/Disable stop action must not invalidate a later lease.
                main.stopAction = ParticleSystemStopAction.None;
                var renderer = entry.particles[i].GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.supportsMeshInstancing) renderer.enableGPUInstancing = true;
            }
            instances.Add(instance, entry); CreatedCount++;
        }
        Reset(entry);
        entry.instance.transform.SetParent(parent != null ? parent : inactiveRoot.transform, false);
        entry.instance.transform.SetPositionAndRotation(position, rotation); entry.instance.transform.localScale = prefab.transform.localScale;
        entry.deadline = double.PositiveInfinity; entry.activeIndex = activeEntries.Count; activeEntries.Add(entry);
        if (parent == null)
        {
            entry.instance.transform.SetParent(null, true);
            entry.instance.SetActive(prefab.activeSelf);
        }
        return entry.instance;
    }

    static void Reset(Entry entry)
    {
        entry.instance.SetActive(false);
        for (int i = 1; i < entry.nodes.Length; i++)
        {
            if (entry.nodes[i] == null) continue;
            entry.nodes[i].localPosition = entry.positions[i]; entry.nodes[i].localRotation = entry.rotations[i];
            entry.nodes[i].localScale = entry.scales[i]; entry.nodes[i].gameObject.SetActive(entry.active[i]);
        }
        for (int i = 0; i < entry.particles.Length; i++)
        {
            if (entry.particles[i] == null) continue;
            entry.particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = entry.particles[i].main; main.simulationSpeed = entry.speeds[i]; main.stopAction = ParticleSystemStopAction.None;
        }
        foreach (var audio in entry.audio) if (audio != null) audio.Stop();
    }

    public bool Schedule(GameObject instance, float seconds, double now)
    {
        if (instance == null || !instances.TryGetValue(instance, out var entry)) return false;
        if (entry.activeIndex >= 0) entry.deadline = now + Mathf.Max(.1f, seconds);
        return true;
    }

    public bool Release(GameObject instance)
    {
        if (instance == null || !instances.TryGetValue(instance, out var entry)) return false;
        if (entry.activeIndex < 0) return true; // Already returned by lifetime or turn cleanup.
        RemoveActive(entry); Reset(entry); released?.Invoke(instance);
        if (!available.TryGetValue(entry.prefab, out var stack)) { stack = new Stack<Entry>(); available.Add(entry.prefab, stack); }
        if (stack.Count >= perPrefabLimit || CachedCount >= totalLimit)
        {
            instances.Remove(instance); Delete(instance); return true;
        }
        instance.transform.SetParent(inactiveRoot.transform, false); stack.Push(entry); CachedCount++;
        return true;
    }

    public void Tick(double now)
    {
        for (int i = activeEntries.Count - 1; i >= 0; i--)
        {
            var entry = activeEntries[i];
            if (entry.instance == null) { RemoveActive(entry); instances.Remove(entry.instance); }
            else if (now >= entry.deadline) Release(entry.instance);
        }
    }
    void RemoveActive(Entry entry)
    {
        int index = entry.activeIndex, last = activeEntries.Count - 1;
        if (index < 0) return;
        if (index != last) { activeEntries[index] = activeEntries[last]; activeEntries[index].activeIndex = index; }
        activeEntries.RemoveAt(last); entry.activeIndex = -1; entry.deadline = double.PositiveInfinity;
    }
    public void Dispose()
    {
        foreach (var entry in instances.Values) if (entry.instance != null) Delete(entry.instance);
        instances.Clear(); available.Clear(); eligibility.Clear(); activeEntries.Clear(); CachedCount = 0;
        if (inactiveRoot != null) Delete(inactiveRoot); inactiveRoot = null;
    }
    static void Delete(GameObject instance)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(instance); else UnityEngine.Object.DestroyImmediate(instance);
    }
}
