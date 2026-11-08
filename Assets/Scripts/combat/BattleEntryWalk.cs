using UnityEngine;

/// <summary>One battle-opening walk. CombatManager owns its lifetime and final slot.</summary>
public sealed class BattleEntryWalk
{
    readonly BattleUnit unit;
    readonly Animator animator;
    readonly Vector3 startPosition, destination;
    readonly Quaternion destinationRotation;
    readonly float distance, moveSpeed, originalAnimationSpeed;
    readonly AnimatorCullingMode originalCulling;
    float travelled;
    bool ownsAnimation;

    public bool IsComplete { get; private set; }

    public BattleEntryWalk(BattleUnit member, float spawnDistance, float speed)
    {
        unit = member;
        IsComplete = true;
        if (unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy) return;
        destination = unit.transform.position;
        destinationRotation = unit.transform.rotation;
        distance = Mathf.Max(0f, spawnDistance);
        moveSpeed = Mathf.Max(.01f, speed);
        if (distance <= .001f) return;

        animator = unit.animator;
        int walkState = Animator.StringToHash(unit.battleEntryWalkStateName ?? "");
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(unit.battleEntryWalkStateName) || !animator.HasState(0, walkState))
        {
            Debug.LogWarning($"[{unit.unitName}] Chưa có state '{unit.battleEntryWalkStateName}' để đi vào trận. Kiểm tra Battle Entry Walk State Name trên BattleUnit.", unit);
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(destinationRotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
        startPosition = destination - forward.normalized * distance;
        originalAnimationSpeed = animator.speed;
        originalCulling = animator.cullingMode;
        ownsAnimation = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = Mathf.Max(.01f, unit.battleEntryAnimationSpeed);
        if (animator.applyRootMotion) animator.applyRootMotion = false;
        foreach (var parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == "BattleStart")
                animator.ResetTrigger(parameter.nameHash);

        unit.transform.SetPositionAndRotation(startPosition, destinationRotation);
        animator.Play(walkState, 0, 0f);
        animator.Update(0f);
        IsComplete = false;
    }

    public void Tick(float deltaTime)
    {
        if (IsComplete) return;
        if (unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy)
        {
            Finish(false);
            return;
        }
        travelled = Mathf.Min(distance, travelled + moveSpeed * Mathf.Max(0f, deltaTime));
        unit.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, destination, travelled / distance), destinationRotation);
        if (travelled >= distance) Finish(true);
    }

    /// <summary>Also used when the battle is disabled mid-intro. Never leaves an altered Animator speed.</summary>
    public void Finish(bool snapToSlot = true)
    {
        if (IsComplete) return;
        IsComplete = true;
        if (unit != null && snapToSlot) unit.transform.SetPositionAndRotation(destination, destinationRotation);
        if (animator != null && ownsAnimation)
        {
            animator.speed = originalAnimationSpeed;
            animator.cullingMode = originalCulling;
            if (unit != null && !unit.IsDead && animator.isActiveAndEnabled) unit.ReturnToCombatIdle();
        }
        ownsAnimation = false;
    }
}
