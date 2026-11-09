using Unity.Cinemachine;
using UnityEngine;

/// <summary>Adds contact feedback to the live shot without taking camera ownership.</summary>
[DisallowMultipleComponent]
public sealed class ImpactCameraController : MonoBehaviour
{
    // Isolate combat feedback from authored scene and Timeline impulse channels.
    internal const int ImpulseChannel = 1 << 20;

    [SerializeField] CombatCameraImpactProfile defaultProfile;
    [SerializeField, Range(0f, 1f)] float enemyFeedbackMultiplier = .55f;
    [SerializeField, Range(0f, 1f)] float parryFeedbackMultiplier = .65f;

    readonly CinemachineImpulseDefinition impulse = new CinemachineImpulseDefinition
    {
        ImpulseChannel = ImpulseChannel,
        ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
        ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion,
        DissipationDistance = 1f
    };

    CinemachineImpulseManager.ImpulseEvent currentImpulse;
    ISignalSource6D currentSignal;
    float impulseEnd;
    float pushStart;
    float pushDuration;
    float startPush;
    float peakPush;

    Animator pausedAnimator;
    float speedBeforeHitStop;
    float appliedSpeed;
    float hitStopEnd;

    int lastImpactFrame = -1;
    BattleUnit lastAttacker;
    ActionData lastAction;

    public bool IsActive => isActiveAndEnabled &&
        (Time.unscaledTime < impulseEnd ||
         (pushDuration > 0f && Time.unscaledTime < pushStart + pushDuration) || pausedAnimator != null);

    public void Attach(CinemachineCamera camera)
    {
        if (camera == null) return;
        if (!camera.TryGetComponent(out CombatCameraImpactExtension extension))
            extension = camera.gameObject.AddComponent<CombatCameraImpactExtension>();
        extension.Controller = this;
    }

    public void Play(ActionData action, BattleUnit attacker, BattleUnit target, bool critical = false,
        bool parried = false, bool allowLocalHitStop = true)
    {
        if (!isActiveAndEnabled || attacker == null || target == null) return;

        // A single AoE can resolve several targets in the same animation event.
        // Keep one camera impulse while each gameplay hit still resolves.
        if (lastImpactFrame == Time.frameCount && lastAttacker == attacker && lastAction == action) return;
        lastImpactFrame = Time.frameCount;
        lastAttacker = attacker;
        lastAction = action;

        CombatCameraImpactProfile profile = action != null && action.cameraImpactProfile != null
            ? action.cameraImpactProfile : defaultProfile;
        float strength = attacker.isPlayer ? 1f : enemyFeedbackMultiplier;
        if (parried) strength *= parryFeedbackMultiplier;
        if (critical && !parried) strength *= profile != null ? profile.criticalMultiplier : 1.35f;

        float shake = (profile != null ? profile.shakeStrength : .09f) * strength;
        float duration = profile != null ? profile.shakeDuration : .16f;
        if (shake > 0f && duration > 0f)
        {
            CancelImpulse();
            Vector3 direction = Vector3.ProjectOnPlane(target.transform.position - attacker.transform.position, Vector3.up);
            direction = (direction.normalized * .7f - Vector3.up).normalized;
            impulse.ImpulseDuration = duration;
            currentImpulse = impulse.CreateAndReturnEvent(target.transform.position, direction * shake);
            currentSignal = currentImpulse?.SignalSource;
            impulseEnd = Time.unscaledTime + duration + .06f;
        }

        float zoom = (profile != null ? profile.pushInDegrees : 1.5f) * strength;
        if (zoom > 0f)
        {
            startPush = GetFieldOfViewKick();
            peakPush = Mathf.Max(startPush, zoom);
            pushStart = Time.unscaledTime;
            pushDuration = Mathf.Max(.01f, profile != null ? profile.pushInDuration : .19f);
        }

        // Enemy animation events can open later parry windows in this combo.
        // Holding only a Player attack Animator preserves those timings.
        if (allowLocalHitStop && !parried && attacker.isPlayer && profile != null && profile.playerHitStopDuration > 0f)
            StartHitStop(attacker.animator, profile.playerHitStopDuration, profile.playerHitStopSpeed);
    }

    internal float GetFieldOfViewKick()
    {
        if (!isActiveAndEnabled || pushDuration <= 0f) return 0f;
        float progress = Mathf.Clamp01((Time.unscaledTime - pushStart) / pushDuration);
        const float attackFraction = .19f;
        if (progress < attackFraction)
            return Mathf.SmoothStep(startPush, peakPush, progress / attackFraction);
        return Mathf.SmoothStep(peakPush, 0f, (progress - attackFraction) / (1f - attackFraction));
    }

    internal bool TryGetImpulse(Vector3 cameraPosition, out Vector3 offset, out Quaternion rotation)
    {
        offset = Vector3.zero;
        rotation = Quaternion.identity;
        return isActiveAndEnabled && Time.unscaledTime < impulseEnd &&
               CinemachineImpulseManager.Instance.GetStrongestImpulseAt(cameraPosition, false, ImpulseChannel,
                   out offset, out rotation);
    }

    public void ResetFeedback()
    {
        CancelImpulse();
        pushDuration = 0f;
        lastImpactFrame = -1;
        lastAttacker = null;
        lastAction = null;
        StopHitStop();
    }

    void CancelImpulse()
    {
        // Cinemachine pools impulse events. Check the signal before touching one.
        if (currentImpulse != null && currentSignal != null && ReferenceEquals(currentImpulse.SignalSource, currentSignal))
        {
            currentImpulse.Channel = 0;
            float now = CinemachineImpulseManager.Instance.CurrentTime;
            // Leave a positive lifetime so the manager can recycle the event.
            if (now > currentImpulse.StartTime && now < currentImpulse.StartTime + currentImpulse.Envelope.Duration)
                currentImpulse.Cancel(now, true);
        }
        currentImpulse = null;
        currentSignal = null;
        impulseEnd = 0f;
    }

    void StartHitStop(Animator animator, float duration, float speedMultiplier)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
        if (pausedAnimator == animator && Mathf.Approximately(animator.speed, appliedSpeed))
        {
            hitStopEnd = Mathf.Max(hitStopEnd, Time.unscaledTime + duration);
            return;
        }

        StopHitStop();
        pausedAnimator = animator;
        speedBeforeHitStop = animator.speed;
        appliedSpeed = speedBeforeHitStop * Mathf.Clamp01(speedMultiplier);
        animator.speed = appliedSpeed;
        hitStopEnd = Time.unscaledTime + duration;
    }

    void StopHitStop()
    {
        // Leave another system's Animator speed intact if it changed in the meantime.
        if (pausedAnimator != null && Mathf.Approximately(pausedAnimator.speed, appliedSpeed))
            pausedAnimator.speed = speedBeforeHitStop;
        pausedAnimator = null;
    }

    void Update()
    {
        if (pausedAnimator != null && Time.unscaledTime >= hitStopEnd) StopHitStop();
        if (currentImpulse != null && Time.unscaledTime >= impulseEnd) CancelImpulse();
    }

    void OnDisable() => ResetFeedback();
}
