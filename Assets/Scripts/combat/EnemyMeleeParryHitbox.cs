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

    [Tooltip("Extra bone-mounted colliders for a flexible or segmented weapon, such as a whip. A single overlap is enough.")]
    public Collider[] additionalHitboxes = new Collider[0];

    [Tooltip("Optional visible weapon. Hidden weapons cannot parry. Leave empty for an unarmed attack volume.")]
    public Renderer weaponRenderer;

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
        if (additionalHitboxes != null)
            foreach (Collider part in additionalHitboxes)
                if (part != null) part.isTrigger = true;
    }

    public bool TryGetContact(PlayerMeleeParryZone zone, out Vector3 contactPoint)
    {
        contactPoint = transform.position;
        if (zone == null || zone.guardCollider == null || !isActiveAndEnabled ||
            (weaponRenderer != null && (!weaponRenderer.enabled || !weaponRenderer.gameObject.activeInHierarchy)) ||
            !zone.isActiveAndEnabled || !zone.guardCollider.enabled ||
            !zone.guardCollider.gameObject.activeInHierarchy ||
            (parryZoneLayers.value & (1 << zone.guardCollider.gameObject.layer)) == 0)
            return false;

        if (TryGetColliderContact(hitbox, zone.guardCollider, out contactPoint)) return true;
        if (additionalHitboxes != null)
            foreach (Collider part in additionalHitboxes)
                if (part != hitbox && TryGetColliderContact(part, zone.guardCollider, out contactPoint)) return true;
        return false;
    }

    private static bool TryGetColliderContact(Collider part, Collider guard, out Vector3 contactPoint)
    {
        contactPoint = Vector3.zero;
        if (part == null || !part.enabled || !part.gameObject.activeInHierarchy || !part.bounds.Intersects(guard.bounds))
            return false;

        if (Physics.ComputePenetration(
                part, part.transform.position, part.transform.rotation,
                guard, guard.transform.position, guard.transform.rotation,
                out _, out _))
        {
            Vector3 a = part.ClosestPoint(guard.bounds.center);
            Vector3 b = guard.ClosestPoint(a);
            contactPoint = (a + b) * 0.5f;
            return true;
        }

        // Treat near-touching surfaces as contact to avoid a missed parry from tiny physics tolerances.
        Vector3 first = part.ClosestPoint(guard.bounds.center);
        Vector3 second = guard.ClosestPoint(first);
        if ((first - second).sqrMagnitude > 0.000225f) return false;
        contactPoint = (first + second) * 0.5f;
        return true;
    }
}
