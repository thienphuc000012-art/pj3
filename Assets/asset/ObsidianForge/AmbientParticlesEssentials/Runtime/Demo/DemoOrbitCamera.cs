using UnityEngine;

namespace ObsidianForge.AmbientParticlesEssentials.Demo
{
    /// <summary>First-person camera controller for the open demo environment.</summary>
    [DisallowMultipleComponent]
    public sealed class DemoOrbitCamera : MonoBehaviour
    {
        [Header("Demo Navigation")]
        [Min(0.1f)] [SerializeField] private float walkSpeed = 5f;
        [Min(1f)] [SerializeField] private float sprintMultiplier = 2f;
        [Min(1f)] [SerializeField] private float turnSpeed = 105f;

        private float yaw;
        private float pitch;
        private bool forward, back, left, right, turnLeft, turnRight, sprint;

        // IMGUI keyboard events keep the demo independent of either input package/backend.
        private void OnGUI()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown && e.type != EventType.KeyUp) return;
            var held = e.type == EventType.KeyDown;
            switch (e.keyCode)
            {
                case KeyCode.W: forward = held; break;
                case KeyCode.S: back = held; break;
                case KeyCode.A: left = held; break;
                case KeyCode.D: right = held; break;
                case KeyCode.Q: turnLeft = held; break;
                case KeyCode.E: turnRight = held; break;
                case KeyCode.LeftShift: case KeyCode.RightShift: sprint = held; break;
            }
        }
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) forward = back = left = right = turnLeft = turnRight = sprint = false;
        }

        private void Awake()
        {
            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizePitch(angles.x);
        }

        private void Update()
        {
            UpdateLook();

            var input = new Vector3(
                (right ? 1f : 0f) - (left ? 1f : 0f),
                0f,
                (forward ? 1f : 0f) - (back ? 1f : 0f));
            if (input.sqrMagnitude < 0.001f)
                return;

            var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var movement = (planarForward * input.z + transform.right * input.x + Vector3.up * input.y).normalized;
            var speed = walkSpeed * (sprint ? sprintMultiplier : 1f);
            var position = transform.position + movement * speed * Time.deltaTime;
            position.y = Mathf.Clamp(position.y, 1f, 5f);
            transform.position = position;
        }

        private void UpdateLook()
        {
            var turnInput = (turnRight ? 1f : 0f) - (turnLeft ? 1f : 0f);
            if (Mathf.Approximately(turnInput, 0f))
                return;

            yaw += turnInput * turnSpeed * Time.deltaTime;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private static float NormalizePitch(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
