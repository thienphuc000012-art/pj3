using System;
using UnityEngine;

namespace ObsidianForge.AmbientParticlesEssentials
{
    /// <summary>One share of the total particle budget. All motion is simulated by Unity.</summary>
    [Serializable]
    public sealed class AmbientParticleLayer
    {
        public string name = "Fine motes";
        [Range(0f, 1f)] public float budgetShare = 0.7f;
        public Material material;
        [Range(0f, 8f)] public float brightness = 1f;
        [Min(0.01f)] public float sizeMultiplier = 1f;
        [Range(.1f, 4f)] public float lifetimeMultiplier = 1f;
        [Min(0f)] public float speedMultiplier = 1f;
        [Min(0f)] public float noiseMultiplier = 1f;
        [Min(0.01f)] public float noiseFrequency = 0.15f;
        [Min(0f)] public float noiseScroll = 0.08f;
        public Vector2 verticalSpeed = Vector2.zero;
        public Vector2 angularSpeed = Vector2.zero;
        [HideInInspector] public Vector2 aspect = Vector2.one; // Serialized compatibility; round billboards use uniform size.
        [Range(0f, 1f)] public float brightnessVariation = 0.2f;
        [ColorUsage(true, false)] public Color tint = Color.white;
        [Header("Motion trails (independent of the round particle)")]
        public bool trails;
        [Tooltip("Used by native wind wisps only. Style trail motes use the round particle material.")]
        public Material trailMaterial;
        [Range(0f, 1f)] public float trailRatio = 1f;
        [Tooltip("Maximum trail duration in seconds. Shorter-lived particles have proportionally shorter trails.")]
        [Range(.1f, 5f)] public float trailLifetime = 1.6f;
        [Range(.01f, 1.5f)] public float trailWidth = .12f;
        [Range(.04f, .3f)] public float trailVertexDistance = .08f;
        [Range(0f, 1f)] public float trailOpacity = .45f;
        [Tooltip("Small 3D motes deposited at each sampled position of a style trail. A shared hard cap protects frame time.")]
        [Range(1, 4)] public int trailMotesPerSample = 3;
        [Header("Detached wake (fourth style layer only)")]
        public bool shedResidue;
        [Tooltip("Share of this layer's existing budget reserved for independently fading wake particles.")]
        [Range(.5f, .9f)] public float residueBudgetShare = .75f;
        [Range(.2f, 3f)] public float residueLifetime = 1.2f;
        [Range(1f, 12f)] public float residueRate = 6f;
        [Range(.1f, 3.5f)] public float residueSize = .65f;
        [Range(.01f, 1.2f)] public float residueSpread = .08f;
        [Range(0f, .5f)] public float residueDrift = .12f;
        [Range(0f, 1f)] public float residueOpacity = .85f;
        [ColorUsage(true, false)] public Color residueEndTint = new Color(1f,.8f,.6f,0f);

        public AmbientParticleLayer(string label, float share, float size, float speed,
            float noise, Vector2 drift, Vector2 spin, Vector2 proportions, Color color)
        {
            name = label; budgetShare = share; sizeMultiplier = size; speedMultiplier = speed;
            noiseMultiplier = noise; verticalSpeed = drift; angularSpeed = spin;
            aspect = proportions; tint = color;
        }
    }
}
