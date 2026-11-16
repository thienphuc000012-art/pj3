using System;
using UnityEngine;

[Serializable]
public sealed class AdventureInteractionVfxSettings
{
    [Tooltip("Phát tại Player chỉ khi Save Point thực sự hồi máu cho đội.")]
    public GameObject healPrefab;
    public Vector3 healOffset = new Vector3(0, .03f, 0);
    [Min(.01f)] public float healScale = 1f;
    [Tooltip("Phát tại vị trí item/rương khi nhận thưởng thành công; hiệu ứng đứng yên dù vật thể biến mất.")]
    public GameObject pickupPrefab;
    public Vector3 pickupOffset = new Vector3(0, .03f, 0);
    [Min(.01f)] public float pickupScale = 1f;
    [Min(.1f), Tooltip("Giới hạn thời gian tồn tại theo giây thực. Hiệu ứng tự dọn sớm hơn khi particle kết thúc.")]
    public float maximumLifetime = 12f;
}

/// <summary>A scene-owned cast that keeps playing while the rest menu pauses game time.</summary>
[DisallowMultipleComponent]
public sealed class AdventureInteractionVfx : MonoBehaviour
{
    ParticleSystem[] particles;
    float age, maximumLifetime;

    public static AdventureInteractionVfx Play(GameObject prefab, Transform anchor, Vector3 offset, float scale, float lifetime, bool followAnchor = true)
    {
        if (prefab == null || anchor == null || !anchor.gameObject.activeInHierarchy) return null;
        var instance = Instantiate(prefab, anchor, false);
        instance.name = prefab.name + " (Interaction)";
        if (followAnchor) instance.transform.localPosition = offset;
        else
        {
            Vector3 position = anchor.position + anchor.rotation * offset;
            Quaternion rotation = anchor.rotation * prefab.transform.localRotation;
            // Keep the scene, but detach before the collected object is hidden or destroyed.
            instance.transform.SetParent(null, false);
            instance.transform.SetPositionAndRotation(position, rotation);
        }
        instance.transform.localScale = prefab.transform.localScale * Mathf.Max(.01f, scale);
        var cast = instance.TryGetComponent<AdventureInteractionVfx>(out var existing) ? existing : instance.AddComponent<AdventureInteractionVfx>();
        cast.age = 0; cast.maximumLifetime = Mathf.Max(.1f, lifetime);
        cast.particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var system in cast.particles)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false; main.playOnAwake = false; main.useUnscaledTime = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
        }
        instance.SetActive(true);
        foreach (var system in cast.particles) if (system.gameObject.activeInHierarchy) system.Play(false);
        return cast;
    }

    void Update() => Tick(Time.unscaledDeltaTime);
    void Tick(float seconds)
    {
        age += Mathf.Max(0, seconds);
        bool alive = false;
        if (particles != null)
            foreach (var system in particles)
                if (system != null && system.gameObject.activeInHierarchy && system.IsAlive(false)) { alive = true; break; }
        if (age < maximumLifetime && (age < .15f || alive)) return;
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
