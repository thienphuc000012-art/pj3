using UnityEngine;

namespace ObsidianForge.AmbientParticlesEssentials.Demo
{
    /// <summary>Keeps the demo floor under the camera, beyond its far clipping plane.</summary>
    [DisallowMultipleComponent]
    public sealed class DemoEndlessGround : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField] private float snapDistance = 10f;

        private void LateUpdate()
        {
            if (observer == null && Camera.main != null) observer = Camera.main.transform;
            if (observer == null) return;
            var step = Mathf.Max(1f,snapDistance);
            transform.position = new Vector3(
                Mathf.Round(observer.position.x/step)*step,0f,
                Mathf.Round(observer.position.z/step)*step);
        }
    }
}
