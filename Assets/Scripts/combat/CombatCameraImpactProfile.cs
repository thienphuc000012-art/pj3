using UnityEngine;

[CreateAssetMenu(fileName = "Camera Impact", menuName = "Combat/Camera/Impact Profile")]
public sealed class CombatCameraImpactProfile : ScriptableObject
{
    [Range(0f, .5f), Tooltip("Cinemachine impulse strength in world units. Set to zero to disable shake.")]
    public float shakeStrength = .09f;
    [Range(.04f, .5f)] public float shakeDuration = .16f;

    [Range(0f, 6f), Tooltip("Brief reduction in vertical FOV, in degrees.")]
    public float pushInDegrees = 1.5f;
    [Range(.05f, .6f)] public float pushInDuration = .19f;
    [Range(0f, 2f)] public float criticalMultiplier = 1.35f;

    [Range(0f, .12f), Tooltip("Optional real-time pause on the attacking Player Animator only. Enemy attacks, projectiles and the battle clock are unaffected.")]
    public float playerHitStopDuration;
    [Range(0f, 1f)] public float playerHitStopSpeed = .08f;
}
