using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public sealed class SkillCinematicRig : MonoBehaviour
{
    [Tooltip("Optional child anchor that follows the attacker's current position.")]
    [SerializeField] private Transform attackerAnchor;
    [SerializeField] private Vector3 attackerAnchorOffset = new Vector3(0f, 1.25f, 0f);
    [Tooltip("Optional child anchor that follows the target's current position.")]
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private Vector3 targetAnchorOffset = new Vector3(0f, 1.25f, 0f);
    [Tooltip("Optional child anchor at the midpoint between the two live anchors.")]
    [SerializeField] private Transform combatCenter;

    internal void UpdateAnchors(Transform attacker, Transform target, Vector3 attackerSlot, Vector3 targetSlot)
    {
        Vector3 a = (attacker != null ? attacker.position : attackerSlot) + transform.rotation * attackerAnchorOffset;
        Vector3 b = (target != null ? target.position : targetSlot) + transform.rotation * targetAnchorOffset;
        if (IsChildAnchor(attackerAnchor)) attackerAnchor.position = a;
        if (IsChildAnchor(targetAnchor)) targetAnchor.position = b;
        if (IsChildAnchor(combatCenter)) combatCenter.position = (a + b) * .5f;
    }

    private bool IsChildAnchor(Transform anchor)
    {
        return anchor != null && anchor != transform && anchor.IsChildOf(transform);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (attackerAnchor != null) Gizmos.DrawWireSphere(attackerAnchor.position, .15f);
        Gizmos.color = Color.red;
        if (targetAnchor != null) Gizmos.DrawWireSphere(targetAnchor.position, .15f);
        Gizmos.color = Color.yellow;
        if (combatCenter != null) Gizmos.DrawWireSphere(combatCenter.position, .18f);
        if (attackerAnchor != null && targetAnchor != null)
            Gizmos.DrawLine(attackerAnchor.position, targetAnchor.position);
    }
}
