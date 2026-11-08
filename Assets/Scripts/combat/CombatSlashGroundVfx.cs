using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Separates the package's ground-only Decal subtree from the airborne slash.</summary>
public sealed class CombatSlashGroundVfx
{
    sealed class GroundGroup
    {
        public Transform root;
        public ParticleSystem[] particles;
        public Quaternion authoredRotation;
        public Vector3 lastPosition;
        public Quaternion lastRotation;
        public ParticleSystem stamp;
        public float twistMin, twistMax;
        public bool hasGround, emitting;
    }

    readonly Transform projectile;
    readonly int layers;
    readonly float probeDistance, heightOffset;
    readonly List<GroundGroup> groups = new List<GroundGroup>();
    readonly HashSet<ParticleSystem> ownedParticles = new HashSet<ParticleSystem>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    bool stopped;
    Vector3 lastHeading = Vector3.forward;

    public CombatSlashGroundVfx(Transform root, int groundLayers, float rayDistance, float surfaceOffset)
    {
        projectile = root;
        layers = groundLayers;
        probeDistance = Mathf.Max(.1f, rayDistance);
        heightOffset = surfaceOffset;
        foreach (var decal in root.GetComponentsInChildren<MaykerStudio.VFX.SlashDecal>(true))
        {
            // Combat owns downward probing; the demo uses the rolling slash's local X.
            decal.enabled = false;
            var group = new GroundGroup
            {
                root = decal.transform,
                particles = decal.GetComponentsInChildren<ParticleSystem>(true),
                authoredRotation = decal.AuthoredGroundRotation
            };
            foreach (var particles in group.particles)
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                // Emitted rocks/puddles must not inherit the next frame's flight or sword roll.
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ownedParticles.Add(particles);
            }
            var stampRenderer = decal.GetComponent<ParticleSystemRenderer>();
            var stamp = decal.GetComponent<ParticleSystem>();
            if (stamp != null && stampRenderer != null && stampRenderer.renderMode == ParticleSystemRenderMode.Mesh)
            {
                group.stamp = stamp;
                var main = stamp.main;
                group.twistMin = Mathf.Min(main.startRotationZ.constantMin, main.startRotationZ.constantMax);
                group.twistMax = Mathf.Max(main.startRotationZ.constantMin, main.startRotationZ.constantMax);
                // World particles keep their own surface orientation after emission.
                // The package's fixed local rotation is unsuitable for arbitrary combat headings.
                stampRenderer.alignment = ParticleSystemRenderSpace.World;
                main.startRotation3D = true;
                float length = (main.startSize3D ? main.startSizeY.constantMax : main.startSize.constantMax) *
                    (stampRenderer.mesh != null ? stampRenderer.mesh.bounds.size.y : 1f);
                var emission = stamp.emission;
                // Overlap the central, opaque part of neighbouring brush stamps.
                emission.rateOverDistance = Mathf.Max(emission.rateOverDistance.constantMax, 4f / Mathf.Max(.1f, length));
            }
            groups.Add(group);
        }
    }

    public bool Owns(ParticleSystem particles) => ownedParticles.Contains(particles);

    public void Tick(Vector3 travelDirection)
    {
        if (stopped || projectile == null || groups.Count == 0) return;
        bool grounded = FindGround(out RaycastHit surface);
        foreach (var group in groups)
        {
            if (group.root == null) continue;
            if (grounded)
            {
                Vector3 heading = Vector3.ProjectOnPlane(travelDirection, surface.normal);
                if (heading.sqrMagnitude < .0001f) heading = Vector3.ProjectOnPlane(lastHeading, surface.normal);
                if (heading.sqrMagnitude < .0001f) heading = Vector3.ProjectOnPlane(Vector3.forward, surface.normal);
                heading.Normalize();
                lastHeading = heading;
                group.lastPosition = surface.point + surface.normal * heightOffset;
                group.lastRotation = Quaternion.LookRotation(heading, surface.normal) * group.authoredRotation;
                group.hasGround = true;
                group.root.SetPositionAndRotation(group.lastPosition, group.lastRotation);
                if (group.stamp != null)
                {
                    // Package Quad has a -Z normal and its long texture axis is +Y.
                    // Lay the quad on the surface and point its length along the actual flight path.
                    Vector3 angles = Quaternion.LookRotation(-surface.normal, heading).eulerAngles * Mathf.Deg2Rad;
                    var main = group.stamp.main;
                    main.startRotationX = angles.x;
                    main.startRotationY = angles.y;
                    main.startRotationZ = new ParticleSystem.MinMaxCurve(angles.z + group.twistMin, angles.z + group.twistMax);
                }
                if (!group.emitting)
                {
                    foreach (var particles in group.particles) if (particles != null) particles.Play(false);
                    if (group.stamp != null) group.stamp.Emit(1);
                    group.emitting = true;
                }
            }
            else
            {
                StopGroup(group);
                // Preserve the previous surface pose for any particles still fading there.
                if (group.hasGround) group.root.SetPositionAndRotation(group.lastPosition, group.lastRotation);
            }
        }
    }

    bool FindGround(out RaycastHit surface)
    {
        const float startAbove = .25f;
        var physicsScene = projectile.gameObject.scene.GetPhysicsScene();
        if (!physicsScene.IsValid()) { surface = default; return false; }
        int count = physicsScene.Raycast(projectile.position + Vector3.up * startAbove,
            Vector3.down, hits, probeDistance + startAbove, layers, QueryTriggerInteraction.Ignore);
        surface = default;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            var collider = hit.collider;
            if (collider == null || hit.distance >= bestDistance || Vector3.Dot(hit.normal, Vector3.up) < .5f) continue;
            // Ignore colliders from unrelated scenes sharing the same Physics scene.
            if (collider.gameObject.scene != projectile.gameObject.scene) continue;
            if (collider.isTrigger || collider is CharacterController || collider.transform.IsChildOf(projectile)) continue;
            if (collider.GetComponentInParent<BattleUnit>(true) != null ||
                collider.GetComponentInParent<CombatSwordSlashProjectile>(true) != null) continue;
            surface = hit;
            bestDistance = hit.distance;
        }
        return bestDistance < float.PositiveInfinity;
    }

    public void Stop()
    {
        if (stopped) return;
        stopped = true;
        foreach (var group in groups) StopGroup(group);
    }

    static void StopGroup(GroundGroup group)
    {
        if (!group.emitting) return;
        foreach (var particles in group.particles)
            if (particles != null) particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        group.emitting = false;
    }
}
