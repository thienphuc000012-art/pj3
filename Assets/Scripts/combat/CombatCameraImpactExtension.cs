using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class CombatCameraImpactExtension : CinemachineExtension
{
    internal ImpactCameraController Controller { get; set; }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase camera,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || Controller == null) return;

        // Modify the output state instead of writing the authored Lens or transform.
        float kick = Controller.GetFieldOfViewKick();
        if (kick > 0f && !state.Lens.Orthographic)
            state.Lens.FieldOfView = Mathf.Max(1f, state.Lens.FieldOfView - kick);

        if (Controller.TryGetImpulse(state.GetFinalPosition(), out Vector3 offset, out Quaternion rotation))
        {
            state.PositionCorrection += offset;
            state.OrientationCorrection *= rotation;
        }
    }
}
