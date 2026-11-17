using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Transfers nearby static scenery from maps without chunks. No environment clones or gameplay scripts are created.</summary>
public sealed class CombatSceneEnvironment : IDisposable
{
    sealed class Placement
    {
        public Transform item, parent;
        public Scene scene;
        public Vector3 position, scale;
        public Quaternion rotation;
        public int sibling;
    }
    readonly List<Placement> placements = new List<Placement>();
    GameObject root;
    public GameObject Root => root;

    public static List<GameObject> Collect(Scene scene, Vector3 center, float radius)
    {
        var candidates = new HashSet<GameObject>();
        if (!scene.IsValid() || !scene.isLoaded) return new List<GameObject>();
        float squared = radius * radius;
        void Add(Component source, Bounds bounds)
        {
            if (bounds.SqrDistance(center) > squared) return;
            var lod = source.GetComponentInParent<LODGroup>();
            var item = lod != null ? lod.gameObject : source.gameObject;
            if (CanTransfer(item)) candidates.Add(item);
        }
        foreach (var owner in scene.GetRootGameObjects())
        {
            foreach (var renderer in owner.GetComponentsInChildren<Renderer>())
                if (renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) Add(renderer, renderer.bounds);
            foreach (var collider in owner.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger) Add(collider, collider.bounds);
            foreach (var terrain in owner.GetComponentsInChildren<Terrain>())
                if (terrain.enabled && terrain.terrainData != null)
                {
                    var local = terrain.terrainData.bounds; var matrix = terrain.transform.localToWorldMatrix;
                    Vector3 x = matrix.MultiplyVector(Vector3.right * local.extents.x);
                    Vector3 y = matrix.MultiplyVector(Vector3.up * local.extents.y);
                    Vector3 z = matrix.MultiplyVector(Vector3.forward * local.extents.z);
                    Vector3 extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                    Add(terrain, new Bounds(matrix.MultiplyPoint3x4(local.center), extents * 2));
                }
            foreach (var light in owner.GetComponentsInChildren<Light>())
                if (light.enabled && light.type != LightType.Directional) Add(light, new Bounds(light.transform.position, Vector3.one * light.range * 2));
            foreach (var probe in owner.GetComponentsInChildren<ReflectionProbe>())
                if (probe.enabled) Add(probe, probe.bounds);
        }
        var result = new List<GameObject>();
        foreach (var item in candidates)
        {
            bool covered = false;
            for (var parent = item.transform.parent; parent != null; parent = parent.parent)
                if (candidates.Contains(parent.gameObject)) { covered = true; break; }
            if (!covered) result.Add(item);
        }
        return result;
    }
    static bool Gameplay(Transform item) => item.GetComponent<WorldInteraction>() != null || item.GetComponent<PlayerScript>() != null ||
        item.GetComponent<BattleUnit>() != null || item.GetComponent<NavMeshAgent>() != null || item.GetComponent<Camera>() != null ||
        item.GetComponent<Canvas>() != null || item.GetComponent<MapGameSetup>() != null || item.GetComponent<CampaignSession>() != null;
    static bool CanTransfer(GameObject item)
    {
        if (item == null || !item.activeInHierarchy) return false;
        for (var parent = item.transform; parent != null; parent = parent.parent)
            if (Gameplay(parent) || parent.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) return false;
        foreach (var child in item.GetComponentsInChildren<Transform>(true)) if (Gameplay(child)) return false;
        return true;
    }
    public static bool TryPrepare(CombatAreaScan scan, out CombatSceneEnvironment transfer, out string error, Scene staging = default)
    {
        transfer = null;
        if (scan == null || !scan.UsesSceneEnvironment) { error = "Không có môi trường scene để chuyển sang trận đấu."; return false; }
        var prepared = new CombatSceneEnvironment();
        prepared.root = new GameObject("Captured Map Environment"); prepared.root.SetActive(false);
        if (staging.IsValid() && staging.isLoaded) SceneManager.MoveGameObjectToScene(prepared.root, staging);
        else if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(prepared.root);
        else { Destroy(prepared.root); error = "Edit Mode requires an isolated staging scene."; return false; }
        var proxies = new Dictionary<Transform, Transform>();
        Transform ParentProxy(Transform original)
        {
            if (original == null) return prepared.root.transform;
            if (proxies.TryGetValue(original, out var proxy)) return proxy;
            proxy = new GameObject(original.name).transform;
            proxy.SetParent(ParentProxy(original.parent), false);
            proxy.localPosition = original.localPosition; proxy.localRotation = original.localRotation; proxy.localScale = original.localScale;
            proxies.Add(original, proxy); return proxy;
        }
        try
        {
            foreach (var item in scan.EnvironmentObjects)
            {
                if (!CanTransfer(item)) throw new InvalidOperationException("Môi trường đã thay đổi sau khi quét: " + (item != null ? item.name : "missing object"));
                var target = item.transform;
                var placement = new Placement { item = target, parent = target.parent, scene = item.scene, sibling = target.GetSiblingIndex(), position = target.localPosition, rotation = target.localRotation, scale = target.localScale };
                // Recreate only ancestor transforms, preserving negative/nonuniform scales and shear exactly.
                Transform parent = ParentProxy(target.parent); prepared.placements.Add(placement);
                target.SetParent(parent, false);
            }
            transfer = prepared; error = null; return true;
        }
        catch (Exception exception) { prepared.Dispose(); error = "Không thể giữ môi trường cho trận đấu: " + exception.Message; return false; }
    }
    public bool Stage(Scene destination, out string error)
    {
        if (root == null || !destination.IsValid() || !destination.isLoaded) { error = "Môi trường đã quét không còn sẵn sàng."; return false; }
        SceneManager.MoveGameObjectToScene(root, destination); root.SetActive(true); Physics.SyncTransforms(); error = null; return true;
    }
    // Combat scene now owns the environment; unloading the battle frees it normally.
    public void Release() { placements.Clear(); root = null; }
    public void Dispose()
    {
        foreach (var placement in placements)
        {
            if (placement.item == null || !placement.scene.IsValid() || !placement.scene.isLoaded) continue;
            if (placement.parent != null) placement.item.SetParent(placement.parent, false);
            else { placement.item.SetParent(null, false); SceneManager.MoveGameObjectToScene(placement.item.gameObject, placement.scene); }
            placement.item.localPosition = placement.position; placement.item.localRotation = placement.rotation; placement.item.localScale = placement.scale;
        }
        placements.Sort((a, b) => a.sibling.CompareTo(b.sibling));
        foreach (var placement in placements) if (placement.item != null && placement.scene.IsValid() && placement.scene.isLoaded) placement.item.SetSiblingIndex(placement.sibling);
        placements.Clear(); if (root != null) Destroy(root); root = null;
    }
    static void Destroy(GameObject value) { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
}
