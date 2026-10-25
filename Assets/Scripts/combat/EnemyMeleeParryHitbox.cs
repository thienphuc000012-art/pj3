using UnityEngine;

/// <summary>
/// Marks a small collider on an enemy weapon or melee strike area as a parryable hitbox.
/// Contact is queried on the DealDamage animation event so each combo hit is checked separately.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyMeleeParryHitbox : MonoBehaviour
{
    [Tooltip("Small primitive collider placed over the weapon edge or attack volume.")]
    public Collider hitbox;

    [Tooltip("Only player parry-zone layers included here can block this hit.")]
    public LayerMask parryZoneLayers = ~0;

    private void Reset()
    {
        hitbox = GetComponent<Collider>();
    }

    private void OnValidate()
    {
        if (hitbox == null) hitbox = GetComponent<Collider>();
        if (hitbox != null) hitbox.isTrigger = true;
    }

    public bool TryGetContact(PlayerMeleeParryZone zone, out Vector3 contactPoint)
    {
        contactPoint = transform.position;
        if (zone == null || hitbox == null || zone.guardCollider == null ||
            !isActiveAndEnabled || !hitbox.enabled || !hitbox.gameObject.activeInHierarchy ||
            !zone.isActiveAndEnabled || !zone.guardCollider.enabled ||
            !zone.guardCollider.gameObject.activeInHierarchy ||
            (parryZoneLayers.value & (1 << zone.guardCollider.gameObject.layer)) == 0 ||
            !hitbox.bounds.Intersects(zone.guardCollider.bounds))
            return false;

        if (Physics.ComputePenetration(
                hitbox, hitbox.transform.position, hitbox.transform.rotation,
                zone.guardCollider, zone.guardCollider.transform.position, zone.guardCollider.transform.rotation,
                out _, out _))
        {
            Vector3 a = hitbox.ClosestPoint(zone.guardCollider.bounds.center);
            Vector3 b = zone.guardCollider.ClosestPoint(a);
            contactPoint = (a + b) * 0.5f;
            return true;
        }

        // Treat near-touching surfaces as contact to avoid a missed parry from tiny physics tolerances.
        Vector3 first = hitbox.ClosestPoint(zone.guardCollider.bounds.center);
        Vector3 second = zone.guardCollider.ClosestPoint(first);
        if ((first - second).sqrMagnitude > 0.000225f) return false;
        contactPoint = (first + second) * 0.5f;
        return true;
    }
}
