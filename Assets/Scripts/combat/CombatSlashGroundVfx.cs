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
        public bool hasGround, emitting;
    }

    readonly Transform projectile;
    readonly int layers;
    readonly float probeDistance, heightOffset;
    readonly List<GroundGroup> groups = new List<GroundGroup>();
    readonly HashSet<ParticleSystem> ownedParticles = new HashSet<ParticleSystem>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    bool stopped;

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
            groups.Add(group);
        }
    }

    public bool Owns(ParticleSystem particles) => ownedParticles.Contains(particles);

    public void Tick()
    {
        if (stopped || projectile == null || groups.Count == 0) return;
        bool grounded = FindGround(out RaycastHit surface);
        foreach (var group in groups)
        {
            if (group.root == null) continue;
            if (grounded)
            {
                group.lastPosition = surface.point + surface.normal * heightOffset;
                group.lastRotation = Quaternion.FromToRotation(Vector3.up, surface.normal) * group.authoredRotation;
                group.hasGround = true;
                group.root.SetPositionAndRotation(group.lastPosition, group.lastRotation);
                if (!group.emitting)
                {
                    foreach (var particles in group.particles) if (particles != null) particles.Play(false);
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
