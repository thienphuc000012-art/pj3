using UnityEngine;

internal sealed class CombatCameraTransitionController
{
    Vector3 fromPosition;
    Quaternion fromRotation;
    float fromFov, duration, elapsed;
    CombatCameraEase ease;
    AnimationCurve curve;
    bool checkSubjectClearance;
    Vector3 clearanceOffset;

    public void Begin(Vector3 position, Quaternion rotation, float fov, CombatCameraShot shot, float seconds,
        bool avoidSubject = false)
    {
        fromPosition = position;
        fromRotation = rotation;
        fromFov = fov;
        ease = shot.easing;
        curve = shot.customBlend;
        duration = ease == CombatCameraEase.Cut ? 0 : Mathf.Max(0, seconds);
        checkSubjectClearance = avoidSubject;
        clearanceOffset = Vector3.zero;
        elapsed = 0;
    }

    public void Update(ref Vector3 position, ref Quaternion rotation, ref float fov,
        Vector3 goalPosition, Quaternion goalRotation, float goalFov, float damping, float deltaTime, Vector3 aim)
    {
        if (elapsed < duration)
        {
            elapsed = Mathf.Min(elapsed + deltaTime, duration);
            float t = elapsed / duration;
            switch (ease)
            {
                case CombatCameraEase.EaseIn: t *= t; break;
                case CombatCameraEase.EaseOut: t = 1 - (1 - t) * (1 - t); break;
                case CombatCameraEase.Custom:
                    if (curve != null && t < 1) t = Mathf.Clamp01(curve.Evaluate(t));
                    break;
                default: t = Mathf.SmoothStep(0, 1, t); break;
            }
            if (checkSubjectClearance)
            {
                // Pick the side once. A moving focus must not flip the path mid-blend.
                clearanceOffset = GetClearanceOffset(aim, goalPosition);
                checkSubjectClearance = false;
            }
            position = Vector3.Lerp(fromPosition, goalPosition, t) + clearanceOffset * (4f * t * (1f - t));
            rotation = Quaternion.Slerp(fromRotation, goalRotation, t);
            fov = Mathf.Lerp(fromFov, goalFov, t);
            return;
        }

        float follow = damping <= 0 ? 1 : 1 - Mathf.Exp(-deltaTime / damping);
        position = Vector3.Lerp(position, goalPosition, follow);
        rotation = Quaternion.Slerp(rotation, goalRotation, follow);
        fov = Mathf.Lerp(fov, goalFov, follow);
    }

    Vector3 GetClearanceOffset(Vector3 aim, Vector3 goalPosition)
    {
        Vector3 start = Vector3.ProjectOnPlane(fromPosition - aim, Vector3.up);
        Vector3 end = Vector3.ProjectOnPlane(goalPosition - aim, Vector3.up);
        if (start.sqrMagnitude < .01f || end.sqrMagnitude < .01f || Vector3.Dot(start, end) >= 0f)
            return Vector3.zero;

        Vector3 segment = end - start;
        float nearest = Mathf.Clamp01(-Vector3.Dot(start, segment) / segment.sqrMagnitude);
        Vector3 closest = start + segment * nearest;
        if (closest.sqrMagnitude >= 1f) return Vector3.zero;

        // Only the camera position detours. Its rotation always takes the short blend.
        Vector3 side = Vector3.Cross(Vector3.up, segment).normalized;
        if (Vector3.Dot(closest, side) < -.01f) side = -side;
        return side * (1.2f - closest.magnitude);
    }
}
