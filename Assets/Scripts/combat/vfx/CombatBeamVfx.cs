using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fits RFX4's Distance particles between combat units without physics raycasts.
/// Damage and parry remain owned by combat animation events.
/// </summary>
public sealed class CombatBeamVfx : MonoBehaviour
{
    private BattleUnit caster;
    public BattleUnit Caster => caster;
    public bool HasEmitted { get; private set; }
    private System.Action onImpact;
    private bool stopAfterEmission;
    private BattleUnit target;
    private Vector3 startOffset;
    private Vector3 targetOffset;
    private readonly List<ParticleSystem> distanceParticles = new List<ParticleSystem>();
    private readonly List<ParticleSystemRenderer> distanceRenderers = new List<ParticleSystemRenderer>();

    // Called on an inactive clone, before any prefab OnEnable callbacks.
    public void Initialize(BattleUnit owner, BattleUnit destination, ActionData action,
        System.Action impact = null)
    {
        caster = owner;
        target = destination;
        startOffset = action.projectileStartOffset;
        targetOffset = action.projectileTargetOffset;
        onImpact = impact;

        RFX4_PlaybackSpeed playback = GetComponent<RFX4_PlaybackSpeed>();
        if (playback == null) playback = gameObject.AddComponent<RFX4_PlaybackSpeed>();
        playback.playbackSpeed = Mathf.Max(0.01f, action.beamPlaybackSpeed);

        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!particles.name.Contains("Distance")) continue;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) continue;
            distanceParticles.Add(particles);
            distanceRenderers.Add(renderer);
        }

        UpdateEndpoints();
    }

    private void LateUpdate()
    {
        if (caster == null || target == null || !caster.isActiveAndEnabled ||
            !target.isActiveAndEnabled || caster.currentHP <= 0)
        {
            Destroy(gameObject);
            return;
        }

        UpdateEndpoints();
        if (!HasEmitted)
        {
            // Wait for real particles, including their authored start delay and
            // playback speed. A beam impacts as it appears, not after it fades.
            foreach (ParticleSystem particles in distanceParticles)
            {
                if (particles == null || particles.particleCount == 0) continue;
                HasEmitted = true;
                onImpact?.Invoke();
                onImpact = null;
                if (stopAfterEmission) Destroy(gameObject, 0.2f);
                break;
            }
        }
    }

    public void FinishAttack()
    {
        if (stopAfterEmission) return;
        stopAfterEmission = true;
        if (HasEmitted) Destroy(gameObject);
    }

    private void UpdateEndpoints()
    {
        if (caster == null || target == null) return;
        Vector3 start = caster.VfxOrigin.TransformPoint(startOffset);
        Vector3 end = target.GetVfxTargetPosition(targetOffset);
        Vector3 direction = end - start;
        transform.position = start;
        if (direction.sqrMagnitude > 0.000001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        for (int i = 0; i < distanceParticles.Count; i++)
        {
            if (distanceParticles[i] == null || distanceRenderers[i] == null) continue;
            // Same length conversion used by RFX4_RaycastCollision, but the
            // endpoint is the selected unit, independent of scenery/colliders.
            float size = Mathf.Max(0.0001f, distanceParticles[i].main.startSize.constantMax);
            distanceRenderers[i].lengthScale = direction.magnitude / size;
        }
    }
}
