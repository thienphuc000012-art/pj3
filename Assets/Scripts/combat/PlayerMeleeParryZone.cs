using UnityEngine;

/// <summary>
/// Collider marking the small area where the player's weapon/guard can meet an enemy strike.
/// The zone should stay enabled; ParrySystem decides whether its contact can parry.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerMeleeParryZone : MonoBehaviour
{
    [Tooltip("Small trigger collider centered on the player's guarding weapon or hands.")]
    public Collider guardCollider;

    private void Reset()
    {
        guardCollider = GetComponent<Collider>();
    }

    private void OnValidate()
    {
        if (guardCollider == null) guardCollider = GetComponent<Collider>();
        if (guardCollider != null) guardCollider.isTrigger = true;
    }
}
