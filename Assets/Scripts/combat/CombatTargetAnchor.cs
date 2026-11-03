using UnityEngine;

/// <summary>Optional per-character overrides; otherwise selection uses mesh bounds.</summary>
[DisallowMultipleComponent]
public sealed class CombatTargetAnchor : MonoBehaviour
{
    [Tooltip("Điểm giữa icon target. Bỏ trống để tự lấy tâm các mesh nhân vật.")]
    public Transform center;
    [Tooltip("Offset theo trục local của điểm Center, hoặc của BattleUnit.")]
    public Vector3 localOffset;
    [Min(.01f), Tooltip("Kích thước icon riêng cho nhân vật này, nhân với Marker Size.")]
    public float markerScale = 1f;
    [Tooltip("Bỏ trống để tự lấy MeshRenderer/SkinnedMeshRenderer. Không đưa Particle/Trail vào đây.")]
    public Renderer[] outlineRenderers;
}
