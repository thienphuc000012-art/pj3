using UnityEngine;

/// <summary>Optional authoring overrides on the BattleUnit, especially for flying or oversized bosses.</summary>
[DisallowMultipleComponent]
public sealed class CombatCameraFraming : MonoBehaviour
{
    [Tooltip("Default aim point. Place around the chest or head.")]
    public Transform focusAnchor;
    [Tooltip("Optional face or upper chest aim point for the frontal Target Selection shot.")]
    public Transform selectionAnchor;
    [Tooltip("Optional framing box in world units around the selection point. Leave zero to frame the character.")]
    public Vector3 selectionBoundsSize;
    [Tooltip("Optional different aim point for attack and recovery shots.")]
    public Transform attackAnchor;
    [Tooltip("Additional points to keep in view, such as feet, head, and wing tips.")]
    public Transform[] frameAnchors;
    [Tooltip("Optional override in world units, relative to the unit's root position.")]
    public Vector3 boundsCenterOffset = new Vector3(0, 1, 0);
    [Tooltip("Leave zero to use the model bounds. Set all three dimensions for an explicit envelope.")]
    public Vector3 boundsSize;
}
