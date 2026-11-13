using System.Collections.Generic;
using UnityEngine;

/// <summary>Optional grouping/opt-out, also supports scenery without colliders.</summary>
[DisallowMultipleComponent, ExecuteAlways]
public sealed class CameraOcclusionObject : MonoBehaviour
{
    [Tooltip("Bật để vật thể này luôn giữ nguyên khi che camera.")]
    public bool ignoreOcclusion;
    [Tooltip("Những renderer cùng mờ. Để trống để lấy các mesh con, bao gồm LOD.")]
    public Renderer[] renderers;
    internal static readonly HashSet<CameraOcclusionObject> Active = new HashSet<CameraOcclusionObject>();
    Renderer[] cached;
    public Renderer[] Renderers => renderers != null && renderers.Length > 0 ? renderers :
        cached ?? (cached = GetComponentsInChildren<Renderer>(true));
    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);
    void OnTransformChildrenChanged() => cached = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry() => Active.Clear();
}
