using System;
using UnityEngine;

/// <summary>Combat-owned flight for Free Slash VFX. Preserves the package's authored +Z flight and world-space trails.</summary>
public sealed class CombatSwordSlashProjectile : MonoBehaviour
{
    BattleUnit target;
    Vector3 destination, targetOffset, bladeAxis, localEuler;
    float speed, lifetime, elapsed, flightTimeout;
    bool initialized, cancelled;
    Action onImpact;
    CombatSlashGroundVfx groundVfx;
    int groundLayers = Physics.DefaultRaycastLayers;
    float groundProbeDistance = 20f, groundHeightOffset = .02f;
    public bool HasArrived { get; private set; }
    public event Action Cancelled;

    public void ConfigureGrounding(LayerMask layers, float probeDistance, float heightOffset)
    {
        groundLayers = layers;
        groundProbeDistance = Mathf.Max(.1f, probeDistance);
        groundHeightOffset = heightOffset;
    }

    public static bool IsPackageProjectile(GameObject prefab) => prefab != null &&
        prefab.GetComponent<MaykerStudio.Demo.Projectile>() != null;

    public static Quaternion FlightRotation(Vector3 direction, Vector3 blade, Vector3 correction)
    {
        if (direction.sqrMagnitude < .000001f) direction = Vector3.forward;
        direction.Normalize();
        Vector3 right = Vector3.ProjectOnPlane(blade, direction);
        if (right.sqrMagnitude < .0001f) right = Vector3.ProjectOnPlane(Vector3.up, direction);
        if (right.sqrMagnitude < .0001f) right = Vector3.ProjectOnPlane(Vector3.right, direction);
        Vector3 up = Vector3.Cross(direction, right.normalized).normalized;
        return Quaternion.LookRotation(direction, up) * Quaternion.Euler(correction);
    }

    public void Initialize(BattleUnit selectedTarget, Vector3 fallbackTarget, Vector3 offset,
        float travelSpeed, float tailLifetime, Vector3 releaseBladeAxis, Vector3 eulerCorrection, Action impact)
    {
        target = selectedTarget;
        destination = fallbackTarget;
        targetOffset = offset;
        speed = Mathf.Max(.1f, travelSpeed);
        lifetime = Mathf.Max(.1f, tailLifetime);
        bladeAxis = releaseBladeAxis;
        localEuler = eulerCorrection;
        onImpact = impact;
        elapsed = 0f;
        flightTimeout = Mathf.Max(lifetime, Vector3.Distance(transform.position, destination) / speed + 1f);
        cancelled = HasArrived = false;
        initialized = true;

        // The demo's Fire/Update and combat must never both translate the root.
        foreach (var demo in GetComponentsInChildren<MaykerStudio.Demo.Projectile>(true)) demo.enabled = false;
        foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            // A root system destroying itself would also erase the still-alive child trails.
            main.stopAction = ParticleSystemStopAction.None;
        }
        groundVfx = new CombatSlashGroundVfx(transform, groundLayers, groundProbeDistance, groundHeightOffset);
        transform.rotation = FlightRotation(destination - transform.position, bladeAxis, localEuler);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        // Position ground emitters first, then start only airborne systems.
        // Play(true) would also burst the rocks/puddles at the sword's altitude.
        groundVfx.Tick(destination - transform.position);
        foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            if (!groundVfx.Owns(particles)) particles.Play(false);
    }

    void Update() => Tick(Time.deltaTime);

    public void Tick(float deltaTime)
    {
        if (!initialized || HasArrived || cancelled) return;
        if (target != null && !target.IsDead) destination = target.GetVfxTargetPosition(targetOffset);
        float step = Mathf.Max(0f, deltaTime);
        Vector3 direction = destination - transform.position;
        if (direction.sqrMagnitude > .000001f)
            transform.rotation = FlightRotation(direction, bladeAxis, localEuler);
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * step);
        groundVfx?.Tick(direction);
        if ((destination - transform.position).sqrMagnitude <= .0001f)
        {
            transform.position = destination;
            HasArrived = true;
            StopEmitting();
            float tail = lifetime;
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                float remaining = main.startLifetime.constantMax;
                var trails = particles.trails;
                if (trails.enabled) remaining += trails.lifetime.constantMax;
                tail = Mathf.Max(tail, remaining / Mathf.Max(.01f, main.simulationSpeed));
            }
            var impact = onImpact;
            onImpact = null;
            Cancelled = null;
            if (Application.isPlaying) Destroy(gameObject, tail);
            impact?.Invoke();
            return;
        }
        elapsed += step;
        if (elapsed >= flightTimeout)
        {
            CancelFlight();
            if (Application.isPlaying) Destroy(gameObject);
        }
    }

    void StopEmitting()
    {
        groundVfx?.Stop();
        // Prevent the grounding helper from starting new decal particles after impact.
        foreach (var decal in GetComponentsInChildren<MaykerStudio.VFX.SlashDecal>(true)) decal.enabled = false;
        foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    void CancelFlight()
    {
        if (!initialized || HasArrived || cancelled) return;
        cancelled = true;
        onImpact = null;
        StopEmitting();
        Cancelled?.Invoke();
        Cancelled = null;
    }

    void OnDisable() => CancelFlight();
}
