using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fits RFX4 distance beams or an opted-in forward particle stream to combat targets.
/// Reports impact to CombatManager; animation events control emission and stopping.
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
    private ParticleSystem stream;
    private ParticleSystem.Particle[] streamBuffer;
    private float streamTravelTime;
    private bool particleStream;

    // Called on an inactive clone, before any prefab OnEnable callbacks.
    public void Initialize(BattleUnit owner, BattleUnit destination, ActionData action,
        System.Action impact = null)
    {
        caster = owner;
        target = destination;
        startOffset = action.projectileStartOffset;
        targetOffset = action.projectileTargetOffset;
        onImpact = impact;
        particleStream = action.beamParticleStream;
        streamTravelTime = Mathf.Max(.05f, action.beamStreamTravelTime);

        RFX4_PlaybackSpeed playback = GetComponent<RFX4_PlaybackSpeed>();
        if (playback == null) playback = gameObject.AddComponent<RFX4_PlaybackSpeed>();
        playback.playbackSpeed = Mathf.Max(0.01f, action.beamPlaybackSpeed);

        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particleStream && particles.name == action.beamStreamParticleName)
            {
                stream = particles;
                stream.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = stream.main;
                main.prewarm = false;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startSpeed = 0;
                main.startLifetime = streamTravelTime + .1f;
                main.gravityModifier = 0;
                // The source prefab emits throughout a five-metre cone volume.
                // Emit at its base instead, otherwise nearby targets are hit at spawn.
                var shape = stream.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.position = Vector3.zero;
                shape.rotation = Vector3.zero;
                shape.length = .01f;
                streamBuffer = new ParticleSystem.Particle[main.maxParticles];
                var velocity = stream.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = 0; velocity.y = 0;
                continue;
            }
            bool isBeamParticle = action.beamParticleNames != null && action.beamParticleNames.Length > 0
                ? System.Array.IndexOf(action.beamParticleNames, particles.name) >= 0
                : particles.name.Contains("Distance");
            if (!isBeamParticle) continue;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) continue;
            distanceParticles.Add(particles);
            distanceRenderers.Add(renderer);
        }

        UpdateEndpoints();
        if (particleStream && stream == null) Debug.LogError("Beam stream particle system not found: " + action.beamStreamParticleName, this);
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
        if (particleStream)
        {
            UpdateStreamImpact();
            return;
        }
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

    private void UpdateStreamImpact()
    {
        if (stream == null) return;
        Vector3 end = target.GetVfxTargetPosition(targetOffset);
        Vector3 axis = transform.forward;
        float length = Vector3.Dot(end - transform.position, axis);
        int count = stream.GetParticles(streamBuffer);
        bool reached = false;
        for (int i = 0; i < count; i++)
        {
            Vector3 world = stream.transform.TransformPoint(streamBuffer[i].position);
            if (Vector3.Dot(world - transform.position, axis) < length) continue;
            reached = true;
            // Don't send the flame past the target into the background.
            streamBuffer[i].remainingLifetime = 0;
        }
        stream.SetParticles(streamBuffer, count);
        if (reached && !HasEmitted)
        {
            HasEmitted = true;
            onImpact?.Invoke(); onImpact = null;
            if (stopAfterEmission) Destroy(gameObject, .2f);
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

        if (stream != null)
        {
            stream.transform.SetPositionAndRotation(start, transform.rotation);
            var velocity = stream.velocityOverLifetime;
            float zScale = Mathf.Max(.0001f, stream.transform.TransformVector(Vector3.forward).magnitude);
            velocity.z = direction.magnitude / (streamTravelTime * zScale);
        }

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
